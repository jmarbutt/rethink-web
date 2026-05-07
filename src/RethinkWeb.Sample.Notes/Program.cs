using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RethinkWeb;
using RethinkWeb.Http;
using RethinkWeb.Mcp;
using RethinkWeb.Metadata;
using RethinkWeb.Render.Razor;
using RethinkWeb.Sample.Notes;
using RethinkWeb.Store.EfCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<NotesDb>(o => o.UseSqlite("Data Source=notes.db"));

builder.Services.AddRethinkWeb()
    .AddEntity<Note>()
    .UseEfCoreFor<Note, NotesDb>()
    .UseRazorRenderer()
    .AddRethinkWebMcpServer();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<NotesDb>().Database.EnsureCreated();
}

app.MapRethinkWeb();
app.MapMcp("/mcp");

app.Run();

public partial class Program;

namespace RethinkWeb.Sample.Notes
{
    [Entity(slug: "notes", displayName: "Notes")]
    public class Note
    {
        [Key] public Guid Id { get; set; }

        [TextBox("Title", GridVisible = true, GridOrder = 1, Required = true,
            Sample = "Buy groceries")]
        public string Title { get; set; } = "";

        [TextBox("Body", Multiline = true, MaxLength = 4000,
            Sample = "Eggs, milk, bread")]
        public string? Body { get; set; }

        [DateBox("Created", Disabled = true, GridVisible = true, GridOrder = 2)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public sealed class NotesDb(DbContextOptions<NotesDb> options) : DbContext(options)
    {
        public DbSet<Note> Notes => Set<Note>();
    }
}
