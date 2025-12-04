using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Treaty.Matching;

/// <summary>
/// Represents a schema built from matcher specifications.
/// Used to validate JSON against a structure defined with Match.* matchers.
/// </summary>
public sealed class MatcherSchema
{
    /// <summary>
    /// Properties of this schema (for object schemas).
    /// </summary>
    public IReadOnlyDictionary<string, MatcherSchemaProperty> Properties { get; }

    /// <summary>
    /// Required property names.
    /// </summary>
    public IReadOnlyList<string> Required { get; }

    /// <summary>
    /// Schema for array items (for array schemas).
    /// </summary>
    public MatcherSchema? ItemSchema { get; }

    /// <summary>
    /// The matcher for this schema node (for leaf nodes).
    /// </summary>
    public IMatcher? Matcher { get; }

    /// <summary>
    /// Whether this schema represents an array.
    /// </summary>
    public bool IsArray { get; }

    /// <summary>
    /// Whether this schema represents an object.
    /// </summary>
    public bool IsObject => Properties.Count > 0;

    /// <summary>
    /// Whether this schema represents a leaf node (single matcher).
    /// </summary>
    public bool IsLeaf => Matcher != null && !IsObject && !IsArray;

    private MatcherSchema(
        IReadOnlyDictionary<string, MatcherSchemaProperty> properties,
        IReadOnlyList<string> required,
        MatcherSchema? itemSchema,
        IMatcher? matcher,
        bool isArray)
    {
        Properties = properties;
        Required = required;
        ItemSchema = itemSchema;
        Matcher = matcher;
        IsArray = isArray;
    }

    /// <summary>
    /// Creates a MatcherSchema from an anonymous object containing matchers.
    /// </summary>
    /// <param name="schema">An anonymous object where properties are IMatcher instances or nested objects.</param>
    /// <param name="options">Optional JSON serializer options for property name resolution.</param>
    /// <returns>A MatcherSchema representing the structure.</returns>
    public static MatcherSchema FromObject(object schema, JsonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(schema);

        // If it's a matcher itself, return a leaf schema
        if (schema is IMatcher matcher)
        {
            return new MatcherSchema(
                new Dictionary<string, MatcherSchemaProperty>(),
                [],
                null,
                matcher,
                false);
        }

        var type = schema.GetType();

        // Handle arrays/collections
        if (type.IsArray || (type.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(type)))
        {
            var enumerable = (System.Collections.IEnumerable)schema;
            var firstItem = enumerable.Cast<object>().FirstOrDefault();
            var itemSchema = firstItem != null ? FromObject(firstItem, options) : null;

            return new MatcherSchema(
                new Dictionary<string, MatcherSchemaProperty>(),
                [],
                itemSchema,
                null,
                true);
        }

        // Handle object with properties
        var properties = new Dictionary<string, MatcherSchemaProperty>(StringComparer.OrdinalIgnoreCase);
        var required = new List<string>();
        var namingPolicy = options?.PropertyNamingPolicy;

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var value = prop.GetValue(schema);
            if (value == null) continue;

            // Determine JSON property name
            var jsonName = GetJsonPropertyName(prop, namingPolicy);

            // Create schema for property value
            MatcherSchema propSchema;
            if (value is IMatcher propMatcher)
            {
                propSchema = new MatcherSchema(
                    new Dictionary<string, MatcherSchemaProperty>(),
                    [],
                    null,
                    propMatcher,
                    false);
            }
            else
            {
                propSchema = FromObject(value, options);
            }

            properties[jsonName] = new MatcherSchemaProperty(prop.Name, jsonName, propSchema, true);
            required.Add(jsonName);
        }

        return new MatcherSchema(properties, required, null, null, false);
    }

    private static string GetJsonPropertyName(PropertyInfo prop, JsonNamingPolicy? namingPolicy)
    {
        // Check for JsonPropertyName attribute
        var jsonAttr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
        if (jsonAttr != null)
            return jsonAttr.Name;

        // Apply naming policy if provided
        if (namingPolicy != null)
            return namingPolicy.ConvertName(prop.Name);

        // Default: use camelCase
        return JsonNamingPolicy.CamelCase.ConvertName(prop.Name);
    }

    /// <summary>
    /// Generates a sample JSON string conforming to this schema.
    /// </summary>
    public string GenerateSample()
    {
        var sample = GenerateSampleValue();
        return JsonSerializer.Serialize(sample, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    internal object? GenerateSampleValue()
    {
        if (Matcher != null)
            return Matcher.GenerateSample();

        if (IsArray && ItemSchema != null)
            return new[] { ItemSchema.GenerateSampleValue() };

        if (IsObject)
        {
            var dict = new Dictionary<string, object?>();
            foreach (var (name, prop) in Properties)
            {
                dict[name] = prop.Schema.GenerateSampleValue();
            }
            return dict;
        }

        return null;
    }
}

/// <summary>
/// Represents a property within a MatcherSchema.
/// </summary>
public sealed class MatcherSchemaProperty
{
    /// <summary>
    /// The CLR property name.
    /// </summary>
    public string ClrName { get; }

    /// <summary>
    /// The JSON property name.
    /// </summary>
    public string JsonName { get; }

    /// <summary>
    /// The schema for this property's value.
    /// </summary>
    public MatcherSchema Schema { get; }

    /// <summary>
    /// Whether this property is required.
    /// </summary>
    public bool IsRequired { get; }

    internal MatcherSchemaProperty(string clrName, string jsonName, MatcherSchema schema, bool isRequired)
    {
        ClrName = clrName;
        JsonName = jsonName;
        Schema = schema;
        IsRequired = isRequired;
    }
}
