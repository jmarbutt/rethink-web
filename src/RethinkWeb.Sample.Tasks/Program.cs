using Microsoft.EntityFrameworkCore;
using RethinkWeb;
using RethinkWeb.Events;
using RethinkWeb.Http;
using RethinkWeb.Mcp;
using RethinkWeb.Render.Razor;
using RethinkWeb.Sample.Tasks;
using RethinkWeb.Sample.Tasks.Actions;
using RethinkWeb.Sample.Tasks.Entities;
using RethinkWeb.Sample.Tasks.Events;
using RethinkWeb.Sample.Tasks.Queries;
using RethinkWeb.Store.EfCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TasksDb>(o => o.UseSqlite("Data Source=tasks.db"));

builder.Services
    .AddRethinkWeb()
    .AddEntity<Todo>()
    .UseEfCoreFor<Todo, TasksDb>()
    .AddQuery<ListTasksQuery>()
    .AddAction<MarkCompleteAction>()
    .AddMutation<RenameTaskMutation>()
    .AddEventSubscriber<EntitySaved<Todo>, StampCompletedAtSubscriber>()
    .UseRazorRenderer()
    .AddRethinkWebMcpServer();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TasksDb>();
    db.Database.EnsureCreated();
    if (!db.Todos.Any())
    {
        db.Todos.AddRange(
            new Todo { Id = Guid.NewGuid(), Title = "Try out RethinkWeb" },
            new Todo { Id = Guid.NewGuid(), Title = "Read the docs" },
            new Todo { Id = Guid.NewGuid(), Title = "Wire up an MCP client",
                Notes = "See docs/mcp-clients.md" });
        db.SaveChanges();
    }
}

app.MapRethinkWeb();
app.MapMcp("/mcp");

app.Run();

public partial class Program;
