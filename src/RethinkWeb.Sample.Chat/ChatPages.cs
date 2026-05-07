using System.Net;
using RethinkWeb.Sample.Chat.Entities;

namespace RethinkWeb.Sample.Chat;

/// <summary>
/// Plain-HTML page templates for the chat UI. The metadata renderer doesn't fit
/// a chat layout, so we hand-roll HTML — exactly the "escape hatch" the framework
/// is supposed to allow. The framework still owns the data path (entities,
/// actions, events, MCP, manifest) — we just render the page differently.
/// </summary>
internal static class ChatPages
{
    private const string Layout = """
        <!DOCTYPE html>
        <html><head>
        <meta charset="utf-8" />
        <title>Chat — RethinkWeb sample</title>
        <script src="https://unpkg.com/htmx.org@2.0.3"></script>
        <script src="https://unpkg.com/htmx-ext-sse@2.2.2/sse.js"></script>
        <style>
            body { font-family: system-ui, sans-serif; max-width: 720px; margin: 1rem auto; padding: 0 1rem; }
            nav a { color: #2563eb; text-decoration: none; margin-right: 1rem; }
            #messages { border: 1px solid #ddd; padding: 1rem; min-height: 300px; max-height: 60vh; overflow-y: auto; background: #fafafa; }
            .msg { padding: 0.5rem 0; border-bottom: 1px solid #eee; }
            .msg strong { color: #2563eb; }
            .msg time { color: #999; font-size: 0.85em; margin-left: 0.5rem; }
            .msg p { margin: 0.25rem 0 0; }
            form { display: flex; gap: 0.5rem; margin-top: 1rem; }
            input { padding: 0.5rem; border: 1px solid #ccc; border-radius: 4px; font: inherit; }
            input[name="Body"] { flex: 1; }
            button { padding: 0.5rem 1rem; background: #2563eb; color: white; border: 0; border-radius: 4px; cursor: pointer; }
        </style>
        </head><body>
        <nav><a href="/">Home</a> <a href="/chat">Channels</a> <a href="/_framework/manifest" target="_blank">Manifest</a></nav>
        {{CONTENT}}
        </body></html>
        """;

    public static string IndexPage(IEnumerable<Channel> channels)
    {
        var items = string.Join("",
            channels.Select(c => $"<li><a href=\"/chat/{c.Id}\">#{WebUtility.HtmlEncode(c.Name)}</a> — {WebUtility.HtmlEncode(c.Description ?? "")}</li>"));
        return Layout.Replace("{{CONTENT}}", $"<h1>Channels</h1><ul>{items}</ul>");
    }

    public static string ChannelPage(Channel channel, IEnumerable<Message> messages)
    {
        var initial = string.Join("", messages.Select(MessageHtml));
        var content = $"""
            <h1>#{WebUtility.HtmlEncode(channel.Name)}</h1>
            <div id="messages"
                 hx-ext="sse"
                 sse-connect="/chat/{channel.Id}/stream"
                 sse-swap="message"
                 hx-swap="beforeend">
                {initial}
            </div>
            {ComposerForm(channel.Id, "")}
            """;
        return Layout.Replace("{{CONTENT}}", content);
    }

    public static string ComposerForm(Guid channelId, string author) => $"""
        <form id="composer"
              hx-post="/chat/{channelId}/send"
              hx-target="#composer"
              hx-swap="outerHTML">
            <input type="text" name="Author" value="{WebUtility.HtmlEncode(author)}" placeholder="Your name" required style="max-width: 8rem" />
            <input type="text" name="Body" placeholder="Type a message..." required autofocus />
            <button type="submit">Send</button>
        </form>
        """;

    private static string MessageHtml(Message m) => $"""
        <div class="msg">
          <strong>{WebUtility.HtmlEncode(m.Author)}</strong>
          <time>{m.PostedAt:HH:mm:ss}</time>
          <p>{WebUtility.HtmlEncode(m.Body)}</p>
        </div>
        """;
}
