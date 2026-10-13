# SqlServer.Mcp

An MCP (Model Context Protocol) server that exposes SQL Server diagnostics as tools for AI clients like Claude Code and Claude Desktop. Built on the official [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk).

The server wraps battle-tested diagnostic queries from [SqlServer.EngineeringToolkit](https://github.com/CatFortman/SqlServer.EngineeringToolkit) so an AI agent can investigate a database conversationally: *"why might queries against Orders be slow?"* becomes a chain of real DMV queries instead of guesswork.

> **Status: scaffolded.** The server, transport and tool signatures exist; the tool bodies are stubs. Implementation is tracked in issues #1 to #9.

## Why

Developers ask "why is this slow?" far more often than a DBA has time to answer. This server lets them ask Claude Code, which answers from the same DMV queries a DBA would run, under the developer's own read-only login. Nothing it does can change the database, which is what makes it safe to hand out.

## Design constraints (v1)

- **Read-only.** No DDL, no maintenance operations, no writes. This is a deliberate safety boundary, not a limitation. A request a tool refuses on this basis returns a `business` error (see below).
- **Stdio transport only.** Each client session launches its own server process and talks to it over stdin/stdout. No HTTP, no port, no auth surface.
- **Dumb server, smart client.** Tools execute fixed, parameterized diagnostic queries and return JSON. All reasoning happens on the model side.
- **Single connection target.** One connection string via the `SQLSERVER_CONNECTION` environment variable.
- **Small results.** Text fields truncated to ~500 characters and row counts capped, so results don't flood the client's context window.

## Tools

| Tool | Purpose | Status |
| --- | --- | --- |
| `analyze_table` | Columns, indexes, row count and size for one table | Stub (#1) |
| `check_index_fragmentation` | Fragmented indexes above a threshold, database-wide | Stub (#2) |
| `find_slow_queries` | Top N queries by average CPU from the plan cache | Stub (#3) |
| `check_blocking` | Live blocking-chain snapshot | Stub (#4) |
| `recent_migrations` | Recent MigrationOps applies: "what changed?" | Planned (#9) |

| Resource | Purpose | Status |
| --- | --- | --- |
| `sqlserver://schema/catalog` | Schemas → tables with approximate row counts, so the agent can pick a table without exploratory calls | Planned (#8) |

## Error contract

Every failure comes back as a tool result with `isError: true` and a JSON body the agent can act on (#7):

| `errorCategory` | Example | `isRetryable` |
| --- | --- | --- |
| `transient` | Timeout, server unreachable | Yes |
| `validation` | Table doesn't exist; database not managed by MigrationOps | No |
| `permission` | Login failed, permission denied | No |
| `business` | Request refused by the read-only policy | No |

A query that succeeds with no rows ("nothing is blocked") is **not** an error; it's an empty result. The agent needs to tell those apart.

## Setup

Requires .NET 8 SDK and a reachable SQL Server instance. The project lives in the `SqlServer.Mcp/` folder; from the repo root:

```bash
dotnet build SqlServer.Mcp
dotnet publish SqlServer.Mcp -c Release -o C:/Tools/SqlServer.Mcp
```

Register with Claude Code by adding `.mcp.json` to a project (or use `claude mcp add`). Keep the connection string out of the file with environment-variable expansion, so `.mcp.json` can be committed:

```json
{
  "mcpServers": {
    "sqlserver": {
      "type": "stdio",
      "command": "C:/Tools/SqlServer.Mcp/SqlServer.Mcp.exe",
      "args": [],
      "env": {
        "SQLSERVER_CONNECTION": "${SQLSERVER_CONNECTION}"
      }
    }
  }
}
```

Then set `SQLSERVER_CONNECTION` in your own environment, ideally with a read-only login:

```
Server=localhost;Database=TestDb;Integrated Security=true;TrustServerCertificate=true
```

Use the published executable rather than `dotnet run --project`, which rebuilds before every session starts. `dotnet run` is fine while developing the server itself.

Verify inside a Claude Code session with `/mcp`.

## Debugging without an LLM

Use the MCP Inspector to invoke tools directly. From the repo root:

```bash
npx @modelcontextprotocol/inspector dotnet run --project SqlServer.Mcp
```

Much faster feedback loop than prompting a model while iterating on tool behavior. Verify each error category there before testing with Claude (#5).

## Gotchas

- **Never write to stdout.** With stdio transport, stdout is the JSON-RPC wire: the client parses every line as a protocol message. A stray `Console.WriteLine`, or the default .NET console logger, corrupts the stream. All logging goes to stderr (already configured in `Program.cs`).
- **Tool descriptions are the API.** The `Description` attributes are the only thing the model sees when choosing tools. Iterate on them like a public interface, and re-run the whole selection prompt set (#6) after any change, because one description can shift another tool's routing.
- **Use a read-only SQL login.** The server doesn't write, but defense in depth costs nothing, and it means the server can never see more than its user can.

## Related projects

- [SqlServer.EngineeringToolkit](https://github.com/CatFortman/SqlServer.EngineeringToolkit): source of the diagnostic queries.
- [MigrationOps](https://github.com/CatFortman/MigrationOps): registers this server in its `.mcp.json` so Claude Code inspects real schema before writing migrations; `recent_migrations` reads its history tables.
- [SqlServer.OpsAgent](https://github.com/CatFortman/SqlServer.OpsAgent): attaches this server to its analyst subagents during incident triage.

## Learning context

Part of a hands-on lab for the Claude Certified Architect – Foundations exam. This repo covers tool descriptions and boundaries (2.1), structured error responses (2.2), MCP configuration in Claude Code (2.4) and trimming tool output (5.1). Issues carry the `ccar-f` label.
