using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RethinkWeb.Storage;

namespace RethinkWeb.Store.EfCore;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Replaces the in-memory IEntityStore&lt;TEntity&gt; with an EF-Core-backed store
    /// using the supplied DbContext. Call AFTER <c>AddEntity&lt;TEntity&gt;()</c>.
    /// </summary>
    public static RethinkWebBuilder UseEfCoreFor<TEntity, TContext>(this RethinkWebBuilder builder)
        where TEntity : class
        where TContext : DbContext
    {
        // Replace registration: we win because we run after AddEntity()'s TryAdd.
        builder.Services.RemoveAll<IEntityStore<TEntity>>();
        builder.Services.AddScoped<IEntityStore<TEntity>, EfCoreEntityStore<TEntity, TContext>>();
        return builder;
    }
}
