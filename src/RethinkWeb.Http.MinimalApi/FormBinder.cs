using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using RethinkWeb.Metadata;

namespace RethinkWeb.Http;

/// <summary>
/// Binds form values to entity properties by name. Reflection-based, prototype-grade.
/// Production would replace this with FastEndpoints/MVC binding, or generate strongly-typed
/// binders via source generators.
/// </summary>
internal static class FormBinder
{
    public static void BindToEntity(IFormCollection form, EntityMetadata metadata, object entity)
    {
        foreach (var field in metadata.Fields)
        {
            if (field.Attribute.Disabled) continue;

            var raw = form[field.Name].ToString();
            var value = ConvertValue(raw, field.Property);
            field.SetValue(entity, value);
        }
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
