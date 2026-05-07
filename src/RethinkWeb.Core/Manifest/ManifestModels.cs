namespace RethinkWeb.Manifest;

/// <summary>
/// Single source of truth: humans read this at /_docs, LLMs read this for context,
/// MCP clients read this for tools/list. One document, three audiences.
/// </summary>
public sealed record ManifestDocument(
    string FrameworkVersion,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ManifestEntity> Entities);

public sealed record ManifestEntity(
    string Slug,
    string DisplayName,
    string? ReadPermission,
    string? WritePermission,
    IReadOnlyList<ManifestField> Fields,
    IReadOnlyList<ManifestAction> Actions);

public sealed record ManifestField(
    string Name,
    string Label,
    string Kind,
    bool Required,
    bool Disabled,
    string? ReadPermission,
    string? EditPermission,
    string? Sample);

public sealed record ManifestAction(
    string Name,
    string DisplayName,
    string? Description,
    string? Permission,
    string? Icon,
    bool ExposeToMcp,
    JsonSchema InputSchema,
    JsonSchema OutputSchema);

/// <summary>Tiny JSON-Schema-ish representation. Enough for MCP tools.</summary>
public sealed record JsonSchema(
    string Type,
    IReadOnlyDictionary<string, JsonSchemaProperty> Properties,
    IReadOnlyList<string> Required);

public sealed record JsonSchemaProperty(
    string Type,
    string? Format = null,
    string? Description = null);
