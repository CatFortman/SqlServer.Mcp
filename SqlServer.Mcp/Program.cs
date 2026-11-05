using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SqlServer.Mcp.Data;

var builder = Host.CreateApplicationBuilder(args);

// CRITICAL: stdout is the JSON-RPC protocol channel for stdio transport.
// All logging must go to stderr or the protocol stream gets corrupted.
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

var jsonOptions = new JsonSerializerOptions
{
    TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
};

builder.Services.AddSingleton<SqlQueryRunner>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
