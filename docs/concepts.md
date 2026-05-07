# Concepts

The mental model: one developer-authored app layer, one permission-scoped manifest contract, many consumers.

RethinkWeb's core vocabulary is:

```text
Entity          durable business object and semantic fields
View Profile    planned presentation contract for context-specific views
Query           typed read operation
Mutation        typed state-changing operation
Action          user-facing or compatibility name for an invokable mutation
Event           fact emitted after data changes
Lifecycle Fact  append-only observation of what happened
Manifest        public contract for renderers, HTTP, MCP, docs, inspectors, and agents
```

## 1. Entity

A C# class with `[Entity(slug, displayName)]`. Properties carry field attributes that describe semantic field kind, labels, validation hints, permissions, and current MVP generated-form behavior. The `Id` property is required and is the primary key.

Entity metadata should not become the home for every layout, widget, personalization, or renderer decision. If every field attribute turns into a tiny UI framework, congratulations, the architecture has fallen down the stairs.

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

The `slug` is the URL segment (`/tasks`, `/tasks/{id}`). The `displayName` is the human-facing entity name. Register with `.AddEntity<Todo>()` in `Program.cs`. Reflection at startup builds an `EntityMetadata` cache; runtime reads from the cache.

## 2. Field Attributes

Each attribute corresponds to a `FieldKind` the current renderer knows how to draw.

| Attribute | FieldKind | Current default rendering |
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
public bool Disabled          { get; init; } // read-only in edit views
public bool GridVisible       { get; init; } // appears in MVP grid listings
public int  GridOrder         { get; init; } // MVP grid column sort order
public bool Required          { get; init; } // server-enforced on save
public string? ReadPermission { get; init; } // permission to see at all
public string? EditPermission { get; init; } // permission to edit
public string? Sample         { get; init; } // placeholder used in docs/manifest
```

The current `GridVisible` and `GridOrder` properties are MVP conveniences. The roadmap moves richer presentation concerns into View Profiles so entity fields remain semantic.

## 3. View Profiles

View Profiles are planned, not implemented yet.

A View Profile describes how an entity or operation should appear in a specific context:

- `grid`: dense list/table for scanning records.
- `detail`: full read view with ordered sections and fields.
- `edit`: write form with validation and field grouping.
- `card`: compact summary for dashboards and related records.
- `lookup`: small search/selection shape for combo boxes and reference pickers.
- `operationForm`: input shape for a query, mutation, or action.
- `custom`: renderer hint for screens that should use hand-written Razor/HTML.

This creates the needed split:

```text
Entity field:     Title is required text.
View profile:     In the card view, show Title first and Status second.
Renderer:         Draw Title as the default text field, or use a richer control.
Custom screen:    Ignore the generated profile and render a specialized page.
```

The manifest should eventually expose view profiles so renderers, inspectors, docs, and agents can reason about how an object is meant to appear without reverse-engineering Razor markup.

## 4. Query

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
- Manifest: top-level `queries` array with input schema, output schema, permission, MCP exposure, and cache policy
- Future Inspector: runnable from a generated query explorer

The default `IQueryCache` is a no-op. Cache metadata exists now so apps can later opt into per-tenant or per-user caching without changing query handlers.

## 5. Mutation And Action

`IMutation<TEntity, TInput, TOutput>` is the long-term state-changing operation primitive. A mutation receives the loaded entity, a typed input DTO, and an `IMutationContext` with auth, clock, and event bus.

`IAction<TEntity, TInput, TOutput>` is still supported. Treat it as compatibility and as user-facing language for a button or invoked operation. In the UI, "Mark Complete" is an action. In the app contract, it is an entity-scoped mutation.

```csharp
public sealed record RenameTaskInput(string Title);
public sealed record RenameTaskResult(Guid TodoId, string Title);

[Mutation(
    name: "rename",
    displayName: "Rename Task",
    Description = "Rename a task.",
    Icon = "pencil")]
public sealed class RenameTaskMutation(IEntityStore<Todo> store)
    : IMutation<Todo, RenameTaskInput, RenameTaskResult>
{
    public async Task<RenameTaskResult> ExecuteAsync(
        Todo entity,
        RenameTaskInput input,
        IMutationContext context,
        CancellationToken ct = default)
    {
        entity.Title = input.Title;
        await store.SaveAsync(entity, ct);
        return new RenameTaskResult(entity.Id, entity.Title);
    }
}
```

Registered mutations are accessible as:

- HTTP: `POST /tasks/{id}/mutations/rename`
- MCP: tool name `tasks.rename`
- Manifest: listed under the entity's `mutations` array
- Future Inspector: runnable from an operation explorer

Existing actions continue to work at `POST /{slug}/{id}/actions/{name}` and as MCP tools named `{slug}.{name}`. Docs and new framework features should prefer Query/Mutation vocabulary unless they are describing user-facing buttons.

## 6. Event

Two flavors exist today.

### `EntitySaved<TEntity>` (auto-published)

The framework publishes this event after every entity save (form post, action, mutation, future workflow step). Subscribe to react to changes regardless of which write path produced them:

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

Register with `.AddEventSubscriber<EntitySaved<Todo>, StampCompletedAtSubscriber>()`. The `PublishingEntityStore` decorator's recursion guard prevents the subscriber's re-save from re-publishing.

### Custom events

Inside an action or mutation, use `context.Events.PublishAsync(myEvent)` to fan out to subscribers. The default in-proc bus dispatches synchronously. Adapter packages can swap in durable buses without changing operation code.

The `IEventContext` passed to subscribers carries:

- `SourceUserId` from `IAuthContext.UserId` at publish time
- `PublishedAt` from `IClock.UtcNow`
- `CorrelationId` from `IIdGenerator.NewId()`

In tests, swap in `FakeClock` and `FakeIdGenerator` for deterministic correlation IDs and timestamps.

## 7. Lifecycle Fact

Lifecycle Facts are planned, not implemented yet.

A Lifecycle Fact is an append-only observation recorded by the framework. The first version should record operation facts, not full event sourcing:

- Actor and tenant
- Correlation id
- Operation kind and name
- Entity slug and id when applicable
- Start/end timestamps
- Status and error summary
- Compact input/output or before/after summaries where safe

Lifecycle facts should answer "what happened and why?" for humans, tools, and agents. They should not become the source of truth in the first version. Full snapshots, point-in-time rebuild, and event-sourced storage can come later if the pressure is real.

## 8. Manifest

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
        { "name": "Title", "label": "Title", "kind": "Text", "required": true, "sample": "Write the docs" }
      ],
      "actions": [
        {
          "name": "mark-complete",
          "displayName": "Mark Complete",
          "description": "Mark a task as completed. Idempotent.",
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

The manifest is **the** public contract. Attributes and interfaces are the authoring model; runtime metadata is the implementation detail; the manifest is what clients consume.

Manifest consumers include:

- **Renderers** for generated HTML and future presentation surfaces
- **Humans** via the `/_framework` Inspector page and future docs
- **MCP clients** via exposed tools and schemas
- **LLMs and agents** via permission-scoped app context plus future lifecycle facts

The manifest **filters by user permission**. Entities, fields, queries, mutations, and actions the current `IAuthContext` cannot access do not appear in the manifest. This is the mechanism that keeps docs, MCP, and LLM surfaces honest about what a user is allowed to see.

## How They Fit

```text
                 Developer-authored app layer
 Entity + View Profile + Query + Mutation + Action + Event
                              |
                              v
                       Runtime metadata
                              |
                              v
                Permission-scoped manifest
                    /       |       |       \
                   v        v       v        v
              Renderer    HTTP     MCP    Inspector/docs/agents
                   \        |       /
                    \       v      /
                      Dispatchers
                              |
                              v
                         Entity save
                              |
                              v
                     Event + lifecycle fact
                              |
                              v
                 Subscribers / future workflows
```

Same app contract. Multiple consumers. Explicit operation history.

## Where This Is Going

- **View Profiles** that keep presentation concerns separate from entity field metadata.
- **Lifecycle Facts** that record saves, queries, mutations, actions, events, subscribers, and workflow steps.
- **Triggers** that watch for events and start workflows.
- **Workflows** that span days or weeks with durable steps.

These primitives are deliberately not all in the MVP. See [`roadmap.md`](./roadmap.md).
