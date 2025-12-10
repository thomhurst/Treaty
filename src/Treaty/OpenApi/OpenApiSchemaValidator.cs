using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.OpenApi.Models;
using Treaty.Contracts;
using Treaty.Serialization;
using Treaty.Validation;

namespace Treaty.OpenApi;

/// <summary>
/// Validates JSON content against an OpenAPI schema.
/// </summary>
internal sealed class OpenApiSchemaValidator : ISchemaValidator
{
    private readonly OpenApiSchema _schema;
    private readonly IJsonSerializer _serializer;

    public Type? ExpectedType => null;

    public string? SchemaTypeName => GetSchemaTypeName(_schema);

    public OpenApiSchemaValidator(OpenApiSchema schema, IJsonSerializer serializer)
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
        OpenApiSchema schema,
        string endpoint,
        string jsonPath,
        List<ContractViolation> violations,
        PartialValidationConfig? partialValidation)
    {
        // Handle null
        if (node == null)
        {
            if (!schema.Nullable)
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

        var schemaType = schema.Type?.ToLowerInvariant();

        // Handle anyOf, oneOf, allOf
        if (schema.AnyOf?.Count > 0 || schema.OneOf?.Count > 0)
        {
            var subSchemas = schema.AnyOf?.Count > 0 ? schema.AnyOf : schema.OneOf;

            // Try discriminator-based resolution first (strict mode)
            if (TryGetDiscriminatorSchema(node, schema, subSchemas!, out var discriminatorSchema, out var discriminatorValue))
            {
                if (discriminatorValue == null)
                {
                    // Discriminator property missing from object
                    violations.Add(new ContractViolation(
                        endpoint,
                        $"{jsonPath}.{schema.Discriminator!.PropertyName}",
                        $"Missing required discriminator property '{schema.Discriminator.PropertyName}'",
                        ViolationType.MissingRequired,
                        schema.Discriminator.PropertyName,
                        "missing"));
                    return;
                }

                if (discriminatorSchema == null)
                {
                    // Discriminator value doesn't map to any schema
                    var validValues = GetValidDiscriminatorValues(schema, subSchemas!);
                    violations.Add(new ContractViolation(
                        endpoint,
                        $"{jsonPath}.{schema.Discriminator!.PropertyName}",
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
            foreach (var subSchema in subSchemas!)
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

        if (schema.AllOf?.Count > 0)
        {
            // For allOf, all schemas must pass
            foreach (var subSchema in schema.AllOf)
            {
                ValidateNode(node, subSchema, endpoint, jsonPath, violations, partialValidation);
            }
            return;
        }

        switch (schemaType)
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
                if (schema.Properties?.Count > 0)
                {
                    ValidateObject(node, schema, endpoint, jsonPath, violations, partialValidation);
                }
                break;
        }
    }

    private void ValidateObject(
        JsonNode node,
        OpenApiSchema schema,
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
        if (schema.Required != null)
        {
            foreach (var required in schema.Required)
            {
                if (partialValidation?.PropertiesToValidate.Count > 0 &&
                    !partialValidation.PropertiesToValidate.Any(p =>
                        p.Equals(required, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (!obj.ContainsKey(required))
                {
                    violations.Add(new ContractViolation(
                        endpoint,
                        $"{path}.{required}",
                        $"Missing required field '{required}'",
                        ViolationType.MissingRequired));
                }
            }
        }

        // Validate each property
        if (schema.Properties != null)
        {
            foreach (var (propName, propSchema) in schema.Properties)
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
        if (schema.AdditionalPropertiesAllowed == false || partialValidation?.StrictMode == true)
        {
            foreach (var propName in obj.Select(p => p.Key))
            {
                if (schema.Properties == null || !schema.Properties.ContainsKey(propName))
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
        OpenApiSchema schema,
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
        if (schema.MinItems.HasValue && arr.Count < schema.MinItems.Value)
        {
            violations.Add(new ContractViolation(
                endpoint,
                path,
                $"Array has fewer items than minimum",
                ViolationType.OutOfRange,
                $"at least {schema.MinItems} items",
                arr.Count.ToString()));
        }

        if (schema.MaxItems.HasValue && arr.Count > schema.MaxItems.Value)
        {
            violations.Add(new ContractViolation(
                endpoint,
                path,
                $"Array has more items than maximum",
                ViolationType.OutOfRange,
                $"at most {schema.MaxItems} items",
                arr.Count.ToString()));
        }

        // Validate items
        if (schema.Items != null)
        {
            for (int i = 0; i < arr.Count; i++)
            {
                ValidateNode(arr[i], schema.Items, endpoint, $"{path}[{i}]", violations, partialValidation);
            }
        }
    }

    private void ValidateString(JsonNode node, OpenApiSchema schema, string endpoint, string path, List<ContractViolation> violations)
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
        if (schema.Enum?.Count > 0)
        {
            var enumValues = schema.Enum
                .Select(e => e is Microsoft.OpenApi.Any.OpenApiString s ? s.Value : e?.ToString())
                .ToList();

            if (!enumValues.Contains(value))
            {
                violations.Add(new ContractViolation(
                    endpoint,
                    path,
                    $"Value is not one of the allowed enum values",
                    ViolationType.InvalidEnumValue,
                    string.Join(", ", enumValues),
                    value));
            }
        }

        // Validate min/max length
        if (schema.MinLength.HasValue && value.Length < schema.MinLength.Value)
        {
            violations.Add(new ContractViolation(
                endpoint,
                path,
                $"String is shorter than minimum length",
                ViolationType.OutOfRange,
                $"at least {schema.MinLength} characters",
                value.Length.ToString()));
        }

        if (schema.MaxLength.HasValue && value.Length > schema.MaxLength.Value)
        {
            violations.Add(new ContractViolation(
                endpoint,
                path,
                $"String is longer than maximum length",
                ViolationType.OutOfRange,
                $"at most {schema.MaxLength} characters",
                value.Length.ToString()));
        }

        // Validate pattern
        if (!string.IsNullOrEmpty(schema.Pattern))
        {
            if (!Regex.IsMatch(value, schema.Pattern))
            {
                violations.Add(new ContractViolation(
                    endpoint,
                    path,
                    $"Value does not match pattern",
                    ViolationType.PatternMismatch,
                    schema.Pattern,
                    value));
            }
        }

        // Validate format
        if (!string.IsNullOrEmpty(schema.Format))
        {
            var isValid = schema.Format.ToLowerInvariant() switch
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
                    $"Value does not match format '{schema.Format}'",
                    ViolationType.InvalidFormat,
                    schema.Format,
                    value));
            }
        }
    }

    private void ValidateInteger(JsonNode node, OpenApiSchema schema, string endpoint, string path, List<ContractViolation> violations)
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

    private void ValidateNumber(JsonNode node, OpenApiSchema schema, string endpoint, string path, List<ContractViolation> violations)
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

    private void ValidateNumericConstraints(decimal value, OpenApiSchema schema, string endpoint, string path, List<ContractViolation> violations)
    {
        if (schema.Minimum.HasValue)
        {
            if (schema.ExclusiveMinimum == true && value <= schema.Minimum.Value)
            {
                violations.Add(new ContractViolation(
                    endpoint,
                    path,
                    "Value is not greater than minimum",
                    ViolationType.OutOfRange,
                    $"> {schema.Minimum.Value}",
                    value.ToString()));
            }
            else if (value < schema.Minimum.Value)
            {
                violations.Add(new ContractViolation(
                    endpoint,
                    path,
                    "Value is less than minimum",
                    ViolationType.OutOfRange,
                    $">= {schema.Minimum.Value}",
                    value.ToString()));
            }
        }

        if (schema.Maximum.HasValue)
        {
            if (schema.ExclusiveMaximum == true && value >= schema.Maximum.Value)
            {
                violations.Add(new ContractViolation(
                    endpoint,
                    path,
                    "Value is not less than maximum",
                    ViolationType.OutOfRange,
                    $"< {schema.Maximum.Value}",
                    value.ToString()));
            }
            else if (value > schema.Maximum.Value)
            {
                violations.Add(new ContractViolation(
                    endpoint,
                    path,
                    "Value is greater than maximum",
                    ViolationType.OutOfRange,
                    $"<= {schema.Maximum.Value}",
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

    private object? GenerateSampleValue(OpenApiSchema schema)
    {
        // Priority 1: Use example if provided
        if (schema.Example != null)
        {
            return ConvertOpenApiAny(schema.Example);
        }

        // Priority 2: Use enum values
        if (schema.Enum?.Count > 0)
        {
            return ConvertOpenApiAny(schema.Enum[0]);
        }

        var schemaType = schema.Type?.ToLowerInvariant();

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

    private object GenerateSampleObject(OpenApiSchema schema)
    {
        var result = new Dictionary<string, object?>();
        if (schema.Properties != null)
        {
            foreach (var (propName, propSchema) in schema.Properties)
            {
                result[propName] = GenerateSampleValue(propSchema);
            }
        }
        return result;
    }

    private object GenerateSampleArray(OpenApiSchema schema)
    {
        if (schema.Items != null)
        {
            return new[] { GenerateSampleValue(schema.Items) };
        }
        return Array.Empty<object>();
    }

    private string GenerateSampleString(OpenApiSchema schema)
    {
        // Use format-specific values
        return schema.Format?.ToLowerInvariant() switch
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

    private long GenerateSampleInteger(OpenApiSchema schema)
    {
        if (schema.Minimum.HasValue)
            return (long)schema.Minimum.Value;
        if (schema.Maximum.HasValue)
            return (long)schema.Maximum.Value;
        return 1;
    }

    private decimal GenerateSampleNumber(OpenApiSchema schema)
    {
        if (schema.Minimum.HasValue)
            return schema.Minimum.Value;
        if (schema.Maximum.HasValue)
            return schema.Maximum.Value;
        return 1.0m;
    }

    private static object? ConvertOpenApiAny(Microsoft.OpenApi.Any.IOpenApiAny any)
    {
        return any switch
        {
            Microsoft.OpenApi.Any.OpenApiString s => s.Value,
            Microsoft.OpenApi.Any.OpenApiInteger i => i.Value,
            Microsoft.OpenApi.Any.OpenApiLong l => l.Value,
            Microsoft.OpenApi.Any.OpenApiFloat f => f.Value,
            Microsoft.OpenApi.Any.OpenApiDouble d => d.Value,
            Microsoft.OpenApi.Any.OpenApiBoolean b => b.Value,
            Microsoft.OpenApi.Any.OpenApiNull => null,
            Microsoft.OpenApi.Any.OpenApiArray arr => arr.Select(ConvertOpenApiAny).ToArray(),
            Microsoft.OpenApi.Any.OpenApiObject obj => obj.ToDictionary(kv => kv.Key, kv => ConvertOpenApiAny(kv.Value)),
            _ => any?.ToString()
        };
    }

    private static string? GetSchemaTypeName(OpenApiSchema schema)
    {
        var schemaType = schema.Type?.ToLowerInvariant();

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

    /// <summary>
    /// Attempts to resolve a schema using the discriminator.
    /// </summary>
    /// <param name="node">The JSON node being validated.</param>
    /// <param name="schema">The parent schema with oneOf/anyOf.</param>
    /// <param name="subSchemas">The list of subschemas from oneOf/anyOf.</param>
    /// <param name="discriminatorSchema">The resolved schema if successful.</param>
    /// <param name="discriminatorValue">The discriminator property value found.</param>
    /// <returns>True if discriminator resolution was attempted (discriminator present), false otherwise.</returns>
    private static bool TryGetDiscriminatorSchema(
        JsonNode? node,
        OpenApiSchema schema,
        IList<OpenApiSchema> subSchemas,
        out OpenApiSchema? discriminatorSchema,
        out string? discriminatorValue)
    {
        discriminatorSchema = null;
        discriminatorValue = null;

        // Check if discriminator is defined
        if (schema.Discriminator?.PropertyName == null)
        {
            return false; // No discriminator, use fallback
        }

        // Verify node is an object
        if (node is not JsonObject jsonObj)
        {
            return false; // Can't use discriminator on non-objects
        }

        var propName = schema.Discriminator.PropertyName;

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
        if (schema.Discriminator.Mapping != null &&
            schema.Discriminator.Mapping.TryGetValue(discriminatorValue, out var schemaRef))
        {
            discriminatorSchema = ResolveSchemaFromReference(schemaRef, subSchemas);
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
    private static OpenApiSchema? ResolveSchemaFromReference(string reference, IList<OpenApiSchema> subSchemas)
    {
        // Reference format: "#/components/schemas/SchemaName"
        var schemaName = reference.Split('/').LastOrDefault();
        if (schemaName == null) return null;

        // Find in subSchemas by Reference.Id or matching name
        return subSchemas.FirstOrDefault(s =>
            s.Reference?.Id?.Equals(schemaName, StringComparison.OrdinalIgnoreCase) == true ||
            GetSchemaName(s)?.Equals(schemaName, StringComparison.OrdinalIgnoreCase) == true);
    }

    /// <summary>
    /// Gets the name of a schema from its reference or title.
    /// </summary>
    private static string? GetSchemaName(OpenApiSchema schema)
    {
        // Try Reference.Id first (for $ref schemas)
        if (schema.Reference?.Id != null)
            return schema.Reference.Id;

        // Fall back to Title
        return schema.Title;
    }

    /// <summary>
    /// Gets the valid discriminator values for error reporting.
    /// </summary>
    private static IEnumerable<string> GetValidDiscriminatorValues(OpenApiSchema schema, IList<OpenApiSchema> subSchemas)
    {
        // Return explicit mapping keys if present
        if (schema.Discriminator?.Mapping?.Count > 0)
        {
            return schema.Discriminator.Mapping.Keys;
        }

        // Otherwise, return schema names from subSchemas
        return subSchemas
            .Select(GetSchemaName)
            .Where(name => name != null)
            .Cast<string>();
    }
}
