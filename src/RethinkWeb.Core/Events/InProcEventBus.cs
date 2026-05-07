using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RethinkWeb.Auth;

namespace RethinkWeb.Events;

/// <summary>
/// Default in-process event bus. Synchronous dispatch to all registered
/// IEventSubscriber&lt;T&gt; for the published type. Subscribers run in DI scope
/// of the publishing call. ~50 lines, no external deps.
/// </summary>
public sealed class InProcEventBus(
    IServiceProvider services,
    IClock clock,
    IIdGenerator ids,
    IAuthContext auth,
    ILogger<InProcEventBus> logger) : IEventBus
{
    public async Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct = default) where TEvent : class
    {
        var subscribers = services.GetServices<IEventSubscriber<TEvent>>().ToArray();
        if (subscribers.Length == 0)
        {
            logger.LogDebug("No subscribers for {EventType}", typeof(TEvent).Name);
            return;
        }

        var ctx = new EventContext
        {
            SourceUserId = auth.UserId,
            PublishedAt = clock.UtcNow,
            CorrelationId = ids.NewId(),
        };

        foreach (var sub in subscribers)
        {
            await sub.HandleAsync(evt, ctx, ct);
        }
    }

    private sealed class EventContext : IEventContext
    {
        public string? SourceUserId { get; init; }
        public DateTimeOffset PublishedAt { get; init; }
        public Guid CorrelationId { get; init; }
    }
}
