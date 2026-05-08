# Roadmap

> "I want to keep it simple but I want it all" - these are in tension. Here's the honest cut.

This is personal R&D. There are no dates, no commitments, and the project may stop at any point. The point of this roadmap is to validate the App Manifest Runtime thesis without inheriting an old production schema, product vocabulary, or database shape.

## Product Thesis

The bet under test: **a developer-authored business/app layer can become a permission-scoped manifest that powers web UI, HTTP, MCP, docs, inspectors, agents, and future renderers without scattering application truth across a dozen disconnected surfaces.**

RethinkWeb is not primarily a CRUD generator, admin UI, CQRS framework, MCP wrapper, or anti-React tantrum in a trench coat. It is an intentionally tight app layer with many consumers.

The proof should answer six questions:

- Can one app model produce useful generated views, custom views, typed operations, MCP tools, lifecycle history, and a manifest without a front-end build system?
- Can entity semantics stay separate from presentation concerns through View Profiles?
- Can lifecycle facts explain what happened without committing to event sourcing too early?
- Can permissions, tenancy, events, and typed operations stay visible rather than disappearing into framework magic?
- Can AI agents extend an app by reading the manifest, docs, lifecycle facts, and local conventions instead of reverse-engineering the whole codebase?
- Can the prototype support a realistic multi-step operational workflow without importing legacy tables or legacy naming?

## Core Model

```text
Entity          durable business object and semantic field metadata
View Profile    planned presentation contract for grid/detail/edit/card/lookup/operation forms
Query           typed read operation
Mutation        typed state-changing operation
Action          user-facing or compatibility name for an invokable mutation
Event           fact emitted after data changes
Lifecycle Fact  append-only observation of what happened
Manifest        permission-scoped public contract
```

## Done (MVP)

- [x] Attribute-driven entity metadata (`[Entity]` + field attributes)
- [x] `EntityRegistry` + `EntityMetadata` reflected at startup
- [x] Razor Components renderer (`Layout`, `GridView`, `EditView`, `Field`)
- [x] HTMX-aware endpoint mapper (fragments to `HX-Request`, full pages otherwise)
- [x] Reflective form binder (string to typed property values)
- [x] `IQuery<TInput, TOutput>` + `[Query(...)]`
- [x] `QueryRegistry` + `QueryDispatcher` + no-op `IQueryCache`
- [x] `IMutation<TEntity, TInput, TOutput>` + `[Mutation(...)]` as the long-term entity mutation primitive
- [x] `IAction<TEntity, TInput, TOutput>` + `[Action(...)]` compatibility path
- [x] `ActionRegistry` + `ActionDispatcher` with permission gate
- [x] `IEventBus` with default `InProcEventBus`
- [x] `EntitySaved<TEntity>` auto-published after every save
- [x] `IEventSubscriber<T>` pattern, demonstrated with computed fields and side effects
- [x] `IEntityStore<T>` with default `InMemoryEntityStore<T>`
- [x] EF Core adapter (`EfCoreEntityStore<TEntity, TContext>` + `UseEfCoreFor`)
- [x] `/_framework/manifest` JSON endpoint with permission filtering for entities, fields, actions, mutations, and queries
- [x] MCP server (Streamable HTTP transport) at `/mcp` via the official `ModelContextProtocol.AspNetCore` SDK
- [x] Dynamic MCP tool registration for actions, queries, and mutations with JSON Schema input metadata
- [x] Multi-tenancy as foundational opt-in with `UseMultiTenant<TResolver>()`, `ITenantOwned`, store decoration, and EF Core filters
- [x] `PublishingEntityStore<T>` decorator so `EntitySaved<T>` fires from every save path
- [x] HTTP-level permission enforcement and per-field `EditPermission` skip in `FormBinder`
- [x] `IClock` / `IIdGenerator` / `IAuthContext` injected for deterministic tests
- [x] Three sample apps in a complexity ladder: `Sample.Notes`, `Sample.Tasks`, `Sample.Chat`
- [x] 32 tests passing across 3 test projects

The MVP exists to be poked at. Spend two hours in a neutral work-tracking sample app. Create records, edit them, invoke operations, inspect the manifest, call MCP tools, and look at lifecycle facts once they exist. If that flow does not make you miss React or hand-written API controllers, the bet is paying.

## Validation Scope

Before Phase 2, resist building toward a known legacy data model. The validation app should stay deliberately boring and portable:

- **Workspace** - tenant/account boundary, neutral enough to exercise tenancy.
- **Project** - parent record with status, owner, and lifecycle.
- **Work item** - grid/edit/action baseline with required fields, assignee, priority, status, and due date.
- **Approval** - small workflow candidate with requested/approved/rejected states.
- **Comment or message** - proves custom screens and real-time fragments without making the metadata renderer draw everything.
- **Attachment or external link** - proves escape hatches around file-ish or integration-ish data without building a document system.

Do not import an old schema. Do not rename these into a vertical-market product too early. The framework needs generic pressure first: entities, view profiles, operations, permissions, tenancy, events, lifecycle, workflows, docs, MCP, and AI context.

## Milestone 1: App Contract Foundations

Goal: add the two missing primitives that make the thesis real instead of just a better README.

- [ ] Add a first-pass View Profile model to `Core` for grid, detail, edit, card, lookup, and operation-form contexts.
- [ ] Keep entity field metadata semantic; migrate richer presentation decisions into profiles instead of growing field attributes endlessly.
- [ ] Extend the manifest to expose view profiles in a renderer-neutral shape.
- [ ] Add `ILifecycleSink` and lifecycle fact models for query, mutation, action, save, event, subscriber, and future workflow-step observations.
- [ ] Record actor, tenant, correlation id, operation kind/name, entity slug/id, timestamps, status, duration, and compact safe summaries.
- [ ] Add in-memory defaults and deterministic tests with fake clock, fake id generator, fake auth, and fake tenant.

Acceptance criteria:

- Entity metadata no longer has to carry every presentation concern.
- Lifecycle history answers "what happened and why?" without event sourcing.
- The manifest remains the public contract for both view profiles and lifecycle capabilities.

## Milestone 2: Neutral Operations Sample

Goal: replace abstract confidence with one coherent sample that exercises the framework like a real internal tool.

- [ ] Add `RethinkWeb.Sample.Operations` with `Workspace`, `Project`, `WorkItem`, `Approval`, and `Comment` entities.
- [ ] Use generated views for boring screens: project list, work item list, detail, edit, lookup, and compact card surfaces.
- [ ] Add custom HTML/Razor for one non-grid screen: project dashboard, work item activity view, or approval inbox.
- [ ] Add at least three mutations/actions: assign work item, change status, request approval.
- [ ] Add one query that is not just "list all": project board, overdue work, approval inbox, or work-item timeline.
- [ ] Add one event subscriber that computes derived state after save.
- [ ] Show lifecycle facts for saves, mutations/actions, events, and subscribers.
- [ ] Add tests proving HTTP form save, operation endpoint, query endpoint, manifest exposure, view-profile exposure, lifecycle recording, and MCP invocation.
- [ ] Keep sample names domain-neutral so the framework remains the subject.

Acceptance criteria:

- A new developer can run one command, use the app for 15 minutes, and understand entities, view profiles, operations, manifest, MCP, events, and lifecycle facts.
- At least one screen is generated and at least one screen is custom, proving the escape hatch is real.
- The sample does not require prior knowledge of any existing business database.

## Milestone 3: Framework Inspector Foundation

Goal: make `/_framework` the generated control plane for understanding an app.

- [ ] Add a read-only `/_framework` home page.
- [ ] Show registered entities, fields, field kinds, required flags, read/edit permissions, store type, and view profiles.
- [ ] Show queries, mutations, actions, input schemas, output schemas, cache policy, permissions, and MCP exposure.
- [ ] Show lifecycle fact types, recent records, and correlation ids.
- [ ] Show event subscribers registered for each event type.
- [ ] Show active tenant and user context when available.
- [ ] Link from each entity to generated grid/detail/edit routes and manifest JSON fragments.
- [ ] Add HTTP tests proving the Inspector loads, filters unauthorized metadata, and works in single-tenant mode.

Acceptance criteria:

- A developer can answer "what did the framework register?" without reading startup code.
- The Inspector reads runtime metadata and manifest data. It does not become a second configuration system.
- The first version is read-only. No admin data editing yet.

## Milestone 4: Operation Explorer

Goal: make typed operations testable from the Inspector without Postman, curl, or an MCP client.

- [ ] Add query detail pages with generated input forms from JSON Schema plus View Profile hints.
- [ ] Add mutation/action detail pages with generated input forms from JSON Schema plus View Profile hints.
- [ ] Execute operations through the same dispatchers used by HTTP and MCP.
- [ ] Show result payloads, cache hit/miss, validation errors, permission failures, lifecycle fact ids, and correlation id.
- [ ] Show equivalent `curl` and MCP tool-call payloads for each operation.
- [ ] Add tests for successful execution, validation failure, permission failure, malformed input, and lifecycle recording.

Acceptance criteria:

- A developer can inspect and run every exposed operation from `/_framework`.
- Operation Explorer proves that the manifest contract is good enough to drive tools.
- No operation bypasses normal permission, tenant, validation, dispatcher, or lifecycle paths.

## Milestone 5: Triggers And Workflows

Goal: prove long-running orchestration without committing to a production workflow engine too early.

- [ ] Add `ITrigger<TEvent>` for deciding whether an event should start or advance a workflow.
- [ ] Add `IWorkflow<TInput>` and `IWorkflowEngine` abstractions.
- [ ] Add a minimal in-proc workflow engine for short flows and tests.
- [ ] Support waiting for an event, running a mutation/action step, recording lifecycle facts, and failing with a visible error.
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
- [ ] Render detail, card, lookup, and operation-form View Profiles.
- [ ] Add richer renderer extension points for controls such as contact lookup, search boxes, and rich text editors.
- [ ] Add renderer snapshot tests for permission filtering, profile rendering, validation states, and custom-control registration.

Acceptance criteria:

- Generated screens are safe enough for non-sensitive operational data.
- The renderer does not try to handle every bespoke screen.
- Custom screens remain first-class and documented.

## Milestone 7: Test Harness Package

Goal: make apps built on the framework easy to test without copying fakes around.

- [ ] Extract `RethinkWeb.Testing`.
- [ ] Include fake auth, fake tenant, fake clock, fake id generator, and in-memory lifecycle assertions.
- [ ] Include helpers for invoking queries, mutations, actions, entity saves, and generated view profiles.
- [ ] Include `WebApplicationFactory` helpers for sample apps.
- [ ] Include manifest assertion helpers for permission filtering, operation exposure, view-profile exposure, and lifecycle capability exposure.

Acceptance criteria:

- New sample capabilities come with focused tests instead of broad end-to-end-only coverage.
- App authors can prove framework integration without standing up real infrastructure.

## Milestone 8: Production Hardening

Build these only after the concept app, view profiles, lifecycle, Inspector, and workflow story still feel worth continuing.

- [ ] MCP OAuth/bearer auth wiring bound to `IAuthContext`.
- [ ] MCP error-handling request filter that logs internally and returns generic client-safe errors.
- [ ] Durable lifecycle sink adapter.
- [ ] Hangfire workflow adapter for scheduled and retryable work.
- [ ] Wolverine bus adapter for outbox-backed messaging.
- [ ] Marten store adapter for event-sourced or document-backed entities.
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
- Full event sourcing as a mandatory storage model
- Renderer-specific configuration embedded directly in entities

## How Done Is Measured

This is a thought-exercise prototype. "Done" means the MVP plus the neutral Operations sample can be used for a realistic two-hour operational workflow without making the author miss React, a SPA build system, hand-written API controllers, or duct-taped AI context.

If after 16 hours of building extensions the framework still feels good, build the next milestone. If not, cut scope or stop. The cost of stopping at the MVP is one weekend; the cost of pushing through a weak concept is months.

The abandonment criterion exists to be honored, not admired:

> If after 16 hours the prototype isn't usable end-to-end, the framework is too ambitious as designed. Cut scope or kill it. Write down what you learned.
