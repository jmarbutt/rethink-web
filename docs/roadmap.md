# Roadmap

> "I want to keep it simple but I want it all" - these are in tension. Here's the honest cut.

This is personal R&D. There are no dates, no commitments, and the project may stop at any point. The point of this roadmap is to validate the framework primitives without inheriting an old production schema, product vocabulary, or database shape.

The eventual product direction may be a next-generation operational platform, but this repo should prove concepts in a neutral domain first. If the framework only feels good when it mirrors an existing system, the framework is not generic enough yet.

## Product Thesis

The bet under test: **HTMX + server-rendered metadata + typed queries/mutations can produce a UX I don't miss React for, while making MCP and dev tooling mostly free.**

The proof should answer five questions:

- Can a small app model produce useful CRUD, custom operations, MCP tools, and a manifest without a front-end build system?
- Can custom screens coexist with generated metadata screens without fighting the framework?
- Can permissions, tenancy, events, and typed operations stay visible rather than disappearing into framework magic?
- Can AI agents extend an app by reading the manifest, docs, and local conventions instead of reverse-engineering the whole codebase?
- Can the prototype support a realistic multi-step operational workflow without importing legacy tables or legacy naming?

## Done (MVP)

- [x] Attribute-driven entity metadata (`[Entity]` + 7 field attribute kinds)
- [x] `EntityRegistry` + `EntityMetadata` reflected at startup
- [x] Razor Components renderer (`Layout`, `GridView`, `EditView`, `Field`)
- [x] HTMX-aware endpoint mapper (fragments to `HX-Request`, full pages otherwise)
- [x] Reflective form binder (string to typed property values)
- [x] `IAction<TEntity, TInput, TOutput>` + `[Action(...)]`
- [x] `ActionRegistry` + `ActionDispatcher` with permission gate
- [x] `IQuery<TInput, TOutput>` + `[Query(...)]`
- [x] `QueryRegistry` + `QueryDispatcher` + no-op `IQueryCache`
- [x] `IMutation<TEntity, TInput, TOutput>` + `[Mutation(...)]` as the long-term entity mutation primitive
- [x] `IEventBus` with default `InProcEventBus`
- [x] `EntitySaved<TEntity>` auto-published after every save
- [x] `IEventSubscriber<T>` pattern, demonstrated with computed fields and side effects
- [x] `IEntityStore<T>` with default `InMemoryEntityStore<T>`
- [x] EF Core adapter (`EfCoreEntityStore<TEntity, TContext>` + `UseEfCoreFor`)
- [x] `/_framework/manifest` JSON endpoint with permission filtering for entities, fields, actions, mutations, and queries
- [x] MCP server (Streamable HTTP transport) at `/mcp` via the official `ModelContextProtocol.AspNetCore` SDK
- [x] Dynamic MCP tool registration for actions, queries, and mutations with JSON Schema input metadata
- [x] `IClock` / `IIdGenerator` / `IAuthContext` injected for deterministic tests
- [x] Three sample apps in a complexity ladder: `Sample.Notes`, `Sample.Tasks`, `Sample.Chat`
- [x] Multi-tenancy as foundational opt-in with `UseMultiTenant<TResolver>()`, `ITenantOwned`, store decoration, and EF Core filters
- [x] `PublishingEntityStore<T>` decorator so `EntitySaved<T>` fires from every save path
- [x] Explicit `POST /{slug}/{id}/actions/{name}` endpoint
- [x] Server-enforced `Required` validation in `FormBinder`
- [x] HTTP-level permission enforcement and per-field `EditPermission` skip in `FormBinder`
- [x] JSON Schema required-ness based on `NullabilityInfoContext`
- [x] 32 tests passing across 3 test projects

The MVP exists to be poked at. Spend two hours in a neutral work-tracking sample app. Create records, edit them, invoke operations, inspect the manifest, and call MCP tools. If that flow does not make you miss React or a hand-written API layer, the bet is paying.

## Validation Scope

Before Phase 2, resist building toward a known legacy data model. The validation app should stay deliberately boring and portable:

- **Workspace** - tenant/account boundary, neutral enough to exercise tenancy.
- **Project** - parent record with status, owner, and lifecycle.
- **Work item** - grid/edit/action baseline with required fields, assignee, priority, status, and due date.
- **Approval** - small workflow candidate with requested/approved/rejected states.
- **Comment or message** - proves custom screens and real-time fragments without making the metadata renderer draw everything.
- **Attachment or external link** - proves escape hatches around file-ish or integration-ish data without building a document system.

Do not import an old schema. Do not rename these into a vertical-market product too early. The framework needs generic pressure first: entities, operations, permissions, tenancy, events, workflows, docs, and AI context.

## Milestone 1: Neutral Concept App

Goal: replace abstract confidence with one coherent sample that exercises the framework like a real internal tool.

- [ ] Add `RethinkWeb.Sample.Operations` with `Workspace`, `Project`, `WorkItem`, `Approval`, and `Comment` entities.
- [ ] Use metadata CRUD for the boring screens: project list, work item list, and edit forms.
- [ ] Add custom HTML/Razor for one non-grid screen: project dashboard or work item activity view.
- [ ] Add at least three operations: assign work item, change status, request approval.
- [ ] Add one query that is not just "list all": project board, overdue work, or approval inbox.
- [ ] Add one event subscriber that computes derived state after save.
- [ ] Add tests proving HTTP form save, operation endpoint, query endpoint, manifest exposure, and MCP invocation.
- [ ] Keep sample names domain-neutral so the framework remains the subject.

Acceptance criteria:

- A new developer can run one command, use the app for 15 minutes, and understand entities, operations, manifest, MCP, and events.
- At least one screen is generated and at least one screen is custom, proving the escape hatch is real.
- The sample does not require prior knowledge of any existing business database.

## Milestone 2: Framework Inspector Foundation

Goal: make `/_framework` the generated control plane for understanding an app.

- [ ] Add a read-only `/_framework` home page.
- [ ] Show registered entities, fields, field kinds, required flags, read/edit permissions, and store type.
- [ ] Show queries, mutations, actions, input schemas, output schemas, cache policy, permissions, and MCP exposure.
- [ ] Show event subscribers registered for each event type.
- [ ] Show active tenant and user context when available.
- [ ] Link from each entity to its generated grid/edit routes and manifest JSON fragment.
- [ ] Add HTTP tests proving the Inspector loads, filters unauthorized metadata, and works in single-tenant mode.

Acceptance criteria:

- A developer can answer "what did the framework register?" without reading startup code.
- The Inspector reads runtime metadata/manifest data. It does not become a second configuration system.
- The first version is read-only. No admin data editing yet.

## Milestone 3: Operation Explorer

Goal: make typed operations testable from the Inspector without Postman, curl, or an MCP client.

- [ ] Add query detail pages with generated input forms from JSON Schema.
- [ ] Add action/mutation detail pages with generated input forms from JSON Schema.
- [ ] Execute operations through the same dispatchers used by HTTP and MCP.
- [ ] Show result payloads, cache hit/miss, validation errors, permission failures, and correlation id.
- [ ] Show equivalent `curl` and MCP tool-call payloads for each operation.
- [ ] Add tests for successful query execution, validation failure, permission failure, and malformed input.

Acceptance criteria:

- A developer can inspect and run every exposed operation from `/_framework`.
- Operation Explorer proves that the manifest contract is good enough to drive tools.
- No operation bypasses the normal permission, tenant, validation, or dispatcher path.

## Milestone 4: Lifecycle Stream

Goal: record what happened to an entity in a way humans, tools, and future workflows can inspect.

- [ ] Add lifecycle event models for entity saves, action invocations, mutation invocations, subscriber handling, and workflow steps.
- [ ] Add `ILifecycleSink` and default in-memory implementation.
- [ ] Record actor, tenant, timestamp, correlation id, operation name, entity id, and summarized before/after data where available.
- [ ] Add lifecycle views in the Inspector for entity instance timelines.
- [ ] Add tests proving save/action/subscriber paths write lifecycle records once and keep correlation ids stable.

Acceptance criteria:

- A developer can answer "why did this record change?" from inside the framework.
- Lifecycle data is append-only and observational; it should not control business behavior.
- The design can later support durable adapters without changing app code.

## Milestone 5: Triggers And Workflows

Goal: prove long-running orchestration without committing to a production workflow engine too early.

- [ ] Add `ITrigger<TEvent>` for deciding whether an event should start or advance a workflow.
- [ ] Add `IWorkflow<TInput>` and `IWorkflowEngine` abstractions.
- [ ] Add a minimal in-proc workflow engine for short flows and tests.
- [ ] Support waiting for an event, running a mutation/action step, recording a lifecycle step, and failing with a visible error.
- [ ] Demonstrate with a neutral approval workflow: request approval, approve/reject, update work item state, record timeline.
- [ ] Add deterministic workflow tests with fake clock and fake id generator.

Acceptance criteria:

- Workflow primitives compose existing operations instead of inventing a parallel business layer.
- The sample can demonstrate a multi-step flow without a background-job dependency.
- The abstractions leave room for Hangfire, Wolverine, or Temporal adapters later.

## Milestone 6: Renderer And UX Depth

Goal: keep generated UI useful while preserving escape hatches.

- [ ] Make the renderer consult `IAuthContext` for field read/edit permissions.
- [ ] Render fields without read permission out of the DOM; render read-only fields disabled when edit permission is missing.
- [ ] Improve validation display for HTTP 422 responses.
- [ ] Add detail view renderer for single-record read pages.
- [ ] Add card/list renderer for compact dashboards.
- [ ] Add dashboard renderer only after the Operations sample proves the shape.
- [ ] Add renderer snapshot tests for permission filtering and validation states.

Acceptance criteria:

- Generated screens are safe enough for non-sensitive operational data.
- The renderer does not try to handle every bespoke screen.
- Custom screens remain first-class and documented.

## Milestone 7: Test Harness Package

Goal: make apps built on the framework easy to test without copying fakes around.

- [ ] Extract `RethinkWeb.Testing`.
- [ ] Include fake auth, fake tenant, fake clock, fake id generator, and in-memory lifecycle assertions.
- [ ] Include helpers for invoking queries, mutations, actions, and entity saves.
- [ ] Include `WebApplicationFactory` helpers for sample apps.
- [ ] Include manifest assertion helpers for permission filtering and operation exposure.

Acceptance criteria:

- New sample capabilities come with focused tests instead of broad end-to-end-only coverage.
- App authors can prove framework integration without standing up real infrastructure.

## Milestone 8: Production Hardening

Build these only after the concept app, Inspector, lifecycle, and workflow story still feel worth continuing.

- [ ] MCP OAuth/bearer auth wiring bound to `IAuthContext`.
- [ ] MCP error-handling request filter that logs internally and returns generic client-safe errors.
- [ ] Durable lifecycle sink adapter.
- [ ] Hangfire workflow adapter for scheduled and retryable work.
- [ ] Wolverine bus adapter for outbox-backed messaging.
- [ ] Marten store adapter for event-sourced entities.
- [ ] HTMX SSE adapter package generalized from `Sample.Chat`.
- [ ] `/_docs/{entity}` Markdown renderer generated from the manifest.
- [ ] Packaging, templates, versioning, and release automation.

Acceptance criteria:

- The framework has a credible path from prototype to internal production tool.
- Security-sensitive defaults are documented and test-covered.
- Optional adapters stay optional.

## Maybe Never

Resist these. They're scope-creep that has killed framework projects before.

- Visual workflow designer or no-code builder
- Drag-and-drop UI builder for non-developers
- Real-time collaborative editing
- Dynamic plugin loading
- Runtime entity/schema designer
- Importing an old production schema as the primary proof point
- Self-hosted admin UI for managing app data as a replacement for app-specific UX

## How Done Is Measured

This is a thought-exercise prototype. "Done" means the MVP and neutral concept app can be used for a realistic two-hour operational workflow without making the author miss React, a SPA build system, or hand-written API controllers.

If after 16 hours of building extensions the framework still feels good, build the next milestone. If not, cut scope or stop. The cost of stopping at the MVP is one weekend; the cost of pushing through a weak concept is months.

The abandonment criterion exists to be honored, not admired:

> If after 16 hours the prototype isn't usable end-to-end, the framework is too ambitious as designed. Cut scope or kill it. Write down what you learned.
