using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using SqlServer.Mcp.Data;

namespace SqlServer.Mcp.Tools;

[McpServerToolType]
public static class SchemaTools
{
    [McpServerTool, Description(
        "Analyzes a single table: columns with types and nullability, indexes, " +
        "approximate row count, and storage size. Use this first when investigating " +
        "any table-specific question (performance, design, or data volume).")]
    public static async Task<string> AnalyzeTable(
        SqlQueryRunner runner,
        [Description("Table name, optionally schema-qualified, e.g. 'dbo.Orders' or 'Orders'")]
        string tableName,
        CancellationToken cancellationToken)
    {
        // TODO (issue #2): port the schema analysis script from
        // SqlServer.EngineeringToolkit. Query sys.tables / sys.columns /
        // sys.indexes / sys.dm_db_partition_stats. Validate tableName against
        // sys.tables before interpolating anywhere - never trust it raw.
        var rows = await runner.QueryAsync(
            "SELECT name, create_date, modify_date FROM sys.tables WHERE name = @name",
            new Dictionary<string, object> { ["@name"] = tableName.Split('.').Last() },
            cancellationToken);

        return JsonSerializer.Serialize(rows);
    }

    [McpServerTool, Description(
        "Checks index fragmentation across the database, returning indexes above a " +
        "fragmentation threshold with page counts. Use when investigating degraded " +
        "read performance or before recommending index maintenance.")]
    public static async Task<string> CheckIndexFragmentation(
        SqlQueryRunner runner,
        [Description("Minimum fragmentation percent to report, default 10")]
        int minFragmentationPercent = 10,
        CancellationToken cancellationToken = default)
    {
        // TODO (issue #3): port the fragmentation script using
        // sys.dm_db_index_physical_stats in LIMITED mode. Filter out tiny
        // indexes (< 100 pages) - their fragmentation numbers are noise.
        var rows = await runner.QueryAsync(
            "SELECT DB_NAME() AS DatabaseName",
            cancellationToken: cancellationToken);

        return JsonSerializer.Serialize(rows);
    }
}
