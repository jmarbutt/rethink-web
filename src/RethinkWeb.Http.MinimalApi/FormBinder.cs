using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using RethinkWeb.Auth;
using RethinkWeb.Metadata;

namespace RethinkWeb.Http;

/// <summary>
/// Binds form values to entity properties by name. Reflection-based, prototype-grade.
/// Production would replace this with FastEndpoints/MVC binding, or generate strongly-typed
/// binders via source generators.
///
/// Enforces:
///   - <c>Required</c> — missing/blank field collected as a validation error
///   - <c>EditPermission</c> — silently skipped if the user lacks the permission
///                             (don't trust the client)
///   - <c>Disabled</c> — silently skipped (read-only fields)
/// </summary>
internal static class FormBinder
{
    /// <summary>
    /// Binds form values to entity properties. Returns the list of validation errors,
    /// or empty if all fields bound cleanly.
    /// </summary>
    public static IReadOnlyList<string> BindToEntity(
        IFormCollection form,
        EntityMetadata metadata,
        object entity,
        IAuthContext auth)
    {
        var errors = new List<string>();

        foreach (var field in metadata.Fields)
        {
            if (field.Attribute.Disabled) continue;
            if (field.Attribute.EditPermission is not null
                && !auth.HasPermission(field.Attribute.EditPermission))
            {
                continue;
            }

            var raw = form[field.Name].ToString();

            if (field.Attribute.Required && string.IsNullOrWhiteSpace(raw))
            {
                // Checkbox absence on form is "false", not "missing required true".
                if (field.Attribute.Kind != FieldKind.Checkbox)
                {
                    errors.Add($"{field.Attribute.Label} is required.");
                    continue;
                }
            }

            try
            {
                var value = ConvertValue(raw, field.Property);
                field.SetValue(entity, value);
            }
            catch (Exception ex)
            {
                errors.Add($"{field.Attribute.Label}: {ex.Message}");
            }
        }

        return errors;
    }

    private static object? ConvertValue(string raw, PropertyInfo property)
    {
        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var isNullable = Nullable.GetUnderlyingType(property.PropertyType) is not null
            || !property.PropertyType.IsValueType;

        if (string.IsNullOrEmpty(raw))
        {
            // Checkboxes: missing form field means false.
            if (targetType == typeof(bool)) return isNullable ? null : false;
            return isNullable ? null : Activator.CreateInstance(targetType);
        }

        if (targetType == typeof(string)) return raw;
        if (targetType == typeof(bool)) return raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw == "on";
        if (targetType == typeof(Guid)) return Guid.Parse(raw);
        if (targetType == typeof(int)) return int.Parse(raw, CultureInfo.InvariantCulture);
        if (targetType == typeof(long)) return long.Parse(raw, CultureInfo.InvariantCulture);
        if (targetType == typeof(decimal)) return decimal.Parse(raw, CultureInfo.InvariantCulture);
        if (targetType == typeof(double)) return double.Parse(raw, CultureInfo.InvariantCulture);
        if (targetType == typeof(DateTime)) return DateTime.Parse(raw, CultureInfo.InvariantCulture);
        if (targetType == typeof(DateTimeOffset)) return DateTimeOffset.Parse(raw, CultureInfo.InvariantCulture);

        return Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture);
    }
}
