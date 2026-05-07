using System.Net;
using RethinkWeb.Events;
using RethinkWeb.Sample.Chat.Entities;

namespace RethinkWeb.Sample.Chat.RealTime;

/// <summary>
/// Subscribes to <c>EntitySaved&lt;Message&gt;</c> and broadcasts the rendered
/// message HTML to every SSE client viewing the targeted channel.
///
/// Same event whether the message came from the HTML form, an MCP tool call,
/// or any other write path — that's the unification claim, demonstrated.
/// </summary>
public sealed class MessageBroadcaster(ChatStreamHub hub) : IEventSubscriber<EntitySaved<Message>>
{
    public async Task HandleAsync(EntitySaved<Message> evt, IEventContext context, CancellationToken ct)
    {
        var m = evt.Entity;
        var html = $"""
            <div class="msg">
              <strong>{WebUtility.HtmlEncode(m.Author)}</strong>
              <time>{m.PostedAt:HH:mm:ss}</time>
              <p>{WebUtility.HtmlEncode(m.Body)}</p>
            </div>
            """.Replace("\n", "");
        await hub.PublishAsync(m.ChannelId, html);
    }
}
