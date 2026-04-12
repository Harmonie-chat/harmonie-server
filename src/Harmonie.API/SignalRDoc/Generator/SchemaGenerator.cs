using System.Reflection;
using System.Text.Json;
using Harmonie.API.SignalRDoc.Models;

namespace Harmonie.API.SignalRDoc.Generator;

public sealed class SchemaGenerator
{
    private static readonly HashSet<Type> CollectionGenericDefs = new()
    {
        typeof(List<>),
        typeof(IEnumerable<>),
        typeof(IReadOnlyList<>),
        typeof(IList<>),
        typeof(ICollection<>),
        typeof(HashSet<>),
    };

    /// <summary>
    /// Returns an AsyncApiSchema for the given type, registering complex schemas in the provided dict.
    /// Returns null for void/Task (no payload).
    /// </summary>
    public AsyncApiSchema? GetSchema(Type type, Dictionary<string, AsyncApiSchema> schemas)
    {
        // Nullable<T>
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null)
        {
            var inner = GetSchema(underlying, schemas);
            return inner is null ? null : WithNullable(inner);
        }

        // void / Task (no payload)
        if (type == typeof(void) || type == typeof(Task) || type == typeof(ValueTask))
            return null;

        // Task<T> / ValueTask<T>
        if (type.IsGenericType)
        {
            var def = type.GetGenericTypeDefinition();
            if (def == typeof(Task<>) || def == typeof(ValueTask<>))
                return GetSchema(type.GetGenericArguments()[0], schemas);
        }

        // Primitives
        if (type == typeof(string)) return new AsyncApiSchema { Type = "string" };
        if (type == typeof(bool)) return new AsyncApiSchema { Type = "boolean" };
        if (type == typeof(int) || type == typeof(long) || type == typeof(short)
            || type == typeof(byte) || type == typeof(uint) || type == typeof(ulong))
            return new AsyncApiSchema { Type = "integer" };
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            return new AsyncApiSchema { Type = "number" };
        if (type == typeof(Guid)) return new AsyncApiSchema { Type = "string", Format = "uuid" };
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
            return new AsyncApiSchema { Type = "string", Format = "date-time" };

        // Enum
        if (type.IsEnum)
            return new AsyncApiSchema { Type = "string", Enum = System.Enum.GetNames(type) };

        // Array
        if (type.IsArray)
        {
            var elemType = type.GetElementType()!;
            return new AsyncApiSchema { Type = "array", Items = GetSchema(elemType, schemas) };
        }

        // Generic collection
        if (type.IsGenericType && CollectionGenericDefs.Contains(type.GetGenericTypeDefinition()))
        {
            var elemType = type.GetGenericArguments()[0];
            return new AsyncApiSchema { Type = "array", Items = GetSchema(elemType, schemas) };
        }

        // Complex object — register in components/schemas and return $ref
        return BuildComplexSchema(type, schemas);
    }

    private AsyncApiSchema? BuildComplexSchema(Type type, Dictionary<string, AsyncApiSchema> schemas)
    {
        var typeName = type.Name;

        // Already registered (completed or in-progress placeholder) — return $ref immediately
        if (schemas.ContainsKey(typeName))
            return new AsyncApiSchema { Ref = $"#/components/schemas/{typeName}" };

        // Add placeholder before recursing properties to break circular references
        var objectSchema = new AsyncApiSchema { Type = "object" };
        schemas[typeName] = objectSchema;

        var properties = new Dictionary<string, AsyncApiSchema>();
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var propSchema = GetSchema(prop.PropertyType, schemas);
            if (propSchema is not null)
                properties[JsonNamingPolicy.CamelCase.ConvertName(prop.Name)] = propSchema;
        }

        if (properties.Count > 0)
            objectSchema.Properties = properties;

        return new AsyncApiSchema { Ref = $"#/components/schemas/{typeName}" };
    }

    private static AsyncApiSchema WithNullable(AsyncApiSchema schema)
    {
        return new AsyncApiSchema
        {
            Ref = schema.Ref,
            Type = schema.Type,
            Format = schema.Format,
            Properties = schema.Properties,
            Items = schema.Items,
            Description = schema.Description,
            Enum = schema.Enum,
            Nullable = true,
        };
    }
}
