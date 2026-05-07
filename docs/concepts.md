# Concepts

The mental model. Server-side primitives, one manifest contract.

## 1. Entity

A C# class with `[Entity(slug, displayName)]`. Properties carry field attributes that describe how to render and validate them. The `Id` property is required and is the primary key.

```csharp
[Entity(slug: "tasks", displayName: "Tasks")]
public class Todo
{
    public Guid Id { get; set; }

    [TextBox("Title", GridVisible = true, GridOrder = 1, Required = true)]
    public string Title { get; set; } = "";

    [TextBox("Notes", Multiline = true)]
    public string? Notes { get; set; }

    [CheckBox("Completed", GridVisible = true, GridOrder = 2)]
    public bool Completed { get; set; }

    [DateBox("Completed At", Disabled = true)]
    public DateTime? CompletedAt { get; set; }
}
```

The `slug` is the URL segment (`/tasks`, `/tasks/{id}`). The `displayName` is what the renderer puts in the page title and grid heading.

Register with `.AddEntity<Todo>()` in `Program.cs`. Reflection at startup builds an `EntityMetadata` cache; runtime reads from the cache.

## 2. Field attributes

Each attribute corresponds to a `FieldKind` the renderer knows how to draw.

| Attribute | FieldKind | Renders as |
|---|---|---|
| `[TextBox(label)]` | Text / Email / Multiline | `<input type="text">` (or `email`, or `<textarea>`) |
| `[NumberBox(label)]` | Number | `<input type="number">` |
| `[CurrencyBox(label)]` | Currency | `<input type="number" step="0.01">` |
| `[DateBox(label)]` | Date | `<input type="date">` |
| `[CheckBox(label)]` | Checkbox | `<input type="checkbox">` |
| `[SelectBox(label)]` | Select | `<select>` |
| `[PhoneBox(label)]` | Phone | `<input type="tel">` |

Common properties on every field attribute:

```csharp
public bool Disabled        { get; init; }   // read-only in edit views
public bool GridVisible     { get; init; }   // appears in grid listings
public int  GridOrder       { get; init; }   // column sort order
public bool Required        { get; init; }   // server-enforced on save
public string? ReadPermission { get; init; } // permission to see at all
public string? EditPermission { get; init; } // permission to edit
public string? Sample       { get; init; }   // placeholder used in docs/manifest
```

## 3. Query

A query is a safe, typed read capability. It does not require an entity id and should not mutate state.

```csharp
public sealed record ListTasksInput(bool? IncludeCompleted);
public sealed record TaskListRow(Guid Id, string Title, bool Completed, DateTime? CompletedAt);
public sealed record ListTasksResult(IReadOnlyList<TaskListRow> Tasks);

[Query(
    name: "tasks.list",
    displayName: "List Tasks",
    Description = "List tasks with an optional completed-task filter.",
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

Register with `.AddQuery<ListTasksQuery>()`. From there it appears in:

- HTTP: `POST /_framework/queries/tasks.list`
- MCP: tool name `tasks.list`
- Manifest: listed in the top-level `queries` array with input schema, output schema, permission, MCP exposure, and cache policy
- Future Inspector: runnable from a generated query explorer

The default `IQueryCache` is a no-op. Cache metadata exists now so apps can later opt into per-tenant or per-user caching without changing query handlers.

## 4. Mutation / Action

A class implementing `IAction<TEntity, TInput, TOutput>` with `[Action(name, displayName)]`. Receives the loaded entity, a typed input DTO, and an `IActionContext` with auth/clock/event-bus.

```csharp
public sealed record MarkCompleteInput;
public sealed record MarkCompleteResult(Guid TodoId, bool AlreadyCompleted);

[Action("mark-complete", "Mark Complete",
    Description = "Mark a task as completed. Idempotent — re-running on a completed task is a no-op.",
    Icon = "check")]
public sealed class MarkCompleteAction(IEntityStore<Todo> store)
    : IAction<Todo, MarkCompleteInput, MarkCompleteResult>
{
    public async Task<MarkCompleteResult> ExecuteAsync(
        Todo entity, MarkCompleteInput input, IActionContext context, CancellationToken ct)
    {
        if (entity.Completed) return new MarkCompleteResult(entity.Id, AlreadyCompleted: true);
        entity.Completed = true;
        await store.SaveAsync(entity, ct);
        return new MarkCompleteResult(entity.Id, AlreadyCompleted: false);
    }
}
```

Register with `.AddAction<MarkCompleteAction>()`. From there it's accessible as:

- HTTP: `POST /tasks/{id}/actions/mark-complete` — explicit action endpoint dispatches via `IActionDispatcher`. Form-encoded body or JSON body both work; HTMX-aware response.
- MCP: tool name `tasks.mark-complete` exposed via the official `ModelContextProtocol.AspNetCore` SDK at `/mcp` (Streamable HTTP transport). `inputSchema` is auto-generated from the `MarkCompleteInput` record. Connect via Claude Desktop, MCP Inspector, Cursor, etc. — see [`mcp-clients.md`](./mcp-clients.md).
- Manifest: listed under the `tasks` entity's `actions` array

`IAction<TEntity, TInput, TOutput>` remains the compatibility name for an entity-scoped mutation. New code can use `IMutation<TEntity, TInput, TOutput>` and `[Mutation]`; it appears under the entity's `mutations` array and can be invoked through `POST /tasks/{id}/mutations/{name}`.

## 5. Event

Two flavors:

### `EntitySaved<TEntity>` (auto-published)

The framework publishes this event after every entity save (form post, action, future workflow step). Subscribe to react to changes regardless of which write path produced them:

```csharp
public sealed class StampCompletedAtSubscriber(
    IEntityStore<Todo> store, IClock clock)
    : IEventSubscriber<EntitySaved<Todo>>
{
    public async Task HandleAsync(EntitySaved<Todo> evt, IEventContext context, CancellationToken ct)
    {
        var todo = evt.Entity;
        if (todo.Completed && todo.CompletedAt is null)
        {
            todo.CompletedAt = clock.UtcNow.UtcDateTime;
            await store.SaveAsync(todo, ct);
        }
    }
}
```

Register with `.AddEventSubscriber<EntitySaved<Todo>, StampCompletedAtSubscriber>()`. The `PublishingEntityStore` decorator's recursion guard prevents the subscriber's re-save from re-publishing — no infinite loop.

### Custom events (publish from actions)

Inside an action, use `context.Events.PublishAsync(myEvent)` to fan out to subscribers. The default in-proc bus dispatches synchronously. Adapter packages can swap in durable buses (Wolverine outbox, etc.) without changing the action code.

The `IEventContext` passed to subscribers carries:
- `SourceUserId` — from `IAuthContext.UserId` at publish time
- `PublishedAt` — from `IClock.UtcNow`
- `CorrelationId` — from `IIdGenerator.NewId()`

In tests, swap in `FakeClock` and `FakeIdGenerator` for deterministic correlation IDs and timestamps.

## 6. Manifest

A JSON document at `/_framework/manifest`:

```json
{
  "frameworkVersion": "0.1.0-prototype",
  "generatedAt": "2026-05-07T02:53:08+00:00",
  "entities": [
    {
      "slug": "tasks",
      "displayName": "Tasks",
      "fields": [
        { "name": "Title", "label": "Title", "kind": "Text", "required": true, "sample": "Write the docs" },
        ...
      ],
      "actions": [
        {
          "name": "mark-complete",
          "displayName": "Mark Complete",
          "description": "Mark a task as completed. Idempotent — re-running on a completed task is a no-op.",
          "exposeToMcp": true,
          "inputSchema": {
            "type": "object",
            "properties": {},
            "required": []
          }
        }
      ],
      "mutations": []
    }
  ],
  "queries": [
    {
      "name": "tasks.list",
      "displayName": "List Tasks",
      "cache": { "mode": "PerTenant", "durationSeconds": 30, "dependencies": ["tasks"] }
    }
  ]
}
```

The manifest is **the** public contract. Attributes and interfaces are the authoring model; the manifest is what clients consume. Three audiences consume it:

- **Humans** — via the `/_framework` Inspector page (Phase 2) and `/_docs/{slug}` Markdown views
- **MCP clients** — `/mcp/tools/list` advertises exposed queries, mutations, and actions; `tools/call` dispatches through the relevant registry
- **LLMs** — given the manifest scoped to a user's permissions plus recent lifecycle events, an LLM can answer "how does this work?" with full visibility

The manifest **filters by user permission**. Entities, fields, queries, mutations, and actions the current `IAuthContext` can't access do not appear in the manifest at all. This is the mechanism that keeps the LLM/docs/MCP surfaces honest about what a given user is allowed to see.

## How they fit

```
              [Entity / Query / Mutation attributes]
                              │
                              ↓
                       Runtime metadata
                    /       │         \
                   ↓        ↓          ↓
              Renderer   Manifest   Dispatchers
                   ↓        ↓          ↓
                HTML      JSON    Query result / entity update
                                             ↓
                                           Save
                                             ↓
                                    EntitySaved<T> event
                                             ↓
                                      Subscribers
                                             ↓
                                (more saves, side effects)
```

Same metadata. Multiple readers. One manifest contract. One event stream after writes.

## Where this is going (Phase 2+)

- **Triggers** that watch for events and start workflows — `ITrigger<TEvent>`
- **Workflows** that span days/weeks with durable steps — `IWorkflow<TInput>`, default in-proc engine, `IWorkflowEngine` adapters for Hangfire/Wolverine/Temporal
- **Lifecycle stream** — every event/action/workflow-step for an entity, in order, queryable via `IEntityLifecycle<TEntity>` and rendered as a timeline view

These primitives are deliberately not in the MVP. See [`roadmap.md`](./roadmap.md).
