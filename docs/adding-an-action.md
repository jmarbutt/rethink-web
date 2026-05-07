# Adding an action

End-to-end walkthrough adding a `MarkInactive` action to the `Volunteer` entity introduced in [`adding-an-entity.md`](./adding-an-entity.md). About five minutes.

The whole point: **one definition surfaces as an HTTP endpoint, an MCP tool, and a manifest entry**. You write the handler once.

## 1. Define the input + output

Records keep this terse. Property names become field names in the auto-generated MCP input schema.

```csharp
public sealed record MarkInactiveInput(string Reason);
public sealed record MarkInactiveResult(Guid VolunteerId, DateTime DeactivatedAt);
```

## 2. Write the action

Create `src/RethinkWeb.Sample.Donor/Actions/MarkInactiveAction.cs`:

```csharp
using RethinkWeb.Actions;
using RethinkWeb.Sample.Donor.Entities;
using RethinkWeb.Storage;

namespace RethinkWeb.Sample.Donor.Actions;

public sealed record MarkInactiveInput(string Reason);
public sealed record MarkInactiveResult(Guid VolunteerId, DateTime DeactivatedAt);

[Action(name: "mark-inactive", displayName: "Mark Inactive",
    Description = "Deactivate a volunteer. Reason is recorded in the notes field.",
    Icon = "user-x",
    Permission = "volunteer.deactivate")]
public sealed class MarkInactiveAction(IEntityStore<Volunteer> store)
    : IAction<Volunteer, MarkInactiveInput, MarkInactiveResult>
{
    public async Task<MarkInactiveResult> ExecuteAsync(
        Volunteer entity,
        MarkInactiveInput input,
        IActionContext context,
        CancellationToken ct = default)
    {
        var now = context.Clock.UtcNow;          // never DateTime.UtcNow — see testing.md
        entity.Active = false;
        entity.Notes = $"{entity.Notes}\nDeactivated {now:yyyy-MM-dd}: {input.Reason}".TrimStart();
        await store.SaveAsync(entity, ct);
        return new MarkInactiveResult(entity.Id, now.UtcDateTime);
    }
}
```

Key conventions:

- `name` is URL-safe and shows up in MCP as `volunteers.mark-inactive`.
- `Permission` is checked by `ActionDispatcher` before invocation. With the default `AllowAllAuthContext`, every check passes; in production wire up a real `IAuthContext`.
- Use `context.Clock.UtcNow`, not `DateTime.UtcNow`. (See [`testing.md`](./testing.md).)
- The entity is loaded for you — your `Execute` receives the live instance.

## 3. Register it

In `Program.cs`:

```csharp
builder.Services
    .AddRethinkWeb()
    // ... entities ...
    .AddAction<UpdateAddressAction>()
    .AddAction<MarkInactiveAction>()       // <-- add this
    .UseRazorRenderer();
```

## 4. Try it three ways

### As an MCP tool

The framework hosts a standards-compliant MCP server at `/mcp` using the official `ModelContextProtocol.AspNetCore` SDK (Streamable HTTP transport). Connect any MCP client and the tool appears automatically.

Quickest validation — open **MCP Inspector**:

```bash
npx @modelcontextprotocol/inspector
```

Set transport to **Streamable HTTP**, URL `http://localhost:5099/mcp`, click **Connect**. The Tools tab now lists `volunteers.mark-inactive` with its auto-generated input schema:

```json
{
  "name": "volunteers.mark-inactive",
  "description": "Deactivate a volunteer. Reason is recorded in the notes field.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "entityId": { "type": "string" },
      "input": {
        "type": "object",
        "properties": { "reason": { "type": "string" } },
        "required": ["reason"]
      }
    },
    "required": ["entityId", "input"]
  }
}
```

In the Inspector's call form, paste:

```json
{
  "entityId": "<your-volunteer-guid>",
  "input": { "reason": "Moved out of state" }
}
```

Click **Run**. See [`mcp-clients.md`](./mcp-clients.md) for Claude Desktop, Cursor, and programmatic-C# setups.

### As an HTTP endpoint

```bash
curl -s -X POST http://localhost:5099/volunteers/11111111-1111-1111-1111-111111111111/actions/mark-inactive \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "reason=Moved out of state"
```

(Action-as-HTTP-endpoint dispatch is wired in `EndpointRouteExtensions` — search for `MapEntityEndpoints`.)

### From the manifest

`GET /_framework/manifest` now includes the action under the `volunteers` entity. Docs pages, LLM context, and the future Inspector UI all read from here.

## 5. Test it

Action handlers are unit-testable in isolation — no HTTP, no DB.

```csharp
[Fact]
public async Task MarkInactive_sets_active_false_and_appends_note()
{
    var store = new InMemoryEntityStore<Volunteer>();
    var volunteer = new Volunteer { Id = Guid.NewGuid(), FirstName = "Ada", Active = true };
    await store.SaveAsync(volunteer);

    var clock = new FakeClock(DateTimeOffset.Parse("2026-05-06T10:00:00Z"));
    var ctx = new TestActionContext { Clock = clock };

    var action = new MarkInactiveAction(store);
    var result = await action.ExecuteAsync(volunteer, new MarkInactiveInput("Moved"), ctx, default);

    var saved = await store.GetAsync(volunteer.Id);
    saved!.Active.Should().BeFalse();
    saved.Notes.Should().Contain("2026-05-06").And.Contain("Moved");
    result.DeactivatedAt.Should().Be(clock.UtcNow.UtcDateTime);
}
```

Notice the `FakeClock` — without it, the test would be sensitive to wall-clock time. See [`testing.md`](./testing.md) for the full pattern (including a `TestActionContext` helper that's worth pulling into your test project).

## What you didn't have to write

- An MCP tool registration
- A JSON schema for the input
- A controller method or minimal-API delegate
- A binding from form to typed record
- A docs entry
- A permission check (the dispatcher does it from the `[Action(Permission = ...)]` attribute)
- An audit log entry (subscribers to `EntitySaved<Volunteer>` fire automatically after the action's save)

## Constraints (read these before getting clever)

- **Input must be a `class` or `record`**. Primitives don't bind via the auto-generated MCP input schema. If you only need one parameter, wrap it in a single-property record.
- **Output is JSON-serializable.** It's returned as the MCP `content[].text` payload (JSON-encoded) and as the HTTP response body.
- **Action class must have `ExecuteAsync` exactly** — that's what the dispatcher invokes via reflection. Don't add overloads.
- **Constructor injection works fully.** Resolved via `ActivatorUtilities.CreateInstance` with the request-scoped `IServiceProvider`. Inject `IEntityStore<T>`, `IClock`, your own services.
- **No nested DTOs in the input schema yet.** Phase 2. Today, keep input records flat.

## Where this is going

In Phase 2, actions become the building blocks of **workflows** — multi-step, possibly long-running, possibly suspended-for-days processes. Same `IAction` definitions, composed into `IWorkflow<TInput>`, with the entity's lifecycle stream rendering a timeline of every step. See [`roadmap.md`](./roadmap.md).
