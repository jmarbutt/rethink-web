namespace RethinkWeb.Actions;

/// <summary>
/// The unifying abstraction. ONE action definition becomes:
///   - HTTP endpoint (POST /{entity}/{id}/actions/{name})
///   - MCP tool (tools/list + tools/call)
///   - Rendered button in entity views
///   - Audit/event source
/// </summary>
public interface IAction<TEntity, TInput, TOutput>
    where TEntity : class
    where TInput : class
{
    Task<TOutput> ExecuteAsync(
        TEntity entity,
        TInput input,
        IActionContext context,
        CancellationToken ct = default);
}

/// <summary>
/// Per-invocation context passed to the action. Lets the action publish events,
/// access auth, and read the clock — without static singletons.
/// </summary>
public interface IActionContext
{
    Auth.IAuthContext Auth { get; }
    Events.IEventBus Events { get; }
    IClock Clock { get; }
}

/// <summary>
/// Marks an IAction implementation. Required for registration.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ActionAttribute : Attribute
{
    public ActionAttribute(string name, string displayName)
    {
        Name = name;
        DisplayName = displayName;
    }

    /// <summary>URL-safe action name. Used in HTTP routes and MCP tool names.</summary>
    public string Name { get; }

    /// <summary>Human-friendly label for buttons and docs.</summary>
    public string DisplayName { get; }

    public string? Description { get; init; }
    public string? Permission { get; init; }
    public string? Icon { get; init; }

    /// <summary>Set false to hide this action from the MCP tools/list response.</summary>
    public bool ExposeToMcp { get; init; } = true;
}
