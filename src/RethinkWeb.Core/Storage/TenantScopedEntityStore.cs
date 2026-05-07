using RethinkWeb.Tenancy;

namespace RethinkWeb.Storage;

/// <summary>
/// Decorator that scopes an <see cref="IEntityStore{TEntity}"/> to the current tenant.
/// Only wraps entities implementing <see cref="ITenantOwned"/>; global entities are
/// passed through unchanged.
///
/// Behavior in multi-tenant mode (TenantContext.TenantId is non-null):
/// - <see cref="GetAsync"/>: returns null if the loaded entity belongs to another tenant
/// - <see cref="ListAsync"/>: filters out entities belonging to other tenants
/// - <see cref="SaveAsync"/>: auto-stamps TenantId on insert (when null);
///   throws <see cref="CrossTenantAccessException"/> if the entity already has
///   a different TenantId than the current request's tenant
/// - <see cref="DeleteAsync"/>: only deletes if the entity belongs to current tenant
///
/// Behavior in single-tenant mode (TenantContext.TenantId is null): pass-through.
/// This makes the decorator safe to layer in always — if multi-tenancy is not
/// configured, it's a no-op.
///
/// Filtering happens in memory after loading from the inner store. EF Core users
/// should additionally call <c>modelBuilder.ApplyTenantFilters(...)</c> in
/// <c>OnModelCreating</c> to push filtering to SQL — see RethinkWeb.Store.EfCore.
/// </summary>
public sealed class TenantScopedEntityStore<TEntity>(
    IEntityStore<TEntity> inner,
    ITenantContext tenant) : IEntityStore<TEntity>
    where TEntity : class
{
    private static readonly bool IsTenantOwned =
        typeof(ITenantOwned).IsAssignableFrom(typeof(TEntity));

    public async Task<TEntity?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await inner.GetAsync(id, ct);
        if (entity is null) return null;
        if (!IsTenantOwned || tenant.TenantId is null) return entity;
        return ((ITenantOwned)entity).TenantId == tenant.TenantId ? entity : null;
    }

    public async Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken ct = default)
    {
        var all = await inner.ListAsync(ct);
        if (!IsTenantOwned || tenant.TenantId is null) return all;
        var t = tenant.TenantId;
        return [.. all.Where(e => ((ITenantOwned)e).TenantId == t)];
    }

    public async Task<TEntity> SaveAsync(TEntity entity, CancellationToken ct = default)
    {
        if (IsTenantOwned && tenant.TenantId is not null)
        {
            var owned = (ITenantOwned)entity;
            if (owned.TenantId is null)
            {
                owned.TenantId = tenant.TenantId;   // auto-stamp on insert
            }
            else if (owned.TenantId != tenant.TenantId)
            {
                throw new CrossTenantAccessException(
                    $"Cannot save {typeof(TEntity).Name} for tenant '{owned.TenantId}' from tenant '{tenant.TenantId}' context.");
            }
        }
        return await inner.SaveAsync(entity, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (IsTenantOwned && tenant.TenantId is not null)
        {
            var existing = await inner.GetAsync(id, ct);
            if (existing is null) return;
            if (((ITenantOwned)existing).TenantId != tenant.TenantId)
            {
                throw new CrossTenantAccessException(
                    $"Cannot delete {typeof(TEntity).Name} {id} — it belongs to tenant '{((ITenantOwned)existing).TenantId}'.");
            }
        }
        await inner.DeleteAsync(id, ct);
    }
}
