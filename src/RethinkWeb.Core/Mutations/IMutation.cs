namespace RethinkWeb.Mutations;

/// <summary>
/// State-changing capability exposed through HTTP, MCP, the manifest, and future renderers.
/// This is the long-term name for entity-scoped actions.
/// </summary>
public interface IMutation<TEntity, TInput, TOutput>
    where TEntity : class
    where TInput : class
{
    Task<TOutput> ExecuteAsync(
        TEntity entity,
        TInput input,
        IMutationContext context,
        CancellationToken ct = default);
}

public interface IMutationContext
{
    Auth.IAuthContext Auth { get; }
    Events.IEventBus Events { get; }
    IClock Clock { get; }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class MutationAttribute : Attribute
{
    public MutationAttribute(string name, string displayName)
    {
        Name = name;
        DisplayName = displayName;
    }

    /// <summary>URL-safe mutation name. Entity-scoped MCP tools use "{entitySlug}.{name}".</summary>
    public string Name { get; }

    public string DisplayName { get; }
    public string? Description { get; init; }
    public string? Permission { get; init; }
    public string? Icon { get; init; }
    public bool ExposeToMcp { get; init; } = true;
}
