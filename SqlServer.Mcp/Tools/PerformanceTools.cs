using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SqlServer.Mcp.Data;
using static ToolResults;

namespace SqlServer.Mcp.Tools;

[McpServerToolType]
internal static class PerformanceTools
{
    private const string LogCategory = "SqlServer.Mcp.Tools.PerformanceTools";

    [McpServerTool(Name = ToolNames.FindSlowQueries, ReadOnly = true), Description(
        "Finds the top N slowest queries by average CPU time from the plan cache, " +
        "including execution counts and query text. Use when investigating general " +
        "slowness, high CPU, or when asked 'why is the database slow'. Results only " +
        "cover queries still in the plan cache.")]
    internal static async Task<CallToolResult> FindSlowQueries(
        SqlQueryRunner runner,
        ILoggerFactory loggerFactory,
        [Description("Number of queries to return, default 10, max 50")]
        int top = 10,
        CancellationToken cancellationToken = default)
    {
        var query = "SELECT TOP (@top) session_id, status, cpu_time FROM sys.dm_exec_requests ORDER BY cpu_time DESC";
        return await RunAsync(async () =>
        {
            // TODO (issue #4): port the slow-query script from the toolkit using
            // sys.dm_exec_query_stats + sys.dm_exec_sql_text. Truncate query text
            // to ~500 chars per row - Claude does not need the full batch and
            // oversized tool results waste context.
            var clampedTop = Math.Clamp(top, 1, 50);

            return await runner.QueryAsync(
            query,
            new Dictionary<string, object> { ["@top"] = clampedTop },
            cancellationToken);
        },
            loggerFactory.CreateLogger(LogCategory),
            ToolNames.FindSlowQueries,
            new Dictionary<string, object> { ["top"] = top, ["query"] = query }
        );
    }

    [McpServerTool(Name = ToolNames.CheckBlocking, ReadOnly = true), Description(
        "Reports current blocking: which requests are blocked, which session is blocking " +
        "them, wait types and the statements involved, plus the blocking session's state " +
        "(an idle blocker holding an open transaction is common). Use when queries are " +
        "hanging or timing out right now. This is a live snapshot taken at captured_at_utc, " +
        "not history: an empty blocked_requests list means nothing was blocked at that " +
        "moment, not that nothing was blocked earlier.")]
    internal static async Task<CallToolResult> CheckBlocking(
        SqlQueryRunner runner,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var query = "SELECT SYSUTCDATETIME() AS captured_at_utc, @@SERVERNAME AS server_name";
        return await RunAsync(async () =>
        {
            // Stamp the snapshot with the database server's own clock and name, so the
            // evidence says when and where it was observed. A bare [] can't carry a time.
            var query = "SELECT SYSUTCDATETIME() AS captured_at_utc, @@SERVERNAME AS server_name";
            var snapshot = (await runner.QueryAsync(
                query,
                cancellationToken: cancellationToken))[0];

            // TODO (issue #5): full port of the toolkit's blocking-chain script
            // (lock details, full chain walk). This covers the blocked requests and
            // the state of each one's direct blocker.
            var rows = await runner.QueryAsync(
                """
            SELECT TOP (50)
                r.session_id,
                r.blocking_session_id,
                DB_NAME(r.database_id)   AS database_name,
                r.status,
                r.command,
                r.wait_type,
                r.wait_time              AS wait_time_ms,
                r.wait_resource,
                LEFT(t.text, 500)        AS statement_text,
                b.status                 AS blocker_status,
                b.open_transaction_count AS blocker_open_transaction_count,
                b.program_name           AS blocker_program_name
            FROM sys.dm_exec_requests AS r
            LEFT JOIN sys.dm_exec_sessions AS b ON b.session_id = r.blocking_session_id
            OUTER APPLY sys.dm_exec_sql_text(r.sql_handle) AS t
            WHERE r.blocking_session_id <> 0
            ORDER BY r.wait_time DESC
            """,
                cancellationToken: cancellationToken);

            // SYSUTCDATETIME() comes back as an unspecified-kind DateTime; mark it UTC so
            // the ISO 8601 string ends in Z.
            var capturedAt = DateTime.SpecifyKind((DateTime)snapshot["captured_at_utc"]!, DateTimeKind.Utc);

            return new
            {
                captured_at_utc = capturedAt.ToString("O"),
                server_name = snapshot["server_name"],
                blocked_request_count = rows.Count,
                blocked_requests = rows,
            };
        },
            loggerFactory.CreateLogger(LogCategory),
            ToolNames.CheckBlocking,
            new Dictionary<string, object> { ["query"] = query }
        );
    }
}
