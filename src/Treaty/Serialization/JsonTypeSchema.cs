namespace Treaty.Serialization;

/// <summary>
/// Represents a schema for a JSON type, used for validation.
/// </summary>
public sealed class JsonTypeSchema
{
    /// <summary>
    /// Gets the CLR type this schema represents.
    /// </summary>
    public Type ClrType { get; }

    /// <summary>
    /// Gets the JSON type (object, array, string, number, boolean, null).
    /// </summary>
    public JsonSchemaType SchemaType { get; }

    /// <summary>
    /// Gets the properties of the schema (for object types).
    /// </summary>
    public IReadOnlyDictionary<string, JsonPropertySchema> Properties { get; }

    /// <summary>
    /// Gets the required property names.
    /// </summary>
    public IReadOnlyList<string> Required { get; }

    /// <summary>
    /// Gets the item schema (for array types).
    /// </summary>
    public JsonTypeSchema? ItemSchema { get; }

    /// <summary>
    /// Gets whether additional properties are allowed (for object types).
    /// </summary>
    public bool AdditionalPropertiesAllowed { get; }

    /// <summary>
    /// Gets the format constraint (e.g., "email", "uri", "date-time").
    /// </summary>
    public string? Format { get; }

    /// <summary>
    /// Gets whether this type is nullable.
    /// </summary>
    public bool IsNullable { get; }

    internal JsonTypeSchema(
        Type clrType,
        JsonSchemaType schemaType,
        IReadOnlyDictionary<string, JsonPropertySchema>? properties = null,
        IReadOnlyList<string>? required = null,
        JsonTypeSchema? itemSchema = null,
        bool additionalPropertiesAllowed = true,
        string? format = null,
        bool isNullable = false)
    {
        ClrType = clrType;
        SchemaType = schemaType;
        Properties = properties ?? new Dictionary<string, JsonPropertySchema>();
        Required = required ?? [];
        ItemSchema = itemSchema;
        AdditionalPropertiesAllowed = additionalPropertiesAllowed;
        Format = format;
        IsNullable = isNullable;
    }
}

/// <summary>
/// Represents a property in a JSON object schema.
/// </summary>
public sealed class JsonPropertySchema
{
    /// <summary>
    /// Gets the property name (as it appears in JSON).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the CLR property name.
    /// </summary>
    public string ClrName { get; }

    /// <summary>
    /// Gets the type schema for this property.
    /// </summary>
    public JsonTypeSchema TypeSchema { get; }

    /// <summary>
    /// Gets whether this property is required.
    /// </summary>
    public bool IsRequired { get; }

    /// <summary>
    /// Gets whether this property is nullable.
    /// </summary>
    public bool IsNullable { get; }

    internal JsonPropertySchema(
        string name,
        string clrName,
        JsonTypeSchema typeSchema,
        bool isRequired,
        bool isNullable)
    {
        Name = name;
        ClrName = clrName;
        TypeSchema = typeSchema;
        IsRequired = isRequired;
        IsNullable = isNullable;
    }
}

/// <summary>
/// The JSON schema type.
/// </summary>
public enum JsonSchemaType
{
    /// <summary>JSON object type.</summary>
    Object,
    /// <summary>JSON array type.</summary>
    Array,
    /// <summary>JSON string type.</summary>
    String,
    /// <summary>JSON number type (integer).</summary>
    Integer,
    /// <summary>JSON number type.</summary>
    Number,
    /// <summary>JSON boolean type.</summary>
    Boolean,
    /// <summary>JSON null type.</summary>
    Null,
    /// <summary>Any JSON type.</summary>
    Any
}
