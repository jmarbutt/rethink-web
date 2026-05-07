using RethinkWeb.Storage;
using RethinkWeb.Tenancy;

namespace RethinkWeb.Core.Tests;

public class TenancyTests
{
    public sealed class Widget : ITenantOwned
    {
        public Guid Id { get; set; }
        public string? TenantId { get; set; }
        public string Name { get; set; } = "";
    }

    public sealed class GlobalWidget   // does NOT implement ITenantOwned
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
    }

    private sealed class FixedTenant(string? tenantId) : ITenantContext
    {
        public string? TenantId { get; } = tenantId;
    }

    [Fact]
    public async Task SaveAsync_auto_stamps_TenantId_on_insert()
    {
        var inner = new InMemoryEntityStore<Widget>();
        var tenant = new FixedTenant("acme");
        var store = new TenantScopedEntityStore<Widget>(inner, tenant);

        var w = new Widget { Id = Guid.NewGuid(), Name = "test" };
        await store.SaveAsync(w);

        w.TenantId.Should().Be("acme", "framework should auto-stamp TenantId from the resolved tenant context");
    }

    [Fact]
    public async Task SaveAsync_throws_when_writing_to_a_different_tenant()
    {
        var inner = new InMemoryEntityStore<Widget>();
        var tenant = new FixedTenant("acme");
        var store = new TenantScopedEntityStore<Widget>(inner, tenant);

        var attacker = new Widget { Id = Guid.NewGuid(), TenantId = "other-tenant", Name = "evil" };

        var act = async () => await store.SaveAsync(attacker);

        await act.Should().ThrowAsync<CrossTenantAccessException>()
            .WithMessage("*tenant 'other-tenant' from tenant 'acme'*");
    }

    [Fact]
    public async Task GetAsync_returns_null_for_cross_tenant_entity()
    {
        var inner = new InMemoryEntityStore<Widget>();
        // Directly insert a row tagged for tenant B (bypasses the decorator)
        var w = new Widget { Id = Guid.NewGuid(), TenantId = "tenant-b", Name = "secret" };
        await inner.SaveAsync(w);

        var store = new TenantScopedEntityStore<Widget>(inner, new FixedTenant("tenant-a"));

        var loaded = await store.GetAsync(w.Id);
        loaded.Should().BeNull("the decorator must hide other tenants' rows even if they bypass it on insert");
    }

    [Fact]
    public async Task ListAsync_filters_to_current_tenant_only()
    {
        var inner = new InMemoryEntityStore<Widget>();
        await inner.SaveAsync(new Widget { Id = Guid.NewGuid(), TenantId = "a", Name = "a1" });
        await inner.SaveAsync(new Widget { Id = Guid.NewGuid(), TenantId = "a", Name = "a2" });
        await inner.SaveAsync(new Widget { Id = Guid.NewGuid(), TenantId = "b", Name = "b1" });

        var store = new TenantScopedEntityStore<Widget>(inner, new FixedTenant("a"));
        var results = await store.ListAsync();

        results.Should().HaveCount(2);
        results.Select(w => w.Name).Should().BeEquivalentTo(["a1", "a2"]);
    }

    [Fact]
    public async Task SingleTenant_mode_passes_everything_through_untouched()
    {
        var inner = new InMemoryEntityStore<Widget>();
        var store = new TenantScopedEntityStore<Widget>(inner, new SingleTenantContext());

        var w = new Widget { Id = Guid.NewGuid(), Name = "no tenant" };
        await store.SaveAsync(w);

        w.TenantId.Should().BeNull("single-tenant mode should not stamp anything");
        var loaded = await store.GetAsync(w.Id);
        loaded.Should().NotBeNull();
    }

    [Fact]
    public async Task Decorator_passes_through_for_non_tenant_owned_entity()
    {
        var inner = new InMemoryEntityStore<GlobalWidget>();
        var store = new TenantScopedEntityStore<GlobalWidget>(inner, new FixedTenant("a"));

        var g = new GlobalWidget { Id = Guid.NewGuid(), Name = "global" };
        await store.SaveAsync(g);
        var list = await store.ListAsync();

        list.Should().ContainSingle();
    }

    [Fact]
    public async Task DeleteAsync_throws_when_entity_belongs_to_other_tenant()
    {
        var inner = new InMemoryEntityStore<Widget>();
        var w = new Widget { Id = Guid.NewGuid(), TenantId = "tenant-b", Name = "their data" };
        await inner.SaveAsync(w);

        var store = new TenantScopedEntityStore<Widget>(inner, new FixedTenant("tenant-a"));
        var act = async () => await store.DeleteAsync(w.Id);

        await act.Should().ThrowAsync<CrossTenantAccessException>();
    }
}
