using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using RethinkWeb.Metadata;
using RethinkWeb.Render.Razor.Components;
using RethinkWeb.Rendering;

namespace RethinkWeb.Render.Razor;

/// <summary>
/// Default renderer. Uses Razor Components + Microsoft's HtmlRenderer to produce
/// HTML strings server-side. No SignalR, no WebSocket, no client-side Blazor —
/// pure server-rendered output we hand back as HTMX swap targets.
/// </summary>
public sealed class RazorEntityRenderer(HtmlRenderer renderer) : IEntityRenderer
{
    public Task<string> RenderGridAsync(
        EntityMetadata metadata,
        IReadOnlyList<object> entities,
        CancellationToken ct = default) =>
        RenderComponent<GridView>(new()
        {
            [nameof(GridView.Metadata)] = metadata,
            [nameof(GridView.Entities)] = entities,
        });

    public Task<string> RenderEditAsync(
        EntityMetadata metadata,
        object entity,
        CancellationToken ct = default) =>
        RenderComponent<EditView>(new()
        {
            [nameof(EditView.Metadata)] = metadata,
            [nameof(EditView.Entity)] = entity,
        });

    public Task<string> RenderLayoutAsync(
        string title,
        string contentHtml,
        CancellationToken ct = default) =>
        RenderComponent<Layout>(new()
        {
            [nameof(Layout.Title)] = title,
            [nameof(Layout.ContentHtml)] = contentHtml,
        });

    private Task<string> RenderComponent<TComponent>(Dictionary<string, object?> parameters)
        where TComponent : IComponent =>
        renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<TComponent>(
                ParameterView.FromDictionary(parameters));
            return output.ToHtmlString();
        });
}
