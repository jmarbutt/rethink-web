using RethinkWeb.Events;

namespace RethinkWeb.Storage;

/// <summary>
/// Decorator that publishes <see cref="EntitySaved{TEntity}"/> after every successful save,
/// regardless of which write path triggered it (HTTP form post, action via dispatcher,
/// MCP tool, future workflow step). This is what makes the framework's "subscribe once,
/// react to all changes" promise actually true.
///
/// Recursion guard: subscribers that themselves call <c>SaveAsync</c> on the same entity
/// type within the same async call chain will not re-trigger publication, preventing
/// trivial infinite loops. (Cross-type chains are still possible — keep subscribers
/// idempotent.)
/// </summary>
public sealed class PublishingEntityStore<TEntity>(
    IEntityStore<TEntity> inner,
    IEventBus events) : IEntityStore<TEntity>
    where TEntity : class
{
    private static readonly AsyncLocal<bool> _publishing = new();

    public Task<TEntity?> GetAsync(Guid id, CancellationToken ct = default) =>
        inner.GetAsync(id, ct);

    public Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken ct = default) =>
        inner.ListAsync(ct);

    public async Task<TEntity> SaveAsync(TEntity entity, CancellationToken ct = default)
    {
        var saved = await inner.SaveAsync(entity, ct);

        if (_publishing.Value)
        {
            // We're already inside a publish for this entity type — a subscriber called
            // SaveAsync. Skip re-publishing to avoid infinite recursion.
            return saved;
        }

        _publishing.Value = true;
        try
        {
            await events.PublishAsync(new EntitySaved<TEntity>(saved), ct);
        }
        finally
        {
            _publishing.Value = false;
        }
        return saved;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default) =>
        inner.DeleteAsync(id, ct);
}
