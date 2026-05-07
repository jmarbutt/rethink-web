using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using RethinkWeb.Actions;
using RethinkWeb.Metadata;

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
        IEntityRegistry entities)
    {
        foreach (var action in actions.All)
        {
            if (!action.ExposeToMcp) continue;

            var entity = entities.Get(action.EntityType);
            var tool = BuildToolForAction(rootServices, entity.Slug, action);
            Tools.Add(tool);
        }
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
}
