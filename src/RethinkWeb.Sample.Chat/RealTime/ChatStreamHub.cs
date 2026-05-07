using System.Collections.Concurrent;
using System.Threading.Channels;

namespace RethinkWeb.Sample.Chat.RealTime;

/// <summary>
/// In-memory pub/sub for chat message streaming. One subscriber per HTTP SSE
/// connection; broadcaster fans out new-message HTML to every subscriber on the
/// targeted channel id.
///
/// Lives in the sample, not in RethinkWeb.* — this is the prototype for what
/// Phase 3's `RethinkWeb.RealTime.Sse` adapter package would generalize into
/// a per-entity-type subscription mechanism. Building it here first keeps the
/// framework changes zero while we feel out the API.
/// </summary>
public sealed class ChatStreamHub
{
    private readonly ConcurrentDictionary<Guid, ConcurrentBag<Channel<string>>> _channels = new();

    public Channel<string> Subscribe(Guid channelId)
    {
        var ch = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
        _channels.GetOrAdd(channelId, _ => new()).Add(ch);
        return ch;
    }

    public void Unsubscribe(Guid channelId, Channel<string> subscriber)
    {
        // ConcurrentBag has no Remove; rebuild without the gone subscriber.
        // Acceptable for prototype scale; production wants a different data structure.
        if (_channels.TryGetValue(channelId, out var subs))
        {
            var remaining = subs.Where(s => s != subscriber).ToArray();
            _channels[channelId] = new ConcurrentBag<Channel<string>>(remaining);
        }
        subscriber.Writer.TryComplete();
    }

    public async Task PublishAsync(Guid channelId, string html)
    {
        if (!_channels.TryGetValue(channelId, out var subs)) return;
        foreach (var sub in subs)
        {
            try { await sub.Writer.WriteAsync(html); }
            catch (ChannelClosedException) { /* subscriber went away; ignored */ }
        }
    }
}
