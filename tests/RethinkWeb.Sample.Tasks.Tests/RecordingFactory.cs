using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RethinkWeb.Events;
using RethinkWeb.Sample.Tasks.Entities;

namespace RethinkWeb.Sample.Tasks.Tests;

/// <summary>
/// WebApplicationFactory variant that registers a recording subscriber so we can
/// assert EntitySaved&lt;Todo&gt; publication, and points the SQLite database at a
/// per-fixture tmp file so multiple test classes don't collide on the shared
/// `tasks.db` from Program.cs.
/// </summary>
public sealed class RecordingFactory : WebApplicationFactory<Program>
{
    public TodoRecorder Recorder { get; } = new();

    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(), $"rw-tasks-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var existing = services.Single(d => d.ServiceType == typeof(DbContextOptions<TasksDb>));
            services.Remove(existing);
            services.AddDbContext<TasksDb>(o => o.UseSqlite($"Data Source={_dbPath}"));

            services.AddSingleton(Recorder);
            services.AddSingleton<IEventSubscriber<EntitySaved<Todo>>>(sp =>
                sp.GetRequiredService<TodoRecorder>());
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}

public sealed class TodoRecorder : IEventSubscriber<EntitySaved<Todo>>
{
    public List<Todo> Received { get; } = [];
    public Task HandleAsync(EntitySaved<Todo> evt, IEventContext context, CancellationToken ct)
    {
        Received.Add(evt.Entity);
        return Task.CompletedTask;
    }
    public void Clear() => Received.Clear();
}
