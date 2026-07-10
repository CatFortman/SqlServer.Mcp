using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using SqlServer.Mcp.Data;

namespace SqlServer.Mcp.Tools;

[McpServerToolType]
public static class PerformanceTools
{
    [McpServerTool, Description(
        "Finds the top N slowest queries by average CPU time from the plan cache, " +
        "including execution counts and query text. Use when investigating general " +
        "slowness, high CPU, or when asked 'why is the database slow'. Results only " +
        "cover queries still in the plan cache.")]
    public static async Task<string> FindSlowQueries(
        SqlQueryRunner runner,
        [Description("Number of queries to return, default 10, max 50")]
        int top = 10,
        CancellationToken cancellationToken = default)
    {
        // TODO (issue #4): port the slow-query script from the toolkit using
        // sys.dm_exec_query_stats + sys.dm_exec_sql_text. Truncate query text
        // to ~500 chars per row - Claude does not need the full batch and
        // oversized tool results waste context.
        var clampedTop = Math.Clamp(top, 1, 50);
        var rows = await runner.QueryAsync(
            "SELECT TOP (@top) session_id, status, cpu_time FROM sys.dm_exec_requests ORDER BY cpu_time DESC",
            new Dictionary<string, object> { ["@top"] = clampedTop },
            cancellationToken);

        return JsonSerializer.Serialize(rows);
    }

    [McpServerTool, Description(
        "Reports current blocking chains: which sessions are blocked, what is blocking " +
        "them, wait types, and the statements involved. Use when queries are hanging " +
        "or timing out right now - this is a live snapshot, not historical data.")]
    public static async Task<string> CheckBlocking(
        SqlQueryRunner runner,
        CancellationToken cancellationToken = default)
    {
        // TODO (issue #5): port the blocking-chain script using
        // sys.dm_exec_requests joined on blocking_session_id, with
        // sys.dm_exec_sessions for host/login context.
        var rows = await runner.QueryAsync(
            """
            SELECT r.session_id, r.blocking_session_id, r.wait_type, r.wait_time
            FROM sys.dm_exec_requests r
            WHERE r.blocking_session_id <> 0
            """,
            cancellationToken: cancellationToken);

        return JsonSerializer.Serialize(rows);
    }
}
