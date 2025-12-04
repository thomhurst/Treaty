using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Treaty.Serialization;

/// <summary>
/// Default JSON serializer implementation using System.Text.Json.
/// </summary>
public sealed class SystemTextJsonSerializer : IJsonSerializer
{
    private readonly JsonSerializerOptions _options;
    private readonly Dictionary<Type, JsonTypeSchema> _schemaCache = new();

    /// <summary>
    /// Creates a new instance with default options.
    /// </summary>
    public SystemTextJsonSerializer()
        : this(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        })
    {
    }

    /// <summary>
    /// Creates a new instance with custom options.
    /// </summary>
    /// <param name="options">The JSON serializer options to use.</param>
    public SystemTextJsonSerializer(JsonSerializerOptions options)
    {
        _options = options;
    }

    /// <inheritdoc/>
    public string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, _options);
    }

    /// <inheritdoc/>
    public string Serialize(object? value, Type type)
    {
        return JsonSerializer.Serialize(value, type, _options);
    }

    /// <inheritdoc/>
    public T? Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, _options);
    }

    /// <inheritdoc/>
    public object? Deserialize(string json, Type type)
    {
        return JsonSerializer.Deserialize(json, type, _options);
    }

    /// <inheritdoc/>
    public JsonNode? Parse(string json)
    {
        return JsonNode.Parse(json);
    }

    /// <inheritdoc/>
    public JsonTypeSchema GetSchema<T>() => GetSchema(typeof(T));

    /// <inheritdoc/>
    public JsonTypeSchema GetSchema(Type type)
    {
        if (_schemaCache.TryGetValue(type, out var cached))
            return cached;

        var schema = BuildSchema(type, new HashSet<Type>());
        _schemaCache[type] = schema;
        return schema;
    }

    private JsonTypeSchema BuildSchema(Type type, HashSet<Type> visitedTypes)
    {
        var underlyingType = Nullable.GetUnderlyingType(type);
        var isNullable = underlyingType != null;
        var actualType = underlyingType ?? type;

        // Handle primitive types
        if (actualType == typeof(string))
            return new JsonTypeSchema(type, JsonSchemaType.String, isNullable: isNullable);

        if (actualType == typeof(int) || actualType == typeof(long) ||
            actualType == typeof(short) || actualType == typeof(byte))
            return new JsonTypeSchema(type, JsonSchemaType.Integer, isNullable: isNullable);

        if (actualType == typeof(decimal) || actualType == typeof(double) ||
            actualType == typeof(float))
            return new JsonTypeSchema(type, JsonSchemaType.Number, isNullable: isNullable);

        if (actualType == typeof(bool))
            return new JsonTypeSchema(type, JsonSchemaType.Boolean, isNullable: isNullable);

        if (actualType == typeof(Guid))
            return new JsonTypeSchema(type, JsonSchemaType.String, format: "uuid", isNullable: isNullable);

        if (actualType == typeof(DateTime) || actualType == typeof(DateTimeOffset))
            return new JsonTypeSchema(type, JsonSchemaType.String, format: "date-time", isNullable: isNullable);

        if (actualType == typeof(DateOnly))
            return new JsonTypeSchema(type, JsonSchemaType.String, format: "date", isNullable: isNullable);

        if (actualType == typeof(TimeOnly))
            return new JsonTypeSchema(type, JsonSchemaType.String, format: "time", isNullable: isNullable);

        if (actualType == typeof(Uri))
            return new JsonTypeSchema(type, JsonSchemaType.String, format: "uri", isNullable: isNullable);

        // Handle arrays and lists
        if (actualType.IsArray)
        {
            var elementType = actualType.GetElementType()!;
            var itemSchema = BuildSchema(elementType, visitedTypes);
            return new JsonTypeSchema(type, JsonSchemaType.Array, itemSchema: itemSchema, isNullable: isNullable);
        }

        if (actualType.IsGenericType)
        {
            var genericDef = actualType.GetGenericTypeDefinition();
            if (genericDef == typeof(List<>) || genericDef == typeof(IList<>) ||
                genericDef == typeof(IEnumerable<>) || genericDef == typeof(ICollection<>) ||
                genericDef == typeof(IReadOnlyList<>) || genericDef == typeof(IReadOnlyCollection<>))
            {
                var elementType = actualType.GetGenericArguments()[0];
                var itemSchema = BuildSchema(elementType, visitedTypes);
                return new JsonTypeSchema(type, JsonSchemaType.Array, itemSchema: itemSchema, isNullable: isNullable);
            }

            if (genericDef == typeof(Dictionary<,>) || genericDef == typeof(IDictionary<,>) ||
                genericDef == typeof(IReadOnlyDictionary<,>))
            {
                // Dictionary is treated as object with dynamic properties
                return new JsonTypeSchema(type, JsonSchemaType.Object, isNullable: isNullable);
            }
        }

        // Handle non-generic IEnumerable
        if (typeof(IEnumerable).IsAssignableFrom(actualType) && actualType != typeof(string))
        {
            return new JsonTypeSchema(type, JsonSchemaType.Array, isNullable: isNullable);
        }

        // Handle object/class types
        if (actualType.IsClass || actualType.IsValueType)
        {
            if (visitedTypes.Contains(actualType))
            {
                // Circular reference - return generic object schema
                return new JsonTypeSchema(type, JsonSchemaType.Object, isNullable: isNullable);
            }

            visitedTypes.Add(actualType);

            var properties = new Dictionary<string, JsonPropertySchema>();
            var required = new List<string>();

            var props = actualType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && !p.GetCustomAttributes<JsonIgnoreAttribute>().Any());

            foreach (var prop in props)
            {
                var jsonName = GetJsonPropertyName(prop);
                var propIsNullable = IsPropertyNullable(prop);
                var propSchema = BuildSchema(prop.PropertyType, new HashSet<Type>(visitedTypes));

                var isRequired = !propIsNullable && !prop.PropertyType.IsValueType;
                // For value types, check if it's Nullable<T>
                if (prop.PropertyType.IsValueType && Nullable.GetUnderlyingType(prop.PropertyType) == null)
                {
                    // Non-nullable value type is implicitly required
                    isRequired = true;
                }

                properties[jsonName] = new JsonPropertySchema(jsonName, prop.Name, propSchema, isRequired, propIsNullable);

                if (isRequired)
                    required.Add(jsonName);
            }

            visitedTypes.Remove(actualType);
            return new JsonTypeSchema(type, JsonSchemaType.Object, properties, required, isNullable: isNullable);
        }

        return new JsonTypeSchema(type, JsonSchemaType.Any, isNullable: isNullable);
    }

    private string GetJsonPropertyName(PropertyInfo prop)
    {
        var jsonPropAttr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
        if (jsonPropAttr != null)
            return jsonPropAttr.Name;

        if (_options.PropertyNamingPolicy != null)
            return _options.PropertyNamingPolicy.ConvertName(prop.Name);

        return prop.Name;
    }

    private static bool IsPropertyNullable(PropertyInfo prop)
    {
        // Check for Nullable<T>
        if (Nullable.GetUnderlyingType(prop.PropertyType) != null)
            return true;

        // Check for nullable reference type using NullabilityInfoContext
        var context = new NullabilityInfoContext();
        var nullabilityInfo = context.Create(prop);
        return nullabilityInfo.WriteState == NullabilityState.Nullable ||
               nullabilityInfo.ReadState == NullabilityState.Nullable;
    }
}
