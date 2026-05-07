using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using RethinkWeb.Actions;
using RethinkWeb.Manifest;
using RethinkWeb.Metadata;

namespace RethinkWeb.Mcp;

/// <summary>
/// Minimal MCP-style endpoint surface. Exposes the registered actions as tools.
///
/// PROTOTYPE: This is plain HTTP POST endpoints in MCP-shaped JSON, not the full
/// Streamable HTTP transport. Real Claude Desktop integration would need stdio
/// or SSE. The point here is to PROVE the unification — same action registry
/// surfaces as both HTTP form-post and MCP tool with one definition.
///
/// Tool naming: "{entitySlug}.{actionName}" (e.g. "donor.update-address").
/// Tool input includes a synthetic "entityId" property that gets routed to the
/// dispatcher; remaining fields go into the action's TInput.
/// </summary>
public static class McpEndpointExtensions
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static IEndpointRouteBuilder MapRethinkWebMcp(this IEndpointRouteBuilder routes, string prefix = "/mcp")
    {
        routes.MapGet(prefix + "/tools/list", (
            IManifestBuilder builder,
            IEntityRegistry entities) =>
        {
            var manifest = builder.Build();
            var tools = manifest.Entities
                .SelectMany(e => e.Actions
                    .Where(a => a.ExposeToMcp)
                    .Select(a => BuildToolDescription(e, a)))
                .ToList();
            return Results.Json(new { tools }, JsonOpts);
        });

        routes.MapPost(prefix + "/tools/call", async (
            HttpContext ctx,
            IActionDispatcher dispatcher,
            IEntityRegistry entities,
            IActionRegistry actions) =>
        {
            using var doc = await JsonDocument.ParseAsync(ctx.Request.Body);
            var root = doc.RootElement;
            var name = root.GetProperty("name").GetString()
                ?? throw new InvalidOperationException("'name' is required.");
            var arguments = root.TryGetProperty("arguments", out var args)
                ? args.Clone()
                : JsonDocument.Parse("{}").RootElement.Clone();

            var (slug, actionName) = ParseToolName(name);
            var entityMeta = entities.GetBySlug(slug)
                ?? throw new InvalidOperationException($"Unknown entity '{slug}'.");
            var descriptor = actions.Find(entityMeta.ClrType, actionName)
                ?? throw new InvalidOperationException($"Unknown action '{actionName}' on '{slug}'.");

            var entityId = arguments.TryGetProperty("entityId", out var idProp)
                ? Guid.Parse(idProp.GetString()!)
                : throw new InvalidOperationException("'entityId' is required in arguments.");

            // Strip entityId before deserializing the rest as TInput.
            var argsObj = JsonNode.Parse(arguments.GetRawText())!.AsObject();
            argsObj.Remove("entityId");
            var input = JsonSerializer.Deserialize(argsObj.ToJsonString(), descriptor.InputType, JsonOpts)
                ?? throw new InvalidOperationException("Failed to deserialize action input.");

            var result = await dispatcher.InvokeAsync(slug, actionName, entityId, input, ctx.RequestAborted);

            if (!result.Authorized)
            {
                return Results.Json(new { isError = true, content = new[] { new { type = "text", text = result.Error ?? "Forbidden" } } }, JsonOpts);
            }

            return Results.Json(new
            {
                content = new[]
                {
                    new { type = "text", text = JsonSerializer.Serialize(result.Output, JsonOpts) }
                }
            }, JsonOpts);
        });

        return routes;
    }

    private static object BuildToolDescription(ManifestEntity entity, ManifestAction action)
    {
        // Augment the input schema with a required 'entityId' field — the dispatcher needs
        // to know which entity instance the action targets. Keeps the tool surface simple.
        var props = new Dictionary<string, object>
        {
            ["entityId"] = new { type = "string", format = "uuid", description = $"Id of the {entity.Slug}." },
        };
        foreach (var (k, v) in action.InputSchema.Properties)
        {
            props[k] = new { type = v.Type, format = v.Format, description = v.Description };
        }

        var required = new List<string> { "entityId" };
        required.AddRange(action.InputSchema.Required);

        return new
        {
            name = $"{entity.Slug}.{action.Name}",
            description = action.Description ?? action.DisplayName,
            inputSchema = new
            {
                type = "object",
                properties = props,
                required,
            },
        };
    }

    private static (string Slug, string ActionName) ParseToolName(string name)
    {
        var dot = name.IndexOf('.');
        if (dot < 1 || dot == name.Length - 1)
        {
            throw new InvalidOperationException(
                $"Tool name '{name}' must be of form '{{entitySlug}}.{{actionName}}'.");
        }
        return (name[..dot], name[(dot + 1)..]);
    }
}
