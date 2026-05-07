using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RethinkWeb.Tenancy;

namespace RethinkWeb.Store.EfCore;

/// <summary>
/// EF Core helper that adds a tenant <c>HasQueryFilter</c> to every entity in
/// the model that implements <see cref="ITenantOwned"/>. Pushes tenant filtering
/// down to SQL <c>WHERE</c> clauses instead of relying on the in-memory filter
/// in <c>TenantScopedEntityStore</c>.
///
/// Call this from your DbContext's <c>OnModelCreating</c>:
///
/// <code>
/// public class TasksDb(DbContextOptions&lt;TasksDb&gt; options, ITenantContext tenant)
///     : DbContext(options)
/// {
///     public DbSet&lt;Todo&gt; Todos => Set&lt;Todo&gt;();
///
///     protected override void OnModelCreating(ModelBuilder modelBuilder)
///     {
///         modelBuilder.ApplyTenantFilters(tenant);
///     }
/// }
/// </code>
///
/// Both layers are intentional belt-and-suspenders: the SQL filter is the fast
/// path; the in-memory decorator is a defense-in-depth against query bypasses
/// (raw SQL, IgnoreQueryFilters, ReadAllRows).
/// </summary>
public static class TenantFilterExtensions
{
    public static ModelBuilder ApplyTenantFilters(
        this ModelBuilder modelBuilder,
        ITenantContext tenant)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            if (!typeof(ITenantOwned).IsAssignableFrom(clrType)) continue;

            // Build: e => e.TenantId == null || e.TenantId == tenant.TenantId
            // The "TenantId == null" branch lets deliberately-global rows be visible
            // even in tenanted requests. Drop that disjunct for stricter isolation.
            var parameter = Expression.Parameter(clrType, "e");
            var tenantIdProp = Expression.Property(parameter, nameof(ITenantOwned.TenantId));

            // tenant.TenantId is captured by closure; EF Core re-reads it per query
            // because it's accessed off the live ITenantContext instance.
            var currentTenant = Expression.Property(
                Expression.Constant(tenant),
                nameof(ITenantContext.TenantId));

            var filter = Expression.Lambda(
                Expression.OrElse(
                    Expression.Equal(tenantIdProp, Expression.Constant(null, typeof(string))),
                    Expression.Equal(tenantIdProp, currentTenant)),
                parameter);

            entityType.SetQueryFilter(filter);
        }
        return modelBuilder;
    }
}
