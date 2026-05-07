namespace RethinkWeb.Queries;

public enum QueryCacheMode
{
    None,
    PerTenant,
    PerUser,
}

public sealed record QueryCachePolicy(
    QueryCacheMode Mode,
    TimeSpan? Duration,
    IReadOnlyList<string> Dependencies)
{
    public static QueryCachePolicy None { get; } =
        new(QueryCacheMode.None, null, []);
}

public sealed record QueryCacheKey(
    string QueryName,
    string? TenantId,
    string? UserId,
    string InputJson);

public interface IQueryCache
{
    Task<QueryCacheResult<T>> TryGetAsync<T>(QueryCacheKey key, CancellationToken ct = default);
    Task SetAsync<T>(QueryCacheKey key, T value, QueryCachePolicy policy, CancellationToken ct = default);
    Task InvalidateAsync(string dependency, CancellationToken ct = default);
}

public sealed record QueryCacheResult<T>(bool Hit, T? Value);

public sealed class NullQueryCache : IQueryCache
{
    public Task<QueryCacheResult<T>> TryGetAsync<T>(QueryCacheKey key, CancellationToken ct = default) =>
        Task.FromResult(new QueryCacheResult<T>(false, default));

    public Task SetAsync<T>(QueryCacheKey key, T value, QueryCachePolicy policy, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task InvalidateAsync(string dependency, CancellationToken ct = default) =>
        Task.CompletedTask;
}
