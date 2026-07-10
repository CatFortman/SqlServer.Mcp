# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

An MCP server exposing read-only SQL Server diagnostics as tools, built on the official `ModelContextProtocol` C# SDK (stdio transport). Diagnostic SQL is ported from CatFortman/SqlServer.EngineeringToolkit.

## Commands

- Build: `dotnet build`
- Run (requires `SQLSERVER_CONNECTION` env var): `dotnet run`
- Manual tool testing: `npx @modelcontextprotocol/inspector dotnet run --project .`

## Hard rules

- **Read-only server.** Never add tools that execute DDL, DML writes, or maintenance commands (index rebuilds, DBCC, etc.). If a task seems to require writes, stop and ask.
- **Never write to stdout.** Stdout is the JSON-RPC channel. Logging goes to stderr only (configured in Program.cs). No `Console.WriteLine` anywhere.
- **All queries parameterized.** Identifiers that cannot be parameterized (table names) must be validated against system catalogs (`sys.tables`) before use.
- **Keep tool results small.** Truncate query text fields to ~500 chars. Cap row counts. Oversized results waste the client's context window.
- No new NuGet packages without asking.

## Conventions

- Tools are static methods in `Tools/`, attributed with `[McpServerToolType]` / `[McpServerTool]`, returning JSON-serialized results as `string`.
- `Description` attributes on tools and parameters are user-facing API. When adding or changing a tool, write the description for a model deciding *when* to call it: what it does, when to use it, and any caveats (e.g. "plan cache only, not historical").
- Data access goes through `SqlQueryRunner`. Don't open connections in tool methods.

## Definition of done

- `dotnet build` succeeds with no new warnings.
- Tool verified against a real database via MCP Inspector.
- Description attribute reviewed for tool-selection clarity.
