# Adding an entity

End-to-end walkthrough adding a `Volunteer` entity to the sample app. About five minutes.

## 1. Define the entity

Create `src/RethinkWeb.Sample.Donor/Entities/Volunteer.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using RethinkWeb.Metadata;

namespace RethinkWeb.Sample.Donor.Entities;

[Entity(slug: "volunteers", displayName: "Volunteers")]
public class Volunteer
{
    [Key]
    public Guid Id { get; set; }

    [TextBox("First Name", GridVisible = true, GridOrder = 1, Required = true)]
    public string FirstName { get; set; } = "";

    [TextBox("Last Name", GridVisible = true, GridOrder = 2, Required = true)]
    public string LastName { get; set; } = "";

    [TextBox("Email", GridVisible = true, GridOrder = 3, Sample = "vol@example.com")]
    public string? Email { get; set; }

    [DateBox("Joined", GridVisible = true, GridOrder = 4)]
    public DateTime? JoinedDate { get; set; }

    [CheckBox("Active")]
    public bool Active { get; set; } = true;

    [TextBox("Notes", Multiline = true)]
    public string? Notes { get; set; }
}
```

The `[Key]` attribute is for EF Core; `[Entity]` is for RethinkWeb. Both required.

## 2. Add the DbSet

Edit `src/RethinkWeb.Sample.Donor/SampleContext.cs`:

```csharp
public sealed class SampleContext(DbContextOptions<SampleContext> options) : DbContext(options)
{
    public DbSet<Donor> Donors => Set<Donor>();
    public DbSet<Donation> Donations => Set<Donation>();
    public DbSet<Volunteer> Volunteers => Set<Volunteer>();   // <-- add this
}
```

## 3. Register with the framework

Edit `src/RethinkWeb.Sample.Donor/Program.cs`:

```csharp
builder.Services
    .AddRethinkWeb()
    .AddEntity<Donor>().UseEfCoreFor<Donor, SampleContext>()
    .AddEntity<Donation>().UseEfCoreFor<Donation, SampleContext>()
    .AddEntity<Volunteer>().UseEfCoreFor<Volunteer, SampleContext>()    // <-- add this
    // ... rest unchanged
    .UseRazorRenderer();
```

Order matters: `.AddEntity<T>()` registers the default in-memory store first; `.UseEfCoreFor<T, TContext>()` swaps it for EF Core. If you call them in the other order, the in-memory store wins.

## 4. Drop the SQLite db (dev only)

Because we're using `EnsureCreated()` rather than migrations:

```bash
rm src/RethinkWeb.Sample.Donor/donor-sample.db*
```

In production, add an EF migration with `dotnet ef migrations add AddVolunteers`.

## 5. Run

```bash
dotnet run --project src/RethinkWeb.Sample.Donor
```

You now have:

- `GET /` — index page lists Volunteers alongside Donors and Donations
- `GET /volunteers` — grid view (First Name, Last Name, Email, Joined, with Active filtered out because `GridVisible = false`)
- `GET /volunteers/{id}` — edit form with all six fields
- `POST /volunteers/{id}` — form binding + save + `EntitySaved<Volunteer>` event publish
- `GET /_framework/manifest` — Volunteer entity now appears with full field metadata
- `GET /mcp/tools/list` — no Volunteer-specific tools yet (see [adding an action](./adding-an-action.md))

## 6. Test it (optional)

For a generated entity, the smoke test that gets you the most coverage for the least effort is a `WebApplicationFactory` integration test. Pattern lives in `tests/RethinkWeb.Sample.Donor.Tests/EndToEndTests.cs`:

```csharp
[Fact]
public async Task Volunteers_endpoint_renders_grid()
{
    var client = _factory.CreateClient();
    var html = await client.GetStringAsync("/volunteers");
    html.Should().Contain("Volunteers");
}
```

For unit-testing field metadata or computed-field event subscribers, see [`testing.md`](./testing.md).

## What you didn't have to write

- Routing table
- View files for grid or edit
- A controller
- A DTO for the form post
- A repository or service class
- A `tools/list` registration for MCP
- A docs page
- An audit subscriber (the framework auto-publishes `EntitySaved<Volunteer>` for free)

That's the framework's pitch, demonstrated.

## When the framework isn't enough

If you need a custom screen the metadata renderer can't draw — a kanban board, a chart, a multi-step wizard — write a Razor partial directly in `Program.cs` or a separate component file:

```csharp
app.MapGet("/volunteers/leaderboard", async (SampleContext db, IEntityRenderer renderer) =>
{
    // Your custom query + rendering
    var html = "<h1>Top Volunteers</h1>...";
    var page = await renderer.RenderLayoutAsync("Leaderboard", html);
    return Results.Content(page, "text/html");
});
```

Escape hatch by design. The framework is opinionated about CRUD; outside CRUD, write the damn HTML.
