using System.Collections.Concurrent;
using System.Reflection;

namespace RethinkWeb.Storage;

/// <summary>
/// Default in-memory store. Useful for tests and the prototype.
/// Reflects on an "Id" property to key entities. Production apps swap in EF Core.
/// </summary>
public sealed class InMemoryEntityStore<TEntity> : IEntityStore<TEntity> where TEntity : class
{
    private readonly ConcurrentDictionary<Guid, TEntity> _items = new();
    private readonly PropertyInfo _idProperty =
        typeof(TEntity).GetProperty("Id")
        ?? throw new InvalidOperationException(
            $"InMemoryEntityStore<{typeof(TEntity).Name}> requires an 'Id' property of type Guid.");

    public Task<TEntity?> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_items.TryGetValue(id, out var e) ? e : null);

    public Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<TEntity>>([.. _items.Values]);

    public Task<TEntity> SaveAsync(TEntity entity, CancellationToken ct = default)
    {
        var id = (Guid)(_idProperty.GetValue(entity)
            ?? throw new InvalidOperationException("Entity Id is null."));
        _items[id] = entity;
        return Task.FromResult(entity);
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        _items.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
