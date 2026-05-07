using Microsoft.EntityFrameworkCore;
using RethinkWeb;
using RethinkWeb.Events;
using RethinkWeb.Http;
using RethinkWeb.Mcp;
using RethinkWeb.Render.Razor;
using RethinkWeb.Sample.Donor;
using RethinkWeb.Sample.Donor.Actions;
using RethinkWeb.Sample.Donor.Entities;
using RethinkWeb.Sample.Donor.Events;
using RethinkWeb.Store.EfCore;

var builder = WebApplication.CreateBuilder(args);

// EF Core + SQLite. Single file DB for dev — `donor-sample.db` next to the binary.
builder.Services.AddDbContext<SampleContext>(options =>
    options.UseSqlite("Data Source=donor-sample.db"));

builder.Services
    .AddRethinkWeb()
    .AddEntity<Donor>()
    .UseEfCoreFor<Donor, SampleContext>()
    .AddEntity<Donation>()
    .UseEfCoreFor<Donation, SampleContext>()
    .AddAction<UpdateAddressAction>()
    .AddEventSubscriber<EntitySaved<Donation>, RecomputeDeductibleSubscriber>()
    .UseRazorRenderer()
    .AddRethinkWebMcpServer();

var app = builder.Build();

// Bootstrap DB + seed a couple donors so the grid isn't empty on first run.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SampleContext>();
    db.Database.EnsureCreated();
    if (!db.Donors.Any())
    {
        db.Donors.AddRange(
            new Donor { Id = Guid.NewGuid(), FirstName = "John", LastName = "Smith",
                PrimaryEmail = "john@example.com", City = "Springfield", State = "IL",
                YearToDateTotal = 1500m, Active = true },
            new Donor { Id = Guid.NewGuid(), FirstName = "Jane", LastName = "Doe",
                PrimaryEmail = "jane@example.com", City = "Madison", State = "WI",
                YearToDateTotal = 250m, Active = true });
        db.Donations.Add(new Donation
        {
            Id = Guid.NewGuid(),
            DonorName = "John Smith",
            Amount = 100m,
            AmountDeductible = 100m,
            DonationDate = new DateTime(2026, 5, 1),
        });
        db.SaveChanges();
    }
}

app.MapRethinkWeb();
app.MapMcp("/mcp");

app.Run();

/// <summary>Public marker so WebApplicationFactory&lt;Program&gt; can find the entry point.</summary>
public partial class Program;
