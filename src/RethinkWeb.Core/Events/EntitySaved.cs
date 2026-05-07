namespace RethinkWeb.Events;

/// <summary>
/// Auto-published by the framework after every entity save (form post, action,
/// or any future write path). Subscribe via <c>IEventSubscriber&lt;EntitySaved&lt;TEntity&gt;&gt;</c>
/// to react to changes regardless of which write path produced them.
/// </summary>
public sealed record EntitySaved<TEntity>(TEntity Entity) where TEntity : class;
