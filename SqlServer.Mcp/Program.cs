using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SqlServer.Mcp.Data;

var builder = Host.CreateApplicationBuilder(args);

// CRITICAL: stdout is the JSON-RPC protocol channel for stdio transport.
// All logging must go to stderr or the protocol stream gets corrupted.
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// The server keeps its own log file. MCP hosts treat stderr differently (Claude Code
// stops recording it once the connection is up), so the file is the only place a tool
// failure's full exception is guaranteed to survive. Match a file entry to a tool
// result by its error_id.
var logDirectory = Environment.GetEnvironmentVariable("SQLSERVER_MCP_LOG_DIR")
    ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SqlServer.Mcp",
        "logs");

var fileLogger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File(
        Path.Combine(logDirectory, "sqlserver-mcp-.log"),   // rolls to sqlserver-mcp-yyyyMMdd.log
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        shared: true,   // several server processes (OpsAgent runs, Inspector) can write at once
        outputTemplate: "{Timestamp:o} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

// Added alongside the console provider, not instead of it: Serilog never touches stdout here.
builder.Logging.AddSerilog(fileLogger, dispose: true);

builder.Services.AddSingleton<SqlQueryRunner>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
