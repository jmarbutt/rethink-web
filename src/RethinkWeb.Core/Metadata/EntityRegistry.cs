namespace RethinkWeb.Metadata;

/// <summary>
/// Holds the set of entity types registered with the framework. Built at startup
/// in <c>AddRethinkWeb()</c>; queried by every layer afterward.
/// </summary>
public interface IEntityRegistry
{
    EntityMetadata Get(Type clrType);
    EntityMetadata? GetBySlug(string slug);
    IReadOnlyCollection<EntityMetadata> All { get; }
}

public sealed class EntityRegistry : IEntityRegistry
{
    private readonly Dictionary<Type, EntityMetadata> _byType = [];
    private readonly Dictionary<string, EntityMetadata> _bySlug = new(StringComparer.OrdinalIgnoreCase);

    public void Register(Type clrType)
    {
        var meta = EntityMetadata.Build(clrType);
        _byType[clrType] = meta;
        _bySlug[meta.Slug] = meta;
    }

    public EntityMetadata Get(Type clrType) =>
        _byType.TryGetValue(clrType, out var m)
            ? m
            : throw new InvalidOperationException($"Entity {clrType.FullName} is not registered.");

    public EntityMetadata? GetBySlug(string slug) =>
        _bySlug.TryGetValue(slug, out var m) ? m : null;

    public IReadOnlyCollection<EntityMetadata> All => _byType.Values;
}
