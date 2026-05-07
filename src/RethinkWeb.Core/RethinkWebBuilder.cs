using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RethinkWeb.Actions;
using RethinkWeb.Auth;
using RethinkWeb.Events;
using RethinkWeb.Manifest;
using RethinkWeb.Metadata;
using RethinkWeb.Storage;

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

    public RethinkWebBuilder AddEntity<TEntity>() where TEntity : class
    {
        EntityRegistry.Register(typeof(TEntity));

        // Default to in-memory store, wrapped in PublishingEntityStore so EntitySaved<T>
        // fires on every save regardless of write path. Adapter packages
        // (UseEfCoreFor, UseMartenFor, etc.) replace this and re-wrap themselves.
        Services.TryAddSingleton<InMemoryEntityStore<TEntity>>();
        Services.TryAddScoped<IEntityStore<TEntity>>(sp =>
            new PublishingEntityStore<TEntity>(
                sp.GetRequiredService<InMemoryEntityStore<TEntity>>(),
                sp.GetRequiredService<IEventBus>()));
        return this;
    }

    public RethinkWebBuilder AddAction<TAction>() where TAction : class
    {
        ActionRegistry.Register(typeof(TAction));
        Services.TryAddTransient<TAction>();
        return this;
    }

    public RethinkWebBuilder AddEventSubscriber<TEvent, TSubscriber>()
        where TEvent : class
        where TSubscriber : class, IEventSubscriber<TEvent>
    {
        Services.AddTransient<IEventSubscriber<TEvent>, TSubscriber>();
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
        // Scoped because they consume IAuthContext (per-request).
        services.TryAddScoped<IEventBus, InProcEventBus>();
        services.TryAddScoped<IManifestBuilder, ManifestBuilder>();
        services.TryAddScoped<IActionDispatcher, ActionDispatcher>();

        var builder = new RethinkWebBuilder(services);
        services.AddSingleton<IEntityRegistry>(_ => builder.EntityRegistry);
        services.AddSingleton<IActionRegistry>(_ => builder.ActionRegistry);
        return builder;
    }
}
