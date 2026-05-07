using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using RethinkWeb.Actions;
using RethinkWeb.Metadata;
using RethinkWeb.Mutations;
using RethinkWeb.Queries;

namespace RethinkWeb.Mcp;

/// <summary>
/// Builds the MCP tool collection from the framework's <see cref="IActionRegistry"/>.
/// One <see cref="McpServerTool"/> per registered action, named "{entitySlug}.{actionName}".
///
/// Each tool delegate accepts:
///   - <c>entityId</c>: which entity instance the action targets
///   - <c>input</c>: the action's TInput record (auto-deserialized from JSON)
///   - <see cref="CancellationToken"/>: forwarded by the SDK
///
/// We resolve <see cref="IActionDispatcher"/> from a per-call DI scope created
/// off the root <see cref="IServiceProvider"/> captured at registration time,
/// rather than as a delegate parameter — the dynamic-registration path does not
/// reliably distinguish DI-provided parameters from JSON-bound ones.
///
/// The SDK introspects the JSON-bound parameters to auto-generate a JSON Schema
/// for tools/list.
/// </summary>
public sealed class RethinkWebMcpToolCollection
{
    public McpServerPrimitiveCollection<McpServerTool> Tools { get; } = [];

    public RethinkWebMcpToolCollection(
        IServiceProvider rootServices,
        IActionRegistry actions,
        IQueryRegistry queries,
        IMutationRegistry mutations,
        IEntityRegistry entities)
    {
        foreach (var query in queries.All)
        {
            if (!query.ExposeToMcp) continue;
            Tools.Add(BuildToolForQuery(rootServices, query));
        }

        foreach (var action in actions.All)
        {
            if (!action.ExposeToMcp) continue;

            var entity = entities.Get(action.EntityType);
            var tool = BuildToolForAction(rootServices, entity.Slug, action);
            Tools.Add(tool);
        }

        foreach (var mutation in mutations.All)
        {
            if (!mutation.ExposeToMcp) continue;

            var entity = entities.Get(mutation.EntityType);
            Tools.Add(BuildToolForMutation(rootServices, entity.Slug, mutation));
        }
    }

    private static McpServerTool BuildToolForQuery(
        IServiceProvider rootServices,
        QueryDescriptor query)
    {
        var build = typeof(RethinkWebMcpToolCollection)
            .GetMethod(nameof(BuildQueryToolGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(query.InputType);

        var description = query.Description ?? query.DisplayName;
        return (McpServerTool)build.Invoke(null, [rootServices, query.Name, description])!;
    }

    private static McpServerTool BuildToolForMutation(
        IServiceProvider rootServices,
        string slug,
        MutationDescriptor mutation)
    {
        var build = typeof(RethinkWebMcpToolCollection)
            .GetMethod(nameof(BuildMutationToolGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(mutation.InputType);

        var toolName = $"{slug}.{mutation.Name}";
        var description = mutation.Description ?? mutation.DisplayName;

        return (McpServerTool)build.Invoke(null, [rootServices, slug, mutation.Name, toolName, description])!;
    }

    private static McpServerTool BuildToolForAction(
        IServiceProvider rootServices,
        string slug,
        ActionDescriptor action)
    {
        // Reflection bridge: BuildToolGeneric<TInput> needs a static type for TInput
        // so the SDK can introspect the delegate signature for schema generation.
        var build = typeof(RethinkWebMcpToolCollection)
            .GetMethod(nameof(BuildToolGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(action.InputType);

        var toolName = $"{slug}.{action.Name}";
        var description = action.Description ?? action.DisplayName;

        return (McpServerTool)build.Invoke(null, [rootServices, slug, action.Name, toolName, description])!;
    }

    private static McpServerTool BuildToolGeneric<TInput>(
        IServiceProvider rootServices,
        string slug,
        string actionName,
        string toolName,
        string description)
        where TInput : class
    {
        return McpServerTool.Create(
            async (
                string entityId,
                TInput input,
                CancellationToken ct) =>
            {
                try
                {
                    await using var scope = rootServices.CreateAsyncScope();
                    var dispatcher = scope.ServiceProvider.GetRequiredService<IActionDispatcher>();
                    var result = await dispatcher.InvokeAsync(
                        slug, actionName, Guid.Parse(entityId), input, ct);

                    if (!result.Authorized)
                    {
                        throw new UnauthorizedAccessException(result.Error ?? "Forbidden");
                    }

                    // Tool result content is JSON text — clients parse as needed.
                    return JsonSerializer.Serialize(result.Output);
                }
                catch (Exception ex) when (ex is not UnauthorizedAccessException)
                {
                    // The SDK swallows inner exception details and emits a generic
                    // "An error occurred invoking '<tool>'" by design (avoids leaking
                    // internals). For prototype debugging we surface the real message
                    // as the tool result text — flip this back to throw before going
                    // to production, or wire a request filter as documented in the
                    // SDK's filters guide.
                    return $"ERROR in {actionName}: {ex.GetBaseException().Message}";
                }
            },
            new McpServerToolCreateOptions
            {
                Name = toolName,
                Description = description,
            });
    }

    private static McpServerTool BuildQueryToolGeneric<TInput>(
        IServiceProvider rootServices,
        string queryName,
        string description)
        where TInput : class
    {
        return McpServerTool.Create(
            async (
                TInput input,
                CancellationToken ct) =>
            {
                try
                {
                    await using var scope = rootServices.CreateAsyncScope();
                    var dispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();
                    var result = await dispatcher.InvokeAsync(queryName, input, ct);

                    if (!result.Authorized)
                    {
                        throw new UnauthorizedAccessException(result.Error ?? "Forbidden");
                    }

                    return JsonSerializer.Serialize(result.Output);
                }
                catch (Exception ex) when (ex is not UnauthorizedAccessException)
                {
                    return $"ERROR in {queryName}: {ex.GetBaseException().Message}";
                }
            },
            new McpServerToolCreateOptions
            {
                Name = queryName,
                Description = description,
            });
    }

    private static McpServerTool BuildMutationToolGeneric<TInput>(
        IServiceProvider rootServices,
        string slug,
        string mutationName,
        string toolName,
        string description)
        where TInput : class
    {
        return McpServerTool.Create(
            async (
                string entityId,
                TInput input,
                CancellationToken ct) =>
            {
                try
                {
                    await using var scope = rootServices.CreateAsyncScope();
                    var dispatcher = scope.ServiceProvider.GetRequiredService<IMutationDispatcher>();
                    var result = await dispatcher.InvokeAsync(
                        slug, mutationName, Guid.Parse(entityId), input, ct);

                    if (!result.Authorized)
                    {
                        throw new UnauthorizedAccessException(result.Error ?? "Forbidden");
                    }

                    return JsonSerializer.Serialize(result.Output);
                }
                catch (Exception ex) when (ex is not UnauthorizedAccessException)
                {
                    return $"ERROR in {mutationName}: {ex.GetBaseException().Message}";
                }
            },
            new McpServerToolCreateOptions
            {
                Name = toolName,
                Description = description,
            });
    }
}
