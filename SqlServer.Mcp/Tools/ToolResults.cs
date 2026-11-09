using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

internal static class ToolResults
{
    static readonly JsonSerializerOptions options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
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
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        };

        try
        {
            var payload = await work();
            return new CallToolResult { Content = [new TextContentBlock { Text = JsonSerializer.Serialize(payload, options) }] };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Tool} failed", toolName);           // full detail to stderr/log only
            var (category, reason, retryable, message) = ToolErrors.Classify(ex); // SqlException.Number map
            return new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = JsonSerializer.Serialize(new {
                    error_category = category,
                    reason,
                    is_retryable = retryable,
                    message, tool = toolName,
                    attempted_at_utc = DateTime.UtcNow.ToString("O") ,
                    attempted = attempted ?? []
                    },
                    options)
                }]
            };
        }
    }
}
