namespace RethinkWeb.Lifecycle;

public interface ILifecycleSink
{
    Task RecordAsync(LifecycleFact fact, CancellationToken ct = default);
}

public interface ILifecycleReader
{
    Task<IReadOnlyList<LifecycleFact>> ListAsync(
        LifecycleFactQuery? query = null,
        CancellationToken ct = default);
}
