# Connecting MCP clients

The framework hosts a standards-compliant **Model Context Protocol** server using the official [`ModelContextProtocol.AspNetCore`](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore) SDK. Any MCP client speaking the Streamable HTTP transport (current spec, June 2025) can connect to `http://localhost:5099/mcp` (or wherever you mapped it).

This page covers four common ways to test:

1. [MCP Inspector](#1-mcp-inspector-recommended-for-quick-validation) — official browser-based debug client
2. [Claude Desktop](#2-claude-desktop) — through a stdio bridge
3. [Cursor / Goose / others](#3-cursor--goose--other-clients) — direct HTTP connection
4. [Programmatic from C#](#4-programmatic-from-c) — for tests / your own clients

## Boot the sample app

```bash
dotnet run --project src/RethinkWeb.Sample.Donor --urls http://localhost:5099
```

Endpoint: `http://localhost:5099/mcp`. Transport: Streamable HTTP, stateless. Tools registered: one per `[Action]` declared in the app (currently `donors.update-address`).

## 1. MCP Inspector (recommended for quick validation)

The official browser-based MCP debugger. Zero install — runs via `npx`.

```bash
npx @modelcontextprotocol/inspector
```

Then in the Inspector UI:

- **Transport Type**: `Streamable HTTP`
- **URL**: `http://localhost:5099/mcp`
- Click **Connect**

You should see:
- The `initialize` handshake succeed
- The **Tools** tab list `donors.update-address` with its auto-generated input schema (`entityId`, `input.address1`, `input.city`, …)

To invoke the tool, you need a real donor ID. Get one quickly:

```bash
curl -s http://localhost:5099/donors | grep -o 'donors/[a-f0-9-]\{36\}' | head -1
```

Then in the Inspector's tool call form, paste:

```json
{
  "entityId": "<the guid you just copied>",
  "input": {
    "address1": "1 Test Way",
    "address2": null,
    "city": "Testopolis",
    "state": "CA",
    "postalCode": "90210"
  }
}
```

Click **Run**. You should see a JSON result with `donorId` and `fullAddress`. Refresh the donor edit page in your browser at `http://localhost:5099/donors/<id>` to confirm the address persisted. **Same data path as the HTML form post.**

## 2. Claude Desktop

Claude Desktop uses **stdio** transport, not HTTP. To bridge to your HTTP-hosted server, use [`mcp-remote`](https://www.npmjs.com/package/mcp-remote):

Edit `~/Library/Application Support/Claude/claude_desktop_config.json` (macOS) or `%APPDATA%\Claude\claude_desktop_config.json` (Windows):

```json
{
  "mcpServers": {
    "rethink-web-donor": {
      "command": "npx",
      "args": [
        "-y",
        "mcp-remote",
        "http://localhost:5099/mcp",
        "--transport",
        "http-only"
      ]
    }
  }
}
```

Restart Claude Desktop. In a new chat, the tool icon should show `donors.update-address` available. Ask Claude:

> "List the donors I have, then update donor X's address to 1 Test Way, Testopolis, CA 90210."

Claude will call `donors.update-address` directly. (The "list donors" part doesn't have a tool yet — that's a future addition once a list/query action is added to the framework.)

## 3. Cursor / Goose / other clients

Most MCP clients now support direct Streamable HTTP. Configuration is client-specific but always boils down to:

- **Transport**: Streamable HTTP (or "HTTP" / "SSE" depending on naming)
- **URL**: `http://localhost:5099/mcp`
- **Auth**: none (unless you've wired `IAuthContext`)

For Cursor: `Settings → MCP → Add server → HTTP → URL`.

For Goose: in `~/.config/goose/profiles.yaml` add an `extensions` entry of type `mcp` with the URL.

## 4. Programmatic from C#

This is what `tests/RethinkWeb.Sample.Donor.Tests/EndToEndTests.cs` does. Useful for integration tests or building your own client.

```csharp
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

await using var transport = new HttpClientTransport(new HttpClientTransportOptions
{
    Endpoint = new Uri("http://localhost:5099/mcp"),
    TransportMode = HttpTransportMode.StreamableHttp,
});

await using var client = await McpClient.CreateAsync(transport);

var tools = await client.ListToolsAsync();
var tool = tools.Single(t => t.Name == "donors.update-address");

var result = await tool.CallAsync(new Dictionary<string, object?>
{
    ["entityId"] = "11111111-1111-1111-1111-111111111111",
    ["input"] = new Dictionary<string, object?>
    {
        ["address1"] = "1 Test Way",
        ["city"] = "Testopolis",
        ["state"] = "CA",
        ["postalCode"] = "90210",
    },
});

Console.WriteLine(result.Content.OfType<TextContentBlock>().First().Text);
```

For tests against `WebApplicationFactory<Program>`, the test in this repo uses an **in-memory pipe transport** (no real HTTP) — see `MCP_tools_call_invokes_update_address_via_real_McpClient` for the pattern.

## Troubleshooting

**"An error occurred invoking 'donors.update-address'"** — by SDK design, the protocol-level error message is generic. The framework's prototype build surfaces the real exception text in the tool result content (look for `ERROR in <action>: ...`). Read it. Common causes: invalid GUID for `entityId`, missing required input fields, the entity doesn't exist in the DB.

**Inspector connects but no tools appear** — most likely the action has `ExposeToMcp = false` set on its `[Action]` attribute. Default is `true`.

**Claude Desktop can't reach the server** — `mcp-remote` requires the server to be reachable from Claude Desktop's process. If you're running the app in Docker or behind a firewall, expose the port. Also check that `--transport http-only` is set; otherwise `mcp-remote` may try SSE-only mode.

**Schema looks wrong** — the SDK auto-generates the JSON Schema from the action's `TInput` record properties. Reflect on what your action actually expects: nullable properties become non-required, default values become defaults, `[Description]` attributes on properties (if added) become descriptions in the schema.

**Want auth?** The MCP SDK supports OAuth and bearer tokens via the transport options. Wire `IAuthContext` in the framework first, then add the matching auth middleware in the host. Out of scope for the prototype.
