using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RethinkWeb.Actions;
using RethinkWeb.Auth;
using RethinkWeb.Events;
using RethinkWeb.Manifest;
using RethinkWeb.Metadata;
using RethinkWeb.Mutations;
using RethinkWeb.Queries;
using RethinkWeb.Storage;
using RethinkWeb.Tenancy;

namespace RethinkWeb;

/// <summary>
/// Fluent builder returned from <c>services.AddRethinkWeb()</c>.
/// All registrations use TryAdd* so user overrides win without Remove gymnastics.
/// </summary>
public sealed class RethinkWebBuilder(IServiceCollection services)
{
    public IServiceCollection Services { get; } = services;

    internal EntityRegistry EntityRegistry { get; } = new();
    internal ActionRegistry ActionRegistry { get; } = new();
    internal QueryRegistry QueryRegistry { get; } = new();
    internal MutationRegistry MutationRegistry { get; } = new();

    /// <summary>
    /// True after <see cref="UseMultiTenant{TResolver}"/> has been called. Subsequent
    /// <see cref="AddEntity{T}"/> calls will layer the tenant decorator on the store
    /// when the entity implements <see cref="ITenantOwned"/>. Adapter packages
    /// (Store.EfCore, Store.Marten, etc.) read this to mirror Core's conditional
    /// decorator stacking.
    /// </summary>
    public bool MultiTenantEnabled { get; private set; }

    public RethinkWebBuilder AddEntity<TEntity>() where TEntity : class
    {
        EntityRegistry.Register(typeof(TEntity));

        // Default to in-memory store, wrapped in PublishingEntityStore so EntitySaved<T>
        // fires on every save regardless of write path. Adapter packages
        // (UseEfCoreFor, UseMartenFor, etc.) replace this and re-wrap themselves.
        Services.TryAddSingleton<InMemoryEntityStore<TEntity>>();
        Services.TryAddScoped<IEntityStore<TEntity>>(sp =>
        {
            IEntityStore<TEntity> store = sp.GetRequiredService<InMemoryEntityStore<TEntity>>();
            store = new PublishingEntityStore<TEntity>(store, sp.GetRequiredService<IEventBus>());
            if (MultiTenantEnabled)
            {
                store = new TenantScopedEntityStore<TEntity>(store, sp.GetRequiredService<ITenantContext>());
            }
            return store;
        });
        return this;
    }

    public RethinkWebBuilder AddAction<TAction>() where TAction : class
    {
        ActionRegistry.Register(typeof(TAction));
        Services.TryAddTransient<TAction>();
        return this;
    }

    public RethinkWebBuilder AddQuery<TQuery>() where TQuery : class
    {
        QueryRegistry.Register(typeof(TQuery));
        Services.TryAddTransient<TQuery>();
        return this;
    }

    public RethinkWebBuilder AddMutation<TMutation>() where TMutation : class
    {
        MutationRegistry.Register(typeof(TMutation));
        Services.TryAddTransient<TMutation>();
        return this;
    }

    public RethinkWebBuilder AddEventSubscriber<TEvent, TSubscriber>()
        where TEvent : class
        where TSubscriber : class, IEventSubscriber<TEvent>
    {
        Services.AddTransient<IEventSubscriber<TEvent>, TSubscriber>();
        return this;
    }

    /// <summary>
    /// Switches the framework into multi-tenant mode. Every entity registered AFTER
    /// this call that implements <see cref="ITenantOwned"/> will be auto-scoped to
    /// the current tenant: saves stamp the TenantId, reads filter by TenantId,
    /// cross-tenant access throws.
    ///
    /// Multi-tenancy is opt-in. If you don't call this, the framework runs in
    /// single-tenant mode (which is what every sample today does).
    ///
    /// The resolver is responsible for extracting the tenant id from per-request
    /// context (HTTP header, subdomain, JWT claim, etc.). Implement
    /// <see cref="ITenantResolver"/> in your app for custom resolution; HTTP-host
    /// adapters (RethinkWeb.Http.MinimalApi) ship default resolvers like
    /// <c>HeaderTenantResolver</c>.
    /// </summary>
    public RethinkWebBuilder UseMultiTenant<TResolver>()
        where TResolver : class, ITenantResolver
    {
        MultiTenantEnabled = true;
        Services.AddScoped<ITenantResolver, TResolver>();
        Services.AddScoped<ScopedTenantContext>();
        Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<ScopedTenantContext>());
        return this;
    }
}

public static class ServiceCollectionExtensions
{
    public static RethinkWebBuilder AddRethinkWeb(this IServiceCollection services)
    {
        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddSingleton<IIdGenerator, GuidIdGenerator>();
        services.TryAddScoped<IAuthContext, AllowAllAuthContext>();
        // Default tenant context is single-tenant; UseMultiTenant overrides this.
        services.TryAddScoped<ITenantContext, SingleTenantContext>();
        // Scoped because they consume IAuthContext (per-request).
        services.TryAddScoped<IEventBus, InProcEventBus>();
        services.TryAddScoped<IManifestBuilder, ManifestBuilder>();
        services.TryAddScoped<IActionDispatcher, ActionDispatcher>();
        services.TryAddScoped<IQueryDispatcher, QueryDispatcher>();
        services.TryAddScoped<IMutationDispatcher, MutationDispatcher>();
        services.TryAddSingleton<IQueryCache, NullQueryCache>();

        var builder = new RethinkWebBuilder(services);
        services.AddSingleton<IEntityRegistry>(_ => builder.EntityRegistry);
        services.AddSingleton<IActionRegistry>(_ => builder.ActionRegistry);
        services.AddSingleton<IQueryRegistry>(_ => builder.QueryRegistry);
        services.AddSingleton<IMutationRegistry>(_ => builder.MutationRegistry);
        return builder;
    }
}
