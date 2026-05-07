namespace RethinkWeb.Storage;

/// <summary>
/// Read/write access to a single entity type. Default impl is in-memory;
/// adapter packages (RethinkWeb.Store.EfCore, RethinkWeb.Store.Marten)
/// swap this out without touching app code.
/// </summary>
public interface IEntityStore<TEntity> where TEntity : class
{
    Task<TEntity?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken ct = default);
    Task<TEntity> SaveAsync(TEntity entity, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
