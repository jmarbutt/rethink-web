using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using RethinkWeb.Events;
using RethinkWeb.Manifest;
using RethinkWeb.Metadata;
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

        foreach (var entity in entities.All)
        {
            MapEntityEndpoints(routes, entity);
        }

        return routes;
    }

    private static void MapEntityEndpoints(IEndpointRouteBuilder routes, EntityMetadata entity)
    {
        var slug = entity.Slug;

        routes.MapGet($"/{slug}", async (HttpContext ctx, IServiceProvider sp, IEntityRenderer renderer) =>
        {
            var list = await ListEntities(sp, entity.ClrType);
            var grid = await renderer.RenderGridAsync(entity, list);
            return await Wrap(ctx, renderer, $"{entity.DisplayName} list", grid);
        });

        routes.MapGet($"/{slug}/{{id:guid}}", async (
            HttpContext ctx,
            Guid id,
            IServiceProvider sp,
            IEntityRenderer renderer) =>
        {
            var loaded = await GetEntity(sp, entity.ClrType, id);
            if (loaded is null) return Results.NotFound();
            var form = await renderer.RenderEditAsync(entity, loaded);
            return await Wrap(ctx, renderer, $"Edit {entity.DisplayName}", form);
        });

        routes.MapPost($"/{slug}/{{id:guid}}", async (
            HttpContext ctx,
            Guid id,
            IServiceProvider sp,
            IEntityRenderer renderer) =>
        {
            var loaded = await GetEntity(sp, entity.ClrType, id);
            if (loaded is null) return Results.NotFound();

            var form = await ctx.Request.ReadFormAsync();
            FormBinder.BindToEntity(form, entity, loaded);
            await SaveEntity(sp, entity.ClrType, loaded);
            await PublishEntitySaved(sp, entity.ClrType, loaded, ctx.RequestAborted);

            // Re-load: subscribers may have mutated and re-saved the entity.
            loaded = (await GetEntity(sp, entity.ClrType, id))!;

            var html = await renderer.RenderEditAsync(entity, loaded);
            // HTMX: return only the fragment so it swaps in place. Non-HTMX: wrap in layout.
            return await Wrap(ctx, renderer, $"Edit {entity.DisplayName}", html);
        });
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

    private static async Task PublishEntitySaved(IServiceProvider sp, Type entityType, object entity, CancellationToken ct)
    {
        var bus = sp.GetRequiredService<IEventBus>();
        var eventType = typeof(EntitySaved<>).MakeGenericType(entityType);
        var evt = Activator.CreateInstance(eventType, entity)!;
        var publish = typeof(IEventBus).GetMethod(nameof(IEventBus.PublishAsync))!.MakeGenericMethod(eventType);
        var task = (Task)publish.Invoke(bus, [evt, ct])!;
        await task;
    }
}
