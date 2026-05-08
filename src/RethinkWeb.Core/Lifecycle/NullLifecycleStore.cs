namespace RethinkWeb.Lifecycle;

public sealed class NullLifecycleStore : ILifecycleSink, ILifecycleReader
{
    public Task RecordAsync(LifecycleFact fact, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<LifecycleFact>> ListAsync(
        LifecycleFactQuery? query = null,
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<LifecycleFact>>([]);
}
