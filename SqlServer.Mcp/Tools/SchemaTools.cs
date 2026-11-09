using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SqlServer.Mcp.Data;
using static ToolResults;

namespace SqlServer.Mcp.Tools;

[McpServerToolType]
internal static class SchemaTools
{
    private const string LogCategory = "SqlServer.Mcp.Tools.SchemaTools";

    [McpServerTool(Name = ToolNames.AnalyzeTable, ReadOnly = true), Description(
        "Analyzes a single table: columns with types and nullability, indexes, " +
        "approximate row count, and storage size. Use this first when investigating " +
        "any table-specific question (performance, design, or data volume).")]
    internal static async Task<CallToolResult> AnalyzeTable(
        SqlQueryRunner runner,
        ILoggerFactory loggerFactory,
        [Description("Table name, optionally schema-qualified, e.g. 'dbo.Orders' or 'Orders'")]
        string tableName,
        CancellationToken cancellationToken)
    {
        var query = "SELECT name, create_date, modify_date FROM sys.tables WHERE name = @name";
        return await RunAsync(async () =>
        {
            // TODO (issue #2): port the schema analysis script from
            // SqlServer.EngineeringToolkit. Query sys.tables / sys.columns /
            // sys.indexes / sys.dm_db_partition_stats. Validate tableName against
            // sys.tables before interpolating anywhere - never trust it raw.
            return await runner.QueryAsync(
                query,
                new Dictionary<string, object> { ["@name"] = tableName.Split('.').Last() },
                cancellationToken);
        },
            loggerFactory.CreateLogger(LogCategory),
            ToolNames.AnalyzeTable,
            new Dictionary<string, object> { ["table_name"] = tableName, ["query"] = query }
        );
    }

    [McpServerTool(Name = ToolNames.CheckIndexFragmentation, ReadOnly = true), Description(
        "Checks index fragmentation across the database, returning indexes above a " +
        "fragmentation threshold with page counts. Use when investigating degraded " +
        "read performance or before recommending index maintenance.")]
    internal static async Task<CallToolResult> CheckIndexFragmentation(
        SqlQueryRunner runner,
                ILoggerFactory loggerFactory,

        [Description("Minimum fragmentation percent to report, default 10")]
        int minFragmentationPercent = 10,
        CancellationToken cancellationToken = default)
    {
        var query = "SELECT DB_NAME() AS DatabaseName";
        return await RunAsync(async () =>
        {
            // TODO (issue #3): port the fragmentation script using
            // sys.dm_db_index_physical_stats in LIMITED mode. Filter out tiny
            // indexes (< 100 pages) - their fragmentation numbers are noise.
            return await runner.QueryAsync(
            query,
            cancellationToken: cancellationToken);
        },
            loggerFactory.CreateLogger(LogCategory),
            ToolNames.CheckIndexFragmentation,
            new Dictionary<string, object> { ["min_fragmentation_percent"] = minFragmentationPercent, ["query"] = query }
            );
    }
}
