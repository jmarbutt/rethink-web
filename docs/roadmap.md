# Roadmap

> "I want to keep it simple but I want it all" — these are in tension. Here's the honest cut.

This is personal R&D. There are no dates, no commitments, and the project may stop at any point. The point of this doc is to keep the prototype focused on its actual job (validate the core bet) and to be honest about what's not in it yet.

## Done (MVP)

The bet under test: **HTMX + server-rendered metadata + typed queries/mutations can produce a UX I don't miss React for, while making MCP and dev tooling mostly free.**

- [x] Attribute-driven entity metadata (`[Entity]` + 7 field attribute kinds)
- [x] `EntityRegistry` + `EntityMetadata` reflected at startup
- [x] Razor Components renderer (`Layout`, `GridView`, `EditView`, `Field`)
- [x] HTMX-aware endpoint mapper (fragments to `HX-Request`, full pages otherwise)
- [x] Reflective form binder (string → typed property values)
- [x] `IAction<TEntity, TInput, TOutput>` + `[Action(...)]`
- [x] `ActionRegistry` + `ActionDispatcher` with permission gate
- [x] `IQuery<TInput, TOutput>` + `[Query(...)]`
- [x] `QueryRegistry` + `QueryDispatcher` + no-op `IQueryCache`
- [x] `IMutation<TEntity, TInput, TOutput>` + `[Mutation(...)]` as the long-term entity mutation primitive
- [x] `IEventBus` with default `InProcEventBus`
- [x] `EntitySaved<TEntity>` auto-published after every save
- [x] `IEventSubscriber<T>` pattern, demonstrated with `RecomputeDeductibleSubscriber`
- [x] `IEntityStore<T>` with default `InMemoryEntityStore<T>`
- [x] EF Core adapter (`EfCoreEntityStore<TEntity, TContext>` + `UseEfCoreFor`)
- [x] `/_framework/manifest` JSON endpoint with permission filtering for entities, fields, actions, mutations, and queries
- [x] **MCP server (Streamable HTTP transport)** at `/mcp` via the official `ModelContextProtocol.AspNetCore` SDK — Claude-Desktop / MCP-Inspector / Cursor compatible
- [x] Dynamic MCP tool registration: one `McpServerTool` per registered action, JSON Schema auto-generated from `TInput`
- [x] `IClock` / `IIdGenerator` / `IAuthContext` injected for deterministic tests
- [x] Three sample apps in a complexity ladder: `Sample.Notes` (CRUD floor), `Sample.Tasks` (action + EntitySaved subscriber, canonical demo), `Sample.Chat` (two entities + HTMX-SSE real-time)
- [x] **Multi-tenancy as foundational opt-in** — `UseMultiTenant<TResolver>()` + `ITenantOwned` marker + `TenantScopedEntityStore<T>` decorator (auto-stamp + filter + cross-tenant throws) + `ApplyTenantFilters` EF Core helper for SQL-side filtering. Discriminator-column model. Single-tenant remains the default with no ceremony.
- [x] **`PublishingEntityStore<T>` decorator** — `EntitySaved<T>` fires from EVERY save path (HTMX form, action via dispatcher, MCP tool, future workflow), with AsyncLocal recursion guard for subscriber re-saves
- [x] **Explicit `POST /{slug}/{id}/actions/{name}`** endpoint — accepts form-encoded or JSON input, dispatches via `IActionDispatcher`, returns HTMX-aware response
- [x] **Server-enforced `Required`** in `FormBinder` (collects errors, returns HTTP 422)
- [x] **HTTP-level permission enforcement**: `entity.ReadPermission` on GET, `entity.WritePermission` on POST + actions, per-field `EditPermission` skip in `FormBinder`
- [x] **JSON Schema required-ness** uses `NullabilityInfoContext` so non-nullable strings show as required (vs the earlier `!IsClass` heuristic that always marked them optional)
- [x] 32 tests passing across 3 test projects

The MVP exists to be poked at. Spend two hours of "donor admin work" in the sample app. If you don't miss React, the bet is paying.

## Phase 2 (after the MVP bet validates)

Build these only if the prototype gets daily use. Each is a meaningful chunk of work; doing them in the prototype now would bloat the design before the bet is even tested.

- [ ] **Triggers** — `ITrigger<TEvent>` watches the event bus, decides whether to fire, builds workflow input
- [ ] **Workflows** — `IWorkflow<TInput>`, `IWorkflowEngine` abstraction, default in-proc state-machine engine (~200 LoC) for short flows
- [ ] **Lifecycle stream** — `IEntityLifecycle<TEntity>` queryable timeline of events/actions/workflow steps per entity
- [ ] **Lifecycle timeline view** — built-in renderer kind, no per-app code
- [ ] **Framework Inspector** — `/_framework` admin UI listing entities, actions, events+subscribers, triggers, workflows, recent execution traces, permission map. Django-Admin-meets-OpenTelemetry for the framework's own metadata.
- [ ] **Query Explorer** inside the Inspector — inspect input/output schema, run queries, view cache policy, and test tenant/user-scoped results.
- [ ] **Per-field permission filtering in the *renderer*** — server-side write enforcement is in (per-field `EditPermission` is consulted in `FormBinder`); the renderer still draws all fields. Phase 2 makes the renderer consult `IAuthContext` so unauthorized fields don't display, and editable-but-not-by-this-user fields render disabled.
- [ ] **More renderer kinds** — Card, Detail, Dashboard
- [ ] **Hangfire workflow adapter** — `RethinkWeb.Workflow.Hangfire` for durable jobs / scheduled steps
- [ ] **`RethinkWeb.Testing` package** extracted from inline test fakes — `TestHost`, `EventBusAssertions`, `WorkflowRunner`, `ActionInvoker<T>`
- [x] **Action-as-HTTP-endpoint** — explicit `POST /<entity>/<id>/actions/<name>` endpoint for non-HTMX clients

The killer Phase 2 feature is the **Framework Inspector**. It's the one that delivers on "I want to get back to a single strongly coupled monolith where I can see all the pieces." Don't cut it.

## Phase 3 (only if you're still using the framework daily)

- [ ] **Wolverine bus adapter** — `RethinkWeb.Bus.Wolverine` for production-grade messaging (durable outbox, sagas, scheduling)
- [ ] **MediatR adapter** — `RethinkWeb.Bus.MediatR` for shops already using it
- [ ] **MassTransit adapter** — `RethinkWeb.Bus.MassTransit` for cross-process messaging
- [ ] **Workflow saga adapter** — `RethinkWeb.Workflow.Wolverine` running workflows as Wolverine sagas with durable outbox
- [ ] **Temporal adapter** — `RethinkWeb.Workflow.Temporal` for workflows that span months/services
- [ ] **Marten event-sourced store** — `RethinkWeb.Store.Marten` for entities that want full event sourcing
- [ ] **LLM doc-helper adapter** — `RethinkWeb.Llm.Anthropic` (and OpenAI variant) — answers user questions using manifest + lifecycle as context, with mandatory "show me what the LLM saw" transparency
- [ ] **MCP OAuth + auth wiring** — the SDK supports OAuth/bearer; framework needs to bind it to `IAuthContext` so MCP tool calls respect the same permission model as web requests
- [ ] **MCP error-handling request filter** — replace the "surface inner exception in tool result text" prototype hack with a proper filter that logs internally and returns generic errors to clients
- [ ] **HTMX SSE adapter package** — generalize the per-channel SSE pattern proven in `Sample.Chat` (`ChatStreamHub` + `MessageBroadcaster`) into `RethinkWeb.RealTime.Sse`. Subscribe to `EntitySaved<T>` events, push rendered fragments to subscribed clients filtered by entity id or query.
- [ ] **`/_docs/{entity}` Markdown renderer** — generates human-readable docs from the manifest, scoped to user permissions
- [ ] **Mobile JSON-API renderer** — alternate `IEntityRenderer` that emits JSON instead of HTML, for mobile clients that don't want a webview

## Maybe never

Resist these. They're scope-creep that has killed framework projects before.

- Visual workflow designer / no-code builder
- Drag-and-drop UI builder for non-developers
- Real-time collaborative editing (you've outgrown this framework if you need it)
- Code generation / scaffolding CLI (a `dotnet new` template is enough if useful at all)
- Plugin system / dynamic loading
- Self-hosted admin UI for managing entities at runtime (the Inspector reads, doesn't write)

## How "done" is measured

This is a thought-exercise prototype. "Done" means the MVP can be used for two hours of real donor-admin work without making the author miss React. If after 16 hours of building extensions to it the framework still feels good, build Phase 2. If not, kill it; the cost of stopping at the MVP is one weekend, the cost of stopping after Phase 2 is months.

The abandonment criterion exists to be honored, not admired. From the design doc:

> If after 16 hours the prototype isn't usable end-to-end, the framework is too ambitious as designed. Cut scope or kill it. Write down what you learned.
