using Microsoft.Extensions.DependencyInjection;
using RethinkWeb.Auth;
using RethinkWeb.Queries;
using RethinkWeb.Tenancy;

namespace RethinkWeb.Core.Tests;

public class QueryDispatcherTests
{
    public sealed record CachedInput(string Filter);
    public sealed record CachedOutput(int Count);

    public sealed class Counter
    {
        public int Value { get; private set; }
        public int Next() => ++Value;
    }

    [Query(
        name: "widgets.cached",
        displayName: "Cached Widgets",
        Cache = QueryCacheMode.PerTenant,
        CacheSeconds = 60,
        DependsOn = ["widgets"])]
    public sealed class CachedQuery(Counter counter) : IQuery<CachedInput, CachedOutput>
    {
        public Task<CachedOutput> ExecuteAsync(CachedInput input, IQueryContext context, CancellationToken ct)
            => Task.FromResult(new CachedOutput(counter.Next()));
    }

    [Fact]
    public async Task QueryDispatcher_uses_query_cache_when_policy_is_enabled()
    {
        var queries = new QueryRegistry();
        queries.Register(typeof(CachedQuery));
        var cache = new RecordingQueryCache();
        var counter = new Counter();
        var services = new ServiceCollection()
            .AddSingleton(counter)
            .BuildServiceProvider();

        var dispatcher = new QueryDispatcher(
            services,
            queries,
            new AllowAllAuthContext(),
            new FixedTenant("tenant-a"),
            new FakeClock(DateTimeOffset.Parse("2026-05-07T10:00:00Z")),
            cache);

        var first = await dispatcher.InvokeAsync("widgets.cached", new CachedInput("open"));
        var second = await dispatcher.InvokeAsync("widgets.cached", new CachedInput("open"));

        first.CacheHit.Should().BeFalse();
        second.CacheHit.Should().BeTrue();
        first.Output.Should().BeEquivalentTo(new CachedOutput(1));
        second.Output.Should().BeEquivalentTo(new CachedOutput(1));
        counter.Value.Should().Be(1, "the second invocation should come from IQueryCache");
        cache.LastSetPolicy.Should().NotBeNull();
        cache.LastSetPolicy!.Dependencies.Should().Contain("widgets");
        cache.LastKey.Should().NotBeNull();
        cache.LastKey!.TenantId.Should().Be("tenant-a");
    }

    private sealed class FixedTenant(string tenantId) : ITenantContext
    {
        public string? TenantId { get; } = tenantId;
    }

    private sealed class RecordingQueryCache : IQueryCache
    {
        private readonly Dictionary<QueryCacheKey, object> _values = [];

        public QueryCacheKey? LastKey { get; private set; }
        public QueryCachePolicy? LastSetPolicy { get; private set; }

        public Task<QueryCacheResult<T>> TryGetAsync<T>(QueryCacheKey key, CancellationToken ct = default)
        {
            LastKey = key;
            return Task.FromResult(_values.TryGetValue(key, out var value)
                ? new QueryCacheResult<T>(true, (T)value)
                : new QueryCacheResult<T>(false, default));
        }

        public Task SetAsync<T>(QueryCacheKey key, T value, QueryCachePolicy policy, CancellationToken ct = default)
        {
            LastKey = key;
            LastSetPolicy = policy;
            _values[key] = value!;
            return Task.CompletedTask;
        }

        public Task InvalidateAsync(string dependency, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
