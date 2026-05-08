namespace RethinkWeb.Lifecycle;

public enum LifecycleFactKind
{
    Query,
    Mutation,
    Action,
    Save,
    Event,
    Subscriber,
    WorkflowStep,
}

public enum LifecycleFactStatus
{
    Started,
    Completed,
    Failed,
    Denied,
}

public sealed record LifecycleFact(
    Guid Id,
    LifecycleFactKind Kind,
    LifecycleFactStatus Status,
    string OperationName,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt = null,
    string? TenantId = null,
    string? ActorId = null,
    Guid? CorrelationId = null,
    string? EntityType = null,
    Guid? EntityId = null,
    string? Summary = null,
    string? Error = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record LifecycleFactQuery(
    string? TenantId = null,
    string? ActorId = null,
    Guid? CorrelationId = null,
    LifecycleFactKind? Kind = null,
    LifecycleFactStatus? Status = null,
    string? OperationName = null,
    string? EntityType = null,
    Guid? EntityId = null,
    DateTimeOffset? StartedAfter = null,
    DateTimeOffset? StartedBefore = null,
    int? Limit = null);
