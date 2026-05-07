# Multi-tenancy

Multi-tenancy is a **first-class but optional** primitive. By default the framework runs in single-tenant mode and every sample (`Notes`, `Tasks`, `Chat`) does today. Opt in via `services.AddRethinkWeb().UseMultiTenant<TResolver>()` and every entity registered after that point — when it implements `ITenantOwned` — becomes auto-scoped to the current request's tenant.

## What's foundational

Tenancy threads through three places in the framework:

1. **Entity store** — `TenantScopedEntityStore<T>` decorator wraps every store. On save: auto-stamps `TenantId` from `ITenantContext`. On read: filters out other tenants' rows. On cross-tenant write: throws `CrossTenantAccessException`.
2. **Auth** — entity-level `WritePermission` and per-field `EditPermission` are still consulted; tenancy is *additional* gating, not a replacement. A user inside tenant A who lacks `donor.edit` still can't edit donors.
3. **Cache keys / event log / queries** *(when those land in Phase 2/3)* — naturally include `TenantId` so cached data, lifecycle events, and reactive subscriptions don't bleed across tenants.

## The discriminator-column model

The shipped default is the simplest model: **single database, every tenant-scoped row carries a `TenantId` column**. Pros: cheapest onboarding (insert a row), one connection pool, one backup, one migration. Con: a bug in the filter is a data leak. Mitigated by *both* an in-memory decorator (`TenantScopedEntityStore`) AND EF Core `HasQueryFilter` (`ApplyTenantFilters` extension) — defense in depth.

For schema-per-tenant or DB-per-tenant, write a custom adapter (Phase 3 — not in the box).

## Opt in

### 1. Make entities `ITenantOwned`

```csharp
[Entity(slug: "todos", displayName: "Todos")]
public class Todo : ITenantOwned
{
    [Key] public Guid Id { get; set; }
    public string? TenantId { get; set; }   // framework auto-stamps on save

    [TextBox("Title", Required = true)]
    public string Title { get; set; } = "";
}
```

The `TenantId` is nullable — null means "global, visible across tenants." Most tenant-scoped rows will have it stamped on insert; deliberately-cross-tenant rows (e.g., shared system configuration) keep it null.

### 2. Register a resolver

```csharp
using RethinkWeb.Http.Tenancy;

builder.Services
    .AddHttpContextAccessor()           // required by HeaderTenantResolver
    .AddRethinkWeb()
    .UseMultiTenant<HeaderTenantResolver>()
    .AddEntity<Todo>()
    .UseEfCoreFor<Todo, TasksDb>();
```

`HeaderTenantResolver` reads `X-Tenant-Id` from the request. For production, write your own resolver that reads from authenticated claims, subdomain, or whatever source matches your tenancy story:

```csharp
public sealed class ClaimTenantResolver(IHttpContextAccessor accessor) : ITenantResolver
{
    public Task<string?> ResolveAsync(CancellationToken ct = default)
    {
        var tenant = accessor.HttpContext?.User.FindFirst("tenant")?.Value;
        return Task.FromResult(string.IsNullOrEmpty(tenant) ? null : tenant);
    }
}
```

### 3. Mount the middleware

```csharp
var app = builder.Build();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRethinkWebTenancy();   // <-- runs ITenantResolver, populates ScopedTenantContext
app.MapRethinkWeb();
app.MapMcp("/mcp");
```

Order matters: tenancy middleware runs *after* auth (so claims are available) and *before* the entity routes (so handlers see the resolved tenant).

### 4. Push the filter to SQL (EF Core only)

For EF Core stores, also call `ApplyTenantFilters` in your `DbContext.OnModelCreating` so the SQL `WHERE` clause filters by tenant on the database side, not in memory after loading every row:

```csharp
public sealed class TasksDb(DbContextOptions<TasksDb> options, ITenantContext tenant)
    : DbContext(options)
{
    public DbSet<Todo> Todos => Set<Todo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyTenantFilters(tenant);
    }
}
```

Both the SQL filter and the in-memory decorator stay in place. Defense in depth — if someone uses raw SQL or `IgnoreQueryFilters()`, the in-memory decorator still hides cross-tenant rows.

## Behavior in single-tenant mode

If you *don't* call `UseMultiTenant<TResolver>()`:
- `ITenantContext` resolves to `SingleTenantContext` (always returns null)
- `TenantScopedEntityStore<T>` decorator is **not** layered onto stores
- `ITenantOwned` entities work fine; `TenantId` just stays null
- No middleware ceremony required

You pay nothing for tenancy you don't use.

## Behavior in multi-tenant mode without a resolved tenant

If `UseMultiTenant` is configured but the resolver returns null for a given request (e.g., missing header, anonymous endpoint):
- `ITenantContext.TenantId` is null for that request
- `TenantScopedEntityStore` falls back to pass-through (decorators stay no-op when `TenantId` is null)
- This means anonymous routes can read across tenants — be careful with public endpoints when tenancy is enabled

The right move for production: make your resolver throw or return a sentinel value (`"public"`) so the framework can enforce a "no tenant = no read" policy.

## What doesn't change

- `IAuthContext` and permissions — orthogonal to tenancy. A user in tenant A still needs `donor.edit` to edit a donor.
- Action dispatcher — actions run inside the request scope, so they automatically pick up the resolved tenant.
- MCP tool calls — they enter through the same dispatcher; if a request resolves a tenant, MCP tool calls in that request are tenant-scoped.

## Tests

`tests/RethinkWeb.Core.Tests/TenancyTests.cs` covers:

- Auto-stamp `TenantId` on insert
- Cross-tenant write throws `CrossTenantAccessException`
- Cross-tenant read returns null
- List filters by current tenant
- Single-tenant mode is pass-through (no stamping)
- Non-`ITenantOwned` entities are unaffected
- Cross-tenant delete throws

Pattern for unit-testing your own tenant-scoped logic:

```csharp
[Fact]
public async Task Action_persists_in_correct_tenant()
{
    var inner = new InMemoryEntityStore<Todo>();
    var tenant = new FixedTenant("acme");
    var store = new TenantScopedEntityStore<Todo>(inner, tenant);

    var action = new MarkCompleteAction(store);
    await action.ExecuteAsync(...);

    var saved = (await store.ListAsync()).Single();
    saved.TenantId.Should().Be("acme");
}
```

## What's not solved yet

- **Tenant-aware MCP authentication.** Today an MCP client could send a `X-Tenant-Id` header to switch tenants per call. Production needs the MCP transport to derive tenant from authenticated identity — not in the box.
- **Tenant-onboarding/bootstrapping flows.** Creating a tenant, seeding initial data, soft-deleting a tenant — your concern, not the framework's.
- **Per-tenant feature flags or schema variants.** Phase 2+ if needed.
- **Tenant-aware Inspector dashboard.** When the Framework Inspector lands (Phase 2), it'll need a tenant filter.
