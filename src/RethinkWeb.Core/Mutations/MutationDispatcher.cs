using Microsoft.Extensions.DependencyInjection;
using RethinkWeb.Auth;
using RethinkWeb.Events;
using RethinkWeb.Metadata;
using RethinkWeb.Storage;

namespace RethinkWeb.Mutations;

public interface IMutationDispatcher
{
    Task<MutationResult> InvokeAsync(
        string entitySlug,
        string mutationName,
        Guid entityId,
        object input,
        CancellationToken ct = default);
}

public sealed record MutationResult(bool Authorized, object? Output, string? Error);

public sealed class MutationDispatcher(
    IServiceProvider services,
    IEntityRegistry entities,
    IMutationRegistry mutations,
    IAuthContext auth,
    IEventBus events,
    IClock clock) : IMutationDispatcher
{
    public async Task<MutationResult> InvokeAsync(
        string entitySlug,
        string mutationName,
        Guid entityId,
        object input,
        CancellationToken ct = default)
    {
        var entityMeta = entities.GetBySlug(entitySlug)
            ?? throw new InvalidOperationException($"Unknown entity slug '{entitySlug}'.");

        if (entityMeta.WritePermission is not null && !auth.HasPermission(entityMeta.WritePermission))
        {
            return new MutationResult(Authorized: false, Output: null, Error: "Forbidden");
        }

        var descriptor = mutations.Find(entityMeta.ClrType, mutationName)
            ?? throw new InvalidOperationException(
                $"No mutation '{mutationName}' registered for entity '{entitySlug}'.");

        if (descriptor.Permission is not null && !auth.HasPermission(descriptor.Permission))
        {
            return new MutationResult(Authorized: false, Output: null, Error: "Forbidden");
        }

        var storeType = typeof(IEntityStore<>).MakeGenericType(entityMeta.ClrType);
        var store = services.GetRequiredService(storeType);
        var getMethod = storeType.GetMethod(nameof(IEntityStore<object>.GetAsync))!;
        var entityTask = (Task)getMethod.Invoke(store, [entityId, ct])!;
        await entityTask;
        var entity = entityTask.GetType().GetProperty("Result")!.GetValue(entityTask)
            ?? throw new InvalidOperationException(
                $"{entitySlug} {entityId} not found.");

        var mutationInstance = ActivatorUtilities.CreateInstance(services, descriptor.ImplementationType);
        var executeMethod = descriptor.ImplementationType.GetMethod("ExecuteAsync")
            ?? throw new InvalidOperationException(
                $"Mutation {descriptor.ImplementationType.Name} has no ExecuteAsync method.");

        var ctx = new MutationContext(auth, events, clock);
        var resultTask = (Task)executeMethod.Invoke(mutationInstance, [entity, input, ctx, ct])!;
        await resultTask;
        var output = resultTask.GetType().GetProperty("Result")!.GetValue(resultTask);

        return new MutationResult(Authorized: true, Output: output, Error: null);
    }

    private sealed class MutationContext(IAuthContext auth, IEventBus events, IClock clock) : IMutationContext
    {
        public IAuthContext Auth { get; } = auth;
        public IEventBus Events { get; } = events;
        public IClock Clock { get; } = clock;
    }
}
