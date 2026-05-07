# Concepts

The mental model. Five primitives, one source of truth.

## 1. Entity

A C# class with `[Entity(slug, displayName)]`. Properties carry field attributes that describe how to render and validate them. The `Id` property is required and is the primary key.

```csharp
[Entity(slug: "donors", displayName: "Donors")]
public class Donor
{
    public Guid Id { get; set; }

    [TextBox("First Name", GridVisible = true, GridOrder = 1, Required = true)]
    public string FirstName { get; set; } = "";

    [PhoneBox("Phone Number", Sample = "(555) 123-4567")]
    public string? Phone { get; set; }

    [CurrencyBox("Year-To-Date Total", Disabled = true, GridVisible = true, GridOrder = 5)]
    public decimal YearToDateTotal { get; set; }
}
```

The `slug` is the URL segment (`/donors`, `/donors/{id}`). The `displayName` is what the renderer puts in the page title and grid heading.

Register with `.AddEntity<Donor>()` in `Program.cs`. Reflection at startup builds an `EntityMetadata` cache; runtime reads from the cache.

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

## 3. Action

A class implementing `IAction<TEntity, TInput, TOutput>` with `[Action(name, displayName)]`. Receives the loaded entity, a typed input DTO, and an `IActionContext` with auth/clock/event-bus.

```csharp
public sealed record AddressInput(string Address1, string? Address2, string City, string State, string PostalCode);
public sealed record AddressResult(Guid DonorId, string FullAddress);

[Action("update-address", "Update Address",
    Description = "Update the postal address on a donor record.",
    Icon = "map-pin")]
public sealed class UpdateAddressAction(IEntityStore<Donor> store)
    : IAction<Donor, AddressInput, AddressResult>
{
    public async Task<AddressResult> ExecuteAsync(
        Donor entity, AddressInput input, IActionContext context, CancellationToken ct)
    {
        entity.Address1 = input.Address1;
        entity.City = input.City;
        // ...
        await store.SaveAsync(entity, ct);
        return new AddressResult(entity.Id, $"{input.Address1}, {input.City}");
    }
}
```

Register with `.AddAction<UpdateAddressAction>()`. From there it's accessible as:

- HTTP: `POST /donors/{id}/actions/update-address` (Phase 2 — currently dispatched via the form-post path; see `EndpointRouteExtensions.cs`)
- MCP: tool name `donors.update-address` with `inputSchema` auto-generated from the `AddressInput` record
- Manifest: listed under the `donors` entity's `actions` array

## 4. Event

Two flavors:

### `EntitySaved<TEntity>` (auto-published)

The framework publishes this event after every entity save (form post, action, future workflow step). Subscribe to react to changes regardless of which write path produced them:

```csharp
public sealed class RecomputeDeductibleSubscriber(IEntityStore<Donation> store)
    : IEventSubscriber<EntitySaved<Donation>>
{
    public async Task HandleAsync(EntitySaved<Donation> evt, IEventContext context, CancellationToken ct)
    {
        var d = evt.Entity;
        if (d.AmountDeductible == d.Amount) return; // idempotent
        d.AmountDeductible = d.Amount;
        await store.SaveAsync(d, ct);
    }
}
```

Register with `.AddEventSubscriber<EntitySaved<Donation>, RecomputeDeductibleSubscriber>()`.

### Custom events (publish from actions)

Inside an action, use `context.Events.PublishAsync(myEvent)` to fan out to subscribers. The default in-proc bus dispatches synchronously. Adapter packages can swap in durable buses (Wolverine outbox, etc.) without changing the action code.

The `IEventContext` passed to subscribers carries:
- `SourceUserId` — from `IAuthContext.UserId` at publish time
- `PublishedAt` — from `IClock.UtcNow`
- `CorrelationId` — from `IIdGenerator.NewId()`

In tests, swap in `FakeClock` and `FakeIdGenerator` for deterministic correlation IDs and timestamps.

## 5. Manifest

A JSON document at `/_framework/manifest`:

```json
{
  "frameworkVersion": "0.1.0-prototype",
  "generatedAt": "2026-05-07T02:53:08+00:00",
  "entities": [
    {
      "slug": "donors",
      "displayName": "Donors",
      "fields": [
        { "name": "FirstName", "label": "First Name", "kind": "Text", "required": true, "sample": "John" },
        ...
      ],
      "actions": [
        {
          "name": "update-address",
          "displayName": "Update Address",
          "description": "Update the postal address on a donor record.",
          "exposeToMcp": true,
          "inputSchema": {
            "type": "object",
            "properties": {
              "address1": { "type": "string" },
              ...
            },
            "required": ["address1", "city", ...]
          }
        }
      ]
    }
  ]
}
```

The manifest is **the** source of truth. Three audiences consume it:

- **Humans** — via the `/_framework` Inspector page (Phase 2) and `/_docs/{slug}` Markdown views
- **MCP clients** — `/mcp/tools/list` reads it to advertise tools; `tools/call` dispatches via the action registry
- **LLMs** — given the manifest scoped to a user's permissions plus recent lifecycle events, an LLM can answer "how does this work?" with full visibility

The manifest **filters by user permission**. Entities/actions/fields the current `IAuthContext` can't access do not appear in the manifest at all. This is the mechanism that keeps the LLM/docs/MCP surfaces honest about what a given user is allowed to see.

## How they fit

```
                    [Entity attributes]
                            │
                            ↓
                     EntityMetadata
                    /     │       \
                   ↓      ↓         ↓
              Renderer  Manifest  FormBinder
                   ↓      ↓         ↓
                HTML   JSON      Entity update
                                   ↓
                                 Save
                                   ↓
                          EntitySaved<T> event
                                   ↓
                            Subscribers
                                   ↓
                          (more saves, side effects)
```

Same metadata. Three readers (renderer, manifest, binder). One write path. One event stream after writes.

## Where this is going (Phase 2+)

- **Triggers** that watch for events and start workflows — `ITrigger<TEvent>`
- **Workflows** that span days/weeks with durable steps — `IWorkflow<TInput>`, default in-proc engine, `IWorkflowEngine` adapters for Hangfire/Wolverine/Temporal
- **Lifecycle stream** — every event/action/workflow-step for an entity, in order, queryable via `IEntityLifecycle<TEntity>` and rendered as a timeline view

These primitives are deliberately not in the MVP. See [`roadmap.md`](./roadmap.md).
