# Architecture

## Five layers, abstractions first

Every layer is defined by **interfaces in `RethinkWeb.Core`** with a **zero-dependency default implementation** that lives in the same package. Every external dependency (EF Core, Wolverine, Marten, MCP SDK) ships as a *separate adapter package* you opt into. `RethinkWeb.Core` references nothing outside `Microsoft.Extensions.*` abstractions.

```
┌─────────────────────────────────────────────────────────────┐
│  HTTP layer      ASP.NET Core minimal API endpoint mapper.  │
│                  Routes /<slug>, /<slug>/<id>, actions,     │
│                  manifest. HTMX-aware: returns fragments     │
│                  to HX-Request, full pages otherwise.        │
├─────────────────────────────────────────────────────────────┤
│  Render layer    IEntityRenderer.                            │
│                  Default impl: Razor Components +            │
│                  HtmlRenderer for server-side static         │
│                  rendering. Swap freely.                     │
├─────────────────────────────────────────────────────────────┤
│  Action layer    IAction<TEntity, TInput, TOutput>,         │
│                  IActionRegistry, IActionDispatcher.         │
│                  Single definition surfaces as HTTP          │
│                  endpoint, MCP tool, UI button, audit event. │
├─────────────────────────────────────────────────────────────┤
│  Event layer     IEventBus + IEventSubscriber<T>.           │
│                  Default: in-proc synchronous bus            │
│                  (~50 lines, no deps). Adapter packages:    │
│                  Bus.Wolverine, Bus.MediatR, Bus.MassTransit.│
├─────────────────────────────────────────────────────────────┤
│  Storage layer   IEntityStore<T>.                            │
│                  Default: in-memory dictionary store         │
│                  (for tests + tiny apps). Adapter:          │
│                  Store.EfCore (shipped), Store.Marten,       │
│                  Store.Dapper.                               │
└─────────────────────────────────────────────────────────────┘
```

The **metadata system** (`[Entity]`, `[TextBox]`, `[SelectBox]`, etc.) lives in `Core` and is the single source of truth that all five layers consult.

## Pluggability rule

> No `new SomeImplementation()` in framework code, ever. Constructor-inject `IFoo`. Register a default with `TryAddSingleton`/`TryAddScoped` so user overrides win without `services.Remove()` gymnastics.

If a primitive is registered as `Singleton` but consumes `Scoped` services (e.g. `IAuthContext`), it must itself be `Scoped`. The test host enforces this; production wouldn't catch it. (See [`testing.md`](./testing.md) — this exact bug surfaced during initial scaffolding and is why `IEventBus` and `IManifestBuilder` are scoped, not singleton.)

## Package layout

| Package | Depends on | Purpose |
|---|---|---|
| `RethinkWeb.Core` | `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Logging.Abstractions` | Abstractions, attributes, in-proc defaults, registries, manifest builder. |
| `RethinkWeb.Render.Razor` | `Core`, `Microsoft.AspNetCore.Components.Web` | Razor Components renderer using `HtmlRenderer`. Default `IEntityRenderer`. |
| `RethinkWeb.Http.MinimalApi` | `Core`, ASP.NET Core | Endpoint mapper, form binder, HTMX detection. |
| `RethinkWeb.Mcp` | `Core`, ASP.NET Core | `tools/list` + `tools/call` HTTP endpoints. |
| `RethinkWeb.Store.EfCore` | `Core`, `Microsoft.EntityFrameworkCore` | Swap-in `EfCoreEntityStore<TEntity, TContext>`. |
| `RethinkWeb.Sample.Donor` | All of the above + `Microsoft.EntityFrameworkCore.Sqlite` | Reference app. |

A user can build a working app with just `Core + Render.Razor + Http.MinimalApi + Store.EfCore`. No Wolverine, no Marten, no MCP. Pay-as-you-go.

## Dependency direction

```
                        Sample.Donor (web app)
                       /     |       \      \
                      ↓      ↓        ↓      ↓
              Render.Razor  Http   Mcp   Store.EfCore
                       \     |    /        /
                        ↓    ↓   ↓        ↓
                          Core (abstractions + defaults)
                                  |
                                  ↓
                  Microsoft.Extensions.* abstractions only
```

`Core` knows nothing about HTTP, Razor, MCP, or EF. Each adapter knows about `Core` and one external library. Apps know about `Core` and the adapters they choose to use.

## Why this shape

1. **Test isolation.** Every layer can be unit-tested against the in-proc defaults in `Core` with no infra spun up. See [`testing.md`](./testing.md).
2. **Avoid framework lock-in.** If Wolverine becomes a tire fire, swap to MediatR by changing one `Use*Bus()` call. App code doesn't notice.
3. **Future-proofing the renderer.** Today it's Razor Components. Tomorrow it could be a string builder, a JSON renderer for mobile, or whatever Microsoft renames Blazor to next year. The interface stays.
4. **The metadata is portable.** Entity attributes are pure data. They could drive a different framework's UI generator with no changes — this is also why prior attempts in the author's career survived three rewrites of the rendering layer.

## What's deliberately NOT abstracted

- **Razor itself** is not behind an interface. The renderer is. If you want a different templating engine, write a new `IEntityRenderer`.
- **ASP.NET Core** is not behind an interface. The endpoint mapper is an extension method on `IEndpointRouteBuilder`. If you want a different HTTP host, write a different mapper package.
- **The `[Entity]`/`[TextBox]`/etc. attributes** are not behind an interface. They are the data contract. Treat them as a versioned schema — additive changes only.

## Where it gets harder, not easier

Read [`../README.md`](../README.md#honest-caveats) and the design doc (`~/.claude/plans/ok-so-i-want-precious-bird.md` on the author's machine) for the full list. Headlines:

- The "one weird screen" — analytics dashboards, multi-step wizards, drag-drop — won't fit the renderer. Escape hatch: write a `.razor` partial directly.
- HTMX roundtrips on every change feel slower than React's optimistic updates. Mitigation: Alpine.js for local interactivity.
- Stateless server + stateless HTMX makes multi-step form state awkward. Hidden fields, server-side draft entities, or session storage. None as clean as React's local state.
- "Same actions for mobile" means either a webview or a separate JSON API. Pick one consciously.
