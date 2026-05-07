# Query and Mutation Plan

RethinkWeb is moving toward a Convex-style operation model:

```text
Entity      data shape and field metadata
Query       typed, permission-scoped read capability
Mutation    typed, permission-scoped state change
Event       fact emitted after data changes
Trigger     reacts to events
Workflow    long-running orchestration built from mutations, events, and triggers
Manifest    public contract for renderers, MCP, docs, and the Inspector
```

The important boundary: **the manifest JSON is the public contract**. Attributes and C# interfaces are the authoring model. `EntityMetadata`, query descriptors, and mutation descriptors are runtime metadata. The manifest is what clients consume.

## Why Queries

Entity grids are not enough for production apps or production MCP. A user should be able to expose safe reads like:

```text
tasks.list
projects.search
jobs.recent-failures
work-items.timeline
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

The manifest exposes the query name, input schema, output schema, permission, MCP exposure, cache mode, cache duration, and dependencies.

## Mutations and Existing Actions

The existing `IAction<TEntity, TInput, TOutput>` model is an entity-scoped mutation. It is intentionally preserved for compatibility while the framework grows first-class `IMutation<TEntity, TInput, TOutput>` support.

For now:

- Existing actions continue to work at `POST /{slug}/{id}/actions/{name}` and as MCP tools named `{slug}.{name}`.
- New entity-scoped mutations use `IMutation<TEntity, TInput, TOutput>` and can be exposed at `POST /{slug}/{id}/mutations/{name}`.
- Longer term, docs and dev tools should use the Query/Mutation vocabulary, with "action" treated as the old name for an entity-scoped mutation.

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

Invalidation should flow from events:

```text
Mutation saves entity
  -> PublishingEntityStore publishes EntitySaved<TEntity>
  -> cache invalidates dependencies such as "tasks"
  -> lifecycle log records the mutation, event, handlers, and resulting data
```

This lets the framework eventually cache per tenant or per user without making developers give up control.

## Inspector Implications

The Framework Inspector should be the control plane for the manifest:

- Entities: browse data with explicit tenant/user context.
- Queries: inspect input/output schemas, run queries, view cache policy.
- Mutations: inspect permissions, run test mutations, view resulting events.
- Events: view each event emitted for an entity.
- Triggers and subscribers: show what handled an event and what changed.
- Lifecycle: show every query-relevant state change around a record.

The optimized MVC/HTMX app is the UX developers build for end users. The Inspector is the automatic dev/admin surface generated from the same core primitives.
