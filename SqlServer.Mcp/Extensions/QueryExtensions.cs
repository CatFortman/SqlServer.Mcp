using Microsoft.Data.SqlClient;
using SqlServer.Mcp.Data;

public static class QueryExtensions
{
    [Obsolete("Costs a round-trip to the database.")]
    public static async Task ConfirmConnectionAsync(
        this SqlQueryRunner runner,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Test the connection string before running the query, so we can give a
            // more helpful error message than "Invalid object name 'sys.dm_exec_requests'".
            await runner.QueryAsync("SELECT 1", cancellationToken: cancellationToken);
        }
        catch (SqlException ex) when (ex.Number == 4060)
        {
            throw new InvalidOperationException(
                "Cannot connect to the database. Check that the SQLSERVER_CONNECTION " +
                "environment variable is set correctly and that the database is reachable.", ex);
        }
    }    
}
