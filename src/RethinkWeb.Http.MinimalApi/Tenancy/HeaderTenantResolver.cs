using Microsoft.AspNetCore.Http;
using RethinkWeb.Tenancy;

namespace RethinkWeb.Http.Tenancy;

/// <summary>
/// Default <see cref="ITenantResolver"/> for HTTP hosts: reads the tenant id from
/// a configurable request header (default: <c>X-Tenant-Id</c>).
///
/// Trivial resolver suitable for backend services and integration tests. For
/// public-facing apps, prefer a resolver that derives tenant from authenticated
/// claims or subdomain — header-based resolution trusts the client.
/// </summary>
public sealed class HeaderTenantResolver(IHttpContextAccessor accessor) : ITenantResolver
{
    public const string DefaultHeaderName = "X-Tenant-Id";

    public Task<string?> ResolveAsync(CancellationToken ct = default)
    {
        var ctx = accessor.HttpContext;
        if (ctx is null) return Task.FromResult<string?>(null);
        var value = ctx.Request.Headers[DefaultHeaderName].ToString();
        return Task.FromResult<string?>(string.IsNullOrEmpty(value) ? null : value);
    }
}
