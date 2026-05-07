namespace RethinkWeb.Events;

/// <summary>
/// The pub/sub abstraction. Default impl is in-proc; adapter packages
/// (RethinkWeb.Bus.Wolverine, etc.) swap this out without touching app code.
/// </summary>
public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct = default) where TEvent : class;
}

/// <summary>
/// Implement this for each event you want to react to. Resolved from DI.
/// Subscribers run inside the same scope as the publishing action.
/// </summary>
public interface IEventSubscriber<in TEvent> where TEvent : class
{
    Task HandleAsync(TEvent evt, IEventContext context, CancellationToken ct);
}

/// <summary>
/// Per-publication context. Lets subscribers correlate, log, and trace.
/// </summary>
public interface IEventContext
{
    string? SourceUserId { get; }
    DateTimeOffset PublishedAt { get; }
    Guid CorrelationId { get; }
}
