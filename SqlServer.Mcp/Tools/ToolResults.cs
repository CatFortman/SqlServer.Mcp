using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

internal static class ToolResults
{
    static readonly JsonSerializerOptions options = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    internal static class ToolNames
    {
        internal const string CheckBlocking = "check_blocking";
        internal const string FindSlowQueries = "find_slow_queries";
        internal const string AnalyzeTable = "analyze_table";
        internal const string CheckIndexFragmentation = "check_index_fragmentation";
    }

    internal static async Task<CallToolResult> RunAsync(Func<Task<object>> work,
    ILogger logger,
    string toolName,
    Dictionary<string, object>? attempted = null)
    {
        try
        {
            var payload = await work();
            return new CallToolResult { Content = [new TextContentBlock { Text = JsonSerializer.Serialize(payload, options) }] };
        }
        catch (Exception ex)
        {
            // error_id joins the tool result the model sees to the full exception in the log.
            // sql_error_number is safe to show the model (no stack trace, no connection
            // details) and identifies an unmapped SqlException without opening the log.
            var errorId = Guid.NewGuid().ToString("N");
            var sqlErrorNumber = (ex as SqlException)?.Number;
            var (category, reason, retryable, message) = ToolErrors.Classify(ex); // SqlException.Number map

            // Full detail to stderr and the log file only, never to the model.
            logger.LogError(ex,
                "{Tool} failed: error_id {ErrorId}, reason {Reason}, sql_error_number {SqlErrorNumber}",
                toolName, errorId, reason, sqlErrorNumber);

            return new CallToolResult
            {
                IsError = true,
                Content = [
                    new TextContentBlock
                    { Text = JsonSerializer.Serialize(new {
                        error_category = category,
                        reason,
                        is_retryable = retryable,
                        message,
                        tool = toolName,
                        error_id = errorId,
                        sql_error_number = sqlErrorNumber,
                        attempted_at_utc = DateTime.UtcNow.ToString("O"),
                        attempted = attempted ?? []
                        },
                    options)
                    }
                ]
            };
        }
    }
}
