using System.Text.Json;
using ModelContextProtocol.Protocol;

public static class ExceptionExtensions
{
    public static CallToolResult ToCallToolResult(
        this Exception ex,
        string? message = null,
        bool isRetryable = false,
        string? hint = null)
    {
        // When you want complete control over what the model receives:
        return new CallToolResult
        {
            IsError = true,
            Content =
            [
                new TextContentBlock
                {
                    Text = JsonSerializer.Serialize(new
                    {
                        status = "error",
                        isRetryable,
                        message = message ?? "An unexpected error occurred.",
                        hint = hint ?? ""
                    })
                }
            ]
        };
    }
}
