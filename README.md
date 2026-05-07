# RethinkWeb

> A .NET 9 framework prototype where server-side metadata becomes a web UI, safe HTTP operations, MCP tools, and a manifest contract.

**Status: thought-exercise prototype.** Personal R&D, not shipping. Public so it can be cloned, criticized, and learned from. Not accepting issues or PRs.

## What it is

A single attribute-driven C# app model becomes:

- A **grid view** and **edit form** rendered server-side as HTML
- **HTTP form-post endpoints** that swap fragments via HTMX (no JSON contract, no SPA build pipeline)
- **Queries** for safe, typed reads that can later be cached per tenant or per user
- **Mutations/actions** for server-driven state changes
- **MCP tools** auto-discovered through `tools/list` and invokable via `tools/call`
- A **manifest** at `/_framework/manifest` describing entities, fields, queries, mutations, and actions for humans, docs, renderers, and LLMs

```csharp
[Entity(slug: "tasks", displayName: "Tasks")]
public class Todo
{
    public Guid Id { get; set; }

    [TextBox("Title", GridVisible = true, GridOrder = 1, Required = true)]
    public string Title { get; set; } = "";

    [CheckBox("Completed", GridVisible = true, GridOrder = 2)]
    public bool Completed { get; set; }

    [DateBox("Completed At", Disabled = true)]
    public DateTime? CompletedAt { get; set; }
}

[Action("mark-complete", "Mark Complete", Description = "Mark a task as completed.")]
public sealed class MarkCompleteAction(IEntityStore<Todo> store)
    : IAction<Todo, MarkCompleteInput, MarkCompleteResult> { /* ... */ }
```

That's all the app code needed for a working task list, an editable form with HTMX swaps, an MCP tool named `tasks.mark-complete`, a `EntitySaved<Todo>` event subscriber that auto-stamps `CompletedAt`, and a JSON manifest entry.

## Why

Built by a long-time .NET developer burned out on the React/SPA tax (build pipelines, npm supply chain, type drift between server and client, form pain, timezone hell) but who still has customers expecting React-feeling apps. The bet: **HTMX + server-rendered metadata can produce a UX good enough that you don't miss React** — *and* the same metadata that drives the UI can drive MCP, docs, and LLM context for free.

See [`docs/concepts.md`](./docs/concepts.md) for the mental model and [`docs/architecture.md`](./docs/architecture.md) for how the layers fit.

## Goals

1. **Server-rendered HTML over the wire.** No SPA, no build pipeline, no JSON contract to drift. Types can't go out of sync because there's only one side.
2. **Metadata once, rendered everywhere.** Entity attributes drive grid columns, edit forms, validation, and the manifest. Change the database, the UI follows.
3. **Queries and mutations are first-class operations.** Queries are safe typed reads; mutations/actions change state. Both surface through the manifest, HTTP, MCP, and future renderers.
4. **Pluggable abstractions, opt-in implementations.** `RethinkWeb.Core` has zero external dependencies. Wolverine, Marten, EF Core, MediatR are all opt-in adapter packages — never required.
5. **Each layer testable in isolation.** Default in-proc implementations make unit tests fast. `IClock`/`IIdGenerator`/`IAuthContext` are injected so tests are deterministic.
6. **Tightly coupled, deliberately.** Feature folders. No repository pattern wrapping EF. Conventions over configuration so AI agents can write code in this framework confidently.

## Non-goals

- **Mass adoption.** This is personal use. No issues, no PRs, no roadmap obligations.
- **Cross-platform UI generation.** Web is the target. Mobile gets HTML in a webview, not a code-generated native client.
- **Real-time collaborative editing.** If you need that, you've outgrown this framework.
- **Visual workflow designers, drag-and-drop UI builders.** Code-only.
- **A framework that hides everything.** Manifest is a curl-able JSON document. The Inspector page (Phase 2) shows the registered metadata directly. LLM helpers always show what context they used.

## What works today (MVP)

| Capability | Status |
|---|---|
| Attribute-driven entity metadata (Text/Number/Currency/Date/Checkbox/Select/Phone fields) | ✅ |
| Razor Components renderer (Grid + Edit views) | ✅ |
| HTMX-aware endpoint mapper (returns fragments to `HX-Request`, layouts otherwise) | ✅ |
| Form binding and persistence via reflective EF Core store | ✅ |
| `IQuery<TInput, TOutput>` with manifest + HTTP + MCP exposure | ✅ |
| `IMutation<TEntity, TInput, TOutput>` as the long-term entity mutation primitive | ✅ |
| `IAction<TEntity, TInput, TOutput>` compatibility path with auto-registered HTTP endpoint | ✅ |
| MCP server (Streamable HTTP transport) via official `ModelContextProtocol` SDK — Claude Desktop / MCP Inspector compatible | ✅ |
| MCP tools dynamically registered from `IActionRegistry`; JSON Schema auto-generated from action input record | ✅ |
| `EntitySaved<TEntity>` event auto-published after every save (HTMX form post, action via dispatcher, MCP tool call — all flow through `PublishingEntityStore<T>`) | ✅ |
| Server-enforced `Required` validation in `FormBinder` (returns HTTP 422 on missing required fields) | ✅ |
| HTTP-level `ReadPermission` / `WritePermission` enforcement on entity routes; per-field `EditPermission` skip in `FormBinder` | ✅ |
| Explicit `POST /{slug}/{id}/actions/{name}` endpoint that dispatches via `IActionDispatcher` | ✅ |
| **Multi-tenancy as foundational opt-in** — `UseMultiTenant<TResolver>()` + `ITenantOwned` entities. Discriminator-column model, auto-stamp on save, cross-tenant access throws, defense-in-depth via `TenantScopedEntityStore` decorator + EF Core `HasQueryFilter`. Single-tenant remains the default. | ✅ |
| `IEventSubscriber<T>` pattern for computed fields + side effects | ✅ |
| Manifest at `/_framework/manifest` (entities + fields + queries + mutations + actions + permissions) | ✅ |
| In-proc default implementations of every cross-cutting concern | ✅ |
| `IClock` / `IIdGenerator` / `IAuthContext` injected for deterministic tests | ✅ |
| 32 tests passing across 3 test projects (xUnit + Verify snapshots + WebApplicationFactory + real `McpClient` ↔ `McpServer` over in-memory pipes) | ✅ |

## What's deferred (Phase 2+)

See [`docs/roadmap.md`](./docs/roadmap.md). Headlines:

- **Workflows + triggers + per-entity lifecycle timeline** — the killer feature for multi-day flows like "ACH payment pending for 3 days"
- **Framework Inspector page** at `/_framework` — Django-Admin-meets-OpenTelemetry for the framework's own metadata
- **Wolverine / Marten / Hangfire adapter packages**
- **Per-field permission rendering** — auth metadata exists but renderer doesn't filter yet
- **LLM doc helper** with explicit "show me what the LLM saw" transparency

## Sample apps (simple → complex)

Three sample projects in this repo, each adding one concept to the previous:

| Project | What it demonstrates | Lines of app code |
|---|---|---|
| [`Sample.Notes`](./src/RethinkWeb.Sample.Notes) | Smallest possible app: one entity, no actions, no events. Pure CRUD via auto-generated routes + manifest + MCP. | ~40 |
| [`Sample.Tasks`](./src/RethinkWeb.Sample.Tasks) | Adds an action (`MarkComplete`) and an `EntitySaved<Todo>` subscriber that stamps `CompletedAt`. The unification demo — same action invokable as form post, action endpoint, or MCP tool. | ~120 |
| [`Sample.Chat`](./src/RethinkWeb.Sample.Chat) | Two entities (Channel, Message), an action on Channel that posts a message, and an HTMX-SSE channel that pushes new messages live to subscribed browsers. Demonstrates real-time without WebSockets *and* the escape hatch for hand-rolled HTML when the metadata renderer doesn't fit. | ~200 |

`Sample.Tasks` is the canonical demo and the one the test suite targets. Notes is the "Hello World"; Chat is the maximalist showpiece.

## Quickstart

```bash
git clone https://github.com/jmarbutt/rethink-web.git
cd rethink-web
dotnet run --project src/RethinkWeb.Sample.Tasks
```

Then:
- Open the URL printed in the console (default `http://localhost:5000`)
- Click a task, edit, save — HTMX swaps the form fragment in place
- Check "Completed" on a task; the `EntitySaved<Todo>` subscriber auto-stamps `CompletedAt`
- Visit `/_framework/manifest` to see the JSON description of the app
- Connect an MCP client (Claude Desktop, MCP Inspector, Cursor, etc.) to `http://localhost:5000/mcp` — see [`docs/mcp-clients.md`](./docs/mcp-clients.md)

For real-time, run the chat sample:
```bash
dotnet run --project src/RethinkWeb.Sample.Chat
# then open http://localhost:5000/chat in two tabs and watch messages stream live
```

## Run the tests

```bash
dotnet test
```

32 tests across `RethinkWeb.Core.Tests`, `RethinkWeb.Render.Razor.Tests` (Verify snapshots), and `RethinkWeb.Sample.Tasks.Tests` (WebApplicationFactory end-to-end + real `McpClient` ↔ `McpServer` over in-memory pipes).

## Documentation

- [`docs/architecture.md`](./docs/architecture.md) — layers, package layout, manifest contract, dependency rules
- [`docs/concepts.md`](./docs/concepts.md) — entities, fields, actions, events, manifest mental model
- [`docs/query-mutation-plan.md`](./docs/query-mutation-plan.md) — Query/Mutation direction, cache contract, Inspector implications
- [`docs/adding-an-entity.md`](./docs/adding-an-entity.md) — practical walkthrough
- [`docs/adding-an-action.md`](./docs/adding-an-action.md) — practical walkthrough including MCP exposure
- [`docs/mcp-clients.md`](./docs/mcp-clients.md) — connect Claude Desktop / MCP Inspector / Cursor / programmatic clients
- [`docs/multi-tenancy.md`](./docs/multi-tenancy.md) — `UseMultiTenant<TResolver>()`, `ITenantOwned`, EF filter, resolution strategies
- [`docs/testing.md`](./docs/testing.md) — test patterns per layer, deterministic-by-default rules
- [`docs/roadmap.md`](./docs/roadmap.md) — what's done, what's Phase 2, what's deliberately not

## Honest caveats

- **The author has built something like this 3+ times before.** Each prior version shipped value internally but never reached "mass use." The framework pattern itself is not the failure mode — escape hatches for "the one weird screen" are. Every primitive in here is meant to fail open: write a Razor partial directly when the metadata renderer doesn't fit.
- **`FormBinder` is reflection-heavy.** Production would replace it with FastEndpoints/MVC binding or a source generator. Fine for the prototype. It does enforce `Required` and `EditPermission` server-side; missing required fields return HTTP 422.
- **No security review.** Don't put this on the internet without an `IAuthContext` implementation that isn't `AllowAllAuthContext`. The framework consults `IAuthContext` on entity GET/POST/actions and per-field edits, but the default context grants everything — wire your real one. Don't expose `/mcp` publicly without auth — the MCP SDK supports OAuth but it's not bound to `IAuthContext` here yet.
- **Renderer doesn't filter fields by `ReadPermission` yet** — server-side write enforcement is in (`FormBinder` skips fields the user can't edit), but the rendered HTML shows all fields. Don't rely on hiding sensitive fields via metadata until Phase 2 wires the renderer-side filter.
- **MCP tool error messages are intentionally verbose.** The current implementation surfaces inner exception details in tool result text to help debug the prototype. Flip the catch in `RethinkWebMcpToolCollection` to `throw` (or wire the SDK's error-handling request filter) before exposing this beyond your local machine — otherwise stack traces leak to MCP clients.

## License

MIT (see [`LICENSE`](./LICENSE)).
