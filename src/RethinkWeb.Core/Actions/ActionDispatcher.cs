using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using RethinkWeb.Auth;
using RethinkWeb.Events;
using RethinkWeb.Metadata;
using RethinkWeb.Storage;

namespace RethinkWeb.Actions;

/// <summary>
/// Resolves an action by entity slug + action name, loads the entity, invokes the action,
/// and returns the boxed result. Same code path serves HTTP and MCP.
/// </summary>
public interface IActionDispatcher
{
    Task<ActionResult> InvokeAsync(
        string entitySlug,
        string actionName,
        Guid entityId,
        object input,
        CancellationToken ct = default);
}

public sealed record ActionResult(bool Authorized, object? Output, string? Error);

public sealed class ActionDispatcher(
    IServiceProvider services,
    IEntityRegistry entities,
    IActionRegistry actions,
    IAuthContext auth,
    IEventBus events,
    IClock clock) : IActionDispatcher
{
    public async Task<ActionResult> InvokeAsync(
        string entitySlug,
        string actionName,
        Guid entityId,
        object input,
        CancellationToken ct = default)
    {
        var entityMeta = entities.GetBySlug(entitySlug)
            ?? throw new InvalidOperationException($"Unknown entity slug '{entitySlug}'.");

        var descriptor = actions.Find(entityMeta.ClrType, actionName)
            ?? throw new InvalidOperationException(
                $"No action '{actionName}' registered for entity '{entitySlug}'.");

        if (descriptor.Permission is not null && !auth.HasPermission(descriptor.Permission))
        {
            return new ActionResult(Authorized: false, Output: null, Error: "Forbidden");
        }

        var storeType = typeof(IEntityStore<>).MakeGenericType(entityMeta.ClrType);
        var store = services.GetRequiredService(storeType);
        var getMethod = storeType.GetMethod(nameof(IEntityStore<object>.GetAsync))!;
        var entityTask = (Task)getMethod.Invoke(store, [entityId, ct])!;
        await entityTask;
        var entity = entityTask.GetType().GetProperty("Result")!.GetValue(entityTask)
            ?? throw new InvalidOperationException(
                $"{entitySlug} {entityId} not found.");

        var actionInstance = ActivatorUtilities.CreateInstance(services, descriptor.ImplementationType);
        var executeMethod = descriptor.ImplementationType.GetMethod("ExecuteAsync")
            ?? throw new InvalidOperationException(
                $"Action {descriptor.ImplementationType.Name} has no ExecuteAsync method.");

        var ctx = new ActionContext(auth, events, clock);
        var resultTask = (Task)executeMethod.Invoke(actionInstance, [entity, input, ctx, ct])!;
        await resultTask;
        var output = resultTask.GetType().GetProperty("Result")!.GetValue(resultTask);

        return new ActionResult(Authorized: true, Output: output, Error: null);
    }

    private sealed class ActionContext(IAuthContext auth, IEventBus events, IClock clock) : IActionContext
    {
        public IAuthContext Auth { get; } = auth;
        public IEventBus Events { get; } = events;
        public IClock Clock { get; } = clock;
    }
}
