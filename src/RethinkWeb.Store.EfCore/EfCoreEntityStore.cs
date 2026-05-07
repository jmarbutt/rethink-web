using System.Reflection;
using Microsoft.EntityFrameworkCore;
using RethinkWeb.Storage;

namespace RethinkWeb.Store.EfCore;

/// <summary>
/// EF Core-backed IEntityStore. Resolves the DbContext from DI per request scope.
/// Save uses Upsert semantics — Add if new, Update if tracked.
/// </summary>
public sealed class EfCoreEntityStore<TEntity, TContext>(TContext context) : IEntityStore<TEntity>
    where TEntity : class
    where TContext : DbContext
{
    private readonly DbSet<TEntity> _set = context.Set<TEntity>();

    private static readonly PropertyInfo IdProperty =
        typeof(TEntity).GetProperty("Id")
        ?? throw new InvalidOperationException(
            $"EfCoreEntityStore<{typeof(TEntity).Name}> requires an 'Id' property of type Guid.");

    public Task<TEntity?> GetAsync(Guid id, CancellationToken ct = default) =>
        _set.FindAsync([id], ct).AsTask();

    public async Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken ct = default) =>
        await _set.AsNoTracking().ToListAsync(ct);

    public async Task<TEntity> SaveAsync(TEntity entity, CancellationToken ct = default)
    {
        var id = (Guid)IdProperty.GetValue(entity)!;
        var existing = await _set.FindAsync([id], ct);
        if (existing is null)
        {
            await _set.AddAsync(entity, ct);
        }
        else
        {
            context.Entry(existing).CurrentValues.SetValues(entity);
        }
        await context.SaveChangesAsync(ct);
        return entity;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await _set.FindAsync([id], ct);
        if (existing is not null)
        {
            _set.Remove(existing);
            await context.SaveChangesAsync(ct);
        }
    }
}
