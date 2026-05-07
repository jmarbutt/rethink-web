namespace RethinkWeb.Metadata;

/// <summary>
/// The kinds of UI primitives the renderer knows how to draw.
/// Add a new kind here when introducing a new attribute.
/// </summary>
public enum FieldKind
{
    Text,
    Number,
    Currency,
    Date,
    Checkbox,
    Select,
    Phone,
    Email,
    Hidden,
}

/// <summary>
/// Base for all field attributes. Lives on properties of entity types.
/// Renderers + the manifest builder consume these via reflection.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public abstract class FieldAttribute : Attribute
{
    protected FieldAttribute(FieldKind kind, string label)
    {
        Kind = kind;
        Label = label;
    }

    public FieldKind Kind { get; }
    public string Label { get; }

    /// <summary>Field is read-only in edit views.</summary>
    public bool Disabled { get; init; }

    /// <summary>Field appears in grid listings.</summary>
    public bool GridVisible { get; init; }

    /// <summary>Sort order for grid columns. Lower = leftmost.</summary>
    public int GridOrder { get; init; }

    /// <summary>Required for save.</summary>
    public bool Required { get; init; }

    /// <summary>Permission name needed to see this field at all. Null = no restriction.</summary>
    public string? ReadPermission { get; init; }

    /// <summary>Permission name needed to edit this field. Null = no restriction.</summary>
    public string? EditPermission { get; init; }

    /// <summary>Sample value shown in docs/manifest. Helps LLMs and docs readers.</summary>
    public string? Sample { get; init; }
}

public sealed class TextBoxAttribute : FieldAttribute
{
    public TextBoxAttribute(string label) : base(FieldKind.Text, label) { }

    public bool Email { get; init; }
    public bool Multiline { get; init; }
    public int MaxLength { get; init; }
}

public sealed class NumberBoxAttribute : FieldAttribute
{
    public NumberBoxAttribute(string label) : base(FieldKind.Number, label) { }

    public double Min { get; init; } = double.MinValue;
    public double Max { get; init; } = double.MaxValue;
}

public sealed class CurrencyBoxAttribute : FieldAttribute
{
    public CurrencyBoxAttribute(string label) : base(FieldKind.Currency, label) { }
}

public sealed class DateBoxAttribute : FieldAttribute
{
    public DateBoxAttribute(string label) : base(FieldKind.Date, label) { }

    public bool IncludeTime { get; init; }
}

public sealed class CheckBoxAttribute : FieldAttribute
{
    public CheckBoxAttribute(string label) : base(FieldKind.Checkbox, label) { }
}

public sealed class SelectBoxAttribute : FieldAttribute
{
    public SelectBoxAttribute(string label) : base(FieldKind.Select, label) { }

    /// <summary>Reference to a named option collection registered with the framework.</summary>
    public string? Collection { get; init; }
}

public sealed class PhoneBoxAttribute : FieldAttribute
{
    public PhoneBoxAttribute(string label) : base(FieldKind.Phone, label) { }
}

/// <summary>
/// Marks the entity-level metadata. Goes on the class itself.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class EntityAttribute : Attribute
{
    public EntityAttribute(string slug, string displayName)
    {
        Slug = slug;
        DisplayName = displayName;
    }

    public string Slug { get; }
    public string DisplayName { get; }
    public string? ReadPermission { get; init; }
    public string? WritePermission { get; init; }
}
