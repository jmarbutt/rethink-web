using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RethinkWeb.Tenancy;

namespace RethinkWeb.Http.Tenancy;

/// <summary>
/// Per-request middleware that resolves the current tenant via the registered
/// <see cref="ITenantResolver"/> and stores it in the scoped
/// <see cref="ScopedTenantContext"/>. Runs early in the pipeline so any
/// downstream handler/store decorator sees the resolved tenant.
///
/// Mount via <c>app.UseRethinkWebTenancy()</c> AFTER routing-aware middleware
/// but BEFORE entity endpoints.
/// </summary>
public static class TenancyMiddlewareExtensions
{
    public static IApplicationBuilder UseRethinkWebTenancy(this IApplicationBuilder app)
    {
        return app.Use(async (HttpContext ctx, RequestDelegate next) =>
        {
            var resolver = ctx.RequestServices.GetService<ITenantResolver>();
            var scoped = ctx.RequestServices.GetService<ScopedTenantContext>();
            if (resolver is not null && scoped is not null)
            {
                scoped.TenantId = await resolver.ResolveAsync(ctx.RequestAborted);
            }
            await next(ctx);
        });
    }
}
