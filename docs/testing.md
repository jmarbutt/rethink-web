# Testing

Test the wiring, not the framework. App authors write tests against their own actions, subscribers, and entities — using the framework's in-proc defaults plus a handful of fakes, no infra needed.

## Patterns per layer

| Layer | How to test |
|---|---|
| **Action** | Inject mocks/fakes for `IEntityStore`, `IEventBus`, `IAuthContext`. Call `ExecuteAsync`. Assert output + entity state + emitted events. |
| **Query** | Inject fake stores/services. Call `ExecuteAsync`. Assert output, permission assumptions, and cache metadata through manifest tests. |
| **Mutation** | Same as action. Use the dispatcher or direct handler tests, then assert saved state and emitted events. |
| **Renderer** | Pure function: metadata + entity → string. Use Verify snapshot tests. Diff on regression. |
| **Event subscriber** | Construct subscriber, hand it an event + fake `IEventContext`, assert side effects on the injected store. |
| **Trigger** *(Phase 2)* | Pure predicate. `ShouldFire(evt, ctx) → bool`. Trivial. |
| **Workflow** *(Phase 2)* | Run against in-proc engine with `FakeClock`. Assert step sequence, final state, emitted events. Use `WorkflowRunner.AdvanceTime(TimeSpan)`. |
| **HTTP** | `WebApplicationFactory<Program>` integration tests. Form-post → assert returned HTML fragment + DB state. |
| **MCP** | Same `WebApplicationFactory<Program>`. Invoke action by tool name. Assert same outcome as the HTTP path. Proves the unification works end-to-end. |
| **Manifest** | Snapshot test on `/_framework/manifest`. Catches accidental schema changes immediately. |

## Non-negotiables

These rules are enforced in framework code; apps should follow the same conventions to keep tests fast and deterministic.

### 1. No statics for stateful things

Everything goes through DI. Static *attribute metadata* (read-only data) is fine — that's data, not behavior.

### 2. `DateTimeOffset.UtcNow` is banned in framework code

Use `IClock` (default: `SystemClock`; tests: `FakeClock`).

```csharp
public sealed class FakeClock(DateTimeOffset start) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = start;
    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}
```

This also kills a chunk of the timezone pain that drove the framework in the first place — there's exactly one place a wall-clock value enters the system.

### 3. Every default implementation must be deterministic

- No `Guid.NewGuid()` without injecting `IIdGenerator`
- No `Random` without injecting an `IRandom` (Phase 2 — not yet needed)
- No `DateTime.Now`, no `Environment.TickCount`, no anything that varies between runs

```csharp
public sealed class FakeIdGenerator(params Guid[] ids) : IIdGenerator
{
    private int _i;
    public Guid NewId() => _i < ids.Length ? ids[_i++] : Guid.Empty;
}
```

### 4. Test what's wired, not what's mocked

`WebApplicationFactory<Program>` boots the real DI graph. ASP.NET Core's scope validator catches captive-dependency bugs (singleton consuming scoped) that only show up in production. **Use it.**

This caught a real bug during initial scaffolding: `InProcEventBus` and `ManifestBuilder` were registered as `Singleton` but consumed `IAuthContext` (scoped). The fix was to scope them too. Production wouldn't have caught it; the test host did.

## What ships in `RethinkWeb.Testing` (Phase 2)

Currently the fakes live inside individual test projects. The plan is to extract them into a public `RethinkWeb.Testing` package:

- `TestHost` — boots `Core` + in-mem store + in-proc bus + in-proc workflow engine. Zero infra.
- `FakeAuthContext.As(user, perms)` — set up the user context for permission tests.
- `EventBusAssertions` — `bus.Should().HavePublished<PaymentProcessed>(p => p.Amount == 100m)`.
- `RenderSnapshot.Match(renderer, entity)` — Verify-style snapshot helper.
- `WorkflowRunner.AdvanceTime(TimeSpan)` — fake-clock control for time-dependent workflows.
- `ActionInvoker<TAction>.Invoke(input)` — bypass HTTP, hit the handler directly with full DI.

Until that ships, copy the patterns from `tests/RethinkWeb.Core.Tests/Fakes.cs` and `tests/RethinkWeb.Sample.Tasks.Tests/EndToEndTests.cs`.

## Existing test layout

```
tests/
  RethinkWeb.Core.Tests/
    Fakes.cs                          FakeClock, FakeIdGenerator
    EventBusTests.cs                  InProcEventBus dispatch + context
    RegistryTests.cs                  EntityRegistry + ActionRegistry behavior
    ManifestTests.cs                  Manifest filtering by permissions + query/mutation exposure
    ManifestSchemaTests.cs            NullabilityInfoContext required-ness
    PublishingEntityStoreTests.cs     Decorator publishes EntitySaved on every save
    AuthBoundariesTests.cs            Permission filtering on entities

  RethinkWeb.Render.Razor.Tests/
    RendererSnapshotTests.cs          GridView + EditView output frozen as Verify snapshots
    Snapshots/                        .verified.txt baselines (commit these; .received.txt is gitignored)

  RethinkWeb.Sample.Tasks.Tests/
    RecordingFactory.cs               Custom WebApplicationFactory with EntitySaved recorder
    EndToEndTests.cs                  Manifest + HTMX form post + query endpoint + MCP via real McpClient
    HttpFixesTests.cs                 Action endpoint + EntitySaved publish + Required validation
                                      (all hitting the live WebApplicationFactory<Program>)
```

32 tests today. Run with:

```bash
dotnet test
```

## When a Verify snapshot test fails

First run: there's no `.verified.txt` yet, so the test fails and produces a `.received.txt`. Inspect it; if it looks right, promote it:

```bash
mv tests/.../Snapshots/SomeTest.received.txt tests/.../Snapshots/SomeTest.verified.txt
```

After intentional changes: same workflow. The `.received.*` files are gitignored so they never accidentally land in commits; the `.verified.*` files **are** committed and serve as the regression baseline.

## What NOT to test

- The framework's reflection itself. Trust .NET. Don't write a test that asserts `typeof(Todo).GetCustomAttribute<EntityAttribute>()` returns non-null.
- The Razor rendering pipeline. Verify-snapshot the *output*, not the components themselves.
- Microsoft's DI container. `TryAddSingleton` does what it says.
- HTMX. It's a script tag; if you're testing HTMX, use a browser, not xUnit.

## When you can't unit-test something

Say so. From the global rules: *"if you can't test the UI, say so explicitly rather than claiming success."* The MCP transport is one example — the current plain-HTTP shape is testable; the future Streamable HTTP transport will need an integration test against a real MCP client (the [MCP Inspector](https://github.com/modelcontextprotocol/inspector) is the tool of choice).
