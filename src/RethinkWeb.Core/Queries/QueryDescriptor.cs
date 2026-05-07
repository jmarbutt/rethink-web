namespace RethinkWeb.Queries;

public sealed class QueryDescriptor
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public string? Permission { get; init; }
    public bool ExposeToMcp { get; init; }
    public required Type InputType { get; init; }
    public required Type OutputType { get; init; }
    public required Type ImplementationType { get; init; }
    public required QueryCachePolicy CachePolicy { get; init; }
}
