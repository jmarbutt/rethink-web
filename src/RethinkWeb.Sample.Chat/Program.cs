using System.Net;
using Microsoft.EntityFrameworkCore;
using RethinkWeb;
using RethinkWeb.Actions;
using RethinkWeb.Events;
using RethinkWeb.Http;
using RethinkWeb.Mcp;
using RethinkWeb.Render.Razor;
using RethinkWeb.Sample.Chat;
using RethinkWeb.Sample.Chat.Actions;
using RethinkWeb.Sample.Chat.Entities;
using RethinkWeb.Sample.Chat.RealTime;
using RethinkWeb.Store.EfCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ChatDb>(o => o.UseSqlite("Data Source=chat.db"));
builder.Services.AddSingleton<ChatStreamHub>();

builder.Services
    .AddRethinkWeb()
    .AddEntity<Channel>().UseEfCoreFor<Channel, ChatDb>()
    .AddEntity<Message>().UseEfCoreFor<Message, ChatDb>()
    .AddAction<SendMessageAction>()
    .AddEventSubscriber<EntitySaved<Message>, MessageBroadcaster>()
    .UseRazorRenderer()
    .AddRethinkWebMcpServer();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChatDb>();
    db.Database.EnsureCreated();
    if (!db.Channels.Any())
    {
        db.Channels.AddRange(
            new Channel { Id = Guid.NewGuid(), Name = "general", Description = "Off-topic chatter" },
            new Channel { Id = Guid.NewGuid(), Name = "deploys", Description = "Deployment alerts" });
        db.SaveChanges();
    }
}

// Framework routes (entity grids, edit forms, manifest, MCP)
app.MapRethinkWeb();
app.MapMcp("/mcp");

// --- Custom chat UI: escape-hatch demo ---
// The metadata renderer doesn't fit a chat layout, so we serve plain HTML directly.
// New messages stream in via HTMX SSE. POST goes through IActionDispatcher so the
// SendMessageAction (and EntitySaved subscribers) fires the same as any other path.

app.MapGet("/chat", async (ChatDb db) =>
{
    var channels = await db.Channels.OrderBy(c => c.Name).ToListAsync();
    var html = ChatPages.IndexPage(channels);
    return Results.Content(html, "text/html");
});

app.MapGet("/chat/{id:guid}", async (Guid id, ChatDb db) =>
{
    var channel = await db.Channels.FindAsync(id);
    if (channel is null) return Results.NotFound();
    var messages = await db.Messages
        .Where(m => m.ChannelId == id)
        .OrderBy(m => m.PostedAt)
        .ToListAsync();
    return Results.Content(ChatPages.ChannelPage(channel, messages), "text/html");
});

app.MapPost("/chat/{id:guid}/send", async (
    Guid id,
    HttpContext ctx,
    IActionDispatcher dispatcher) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var input = new SendMessageInput(
        Author: form["Author"].ToString(),
        Body: form["Body"].ToString());
    var result = await dispatcher.InvokeAsync("channels", "send", id, input, ctx.RequestAborted);
    if (!result.Authorized) return Results.StatusCode(StatusCodes.Status403Forbidden);

    // Return a fresh empty input form — HTMX swaps it in, message body is cleared.
    return Results.Content(ChatPages.ComposerForm(id, form["Author"].ToString()), "text/html");
});

app.MapGet("/chat/{id:guid}/stream", async (Guid id, ChatStreamHub hub, HttpContext ctx) =>
{
    ctx.Response.Headers.Append("Content-Type", "text/event-stream");
    ctx.Response.Headers.Append("Cache-Control", "no-cache");
    ctx.Response.Headers.Append("X-Accel-Buffering", "no");
    var sub = hub.Subscribe(id);
    try
    {
        await foreach (var html in sub.Reader.ReadAllAsync(ctx.RequestAborted))
        {
            await ctx.Response.WriteAsync($"event: message\ndata: {html}\n\n", ctx.RequestAborted);
            await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
        }
    }
    catch (OperationCanceledException) { /* client disconnected */ }
    finally
    {
        hub.Unsubscribe(id, sub);
    }
});

app.Run();

public partial class Program;
