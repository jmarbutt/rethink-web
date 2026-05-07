using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RethinkWeb.Storage;
using RethinkWeb.Tenancy;

namespace RethinkWeb.Store.EfCore;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Replaces the in-memory IEntityStore&lt;TEntity&gt; with an EF-Core-backed store
    /// using the supplied DbContext. Call AFTER <c>AddEntity&lt;TEntity&gt;()</c>.
    /// Decorator order (outer→inner): tenant scoping → publishing → EF Core.
    /// </summary>
    public static RethinkWebBuilder UseEfCoreFor<TEntity, TContext>(this RethinkWebBuilder builder)
        where TEntity : class
        where TContext : DbContext
    {
        builder.Services.RemoveAll<IEntityStore<TEntity>>();
        builder.Services.AddScoped<IEntityStore<TEntity>>(sp =>
        {
            IEntityStore<TEntity> store = new EfCoreEntityStore<TEntity, TContext>(
                sp.GetRequiredService<TContext>());
            store = new PublishingEntityStore<TEntity>(
                store, sp.GetRequiredService<RethinkWeb.Events.IEventBus>());
            if (builder.MultiTenantEnabled)
            {
                store = new TenantScopedEntityStore<TEntity>(
                    store, sp.GetRequiredService<ITenantContext>());
            }
            return store;
        });
        return builder;
    }
}
