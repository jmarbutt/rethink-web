namespace RethinkWeb.Tenancy;

/// <summary>
/// Marker interface for entities that are tenant-scoped. Entities NOT implementing
/// this are treated as global (visible across tenants).
///
/// The framework's <c>TenantScopedEntityStore</c> decorator:
/// - Auto-stamps <see cref="TenantId"/> on save (insert) when the current
///   <see cref="ITenantContext"/> is set and the entity's TenantId is null.
/// - Filters Get/List by current tenant.
/// - Throws <see cref="CrossTenantAccessException"/> on attempts to save an
///   entity belonging to a different tenant than the current one.
///
/// Use a nullable string to allow deliberately-global entities (e.g.,
/// system configuration owned by no tenant).
/// </summary>
public interface ITenantOwned
{
    string? TenantId { get; set; }
}

public sealed class CrossTenantAccessException(string message) : InvalidOperationException(message);
