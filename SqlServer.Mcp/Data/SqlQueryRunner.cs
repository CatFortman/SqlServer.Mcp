using System.Data;
using Microsoft.Data.SqlClient;

namespace SqlServer.Mcp.Data;

/// <summary>
/// Thin, read-only query executor. Deliberately dumb: opens a connection,
/// runs a parameterized query, returns rows as dictionaries. All intelligence
/// lives on the MCP client (Claude) side.
/// </summary>
public sealed class SqlQueryRunner
{
    private readonly string _connectionString;

    public SqlQueryRunner()
    {
        _connectionString = Environment.GetEnvironmentVariable("SQLSERVER_CONNECTION")
            ?? throw new InvalidOperationException(
                "SQLSERVER_CONNECTION environment variable is not set. " +
                "Provide it via the 'env' block in .mcp.json.");
    }

    public async Task<List<Dictionary<string, object?>>> QueryAsync(
        string sql,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection)
        {
            CommandType = CommandType.Text,
            CommandTimeout = 30
        };

        if (parameters is not null)
        {
            foreach (var (name, value) in parameters)
            {
                command.Parameters.AddWithValue(name, value);
            }
        }

        var rows = new List<Dictionary<string, object?>>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(reader.FieldCount);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            rows.Add(row);
        }

        return rows;
    }
}
