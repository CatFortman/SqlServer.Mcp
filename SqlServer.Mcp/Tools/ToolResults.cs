using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

internal static class ToolResults
{
    internal static async Task<CallToolResult> RunAsync(Func<Task<object>> work, ILogger logger, string toolName)
    {
        try
        {
            var payload = await work();
            return new CallToolResult { Content = [new TextContentBlock { Text = JsonSerializer.Serialize(payload) }] };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Tool} failed", toolName);           // full detail to stderr only
            var (category, retryable, message) = ToolErrors.Classify(ex); // your SqlException.Number map
            return new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = JsonSerializer.Serialize(new {
                    error_category = category, is_retryable = retryable, message, tool = toolName }) }]
            };
        }
    }
}
