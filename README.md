# RethinkWeb

> A .NET 9 framework prototype where one entity definition becomes a web UI, an HTTP API, and an MCP tool.

**Status: thought-exercise prototype.** Personal R&D, not shipping. Public so it can be cloned, criticized, and learned from. Not accepting issues or PRs.

## What it is

A single attribute-driven C# entity becomes:

- A **grid view** and **edit form** rendered server-side as HTML
- **HTTP form-post endpoints** that swap fragments via HTMX (no JSON contract, no SPA build pipeline)
- **MCP tools** auto-discovered through `tools/list` and invokable via `tools/call`
- A **manifest** at `/_framework/manifest` describing every entity, field, and action — for humans, docs, and LLMs

```csharp
[Entity(slug: "donors", displayName: "Donors")]
public class Donor
{
    public Guid Id { get; set; }

    [TextBox("First Name", GridVisible = true, GridOrder = 1, Required = true)]
    public string FirstName { get; set; } = "";

    [CurrencyBox("Year-To-Date Total", Disabled = true, GridVisible = true, GridOrder = 5)]
    public decimal YearToDateTotal { get; set; }
}

[Action("update-address", "Update Address", Description = "Update the postal address.")]
public sealed class UpdateAddressAction(IEntityStore<Donor> store)
    : IAction<Donor, AddressInput, AddressResult> { /* ... */ }
```

That's all the app code needed for a working donor list, an editable form with HTMX swaps, an MCP tool named `donors.update-address`, and a JSON manifest entry.

## Why

Built by a long-time .NET developer burned out on the React/SPA tax (build pipelines, npm supply chain, type drift between server and client, form pain, timezone hell) but who still has customers expecting React-feeling apps. The bet: **HTMX + server-rendered metadata can produce a UX good enough that you don't miss React** — *and* the same metadata that drives the UI can drive MCP, docs, and LLM context for free.

See [`docs/concepts.md`](./docs/concepts.md) for the mental model and [`docs/architecture.md`](./docs/architecture.md) for how the layers fit.

## Goals

1. **Server-rendered HTML over the wire.** No SPA, no build pipeline, no JSON contract to drift. Types can't go out of sync because there's only one side.
2. **Metadata once, rendered everywhere.** Entity attributes drive grid columns, edit forms, validation, and the manifest. Change the database, the UI follows.
3. **One action definition surfaces in three places.** HTTP endpoint, MCP tool, rendered button. Same code, same auth, same audit.
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
| `IAction<TEntity, TInput, TOutput>` with auto-registered HTTP endpoint | ✅ |
| MCP `tools/list` + `tools/call` over plain HTTP, schema generated from action input | ✅ |
| `EntitySaved<TEntity>` event auto-published after every save | ✅ |
| `IEventSubscriber<T>` pattern for computed fields + side effects | ✅ |
| Manifest at `/_framework/manifest` (entities + fields + actions + permissions) | ✅ |
| In-proc default implementations of every cross-cutting concern | ✅ |
| `IClock` / `IIdGenerator` / `IAuthContext` injected for deterministic tests | ✅ |
| 12 tests passing across 3 test projects (xUnit + Verify snapshots + WebApplicationFactory) | ✅ |

## What's deferred (Phase 2+)

See [`docs/roadmap.md`](./docs/roadmap.md). Headlines:

- **Workflows + triggers + per-entity lifecycle timeline** — the killer feature for multi-day flows like "ACH donation pending for 3 days"
- **Framework Inspector page** at `/_framework` — Django-Admin-meets-OpenTelemetry for the framework's own metadata
- **MCP Streamable HTTP transport** — current implementation is plain HTTP JSON, fine for testing, not Claude-Desktop-compatible
- **Wolverine / Marten / Hangfire adapter packages**
- **Per-field permission rendering** — auth metadata exists but renderer doesn't filter yet
- **LLM doc helper** with explicit "show me what the LLM saw" transparency

## Quickstart

```bash
git clone https://github.com/jmarbutt/rethink-web.git
cd rethink-web
dotnet run --project src/RethinkWeb.Sample.Donor
```

Then:
- Open the URL printed in the console (default `http://localhost:5000`)
- Click a donor, edit, save — HTMX swaps the form fragment in place
- Visit `/_framework/manifest` to see the JSON description of the app
- POST to `/mcp/tools/list` and `/mcp/tools/call` to invoke actions as MCP tools

## Run the tests

```bash
dotnet test
```

12 tests across `RethinkWeb.Core.Tests`, `RethinkWeb.Render.Razor.Tests` (Verify snapshots), and `RethinkWeb.Sample.Donor.Tests` (WebApplicationFactory end-to-end).

## Documentation

- [`docs/architecture.md`](./docs/architecture.md) — five layers, package layout, dependency rules
- [`docs/concepts.md`](./docs/concepts.md) — entities, fields, actions, events, manifest mental model
- [`docs/adding-an-entity.md`](./docs/adding-an-entity.md) — practical walkthrough
- [`docs/adding-an-action.md`](./docs/adding-an-action.md) — practical walkthrough including MCP exposure
- [`docs/testing.md`](./docs/testing.md) — test patterns per layer, deterministic-by-default rules
- [`docs/roadmap.md`](./docs/roadmap.md) — what's done, what's Phase 2, what's deliberately not

## Honest caveats

- **The author has built something like this 3+ times before.** Each prior version shipped value internally but never reached "mass use." The framework pattern itself is not the failure mode — escape hatches for "the one weird screen" are. Every primitive in here is meant to fail open: write a Razor partial directly when the metadata renderer doesn't fit.
- **The MCP transport is shaped right but not standards-compliant.** `/mcp/tools/list` and `/mcp/tools/call` are plain HTTP JSON — they prove the unification works but won't talk to Claude Desktop until Streamable HTTP is wired up. Phase 2.
- **`FormBinder` is reflection-heavy.** Production would replace it with FastEndpoints/MVC binding or a source generator. Fine for the prototype.
- **No security review.** Don't put this on the internet without an `IAuthContext` implementation that isn't `AllowAllAuthContext`.

## License

MIT (see [`LICENSE`](./LICENSE)).
