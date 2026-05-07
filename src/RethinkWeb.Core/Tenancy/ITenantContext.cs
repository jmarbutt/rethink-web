namespace RethinkWeb.Tenancy;

/// <summary>
/// Per-request tenant context. Set by an <see cref="ITenantResolver"/> via middleware.
///
/// In single-tenant mode (when the framework is NOT configured with
/// <c>UseMultiTenant&lt;TResolver&gt;()</c>), <see cref="TenantId"/> is always null
/// and the framework treats every entity as global. In multi-tenant mode, every
/// request resolves a tenant before any handler runs; entities that implement
/// <see cref="ITenantOwned"/> are auto-scoped to the current tenant.
///
/// Multi-tenancy is foundational but optional — opting out is the default.
/// </summary>
public interface ITenantContext
{
    string? TenantId { get; }
}

/// <summary>
/// The default — no tenant. Framework runs in single-tenant mode.
/// </summary>
public sealed class SingleTenantContext : ITenantContext
{
    public string? TenantId => null;
}

/// <summary>
/// Mutable tenant context populated by middleware on each request. Registered
/// as scoped when <c>UseMultiTenant&lt;TResolver&gt;()</c> is called.
/// </summary>
public sealed class ScopedTenantContext : ITenantContext
{
    public string? TenantId { get; set; }
}
