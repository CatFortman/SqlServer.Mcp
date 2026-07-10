# SqlServer.Mcp

An MCP (Model Context Protocol) server that exposes SQL Server diagnostics as tools for AI clients like Claude Code and Claude Desktop. Built on the official [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk).

The server wraps battle-tested diagnostic queries from [SqlServer.EngineeringToolkit](https://github.com/CatFortman/SqlServer.EngineeringToolkit) so an AI agent can investigate a database conversationally: *"why might queries against Orders be slow?"* becomes a chain of real DMV queries instead of guesswork.

## Design constraints (v1)

- **Read-only.** No DDL, no maintenance operations, no writes. This is a deliberate safety boundary, not a limitation.
- **Stdio transport only.** Local use with Claude Code / Claude Desktop. No HTTP, no auth surface.
- **Dumb server, smart client.** Tools execute fixed, parameterized diagnostic queries and return JSON. All reasoning happens on the model side.
- **Single connection target.** One connection string via environment variable.

## Tools (v1)

| Tool | Purpose |
| --- | --- |
| `analyze_table` | Columns, indexes, row count, and size for one table |
| `check_index_fragmentation` | Fragmented indexes above a threshold |
| `find_slow_queries` | Top N queries by average CPU from the plan cache |
| `check_blocking` | Live blocking-chain snapshot |

## Setup

Requires .NET 8 SDK and a reachable SQL Server instance.

```bash
dotnet build
```

Register with Claude Code by adding `.mcp.json` to any project (or use `claude mcp add`):

```json
{
  "mcpServers": {
    "sqlserver": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "C:/dev/SqlServer.Mcp"],
      "env": {
        "SQLSERVER_CONNECTION": "Server=localhost;Database=TestDb;Integrated Security=true;TrustServerCertificate=true"
      }
    }
  }
}
```

Verify inside a Claude Code session with `/mcp`.

## Debugging without an LLM

Use the MCP Inspector to invoke tools directly:

```bash
npx @modelcontextprotocol/inspector dotnet run --project .
```

Much faster feedback loop than prompting a model while iterating on tool behavior.

## Gotchas

- **Never write to stdout.** Stdio transport means stdout is the JSON-RPC wire. A stray `Console.WriteLine` corrupts the protocol. All logging goes to stderr (already configured in `Program.cs`).
- **Tool descriptions are the API.** The `Description` attributes are the only thing the model sees when choosing tools. Iterate on them like you would a public interface.
- Use a read-only SQL login in the connection string. The server doesn't write, but defense in depth costs nothing.
