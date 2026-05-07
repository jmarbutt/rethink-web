using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RethinkWeb.Events;

namespace RethinkWeb.Sample.Donor.Tests;

/// <summary>
/// Custom WebApplicationFactory that:
///  - registers a recording subscriber so tests can inspect EntitySaved&lt;Donor&gt;
///    publication without spinning up a second host;
///  - re-points the SQLite database at a unique tmp file so this fixture doesn't
///    collide with the EndToEndTests fixture (both run EnsureCreated on bootup).
/// </summary>
public sealed class RecordingFactory : WebApplicationFactory<Program>
{
    public DonorRecorder Recorder { get; } = new();

    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(), $"rw-recording-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var existing = services.Single(d => d.ServiceType == typeof(DbContextOptions<SampleContext>));
            services.Remove(existing);
            services.AddDbContext<SampleContext>(o => o.UseSqlite($"Data Source={_dbPath}"));

            services.AddSingleton(Recorder);
            services.AddSingleton<IEventSubscriber<EntitySaved<Entities.Donor>>>(sp =>
                sp.GetRequiredService<DonorRecorder>());
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}

public sealed class DonorRecorder : IEventSubscriber<EntitySaved<Entities.Donor>>
{
    public List<Entities.Donor> Received { get; } = [];
    public Task HandleAsync(EntitySaved<Entities.Donor> evt, IEventContext context, CancellationToken ct)
    {
        Received.Add(evt.Entity);
        return Task.CompletedTask;
    }
    public void Clear() => Received.Clear();
}
