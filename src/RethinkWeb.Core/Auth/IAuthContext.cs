namespace RethinkWeb.Auth;

/// <summary>
/// Per-request user context. Asked by renderers ("can this user see this field?") and
/// by HTTP/MCP layers ("can this user invoke this action?").
/// </summary>
public interface IAuthContext
{
    string? UserId { get; }
    bool HasPermission(string permission);
}

/// <summary>
/// Default implementation that allows everything. Replace in production.
/// Useful for the prototype, tests, and "I haven't built auth yet" scenarios.
/// </summary>
public sealed class AllowAllAuthContext : IAuthContext
{
    public string? UserId => "anonymous";
    public bool HasPermission(string permission) => true;
}
