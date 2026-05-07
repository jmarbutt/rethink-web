using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using RethinkWeb.Actions;
using RethinkWeb.Auth;
using RethinkWeb.Manifest;
using RethinkWeb.Metadata;
using RethinkWeb.Mutations;
using RethinkWeb.Queries;
using RethinkWeb.Rendering;
using RethinkWeb.Storage;

namespace RethinkWeb.Http;

public static class EndpointRouteExtensions
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Maps the framework's HTTP surface: per-entity grid + edit + save endpoints,
    /// plus the manifest endpoint. HTMX-aware: returns fragments to HX-Request,
    /// full pages otherwise.
    /// </summary>
    public static IEndpointRouteBuilder MapRethinkWeb(this IEndpointRouteBuilder routes)
    {
        var entities = routes.ServiceProvider.GetRequiredService<IEntityRegistry>();

        routes.MapGet("/", async (IEntityRegistry reg, IEntityRenderer renderer) =>
        {
            var html = "<h1>RethinkWeb</h1><ul>"
                + string.Concat(reg.All.Select(e =>
                    $"<li><a href=\"/{e.Slug}\">{e.DisplayName}</a></li>"))
                + "</ul>";
            var page = await renderer.RenderLayoutAsync("RethinkWeb", html);
            return Results.Content(page, "text/html");
        });

        routes.MapGet("/_framework/manifest", (IManifestBuilder builder) =>
        {
            var manifest = builder.Build();
            return Results.Json(manifest, JsonOpts);
        });

        routes.MapPost("/_framework/queries/{queryName}", async (
            HttpContext ctx,
            string queryName,
            IQueryRegistry queries,
            IQueryDispatcher dispatcher) =>
        {
            var descriptor = queries.Find(queryName);
            if (descriptor is null) return Results.NotFound();

            var input = await ReadInput(ctx, descriptor.InputType);
            var result = await dispatcher.InvokeAsync(queryName, input, ctx.RequestAborted);
            if (!result.Authorized)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            return Results.Json(result.Output, JsonOpts);
        });

        foreach (var entity in entities.All)
        {
            MapEntityEndpoints(routes, entity);
        }

        return routes;
    }

    private static void MapEntityEndpoints(IEndpointRouteBuilder routes, EntityMetadata entity)
    {
        var slug = entity.Slug;

        routes.MapGet($"/{slug}", async (
            HttpContext ctx,
            IServiceProvider sp,
            IAuthContext auth,
            IEntityRenderer renderer) =>
        {
            if (entity.ReadPermission is not null && !auth.HasPermission(entity.ReadPermission))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var list = await ListEntities(sp, entity.ClrType);
            var grid = await renderer.RenderGridAsync(entity, list);
            return await Wrap(ctx, renderer, $"{entity.DisplayName} list", grid);
        });

        routes.MapGet($"/{slug}/{{id:guid}}", async (
            HttpContext ctx,
            Guid id,
            IServiceProvider sp,
            IAuthContext auth,
            IEntityRenderer renderer) =>
        {
            if (entity.ReadPermission is not null && !auth.HasPermission(entity.ReadPermission))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var loaded = await GetEntity(sp, entity.ClrType, id);
            if (loaded is null) return Results.NotFound();
            var form = await renderer.RenderEditAsync(entity, loaded);
            return await Wrap(ctx, renderer, $"Edit {entity.DisplayName}", form);
        });

        routes.MapPost($"/{slug}/{{id:guid}}", async (
            HttpContext ctx,
            Guid id,
            IServiceProvider sp,
            IAuthContext auth,
            IEntityRenderer renderer) =>
        {
            if (entity.WritePermission is not null && !auth.HasPermission(entity.WritePermission))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var loaded = await GetEntity(sp, entity.ClrType, id);
            if (loaded is null) return Results.NotFound();

            var form = await ctx.Request.ReadFormAsync();
            var errors = FormBinder.BindToEntity(form, entity, loaded, auth);
            if (errors.Count > 0)
            {
                return Results.UnprocessableEntity(string.Join("\n", errors));
            }
            // PublishingEntityStore decorator publishes EntitySaved<T> from inside Save.
            await SaveEntity(sp, entity.ClrType, loaded);

            // Re-load: subscribers may have mutated and re-saved the entity.
            loaded = (await GetEntity(sp, entity.ClrType, id))!;

            var html = await renderer.RenderEditAsync(entity, loaded);
            // HTMX: return only the fragment so it swaps in place. Non-HTMX: wrap in layout.
            return await Wrap(ctx, renderer, $"Edit {entity.DisplayName}", html);
        });

        routes.MapPost($"/{slug}/{{id:guid}}/actions/{{actionName}}", async (
            HttpContext ctx,
            Guid id,
            string actionName,
            IServiceProvider sp,
            IActionRegistry actions,
            IActionDispatcher dispatcher,
            IAuthContext auth,
            IEntityRenderer renderer) =>
        {
            // Entity write permission gates actions too — you can't act on what you can't write.
            // Per-action Permission is checked separately by ActionDispatcher.
            if (entity.WritePermission is not null && !auth.HasPermission(entity.WritePermission))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var descriptor = actions.Find(entity.ClrType, actionName);
            if (descriptor is null) return Results.NotFound();

            var input = await ReadInput(ctx, descriptor.InputType);
            var result = await dispatcher.InvokeAsync(slug, actionName, id, input, ctx.RequestAborted);

            if (!result.Authorized)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            // Action saved (or didn't); re-render the edit form so HTMX can swap.
            var loaded = await GetEntity(sp, entity.ClrType, id);
            if (loaded is null) return Results.NotFound();
            var html = await renderer.RenderEditAsync(entity, loaded);
            return await Wrap(ctx, renderer, $"Edit {entity.DisplayName}", html);
        });

        routes.MapPost($"/{slug}/{{id:guid}}/mutations/{{mutationName}}", async (
            HttpContext ctx,
            Guid id,
            string mutationName,
            IServiceProvider sp,
            IMutationRegistry mutations,
            IMutationDispatcher dispatcher,
            IEntityRenderer renderer) =>
        {
            var descriptor = mutations.Find(entity.ClrType, mutationName);
            if (descriptor is null) return Results.NotFound();

            var input = await ReadInput(ctx, descriptor.InputType);
            var result = await dispatcher.InvokeAsync(slug, mutationName, id, input, ctx.RequestAborted);

            if (!result.Authorized)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var loaded = await GetEntity(sp, entity.ClrType, id);
            if (loaded is null) return Results.NotFound();
            var html = await renderer.RenderEditAsync(entity, loaded);
            return await Wrap(ctx, renderer, $"Edit {entity.DisplayName}", html);
        });
    }

    private static async Task<object> ReadInput(HttpContext ctx, Type inputType)
    {
        var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        if (ctx.Request.ContentLength == 0)
        {
            return Activator.CreateInstance(inputType)
                ?? throw new InvalidOperationException($"Could not create empty {inputType.Name} input.");
        }

        // Prefer JSON body if present; fall back to form-encoded for HTMX submits.
        if (ctx.Request.HasJsonContentType())
        {
            return await ctx.Request.ReadFromJsonAsync(inputType, jsonOpts)
                ?? throw new InvalidOperationException("Empty JSON body for action input.");
        }

        var form = await ctx.Request.ReadFormAsync();
        var dict = form.ToDictionary(kv => kv.Key, kv => (object?)kv.Value.ToString());
        var json = JsonSerializer.Serialize(dict);
        return JsonSerializer.Deserialize(json, inputType, jsonOpts)
            ?? throw new InvalidOperationException($"Could not bind form to {inputType.Name}.");
    }

    private static async Task<IResult> Wrap(
        HttpContext ctx,
        IEntityRenderer renderer,
        string title,
        string html)
    {
        if (ctx.Request.Headers.ContainsKey("HX-Request"))
        {
            return Results.Content(html, "text/html");
        }

        var page = await renderer.RenderLayoutAsync(title, html);
        return Results.Content(page, "text/html");
    }

    private static async Task<IReadOnlyList<object>> ListEntities(IServiceProvider sp, Type entityType)
    {
        var storeType = typeof(IEntityStore<>).MakeGenericType(entityType);
        var store = sp.GetRequiredService(storeType);
        var listMethod = storeType.GetMethod(nameof(IEntityStore<object>.ListAsync))!;
        var task = (Task)listMethod.Invoke(store, [CancellationToken.None])!;
        await task;
        var result = task.GetType().GetProperty("Result")!.GetValue(task);
        return ((System.Collections.IEnumerable)result!).Cast<object>().ToList();
    }

    private static async Task<object?> GetEntity(IServiceProvider sp, Type entityType, Guid id)
    {
        var storeType = typeof(IEntityStore<>).MakeGenericType(entityType);
        var store = sp.GetRequiredService(storeType);
        var getMethod = storeType.GetMethod(nameof(IEntityStore<object>.GetAsync))!;
        var task = (Task)getMethod.Invoke(store, [id, CancellationToken.None])!;
        await task;
        return task.GetType().GetProperty("Result")!.GetValue(task);
    }

    private static async Task SaveEntity(IServiceProvider sp, Type entityType, object entity)
    {
        var storeType = typeof(IEntityStore<>).MakeGenericType(entityType);
        var store = sp.GetRequiredService(storeType);
        var saveMethod = storeType.GetMethod(nameof(IEntityStore<object>.SaveAsync))!;
        var task = (Task)saveMethod.Invoke(store, [entity, CancellationToken.None])!;
        await task;
    }

}
