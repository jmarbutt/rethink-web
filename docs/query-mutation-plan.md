# Query, Mutation, Action, And Lifecycle Plan

RethinkWeb is moving toward an App Manifest Runtime operation model:

```text
Entity          data shape and semantic field metadata
View Profile    planned presentation contract
Query           typed, permission-scoped read capability
Mutation        typed, permission-scoped state change
Action          user-facing or compatibility name for an invokable mutation
Event           fact emitted after data changes
Lifecycle Fact  append-only operation history
Manifest        public contract for renderers, MCP, HTTP, docs, agents, and the Inspector
```

The important boundary: **the manifest JSON is the public contract**. Attributes and C# interfaces are the authoring model. `EntityMetadata`, query descriptors, mutation descriptors, action descriptors, and future view/lifecycle descriptors are runtime metadata. The manifest is what clients consume.

## Why Queries

Entity grids are not enough for production apps or production MCP. A user should be able to expose safe reads like:

```text
tasks.list
projects.search
work-items.overdue
work-items.timeline
approvals.inbox
```

without exposing SQL, raw EF, or unbounded database access to an LLM or UI renderer.

Queries are typed:

```csharp
[Query(
    name: "tasks.list",
    displayName: "List Tasks",
    Permission = "tasks.read",
    Cache = QueryCacheMode.PerTenant,
    CacheSeconds = 30,
    DependsOn = ["tasks"])]
public sealed class ListTasksQuery(IEntityStore<Todo> store)
    : IQuery<ListTasksInput, ListTasksResult>
{
    public async Task<ListTasksResult> ExecuteAsync(
        ListTasksInput input,
        IQueryContext context,
        CancellationToken ct)
    {
        ...
    }
}
```

The manifest exposes query name, display name, input schema, output schema, permission, MCP exposure, cache mode, cache duration, and dependencies.

## Mutations And Existing Actions

`IMutation<TEntity, TInput, TOutput>` is the long-term state-changing primitive.

The existing `IAction<TEntity, TInput, TOutput>` model remains for compatibility and for user-facing language. A user clicks an action button. The app layer should increasingly treat that as an entity-scoped mutation.

For now:

- Existing actions continue to work at `POST /{slug}/{id}/actions/{name}` and as MCP tools named `{slug}.{name}`.
- New entity-scoped state changes should use `IMutation<TEntity, TInput, TOutput>` and `[Mutation]`.
- Mutations are exposed at `POST /{slug}/{id}/mutations/{name}` and as MCP tools named `{slug}.{name}`.
- Docs and dev tools should use Query/Mutation vocabulary unless describing a user-facing button or legacy endpoint.

This keeps the product language humane without letting the architecture turn into a synonym buffet.

## Caching

Query caching is designed as a contract before it is designed as an optimization. The default cache is `NullQueryCache`, so queries execute normally until an app registers a real implementation.

Cache keys should include:

```text
query name
tenant id, when tenant-scoped
user id, when user-scoped
serialized input
eventual manifest/query version
```

Invalidation should flow from events and lifecycle:

```text
Mutation saves entity
  -> PublishingEntityStore publishes EntitySaved<TEntity>
  -> lifecycle sink records save + mutation facts
  -> cache invalidates dependencies such as "tasks"
```

This lets the framework eventually cache per tenant or per user without making developers give up control.

## Lifecycle Facts

Lifecycle history is a core primitive in the roadmap, not merely Inspector decoration.

The first version should record operation facts:

- `CorrelationId`
- `ActorUserId`
- `TenantId`
- `OperationKind` (`query`, `mutation`, `action`, `save`, `event`, `subscriber`, `workflowStep`)
- `OperationName`
- `EntitySlug` and `EntityId` when applicable
- `StartedAt`, `CompletedAt`, and duration
- `Status`
- `ErrorSummary`
- Compact input/output or before/after summaries when safe

The first version should not try to be:

- Full event sourcing
- Complete entity snapshots on every write
- Point-in-time reconstruction
- A replacement for domain events

Those may become adapter-backed capabilities later. Start with enough history to explain application behavior to humans, tools, and agents.

## View Profile Implications

Query and mutation schemas can drive generated operation forms, but the schema alone is not enough for a good UI.

Future View Profiles should describe:

- Which fields appear in a query or mutation form
- Field order, grouping, labels, and help text
- Lookup/search controls for reference-like values
- Compact result/card/list shapes
- Renderer hints for richer controls
- Escape hatches for custom Razor/HTML screens

This keeps operation I/O typed while giving renderers enough context to build useful UI without hard-coding every form.

## Inspector Implications

The Framework Inspector should be the control plane for the manifest:

- Entities: browse metadata, stores, permissions, and future view profiles.
- Queries: inspect schemas, run queries, view cache policy, and see lifecycle facts.
- Mutations/actions: inspect permissions, run test operations, view resulting events and lifecycle facts.
- Events: show what was published and which subscribers handled it.
- Lifecycle: show every relevant operation fact around a record or correlation id.
- View Profiles: show how generated screens are composed without creating a second configuration system.

The optimized MVC/HTMX app is the UX developers build for end users. The Inspector is the automatic dev/admin surface generated from the same core primitives.
