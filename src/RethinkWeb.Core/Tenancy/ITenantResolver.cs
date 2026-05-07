namespace RethinkWeb.Tenancy;

/// <summary>
/// Extracts the tenant id from per-request context. The default abstraction is
/// generic over the request type so non-HTTP hosts (Worker services, MCP-only
/// listeners, tests) can implement custom resolvers without depending on
/// <c>HttpContext</c>.
///
/// HTTP hosts use <c>IHttpTenantResolver</c> in <c>RethinkWeb.Http.MinimalApi</c>;
/// the middleware adapts that to this abstraction.
/// </summary>
public interface ITenantResolver
{
    Task<string?> ResolveAsync(CancellationToken ct = default);
}
