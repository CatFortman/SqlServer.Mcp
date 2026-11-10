using Microsoft.Data.SqlClient;

internal enum ToolErrorReason
{
    Unknown,
    Deadlock,
    ConnectionStringNotSet,
    CommandTimeout,
    ServerUnreachable,
    LoginFailed,
    CannotOpenDatabase,
    Permission,
    QueryDefect,
    InvalidObjectName
}

internal enum ToolErrorCategory { Transient, Validation, Permission, Business }

internal static class ToolErrors
{
    static ToolErrorCategory CategoryOf(ToolErrorReason reason) => reason switch
    {
        ToolErrorReason.CommandTimeout or ToolErrorReason.Deadlock or ToolErrorReason.ServerUnreachable
            => ToolErrorCategory.Transient,
        ToolErrorReason.LoginFailed or ToolErrorReason.Permission
            => ToolErrorCategory.Permission,
        ToolErrorReason.ConnectionStringNotSet or ToolErrorReason.CannotOpenDatabase or ToolErrorReason.InvalidObjectName
            => ToolErrorCategory.Validation,
        ToolErrorReason.QueryDefect
            => ToolErrorCategory.Business,
        _ => ToolErrorCategory.Transient, // Unknown: see point 4
    };

    internal static (ToolErrorCategory category, ToolErrorReason reason, bool retryable, string message) Classify(Exception ex)
    {
        if (ex is InvalidOperationException ioe && ioe.Message.Contains("SQLSERVER_CONNECTION"))
        {
            return (CategoryOf(ToolErrorReason.ConnectionStringNotSet), ToolErrorReason.ConnectionStringNotSet, false, ioe.Message);
        }
        else if (ex is SqlException sqlEx)
        {
            return sqlEx.Number switch
            {
                -2 => (CategoryOf(ToolErrorReason.CommandTimeout), ToolErrorReason.CommandTimeout, true, "Command timed out."), // transient, can retry
                1205 => (CategoryOf(ToolErrorReason.Deadlock), ToolErrorReason.Deadlock, true, "Deadlock occurred."), // transient, can retry
                53 or -1 or 2 or 10054 => (CategoryOf(ToolErrorReason.ServerUnreachable), ToolErrorReason.ServerUnreachable, true, "Cannot reach the SQL Server instance."), // transient, can retry
                4060 => (CategoryOf(ToolErrorReason.CannotOpenDatabase), ToolErrorReason.CannotOpenDatabase, false, "Cannot open the database specified in the connection string."), // validation (wrong database in the connection string) non-transient, cannot retry
                18456 => (CategoryOf(ToolErrorReason.LoginFailed), ToolErrorReason.LoginFailed, false, "Login failed for the user specified in the connection string."), // permission non-transient, cannot retry
                229 => (CategoryOf(ToolErrorReason.Permission), ToolErrorReason.Permission, false, "Permission denied for the operation."), // permission non-transient, cannot retry
                297 or 300 => (CategoryOf(ToolErrorReason.Permission), ToolErrorReason.Permission, false, "The SQL login lacks VIEW SERVER STATE (VIEW SERVER PERFORMANCE STATE on SQL Server 2022+), so server DMVs cannot be read."), // permission non-transient, cannot retry
                102 => (CategoryOf(ToolErrorReason.QueryDefect), ToolErrorReason.QueryDefect, false, "The server’s query is malformed (server defect); report it, don’t retry."), // validation non-transient, cannot retry
                208 => (CategoryOf(ToolErrorReason.InvalidObjectName), ToolErrorReason.InvalidObjectName, false, "Invalid object name specified in the query."), // validation non-transient, cannot retry
                _ => (CategoryOf(ToolErrorReason.Unknown), ToolErrorReason.Unknown, false, "An unexpected error occurred.")
            };
        }
        else
        {
            return (CategoryOf(ToolErrorReason.Unknown), ToolErrorReason.Unknown, false, "An unexpected error occurred.");
        }
    }
}
