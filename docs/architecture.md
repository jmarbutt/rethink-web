# Architecture

RethinkWeb is organized as an **App Manifest Runtime**: the developer-authored app layer is the center, and every rendering or transport surface reads from the same runtime metadata and permission-scoped manifest.

## Layers, Abstractions First

Every layer is defined by **interfaces in `RethinkWeb.Core`** with zero-dependency default implementations where practical. External dependencies (EF Core, Wolverine, Marten, MCP SDK, Hangfire) live in adapter packages you opt into. `RethinkWeb.Core` references nothing outside `Microsoft.Extensions.*` abstractions.

```text
┌─────────────────────────────────────────────────────────────┐
│  App model       Entity, planned View Profile, Query,       │
│                  Mutation, Action compatibility, Event,     │
│                  future Lifecycle Fact metadata.            │
├─────────────────────────────────────────────────────────────┤
│  Manifest layer  Permission-scoped public contract for      │
│                  renderers, HTTP, MCP, docs, inspectors,    │
│                  agents, and future tooling.                │
├─────────────────────────────────────────────────────────────┤
│  HTTP layer      ASP.NET Core minimal API endpoint mapper.  │
│                  Routes /<slug>, /<slug>/<id>, queries,     │
│                  mutations/actions, manifest. HTMX-aware.   │
├─────────────────────────────────────────────────────────────┤
│  Render layer    IEntityRenderer. Default: Razor Components │
│                  + HtmlRenderer for server-side rendering.  │
│                  Future renderers should consume profiles.  │
├─────────────────────────────────────────────────────────────┤
│  Operation layer IQuery<TInput, TOutput>,                   │
│                  IMutation<TEntity, TInput, TOutput>,       │
│                  IAction<TEntity, TInput, TOutput>          │
│                  compatibility, registries, dispatchers.    │
├─────────────────────────────────────────────────────────────┤
│  Event layer     IEventBus + IEventSubscriber<T>.           │
│                  Default: in-proc synchronous bus.          │
│                  Adapter packages can make this durable.    │
├─────────────────────────────────────────────────────────────┤
│  Lifecycle layer Planned ILifecycleSink + operation fact    │
│                  models. Append-only and observational.     │
├─────────────────────────────────────────────────────────────┤
│  Storage layer   IEntityStore<T>. Default: in-memory store. │
│                  Adapter: Store.EfCore today, others later. │
└─────────────────────────────────────────────────────────────┘
```

The metadata system (`[Entity]`, field attributes, `[Query]`, `[Mutation]`, `[Action]`, etc.) is the authoring model. The manifest JSON is the public contract.

## Manifest As The Public Contract

```text
C# app code
  Entities + fields
  View Profiles (planned)
  Queries
  Mutations/actions
  Events
  Lifecycle facts (planned)
  Permissions
        |
        v
Runtime metadata model
        |
        v
Manifest JSON, scoped by tenant + user + permissions
        |
        +--> HTMX/Razor renderer for optimized end-user UX
        +--> Inspector for dev/admin exploration and operation testing
        +--> MCP production surface for LLM clients
        +--> HTTP endpoints
        +--> Future renderers and documentation helpers
        +--> Agent context
```

The MVC/HTMX app a developer creates is an optimized UX for end users. It is not the whole framework. MCP, docs, inspectors, and future renderers should not scrape that UI; they should consume the manifest.

## Package Layout

| Package | Depends on | Purpose |
|---|---|---|
| `RethinkWeb.Core` | `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Logging.Abstractions` | Abstractions, attributes, in-proc defaults, query/mutation/action registries, manifest builder. Future home for View Profile and Lifecycle abstractions. |
| `RethinkWeb.Render.Razor` | `Core`, `Microsoft.AspNetCore.Components.Web` | Razor Components renderer using `HtmlRenderer`. Default `IEntityRenderer`. |
| `RethinkWeb.Http.MinimalApi` | `Core`, ASP.NET Core | Endpoint mapper, form binder, HTMX detection. |
| `RethinkWeb.Mcp` | `Core`, `ModelContextProtocol.AspNetCore` | Hosts a standards-compliant MCP server at `/mcp`. Builds tools from queries, mutations, and action compatibility registrations. |
| `RethinkWeb.Store.EfCore` | `Core`, `Microsoft.EntityFrameworkCore` | Swap-in `EfCoreEntityStore<TEntity, TContext>`. |
| `RethinkWeb.Sample.Notes` | All of the above + SQLite | Smallest sample: one entity, no operations, no events. |
| `RethinkWeb.Sample.Tasks` | Same | Canonical MVP sample: query, action, mutation, event subscriber, MCP coverage. |
| `RethinkWeb.Sample.Chat` | Same | Real-time sample with custom HTML escape hatch. |

A user can build a working app with just `Core + Render.Razor + Http.MinimalApi + Store.EfCore`. No Wolverine, no Marten, no MCP. Pay-as-you-go.

## Dependency Direction

```text
                        Sample.Tasks (web app)
                       /     |       \      \
                      v      v        v      v
              Render.Razor  Http   Mcp   Store.EfCore
                       \     |    /        /
                        v    v   v        v
                          Core (abstractions + defaults)
                                  |
                                  v
                  Microsoft.Extensions.* abstractions only
```

`Core` knows nothing about HTTP, Razor, MCP, or EF. Each adapter knows about `Core` and one external library. Apps know about `Core` and the adapters they choose to use.

## Pluggability Rule

> No `new SomeImplementation()` in framework code, ever. Constructor-inject `IFoo`. Register a default with `TryAddSingleton`/`TryAddScoped` so user overrides win without `services.Remove()` gymnastics.

If a primitive is registered as `Singleton` but consumes `Scoped` services (for example `IAuthContext`), it must itself be `Scoped`. The test host enforces this; production would not catch it early. See [`testing.md`](./testing.md).

For a visual map of package and runtime boundaries, see [`architecture-boundaries.md`](./architecture-boundaries.md).

## View Profiles Boundary

The current MVP lets field attributes carry simple grid/edit hints. That is acceptable for a prototype and too small for the thesis.

The intended split:

- Entity metadata owns field semantics, validation, permissions, and storage participation.
- View Profiles own context-specific presentation: grid, detail, edit, card, lookup, and operation forms.
- Renderers own HTML/control implementation and may swap default controls for richer ones.
- Custom pages remain first-class escape hatches when generated views are the wrong tool.

This boundary matters because "richer combo box for contacts" should not require changing the business object into a UI component catalog.

## Lifecycle Boundary

Lifecycle is planned as a core observational layer, not an adapter afterthought.

The first version should record operation facts:

- Actor, tenant, timestamp, and correlation id
- Operation kind and name
- Entity slug and id when relevant
- Status, duration, and error summary
- Compact safe summaries of input/output or before/after state

It should not be event sourcing in v1. Full snapshots, point-in-time reconstruction, and Marten-style event storage are future adapter pressure, not the first abstraction.

## Why This Shape

1. **The app layer is stable.** Rendering and transport surfaces can change without forcing the business contract to move.
2. **The manifest is portable.** Web, MCP, docs, inspectors, and agents read the same contract instead of reverse-engineering each other.
3. **Test isolation stays practical.** Every layer can be unit-tested against in-proc defaults.
4. **Framework lock-in is bounded.** EF, Wolverine, Marten, Hangfire, and MCP remain opt-in adapter concerns.
5. **AI agents get a map.** Agents can read the manifest and local conventions before editing code, instead of spelunking through disconnected controllers and UI files like tiny confused interns.

## What's Deliberately Not Abstracted

- **Razor itself** is not behind an interface. The renderer is. If you want a different templating engine, write a new `IEntityRenderer`.
- **ASP.NET Core** is not behind an interface. The endpoint mapper is an extension method on `IEndpointRouteBuilder`. If you want a different HTTP host, write a different mapper package.
- **The metadata attributes** are not hidden behind another abstraction. They are the C# authoring model and should evolve additively.
- **Query read-only behavior** is semantic, not sandboxed. Queries receive a read-oriented context, but a handler can still inject services. Production apps should treat side-effecting queries as a contract violation.

## Where It Gets Harder

- Generated views will not fit every screen. Analytics dashboards, multi-step wizards, drag/drop boards, and dense operational consoles still need custom pages.
- View Profiles must avoid becoming a visual designer with code syntax. The point is a renderer contract, not a no-code swamp.
- HTMX roundtrips can feel slower than optimistic client state. Use local interactivity where it earns its keep.
- "Same app for mobile" means webview, manifest-driven custom renderer, or separate JSON API. Pick consciously.
