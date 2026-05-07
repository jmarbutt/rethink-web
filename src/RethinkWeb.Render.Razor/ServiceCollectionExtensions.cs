using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RethinkWeb.Rendering;

namespace RethinkWeb.Render.Razor;

public static class ServiceCollectionExtensions
{
    public static RethinkWebBuilder UseRazorRenderer(this RethinkWebBuilder builder)
    {
        // Host (ASP.NET Core) is expected to have already registered logging.
        builder.Services.TryAddScoped<HtmlRenderer>();
        builder.Services.TryAddScoped<IEntityRenderer, RazorEntityRenderer>();
        return builder;
    }
}
