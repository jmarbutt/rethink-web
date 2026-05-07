using RethinkWeb.Metadata;

namespace RethinkWeb.Rendering;

/// <summary>
/// Renders an entity (or list of entities) to HTML. Default implementation lives in
/// RethinkWeb.Render.Razor and uses Razor Components + HtmlRenderer. Swap freely.
/// </summary>
public interface IEntityRenderer
{
    Task<string> RenderGridAsync(
        EntityMetadata metadata,
        IReadOnlyList<object> entities,
        CancellationToken ct = default);

    Task<string> RenderEditAsync(
        EntityMetadata metadata,
        object entity,
        CancellationToken ct = default);

    /// <summary>Renders the surrounding HTML shell (head/body/htmx/alpine/main slot).</summary>
    Task<string> RenderLayoutAsync(
        string title,
        string contentHtml,
        CancellationToken ct = default);
}
