# DocMost MCP Server

MCP server that exposes [Docmost](https://docmost.com) wiki pages and spaces as tools for AI agents (GitHub Copilot, Claude, etc.).

## Configuration

All configuration is done via **environment variables**:

| Variable | Required | Default | Description |
|---|---|---|---|
| `DOCMOST_URL` | **Yes** | — | Base URL of your Docmost instance (e.g. `http://localhost:3000`) |
| `DOCMOST_EMAIL` | **Yes** | — | Email address for Docmost authentication |
| `DOCMOST_PASSWORD` | **Yes** | — | Password for Docmost authentication |
| `DOCMOST_MCP_TRANSPORT` | No | auto | Transport mode: `stdio` (local, default with TTY) or `http` (remote) |
| `DOCMOST_MCP_PORT` | No | `3001` | Port number for HTTP transport |

### Transport auto-detection

If `DOCMOST_MCP_TRANSPORT` is not set:
- **TTY present** (interactive terminal) → `stdio` mode
- **No TTY** (redirected stdin, container) → `http` mode

## Usage

### Stdio mode (VS Code / Copilot)

Create `.vscode/mcp.json` at the project root:

```json
{
  "servers": {
    "docmost": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "path/to/DocMostMcp.Server/DocMostMcp.Server.csproj"
      ],
      "env": {
        "DOCMOST_URL": "http://localhost:3000",
        "DOCMOST_EMAIL": "admin@example.com",
        "DOCMOST_PASSWORD": "your-password"
      }
    }
  }
}
```

### HTTP mode

```bash
export DOCMOST_MCP_TRANSPORT=http
export DOCMOST_MCP_PORT=3001
export DOCMOST_URL=http://localhost:3000
export DOCMOST_EMAIL=admin@example.com
export DOCMOST_PASSWORD=your-password

dotnet run --project DocMostMcp.Server
```

Then configure the MCP client to connect to `http://localhost:3001/mcp`.

## Tools

| Tool | Description |
|---|---|
| `list_spaces(query?, limit?, cursor?)` | Lists spaces the user has access to |
| `get_space_info(spaceId)` | Gets detailed space information |
| `list_sidebar_pages(spaceId?, pageId?, limit?, cursor?, query?)` | Lists sidebar pages |
| `get_page(pageId, includeContent?, format?)` | Gets page details (default format: markdown) |
| `create_page(spaceId, title?, icon?, parentPageId?, content?)` | Creates a new page (markdown) |
| `update_page(pageId, title?, icon?, content?, operation?)` | Updates a page (markdown, default operation: replace) |
| `delete_page(pageId, permanentlyDelete?)` | Deletes a page (default: soft-delete) |
| `search_pages(query, spaceId?, creatorId?, limit?, offset?)` | Full-text search across pages |

All tools return a standard JSON envelope:
```json
// Success
{ "ok": true, "statusCode": 200, "data": { ... } }

// Error
{ "ok": false, "statusCode": 404, "error": "Page not found", "details": "..." }
```

## How it runs

The server is compiled and published as a **Native AOT** binary for `linux-musl-x64`.

- The Docker image is based on `mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine` (no .NET runtime — the binary is a standalone ELF executable).
- Expected image size: **< 150 MB** (ideally ~100–150 MB).
- Startup time: **milliseconds** (no JIT compilation, no assembly loading).
- Build stage uses the official .NET SDK image with `clang` and `zlib1g-dev` installed for the AOT native compiler toolchain.

> **HTTP endpoint path:** The server serves the MCP streamable HTTP transport at `/mcp` (not at `/`), following the [SDK convention](https://github.com/modelcontextprotocol/csharp-sdk). Clients must connect to `http://<host>:<port>/mcp`.

### Adding a new tool type

The MCP server registers tools via the generic `WithTools<T>()` API (AOT-safe), not via `WithToolsFromAssembly()`.  
To add a new tool class decorated with `[McpServerToolType]`:

1. Add the new class in the `DocMostMcp.Server/Tools/` directory.
2. Register it in `Program.cs` by adding `.WithTools<YourNewToolType>()` to the service configuration (both stdio and HTTP branches).
3. If the new tool returns types that are not yet in the `AppJsonSerializerContext`, add `[JsonSerializable(typeof(...))]` for those types.

Without explicit registration in `Program.cs`, the new tool class will **not** be exposed to MCP clients.

## Troubleshooting

| Problem | Likely cause |
|---|---|
| `DOCMOST_EMAIL and DOCMOST_PASSWORD are correct` | Invalid credentials. Verify env vars |
| `DOCMOST_URL must be an absolute HTTP or HTTPS URL` | `DOCMOST_URL` is missing or malformed |
| Network error connecting to Docmost | Docmost instance is not reachable at the configured URL |
| Port already in use | Change `DOCMOST_MCP_PORT` or stop the process using it |
| Tool returns `{ "ok": false, "statusCode": 401 }` | Session expired or credentials changed. Restart the server |
| 404 al conectar al endpoint HTTP | Apuntando a `http://host:port/` en lugar de `http://host:port/mcp`. Añade `/mcp` al final de la URL |
