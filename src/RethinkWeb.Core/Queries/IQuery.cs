using RethinkWeb.Auth;

namespace RethinkWeb.Queries;

/// <summary>
/// Read-side capability exposed through the manifest, HTTP, MCP, and future renderers.
/// Queries are typed, permission-gated, tenant-aware, and optionally cacheable.
/// </summary>
public interface IQuery<TInput, TOutput>
    where TInput : class
{
    Task<TOutput> ExecuteAsync(
        TInput input,
        IQueryContext context,
        CancellationToken ct = default);
}

public interface IQueryContext
{
    IAuthContext Auth { get; }
    IClock Clock { get; }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class QueryAttribute : Attribute
{
    public QueryAttribute(string name, string displayName)
    {
        Name = name;
        DisplayName = displayName;
    }

    /// <summary>Stable manifest and MCP name, e.g. "tasks.list-open".</summary>
    public string Name { get; }

    public string DisplayName { get; }
    public string? Description { get; init; }
    public string? Permission { get; init; }
    public bool ExposeToMcp { get; init; } = true;
    public QueryCacheMode Cache { get; init; } = QueryCacheMode.None;
    public int CacheSeconds { get; init; }
    public string[] DependsOn { get; init; } = [];
}
