using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.OpenApi;
using Treaty.Contracts;
using Treaty.Serialization;
using Treaty.Validation;

namespace Treaty.OpenApi;

/// <summary>
/// Validates JSON content against an OpenAPI schema.
/// </summary>
internal sealed class OpenApiSchemaValidator : ISchemaValidator
{
    private readonly IOpenApiSchema _schema;
    private readonly IJsonSerializer _serializer;

    public Type? ExpectedType => null;

    public string? SchemaTypeName => GetSchemaTypeName(_schema);

    public OpenApiSchemaValidator(IOpenApiSchema schema, IJsonSerializer serializer)
    {
        _schema = schema;
        _serializer = serializer;
    }

    public IReadOnlyList<ContractViolation> Validate(string json, string path, PartialValidationConfig? partialValidation = null)
    {
        var violations = new List<ContractViolation>();

        try
        {
            var node = JsonNode.Parse(json);
            ValidateNode(node, _schema, path, "$", violations, partialValidation);
        }
        catch (JsonException ex)
        {
            violations.Add(new ContractViolation(
                path,
                "$",
                $"Invalid JSON: {ex.Message}",
                ViolationType.InvalidType));
        }

        return violations;
    }

    public string GenerateSample()
    {
        var sample = GenerateSampleValue(_schema);
        return JsonSerializer.Serialize(sample);
    }

    private void ValidateNode(
        JsonNode? node,
        IOpenApiSchema schema,
        string endpoint,
        string jsonPath,
        List<ContractViolation> violations,
        PartialValidationConfig? partialValidation)
    {
        // Handle null - in v3, check if schema type includes "null" or if nullable types are allowed
        if (node == null)
        {
            var schemaType = schema.Type;
            var allowsNull = schemaType?.HasFlag(JsonSchemaType.Null) == true;
            if (!allowsNull)
            {
                violations.Add(new ContractViolation(
                    endpoint,
                    jsonPath,
                    "Value is null but schema does not allow null",
                    ViolationType.UnexpectedNull,
                    "non-null value",
                    "null"));
            }
            return;
        }

        var schemaTypeStr = GetSchemaTypeString(schema);

        // Handle anyOf, oneOf, allOf
        var anyOf = schema.AnyOf;
        var oneOf = schema.OneOf;
        if (anyOf?.Count > 0 || oneOf?.Count > 0)
        {
            var subSchemas = anyOf?.Count > 0 ? anyOf : oneOf!;

            // Try discriminator-based resolution first (strict mode)
            if (TryGetDiscriminatorSchema(node, schema, subSchemas, out var discriminatorSchema, out var discriminatorValue))
            {
                if (discriminatorValue == null)
                {
                    // Discriminator property missing from object
                    var discriminator = schema.Discriminator;
                    violations.Add(new ContractViolation(
                        endpoint,
                        $"{jsonPath}.{discriminator?.PropertyName}",
                        $"Missing required discriminator property '{discriminator?.PropertyName}'",
                        ViolationType.MissingRequired,
                        discriminator?.PropertyName ?? "unknown",
                        "missing"));
                    return;
                }

                if (discriminatorSchema == null)
                {
                    // Discriminator value doesn't map to any schema
                    var validValues = GetValidDiscriminatorValues(schema, subSchemas);
                    var discriminator = schema.Discriminator;
                    violations.Add(new ContractViolation(
                        endpoint,
                        $"{jsonPath}.{discriminator?.PropertyName}",
                        $"Discriminator value '{discriminatorValue}' does not match any known schema",
                        ViolationType.DiscriminatorMismatch,
                        string.Join(", ", validValues),
                        discriminatorValue));
                    return;
                }

                // Validate ONLY against the discriminator-selected schema
                ValidateNode(node, discriminatorSchema, endpoint, jsonPath, violations, partialValidation);
                return;
            }

            // Fallback to sequential matching (no discriminator present)
            var anyValid = false;
            foreach (var subSchema in subSchemas)
            {
                var subViolations = new List<ContractViolation>();
                ValidateNode(node, subSchema, endpoint, jsonPath, subViolations, partialValidation);
                if (subViolations.Count == 0)
                {
                    anyValid = true;
                    break;
                }
            }
            if (!anyValid)
            {
                violations.Add(new ContractViolation(
                    endpoint,
                    jsonPath,
                    "Value does not match any of the allowed schemas",
                    ViolationType.InvalidType));
            }
            return;
        }

        var allOf = schema.AllOf;
        if (allOf?.Count > 0)
        {
            // For allOf, all schemas must pass
            foreach (var subSchema in allOf)
            {
                ValidateNode(node, subSchema, endpoint, jsonPath, violations, partialValidation);
            }
            return;
        }

        switch (schemaTypeStr)
        {
            case "object":
                ValidateObject(node, schema, endpoint, jsonPath, violations, partialValidation);
                break;

            case "array":
                ValidateArray(node, schema, endpoint, jsonPath, violations, partialValidation);
                break;

            case "string":
                ValidateString(node, schema, endpoint, jsonPath, violations);
                break;

            case "integer":
                ValidateInteger(node, schema, endpoint, jsonPath, violations);
                break;

            case "number":
                ValidateNumber(node, schema, endpoint, jsonPath, violations);
                break;

            case "boolean":
                ValidateBoolean(node, endpoint, jsonPath, violations);
                break;

            default:
                // If type is not specified, infer from node or accept any
                var properties = schema.Properties;
                if (properties?.Count > 0)
                {
                    ValidateObject(node, schema, endpoint, jsonPath, violations, partialValidation);
                }
                break;
        }
    }

    private void ValidateObject(
        JsonNode node,
        IOpenApiSchema schema,
        string endpoint,
        string path,
        List<ContractViolation> violations,
        PartialValidationConfig? partialValidation)
    {
        if (node is not JsonObject obj)
        {
            violations.Add(new ContractViolation(
                endpoint,
                path,
                "Expected object",
                ViolationType.InvalidType,
                "object",
                node.GetValueKind().ToString().ToLowerInvariant()));
            return;
        }

        // Check required properties
        var required = schema.Required;
        if (required != null)
        {
            foreach (var requiredProp in required)
            {
                if (partialValidation?.PropertiesToValidate.Count > 0 &&
                    !partialValidation.PropertiesToValidate.Any(p =>
                        p.Equals(requiredProp, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (!obj.ContainsKey(requiredProp))
                {
                    violations.Add(new ContractViolation(
                        endpoint,
                        $"{path}.{requiredProp}",
                        $"Missing required field '{requiredProp}'",
                        ViolationType.MissingRequired));
                }
            }
        }

        // Validate each property
        var properties = schema.Properties;
        if (properties != null)
        {
            foreach (var (propName, propSchema) in properties)
            {
                if (partialValidation?.PropertiesToValidate.Count > 0 &&
                    !partialValidation.PropertiesToValidate.Any(p =>
                        p.Equals(propName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (obj.TryGetPropertyValue(propName, out var propNode))
                {
                    ValidateNode(propNode, propSchema, endpoint, $"{path}.{propName}", violations, null);
                }
            }
        }

        // Check for unexpected fields
        // Respect explicit additionalProperties: false in OpenAPI spec, OR explicit StrictMode
        // Otherwise, extra fields are ignored by default (lenient mode)
        var additionalPropertiesAllowed = schema.AdditionalPropertiesAllowed;
        if (additionalPropertiesAllowed == false || partialValidation?.StrictMode == true)
        {
            foreach (var propName in obj.Select(p => p.Key))
            {
                if (properties == null || !properties.ContainsKey(propName))
                {
                    violations.Add(new ContractViolation(
                        endpoint,
                        $"{path}.{propName}",
                        $"Unexpected field '{propName}'",
                        ViolationType.UnexpectedField));
                }
            }
        }
    }

    private void ValidateArray(
        JsonNode node,
        IOpenApiSchema schema,
        string endpoint,
        string path,
        List<ContractViolation> violations,
        PartialValidationConfig? partialValidation)
    {
        if (node is not JsonArray arr)
        {
            violations.Add(new ContractViolation(
                endpoint,
                path,
                "Expected array",
                ViolationType.InvalidType,
                "array",
                node.GetValueKind().ToString().ToLowerInvariant()));
            return;
        }

        // Validate min/max items
        var minItems = schema.MinItems;
        if (minItems.HasValue && arr.Count < minItems.Value)
        {
            violations.Add(new ContractViolation(
                endpoint,
                path,
                $"Array has fewer items than minimum",
                ViolationType.OutOfRange,
                $"at least {minItems} items",
                arr.Count.ToString()));
        }

        var maxItems = schema.MaxItems;
        if (maxItems.HasValue && arr.Count > maxItems.Value)
        {
            violations.Add(new ContractViolation(
                endpoint,
                path,
                $"Array has more items than maximum",
                ViolationType.OutOfRange,
                $"at most {maxItems} items",
                arr.Count.ToString()));
        }

        // Validate items
        var items = schema.Items;
        if (items != null)
        {
            for (int i = 0; i < arr.Count; i++)
            {
                ValidateNode(arr[i], items, endpoint, $"{path}[{i}]", violations, partialValidation);
            }
        }
    }

    private void ValidateString(JsonNode node, IOpenApiSchema schema, string endpoint, string path, List<ContractViolation> violations)
    {
        if (node.GetValueKind() != JsonValueKind.String)
        {
            violations.Add(new ContractViolation(
                endpoint,
                path,
                "Expected string",
                ViolationType.InvalidType,
                "string",
                node.GetValueKind().ToString().ToLowerInvariant()));
            return;
        }

        var value = node.GetValue<string>();

        // Validate enum
        var enumValues = schema.Enum;
        if (enumValues?.Count > 0)
        {
            var enumStrings = enumValues
                .Select(e => e is JsonValue jv && jv.TryGetValue<string>(out var s) ? s : e?.ToString())
                .ToList();

            if (!enumStrings.Contains(value))
            {
                violations.Add(new ContractViolation(
                    endpoint,
                    path,
                    $"Value is not one of the allowed enum values",
                    ViolationType.InvalidEnumValue,
                    string.Join(", ", enumStrings.Where(s => s != null)),
                    value));
            }
        }

        // Validate min/max length
        var minLength = schema.MinLength;
        if (minLength.HasValue && value.Length < minLength.Value)
        {
            violations.Add(new ContractViolation(
                endpoint,
                path,
                $"String is shorter than minimum length",
                ViolationType.OutOfRange,
                $"at least {minLength} characters",
                value.Length.ToString()));
        }

        var maxLength = schema.MaxLength;
        if (maxLength.HasValue && value.Length > maxLength.Value)
        {
            violations.Add(new ContractViolation(
                endpoint,
                path,
                $"String is longer than maximum length",
                ViolationType.OutOfRange,
                $"at most {maxLength} characters",
                value.Length.ToString()));
        }

        // Validate pattern
        var pattern = schema.Pattern;
        if (!string.IsNullOrEmpty(pattern))
        {
            if (!Regex.IsMatch(value, pattern))
            {
                violations.Add(new ContractViolation(
                    endpoint,
                    path,
                    $"Value does not match pattern",
                    ViolationType.PatternMismatch,
                    pattern,
                    value));
            }
        }

        // Validate format
        var format = schema.Format;
        if (!string.IsNullOrEmpty(format))
        {
            var isValid = format.ToLowerInvariant() switch
            {
                "email" => IsValidEmail(value),
                "uri" or "url" => Uri.TryCreate(value, UriKind.Absolute, out _),
                "uuid" => Guid.TryParse(value, out _),
                "date-time" => DateTime.TryParse(value, out _),
                "date" => DateOnly.TryParse(value, out _),
                "time" => TimeOnly.TryParse(value, out _),
                "ipv4" => System.Net.IPAddress.TryParse(value, out var ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork,
                "ipv6" => System.Net.IPAddress.TryParse(value, out var ip6) && ip6.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6,
                _ => true
            };

            if (!isValid)
            {
                violations.Add(new ContractViolation(
                    endpoint,
                    path,
                    $"Value does not match format '{format}'",
                    ViolationType.InvalidFormat,
                    format,
                    value));
            }
        }
    }

    private void ValidateInteger(JsonNode node, IOpenApiSchema schema, string endpoint, string path, List<ContractViolation> violations)
    {
        var kind = node.GetValueKind();
        if (kind != JsonValueKind.Number)
        {
            violations.Add(new ContractViolation(
                endpoint,
                path,
                "Expected integer",
                ViolationType.InvalidType,
                "integer",
                kind.ToString().ToLowerInvariant()));
            return;
        }

        if (!node.AsValue().TryGetValue<long>(out var value))
        {
            violations.Add(new ContractViolation(
                endpoint,
                path,
                "Expected integer but got decimal",
                ViolationType.InvalidType));
            return;
        }

        ValidateNumericConstraints(value, schema, endpoint, path, violations);
    }

    private void ValidateNumber(JsonNode node, IOpenApiSchema schema, string endpoint, string path, List<ContractViolation> violations)
    {
        if (node.GetValueKind() != JsonValueKind.Number)
        {
            violations.Add(new ContractViolation(
                endpoint,
                path,
                "Expected number",
                ViolationType.InvalidType,
                "number",
                node.GetValueKind().ToString().ToLowerInvariant()));
            return;
        }

        var value = node.GetValue<decimal>();
        ValidateNumericConstraints(value, schema, endpoint, path, violations);
    }

    private void ValidateNumericConstraints(decimal value, IOpenApiSchema schema, string endpoint, string path, List<ContractViolation> violations)
    {
        // In OpenAPI 3.1 / Microsoft.OpenApi v3, Minimum/Maximum/ExclusiveMinimum/ExclusiveMaximum are all string values
        // ExclusiveMinimum/Maximum are the actual exclusive boundary values (not boolean flags)
        var minimumStr = schema.Minimum;
        var exclusiveMinimumStr = schema.ExclusiveMinimum;

        // Check exclusive minimum (value must be strictly greater than exclusiveMinimum)
        if (!string.IsNullOrEmpty(exclusiveMinimumStr) && decimal.TryParse(exclusiveMinimumStr, out var exclusiveMinimum))
        {
            if (value <= exclusiveMinimum)
            {
                violations.Add(new ContractViolation(
                    endpoint,
                    path,
                    "Value is not greater than exclusive minimum",
                    ViolationType.OutOfRange,
                    $"> {exclusiveMinimum}",
                    value.ToString()));
                return; // Don't double-report
            }
        }

        // Check inclusive minimum (value must be >= minimum)
        if (!string.IsNullOrEmpty(minimumStr) && decimal.TryParse(minimumStr, out var minimum))
        {
            if (value < minimum)
            {
                violations.Add(new ContractViolation(
                    endpoint,
                    path,
                    "Value is less than minimum",
                    ViolationType.OutOfRange,
                    $">= {minimum}",
                    value.ToString()));
                return; // Don't double-report
            }
        }

        var maximumStr = schema.Maximum;
        var exclusiveMaximumStr = schema.ExclusiveMaximum;

        // Check exclusive maximum (value must be strictly less than exclusiveMaximum)
        if (!string.IsNullOrEmpty(exclusiveMaximumStr) && decimal.TryParse(exclusiveMaximumStr, out var exclusiveMaximum))
        {
            if (value >= exclusiveMaximum)
            {
                violations.Add(new ContractViolation(
                    endpoint,
                    path,
                    "Value is not less than exclusive maximum",
                    ViolationType.OutOfRange,
                    $"< {exclusiveMaximum}",
                    value.ToString()));
                return; // Don't double-report
            }
        }

        // Check inclusive maximum (value must be <= maximum)
        if (!string.IsNullOrEmpty(maximumStr) && decimal.TryParse(maximumStr, out var maximum))
        {
            if (value > maximum)
            {
                violations.Add(new ContractViolation(
                    endpoint,
                    path,
                    "Value is greater than maximum",
                    ViolationType.OutOfRange,
                    $"<= {maximum}",
                    value.ToString()));
            }
        }
    }

    private void ValidateBoolean(JsonNode node, string endpoint, string path, List<ContractViolation> violations)
    {
        var kind = node.GetValueKind();
        if (kind != JsonValueKind.True && kind != JsonValueKind.False)
        {
            violations.Add(new ContractViolation(
                endpoint,
                path,
                "Expected boolean",
                ViolationType.InvalidType,
                "boolean",
                kind.ToString().ToLowerInvariant()));
        }
    }

    private static bool IsValidEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        return Regex.IsMatch(value, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
    }

    private object? GenerateSampleValue(IOpenApiSchema schema)
    {
        // Priority 1: Use example if provided
        var example = schema.Example;
        if (example != null)
        {
            return ConvertJsonNode(example);
        }

        // Priority 2: Use enum values
        var enumValues = schema.Enum;
        if (enumValues?.Count > 0)
        {
            return ConvertJsonNode(enumValues[0]);
        }

        var schemaType = GetSchemaTypeString(schema);

        return schemaType switch
        {
            "object" => GenerateSampleObject(schema),
            "array" => GenerateSampleArray(schema),
            "string" => GenerateSampleString(schema),
            "integer" => GenerateSampleInteger(schema),
            "number" => GenerateSampleNumber(schema),
            "boolean" => true,
            _ => schema.Properties?.Count > 0 ? GenerateSampleObject(schema) : null
        };
    }

    private object GenerateSampleObject(IOpenApiSchema schema)
    {
        var result = new Dictionary<string, object?>();
        var properties = schema.Properties;
        if (properties != null)
        {
            foreach (var (propName, propSchema) in properties)
            {
                result[propName] = GenerateSampleValue(propSchema);
            }
        }
        return result;
    }

    private object GenerateSampleArray(IOpenApiSchema schema)
    {
        var items = schema.Items;
        if (items != null)
        {
            return new[] { GenerateSampleValue(items) };
        }
        return Array.Empty<object>();
    }

    private string GenerateSampleString(IOpenApiSchema schema)
    {
        var format = schema.Format;
        // Use format-specific values
        return format?.ToLowerInvariant() switch
        {
            "email" => "user@example.com",
            "uri" or "url" => "https://example.com",
            "uuid" => Guid.NewGuid().ToString(),
            "date-time" => DateTime.UtcNow.ToString("O"),
            "date" => DateOnly.FromDateTime(DateTime.UtcNow).ToString("O"),
            "time" => TimeOnly.FromDateTime(DateTime.UtcNow).ToString("O"),
            "ipv4" => "192.168.1.1",
            "ipv6" => "::1",
            "hostname" => "example.com",
            "byte" => Convert.ToBase64String("sample"u8.ToArray()),
            _ => "string"
        };
    }

    private long GenerateSampleInteger(IOpenApiSchema schema)
    {
        var minimumStr = schema.Minimum;
        if (!string.IsNullOrEmpty(minimumStr) && decimal.TryParse(minimumStr, out var minimum))
            return (long)minimum;
        var maximumStr = schema.Maximum;
        if (!string.IsNullOrEmpty(maximumStr) && decimal.TryParse(maximumStr, out var maximum))
            return (long)maximum;
        return 1;
    }

    private decimal GenerateSampleNumber(IOpenApiSchema schema)
    {
        var minimumStr = schema.Minimum;
        if (!string.IsNullOrEmpty(minimumStr) && decimal.TryParse(minimumStr, out var minimum))
            return minimum;
        var maximumStr = schema.Maximum;
        if (!string.IsNullOrEmpty(maximumStr) && decimal.TryParse(maximumStr, out var maximum))
            return maximum;
        return 1.0m;
    }

    private static object? ConvertJsonNode(JsonNode? node)
    {
        if (node == null)
            return null;

        return node switch
        {
            JsonValue value => ConvertJsonValue(value),
            JsonArray array => array.Select(ConvertJsonNode).ToArray(),
            JsonObject obj => obj.ToDictionary(kv => kv.Key, kv => ConvertJsonNode(kv.Value)),
            _ => node.ToString()
        };
    }

    private static object? ConvertJsonValue(JsonValue value)
    {
        // Use GetValueKind to determine the actual JSON type, then extract appropriately
        // This is more reliable than TryGetValue<T> which has coercion issues
        var kind = value.GetValueKind();

        return kind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => ExtractNumber(value),
            JsonValueKind.String => value.GetValue<string>(),
            JsonValueKind.Null => null,
            _ => value.ToString()
        };
    }

    private static object ExtractNumber(JsonValue value)
    {
        // Try integer types first (more specific), then fall back to double
        if (value.TryGetValue<int>(out var i)) return i;
        if (value.TryGetValue<long>(out var l)) return l;
        if (value.TryGetValue<double>(out var d)) return d;
        if (value.TryGetValue<decimal>(out var dec)) return dec;
        // Fallback - parse from string representation
        var str = value.ToString();
        if (int.TryParse(str, out var intVal)) return intVal;
        if (long.TryParse(str, out var longVal)) return longVal;
        if (double.TryParse(str, out var doubleVal)) return doubleVal;
        return str;
    }

    private static string? GetSchemaTypeName(IOpenApiSchema schema)
    {
        var schemaType = GetSchemaTypeString(schema);

        // Return normalized type name with first letter capitalized
        return schemaType switch
        {
            "object" => "Object",
            "array" => "Array",
            "string" => "String",
            "integer" => "Integer",
            "number" => "Number",
            "boolean" => "Boolean",
            _ => schema.Properties?.Count > 0 ? "Object" : null
        };
    }

    private static string? GetSchemaTypeString(IOpenApiSchema schema)
    {
        var schemaType = schema.Type;
        if (schemaType == null)
            return null;

        // JsonSchemaType is a flags enum, extract the primary type
        if (schemaType.Value.HasFlag(JsonSchemaType.Object)) return "object";
        if (schemaType.Value.HasFlag(JsonSchemaType.Array)) return "array";
        if (schemaType.Value.HasFlag(JsonSchemaType.String)) return "string";
        if (schemaType.Value.HasFlag(JsonSchemaType.Integer)) return "integer";
        if (schemaType.Value.HasFlag(JsonSchemaType.Number)) return "number";
        if (schemaType.Value.HasFlag(JsonSchemaType.Boolean)) return "boolean";

        return null;
    }

    /// <summary>
    /// Attempts to resolve a schema using the discriminator.
    /// </summary>
    private static bool TryGetDiscriminatorSchema(
        JsonNode? node,
        IOpenApiSchema schema,
        IList<IOpenApiSchema> subSchemas,
        out IOpenApiSchema? discriminatorSchema,
        out string? discriminatorValue)
    {
        discriminatorSchema = null;
        discriminatorValue = null;

        var discriminator = schema.Discriminator;

        // Check if discriminator is defined
        if (discriminator?.PropertyName == null)
        {
            return false; // No discriminator, use fallback
        }

        // Verify node is an object
        if (node is not JsonObject jsonObj)
        {
            return false; // Can't use discriminator on non-objects
        }

        var propName = discriminator.PropertyName;

        // Read discriminator property value
        if (!jsonObj.TryGetPropertyValue(propName, out var propNode) ||
            propNode?.GetValueKind() != JsonValueKind.String)
        {
            // Discriminator property missing or not a string - this is an error
            return true; // discriminatorValue is null, caller will handle
        }

        discriminatorValue = propNode.GetValue<string>();
        var discValue = discriminatorValue; // Local copy for lambda

        // Check explicit mapping first
        if (discriminator.Mapping != null &&
            discriminator.Mapping.TryGetValue(discriminatorValue, out var schemaRef))
        {
            // In v3, schemaRef is OpenApiSchemaReference - use its Id property
            // The Id can be null if manually constructed without a document
            var refId = schemaRef.Id;
            if (!string.IsNullOrEmpty(refId))
            {
                discriminatorSchema = ResolveSchemaFromReference(refId, subSchemas);
            }
            else
            {
                // Fallback: try to find by discriminator value matching schema name
                discriminatorSchema = subSchemas.FirstOrDefault(s =>
                    GetSchemaName(s)?.Equals(discValue, StringComparison.OrdinalIgnoreCase) == true);
            }
            return true;
        }

        // Fallback: match discriminator value to schema name (implicit mapping)
        discriminatorSchema = subSchemas.FirstOrDefault(s =>
            GetSchemaName(s)?.Equals(discValue, StringComparison.OrdinalIgnoreCase) == true);

        return true;
    }

    /// <summary>
    /// Resolves a schema from a reference string within the subschemas.
    /// </summary>
    private static IOpenApiSchema? ResolveSchemaFromReference(string reference, IList<IOpenApiSchema> subSchemas)
    {
        // Reference format: "#/components/schemas/SchemaName"
        var schemaName = reference.Split('/').LastOrDefault();
        if (schemaName == null) return null;

        // Find in subSchemas by matching name
        return subSchemas.FirstOrDefault(s =>
            GetSchemaName(s)?.Equals(schemaName, StringComparison.OrdinalIgnoreCase) == true);
    }

    /// <summary>
    /// Gets the name of a schema from its reference or title.
    /// </summary>
    private static string? GetSchemaName(IOpenApiSchema schema)
    {
        // In v3, check if schema is a reference type
        if (schema is OpenApiSchemaReference schemaRef)
        {
            return schemaRef.Id;
        }

        // Fall back to Title
        return schema.Title;
    }

    /// <summary>
    /// Gets the valid discriminator values for error reporting.
    /// </summary>
    private static IEnumerable<string> GetValidDiscriminatorValues(IOpenApiSchema schema, IList<IOpenApiSchema> subSchemas)
    {
        var discriminator = schema.Discriminator;

        // Return explicit mapping keys if present
        if (discriminator?.Mapping?.Count > 0)
        {
            return discriminator.Mapping.Keys;
        }

        // Otherwise, return schema names from subSchemas
        return subSchemas
            .Select(GetSchemaName)
            .Where(name => name != null)
            .Cast<string>();
    }
}
