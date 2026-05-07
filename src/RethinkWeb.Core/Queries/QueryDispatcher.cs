using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RethinkWeb.Auth;
using RethinkWeb.Tenancy;

namespace RethinkWeb.Queries;

public interface IQueryDispatcher
{
    Task<QueryResult> InvokeAsync(
        string queryName,
        object input,
        CancellationToken ct = default);
}

public sealed record QueryResult(bool Authorized, object? Output, string? Error, bool CacheHit = false);

public sealed class QueryDispatcher(
    IServiceProvider services,
    IQueryRegistry queries,
    IAuthContext auth,
    ITenantContext tenant,
    IClock clock,
    IQueryCache cache) : IQueryDispatcher
{
    private static readonly JsonSerializerOptions CacheJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<QueryResult> InvokeAsync(
        string queryName,
        object input,
        CancellationToken ct = default)
    {
        var descriptor = queries.Find(queryName)
            ?? throw new InvalidOperationException($"Unknown query '{queryName}'.");

        if (descriptor.Permission is not null && !auth.HasPermission(descriptor.Permission))
        {
            return new QueryResult(Authorized: false, Output: null, Error: "Forbidden");
        }

        input = CoerceInput(input, descriptor.InputType);

        if (descriptor.CachePolicy.Mode != QueryCacheMode.None)
        {
            var key = BuildCacheKey(descriptor, input);
            var cached = await TryGetCachedAsync(key, descriptor.OutputType, ct);
            if (cached.Hit)
            {
                return new QueryResult(Authorized: true, Output: cached.Value, Error: null, CacheHit: true);
            }

            var output = await ExecuteAsync(descriptor, input, ct);
            await SetCachedAsync(key, output, descriptor.CachePolicy, ct);
            return new QueryResult(Authorized: true, Output: output, Error: null);
        }

        return new QueryResult(
            Authorized: true,
            Output: await ExecuteAsync(descriptor, input, ct),
            Error: null);
    }

    private object CoerceInput(object input, Type inputType)
    {
        if (inputType.IsInstanceOfType(input)) return input;
        if (input is JsonElement json)
        {
            return json.Deserialize(inputType, CacheJsonOptions)
                ?? throw new InvalidOperationException($"Could not bind JSON input to {inputType.Name}.");
        }
        return input;
    }

    private async Task<object?> ExecuteAsync(QueryDescriptor descriptor, object input, CancellationToken ct)
    {
        var queryInstance = ActivatorUtilities.CreateInstance(services, descriptor.ImplementationType);
        var executeMethod = descriptor.ImplementationType.GetMethod("ExecuteAsync")
            ?? throw new InvalidOperationException(
                $"Query {descriptor.ImplementationType.Name} has no ExecuteAsync method.");

        var ctx = new QueryContext(auth, clock);
        var resultTask = (Task)executeMethod.Invoke(queryInstance, [input, ctx, ct])!;
        await resultTask;
        return resultTask.GetType().GetProperty("Result")!.GetValue(resultTask);
    }

    private QueryCacheKey BuildCacheKey(QueryDescriptor descriptor, object input)
    {
        var userId = descriptor.CachePolicy.Mode == QueryCacheMode.PerUser ? auth.UserId : null;
        var tenantId = descriptor.CachePolicy.Mode is QueryCacheMode.PerTenant or QueryCacheMode.PerUser
            ? tenant.TenantId
            : null;
        var inputJson = JsonSerializer.Serialize(input, input.GetType(), CacheJsonOptions);
        return new QueryCacheKey(descriptor.Name, tenantId, userId, inputJson);
    }

    private async Task<QueryCacheResult<object?>> TryGetCachedAsync(
        QueryCacheKey key,
        Type outputType,
        CancellationToken ct)
    {
        var method = typeof(IQueryCache)
            .GetMethod(nameof(IQueryCache.TryGetAsync))!
            .MakeGenericMethod(outputType);
        var task = (Task)method.Invoke(cache, [key, ct])!;
        await task;
        var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
        var hit = (bool)result.GetType().GetProperty(nameof(QueryCacheResult<object>.Hit))!.GetValue(result)!;
        var value = result.GetType().GetProperty(nameof(QueryCacheResult<object>.Value))!.GetValue(result);
        return new QueryCacheResult<object?>(hit, value);
    }

    private async Task SetCachedAsync(
        QueryCacheKey key,
        object? output,
        QueryCachePolicy policy,
        CancellationToken ct)
    {
        if (output is null) return;
        var method = typeof(IQueryCache)
            .GetMethod(nameof(IQueryCache.SetAsync))!
            .MakeGenericMethod(output.GetType());
        var task = (Task)method.Invoke(cache, [key, output, policy, ct])!;
        await task;
    }

    private sealed class QueryContext(IAuthContext auth, IClock clock) : IQueryContext
    {
        public IAuthContext Auth { get; } = auth;
        public IClock Clock { get; } = clock;
    }
}
