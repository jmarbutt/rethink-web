namespace RethinkWeb.Lifecycle;

public sealed class InMemoryLifecycleStore : ILifecycleSink, ILifecycleReader
{
    private readonly Lock _gate = new();
    private readonly List<LifecycleFact> _facts = [];

    public Task RecordAsync(LifecycleFact fact, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _facts.Add(fact);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<LifecycleFact>> ListAsync(
        LifecycleFactQuery? query = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        query ??= new LifecycleFactQuery();

        List<LifecycleFact> snapshot;
        lock (_gate)
        {
            snapshot = [.. _facts];
        }

        IEnumerable<LifecycleFact> facts = snapshot;
        if (query.TenantId is not null)
        {
            facts = facts.Where(f => f.TenantId == query.TenantId);
        }
        if (query.ActorId is not null)
        {
            facts = facts.Where(f => f.ActorId == query.ActorId);
        }
        if (query.CorrelationId is not null)
        {
            facts = facts.Where(f => f.CorrelationId == query.CorrelationId);
        }
        if (query.Kind is not null)
        {
            facts = facts.Where(f => f.Kind == query.Kind);
        }
        if (query.Status is not null)
        {
            facts = facts.Where(f => f.Status == query.Status);
        }
        if (query.OperationName is not null)
        {
            facts = facts.Where(f => f.OperationName == query.OperationName);
        }
        if (query.EntityType is not null)
        {
            facts = facts.Where(f => f.EntityType == query.EntityType);
        }
        if (query.EntityId is not null)
        {
            facts = facts.Where(f => f.EntityId == query.EntityId);
        }
        if (query.StartedAfter is not null)
        {
            facts = facts.Where(f => f.StartedAt >= query.StartedAfter);
        }
        if (query.StartedBefore is not null)
        {
            facts = facts.Where(f => f.StartedAt <= query.StartedBefore);
        }

        facts = facts
            .OrderBy(f => f.StartedAt)
            .ThenBy(f => f.Id);

        if (query.Limit is > 0)
        {
            facts = facts.Take(query.Limit.Value);
        }

        return Task.FromResult<IReadOnlyList<LifecycleFact>>(facts.ToList());
    }
}
