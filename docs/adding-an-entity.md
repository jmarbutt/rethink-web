# Adding an entity

End-to-end walkthrough adding a `Project` entity to the Tasks sample. About five minutes.

## 1. Define the entity

Create `src/RethinkWeb.Sample.Tasks/Entities/Project.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using RethinkWeb.Metadata;

namespace RethinkWeb.Sample.Tasks.Entities;

[Entity(slug: "projects", displayName: "Projects")]
public class Project
{
    [Key]
    public Guid Id { get; set; }

    [TextBox("Name", GridVisible = true, GridOrder = 1, Required = true,
        Sample = "Q3 marketing site")]
    public string Name { get; set; } = "";

    [TextBox("Description", Multiline = true)]
    public string? Description { get; set; }

    [TextBox("Owner", GridVisible = true, GridOrder = 2, Sample = "alice")]
    public string? Owner { get; set; }

    [DateBox("Created", Disabled = true, GridVisible = true, GridOrder = 3)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [CheckBox("Active")]
    public bool Active { get; set; } = true;
}
```

The `[Key]` attribute is for EF Core; `[Entity]` is for RethinkWeb. Both required.

## 2. Add the DbSet

Edit `src/RethinkWeb.Sample.Tasks/TasksDb.cs`:

```csharp
public sealed class TasksDb(DbContextOptions<TasksDb> options) : DbContext(options)
{
    public DbSet<Todo> Todos => Set<Todo>();
    public DbSet<Project> Projects => Set<Project>();   // <-- add this
}
```

## 3. Register with the framework

Edit `src/RethinkWeb.Sample.Tasks/Program.cs`:

```csharp
builder.Services
    .AddRethinkWeb()
    .AddEntity<Todo>().UseEfCoreFor<Todo, TasksDb>()
    .AddEntity<Project>().UseEfCoreFor<Project, TasksDb>()    // <-- add this
    // ... rest unchanged
    .UseRazorRenderer();
```

Order matters: `.AddEntity<T>()` registers the default in-memory store first; `.UseEfCoreFor<T, TContext>()` swaps it for EF Core. If you call them in the other order, the in-memory store wins.

## 4. Drop the SQLite db (dev only)

Because we're using `EnsureCreated()` rather than migrations:

```bash
rm src/RethinkWeb.Sample.Tasks/tasks.db*
```

In production, add an EF migration with `dotnet ef migrations add AddProjects`.

## 5. Run

```bash
dotnet run --project src/RethinkWeb.Sample.Tasks
```

You now have:

- `GET /` — index page lists Projects alongside Tasks
- `GET /projects` — grid view (Name, Owner, Created — `Description` and `Active` filtered out because `GridVisible = false`)
- `GET /projects/{id}` — edit form with all six fields
- `POST /projects/{id}` — form binding + save + `EntitySaved<Project>` event publish
- `GET /_framework/manifest` — Project entity now appears with full field metadata
- MCP server at `/mcp` — no Project-specific tools yet (see [adding an action](./adding-an-action.md)); manifest entries flow into MCP automatically once you have actions

## 6. Test it (optional)

For a generated entity, the smoke test that gets you the most coverage for the least effort is a `WebApplicationFactory` integration test. Pattern lives in `tests/RethinkWeb.Sample.Tasks.Tests/EndToEndTests.cs`:

```csharp
[Fact]
public async Task Projects_endpoint_renders_grid()
{
    var client = _factory.CreateClient();
    var html = await client.GetStringAsync("/projects");
    html.Should().Contain("Projects");
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
- An audit subscriber (the framework auto-publishes `EntitySaved<Project>` for free)

That's the framework's pitch, demonstrated.

## When the framework isn't enough

If you need a custom screen the metadata renderer can't draw — a kanban board, a chart, a multi-step wizard, **a chat UI** — write a Razor partial directly in `Program.cs` or hand-roll HTML and serve it from a custom endpoint. The Chat sample (`src/RethinkWeb.Sample.Chat`) is the canonical demonstration: the framework still owns the data path (entities, actions, events, MCP, manifest) but the chat layout is hand-rolled HTML in `ChatPages.cs`.

```csharp
app.MapGet("/projects/dashboard", async (TasksDb db, IEntityRenderer renderer) =>
{
    // Your custom query + rendering
    var html = "<h1>Project Dashboard</h1>...";
    var page = await renderer.RenderLayoutAsync("Dashboard", html);
    return Results.Content(page, "text/html");
});
```

Escape hatch by design. The framework is opinionated about CRUD; outside CRUD, write the damn HTML.
