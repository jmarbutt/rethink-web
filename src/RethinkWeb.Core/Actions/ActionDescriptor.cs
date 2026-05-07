namespace RethinkWeb.Actions;

/// <summary>
/// Reflected description of one registered action. Built once at registration time.
/// </summary>
public sealed class ActionDescriptor
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public string? Permission { get; init; }
    public string? Icon { get; init; }
    public bool ExposeToMcp { get; init; }

    public required Type EntityType { get; init; }
    public required Type InputType { get; init; }
    public required Type OutputType { get; init; }
    public required Type ImplementationType { get; init; }
}
