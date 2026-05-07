namespace RethinkWeb.Manifest;

/// <summary>
/// Public contract: humans read this at /_docs, LLMs read this for context,
/// MCP clients and renderers read it for exposed capabilities.
/// </summary>
public sealed record ManifestDocument(
    string FrameworkVersion,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ManifestEntity> Entities,
    IReadOnlyList<ManifestQuery> Queries);

public sealed record ManifestEntity(
    string Slug,
    string DisplayName,
    string? ReadPermission,
    string? WritePermission,
    IReadOnlyList<ManifestField> Fields,
    IReadOnlyList<ManifestAction> Actions,
    IReadOnlyList<ManifestMutation> Mutations);

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

public sealed record ManifestMutation(
    string Name,
    string DisplayName,
    string? Description,
    string? Permission,
    string? Icon,
    bool ExposeToMcp,
    JsonSchema InputSchema,
    JsonSchema OutputSchema);

public sealed record ManifestQuery(
    string Name,
    string DisplayName,
    string? Description,
    string? Permission,
    bool ExposeToMcp,
    ManifestQueryCache Cache,
    JsonSchema InputSchema,
    JsonSchema OutputSchema);

public sealed record ManifestQueryCache(
    string Mode,
    int? DurationSeconds,
    IReadOnlyList<string> Dependencies);

/// <summary>Tiny JSON-Schema-ish representation. Enough for MCP tools.</summary>
public sealed record JsonSchema(
    string Type,
    IReadOnlyDictionary<string, JsonSchemaProperty> Properties,
    IReadOnlyList<string> Required);

public sealed record JsonSchemaProperty(
    string Type,
    string? Format = null,
    string? Description = null);
