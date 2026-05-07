using System.Reflection;

namespace RethinkWeb.Metadata;

/// <summary>
/// Reflected metadata about a single entity type. Built once at registration time,
/// then read by every layer (renderers, HTTP, MCP, manifest).
/// </summary>
public sealed class EntityMetadata
{
    public required Type ClrType { get; init; }
    public required string Slug { get; init; }
    public required string DisplayName { get; init; }
    public string? ReadPermission { get; init; }
    public string? WritePermission { get; init; }
    public required IReadOnlyList<FieldMetadata> Fields { get; init; }

    /// <summary>Field that holds the primary key. Convention: a property named "Id".</summary>
    public required FieldMetadata IdField { get; init; }

    public IEnumerable<FieldMetadata> GridFields =>
        Fields.Where(f => f.Attribute.GridVisible).OrderBy(f => f.Attribute.GridOrder);

    public static EntityMetadata Build(Type clrType)
    {
        var entityAttr = clrType.GetCustomAttribute<EntityAttribute>()
            ?? throw new InvalidOperationException(
                $"Type {clrType.FullName} is missing [Entity(slug, displayName)].");

        var fields = clrType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => (Property: p, Attribute: p.GetCustomAttribute<FieldAttribute>()))
            .Where(t => t.Attribute is not null)
            .Select(t => new FieldMetadata
            {
                Property = t.Property,
                Attribute = t.Attribute!,
            })
            .ToList();

        var idProperty = clrType.GetProperty("Id")
            ?? throw new InvalidOperationException(
                $"Entity {clrType.FullName} must have an 'Id' property.");

        // Id field is synthesized — entities don't need to attribute their primary key.
        var idField = new FieldMetadata
        {
            Property = idProperty,
            Attribute = new TextBoxAttribute("Id") { Disabled = true },
        };

        return new EntityMetadata
        {
            ClrType = clrType,
            Slug = entityAttr.Slug,
            DisplayName = entityAttr.DisplayName,
            ReadPermission = entityAttr.ReadPermission,
            WritePermission = entityAttr.WritePermission,
            Fields = fields,
            IdField = idField,
        };
    }
}

public sealed class FieldMetadata
{
    public required PropertyInfo Property { get; init; }
    public required FieldAttribute Attribute { get; init; }

    public string Name => Property.Name;
    public Type Type => Property.PropertyType;

    public object? GetValue(object entity) => Property.GetValue(entity);
    public void SetValue(object entity, object? value) => Property.SetValue(entity, value);
}
