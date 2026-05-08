using Microsoft.Extensions.DependencyInjection;
using RethinkWeb;
using RethinkWeb.Lifecycle;

namespace RethinkWeb.Core.Tests;

public class LifecycleStoreTests
{
    [Fact]
    public async Task InMemoryLifecycleStore_records_and_reads_facts_in_chronological_order()
    {
        var store = new InMemoryLifecycleStore();

        var later = Fact(
            id: Guid.Parse("00000000-0000-0000-0000-000000000002"),
            kind: LifecycleFactKind.Action,
            operationName: "tasks.mark-complete",
            startedAt: DateTimeOffset.Parse("2026-05-07T12:01:00Z"));
        var earlier = Fact(
            id: Guid.Parse("00000000-0000-0000-0000-000000000001"),
            kind: LifecycleFactKind.Save,
            operationName: "tasks.save",
            startedAt: DateTimeOffset.Parse("2026-05-07T12:00:00Z"));

        await store.RecordAsync(later);
        await store.RecordAsync(earlier);

        var facts = await store.ListAsync();

        facts.Select(f => f.Id).Should().Equal(earlier.Id, later.Id);
    }

    [Fact]
    public async Task InMemoryLifecycleStore_filters_by_tenant_entity_correlation_and_kind()
    {
        var store = new InMemoryLifecycleStore();
        var correlationId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var entityId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        await store.RecordAsync(Fact(
            id: Guid.Parse("00000000-0000-0000-0000-000000000001"),
            kind: LifecycleFactKind.Action,
            operationName: "tasks.mark-complete",
            tenantId: "tenant-a",
            entityType: "Todo",
            entityId: entityId,
            correlationId: correlationId));
        await store.RecordAsync(Fact(
            id: Guid.Parse("00000000-0000-0000-0000-000000000002"),
            kind: LifecycleFactKind.Action,
            operationName: "tasks.mark-complete",
            tenantId: "tenant-b",
            entityType: "Todo",
            entityId: entityId,
            correlationId: correlationId));
        await store.RecordAsync(Fact(
            id: Guid.Parse("00000000-0000-0000-0000-000000000003"),
            kind: LifecycleFactKind.Event,
            operationName: "EntitySaved<Todo>",
            tenantId: "tenant-a",
            entityType: "Todo",
            entityId: entityId,
            correlationId: correlationId));

        var facts = await store.ListAsync(new LifecycleFactQuery(
            TenantId: "tenant-a",
            EntityType: "Todo",
            EntityId: entityId,
            CorrelationId: correlationId,
            Kind: LifecycleFactKind.Action));

        facts.Should().ContainSingle();
        facts[0].Id.Should().Be(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    }

    [Fact]
    public async Task Default_lifecycle_registrations_do_not_persist_facts()
    {
        var services = new ServiceCollection()
            .AddRethinkWeb()
            .Services
            .BuildServiceProvider();

        var sink = services.GetRequiredService<ILifecycleSink>();
        var reader = services.GetRequiredService<ILifecycleReader>();

        await sink.RecordAsync(Fact(
            id: Guid.Parse("00000000-0000-0000-0000-000000000001"),
            kind: LifecycleFactKind.Action,
            operationName: "tasks.mark-complete"));

        var facts = await reader.ListAsync();

        facts.Should().BeEmpty("the default lifecycle store should preserve current no-persistence behavior");
    }

    private static LifecycleFact Fact(
        Guid id,
        LifecycleFactKind kind,
        string operationName,
        DateTimeOffset? startedAt = null,
        string? tenantId = null,
        string? actorId = null,
        Guid? correlationId = null,
        string? entityType = null,
        Guid? entityId = null) =>
        new(
            Id: id,
            Kind: kind,
            Status: LifecycleFactStatus.Completed,
            OperationName: operationName,
            StartedAt: startedAt ?? DateTimeOffset.Parse("2026-05-07T12:00:00Z"),
            CompletedAt: startedAt ?? DateTimeOffset.Parse("2026-05-07T12:00:00Z"),
            TenantId: tenantId,
            ActorId: actorId,
            CorrelationId: correlationId,
            EntityType: entityType,
            EntityId: entityId,
            Summary: null,
            Error: null,
            Metadata: new Dictionary<string, string> { ["source"] = "test" });
}
