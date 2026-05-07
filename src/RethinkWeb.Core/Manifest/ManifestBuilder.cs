using System.Reflection;
using RethinkWeb.Actions;
using RethinkWeb.Auth;
using RethinkWeb.Metadata;

namespace RethinkWeb.Manifest;

public interface IManifestBuilder
{
    ManifestDocument Build();
}

public sealed class ManifestBuilder(
    IEntityRegistry entities,
    IActionRegistry actions,
    IAuthContext auth,
    IClock clock) : IManifestBuilder
{
    private const string FrameworkVersion = "0.1.0-prototype";

    public ManifestDocument Build()
    {
        var entityDocs = entities.All
            .Where(e => e.ReadPermission is null || auth.HasPermission(e.ReadPermission))
            .Select(BuildEntity)
            .ToList();

        return new ManifestDocument(
            FrameworkVersion: FrameworkVersion,
            GeneratedAt: clock.UtcNow,
            Entities: entityDocs);
    }

    private ManifestEntity BuildEntity(EntityMetadata e)
    {
        var fields = e.Fields
            .Where(f => f.Attribute.ReadPermission is null || auth.HasPermission(f.Attribute.ReadPermission))
            .Select(f => new ManifestField(
                Name: f.Name,
                Label: f.Attribute.Label,
                Kind: f.Attribute.Kind.ToString(),
                Required: f.Attribute.Required,
                Disabled: f.Attribute.Disabled,
                ReadPermission: f.Attribute.ReadPermission,
                EditPermission: f.Attribute.EditPermission,
                Sample: f.Attribute.Sample))
            .ToList();

        var entityActions = actions.ForEntity(e.ClrType)
            .Where(a => a.Permission is null || auth.HasPermission(a.Permission))
            .Select(a => new ManifestAction(
                Name: a.Name,
                DisplayName: a.DisplayName,
                Description: a.Description,
                Permission: a.Permission,
                Icon: a.Icon,
                ExposeToMcp: a.ExposeToMcp,
                InputSchema: BuildSchema(a.InputType),
                OutputSchema: BuildSchema(a.OutputType)))
            .ToList();

        return new ManifestEntity(
            Slug: e.Slug,
            DisplayName: e.DisplayName,
            ReadPermission: e.ReadPermission,
            WritePermission: e.WritePermission,
            Fields: fields,
            Actions: entityActions);
    }

    /// <summary>
    /// Tiny reflection-based JSON Schema builder. Enough for MCP tool schemas.
    /// Recursive nested types are NOT handled — keep action input DTOs flat for the prototype.
    ///
    /// Required-ness uses NullabilityInfoContext so non-nullable reference types
    /// (e.g. <c>string Name</c>) are correctly marked required, while nullable
    /// reference types (<c>string? Notes</c>) are optional.
    /// </summary>
    private static JsonSchema BuildSchema(Type type)
    {
        if (type == typeof(void) || type == typeof(Task))
        {
            return new JsonSchema("object", new Dictionary<string, JsonSchemaProperty>(), []);
        }

        var unwrapped = Nullable.GetUnderlyingType(type) ?? type;
        if (unwrapped.IsPrimitive || unwrapped == typeof(string) || unwrapped == typeof(decimal)
            || unwrapped == typeof(Guid) || unwrapped == typeof(DateTime) || unwrapped == typeof(DateTimeOffset))
        {
            return new JsonSchema(
                Type: JsonTypeFor(unwrapped),
                Properties: new Dictionary<string, JsonSchemaProperty>(),
                Required: []);
        }

        var nullCtx = new NullabilityInfoContext();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var props = properties.ToDictionary(
            p => CamelCase(p.Name),
            p => new JsonSchemaProperty(JsonTypeFor(Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType)));

        var required = properties
            .Where(p => IsRequiredByNullability(p, nullCtx))
            .Select(p => CamelCase(p.Name))
            .ToList();

        return new JsonSchema("object", props, required);
    }

    private static bool IsRequiredByNullability(PropertyInfo p, NullabilityInfoContext ctx)
    {
        // Value types: required iff the property is non-nullable (Nullable<T> means optional).
        if (p.PropertyType.IsValueType)
        {
            return Nullable.GetUnderlyingType(p.PropertyType) is null;
        }

        // Reference types: read the C# 8 nullable annotation. NotNull = required.
        var info = ctx.Create(p);
        return info.WriteState == NullabilityState.NotNull;
    }

    private static string JsonTypeFor(Type t)
    {
        if (t == typeof(string) || t == typeof(Guid) || t == typeof(DateTime) || t == typeof(DateTimeOffset))
            return "string";
        if (t == typeof(bool))
            return "boolean";
        if (t == typeof(int) || t == typeof(long) || t == typeof(short))
            return "integer";
        if (t == typeof(double) || t == typeof(float) || t == typeof(decimal))
            return "number";
        return "object";
    }

    private static string CamelCase(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s[1..];
}
