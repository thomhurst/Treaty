using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Treaty.Contracts;
using Treaty.Serialization;

namespace Treaty.Validation;

/// <summary>
/// Validates JSON content against a type schema.
/// </summary>
internal sealed class TypeSchemaValidator : ISchemaValidator
{
    private readonly JsonTypeSchema _schema;
    private readonly IJsonSerializer _serializer;

    public Type? ExpectedType => _schema.ClrType;

    public TypeSchemaValidator(JsonTypeSchema schema, IJsonSerializer serializer)
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
            ValidateNode(node, _schema, path, "", violations, partialValidation);
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
        // Use generic serialization since sample is Dictionary/object[], not the actual CLR type
        // The dictionary keys are already in JSON property names (e.g., camelCase)
        return JsonSerializer.Serialize(sample, new JsonSerializerOptions
        {
            WriteIndented = false
        });
    }

    private void ValidateNode(
        JsonNode? node,
        JsonTypeSchema schema,
        string endpoint,
        string jsonPath,
        List<ContractViolation> violations,
        PartialValidationConfig? partialValidation)
    {
        var currentPath = string.IsNullOrEmpty(jsonPath) ? "$" : jsonPath;

        // Handle null
        if (node == null)
        {
            if (!schema.IsNullable)
            {
                violations.Add(new ContractViolation(
                    endpoint,
                    currentPath,
                    "Value is null but schema does not allow null",
                    ViolationType.UnexpectedNull,
                    "non-null value",
                    "null"));
            }
            return;
        }

        switch (schema.SchemaType)
        {
            case JsonSchemaType.Object:
                ValidateObject(node, schema, endpoint, currentPath, violations, partialValidation);
                break;

            case JsonSchemaType.Array:
                ValidateArray(node, schema, endpoint, currentPath, violations, partialValidation);
                break;

            case JsonSchemaType.String:
                ValidateString(node, schema, endpoint, currentPath, violations);
                break;

            case JsonSchemaType.Integer:
                ValidateInteger(node, endpoint, currentPath, violations);
                break;

            case JsonSchemaType.Number:
                ValidateNumber(node, endpoint, currentPath, violations);
                break;

            case JsonSchemaType.Boolean:
                ValidateBoolean(node, endpoint, currentPath, violations);
                break;
        }
    }

    private void ValidateObject(
        JsonNode node,
        JsonTypeSchema schema,
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
        foreach (var required in schema.Required)
        {
            // Skip if partial validation is configured and this property isn't in the list
            if (partialValidation?.PropertiesToValidate.Count > 0 &&
                !partialValidation.PropertiesToValidate.Any(p =>
                    p.Equals(required, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (!obj.ContainsKey(required))
            {
                var propSchema = schema.Properties.GetValueOrDefault(required);
                var clrName = propSchema?.ClrName ?? required;

                violations.Add(new ContractViolation(
                    endpoint,
                    $"{path}.{required}",
                    $"Missing required field '{clrName}'",
                    ViolationType.MissingRequired));
            }
        }

        // Validate each property
        foreach (var (propName, propSchema) in schema.Properties)
        {
            // Skip if partial validation is configured and this property isn't in the list
            if (partialValidation?.PropertiesToValidate.Count > 0 &&
                !partialValidation.PropertiesToValidate.Any(p =>
                    p.Equals(propSchema.ClrName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (obj.TryGetPropertyValue(propName, out var propNode))
            {
                var propPath = $"{path}.{propName}";

                // Check if there's a matcher override for this property
                if (partialValidation?.MatcherConfig?.PropertyMatchers.TryGetValue(propSchema.ClrName ?? propName, out var matcher) == true)
                {
                    // Use matcher validation instead of type validation
                    var matcherViolations = matcher.Validate(propNode, endpoint, propPath);
                    violations.AddRange(matcherViolations);
                }
                else
                {
                    ValidateNode(propNode, propSchema.TypeSchema, endpoint, propPath, violations, null);
                }
            }
        }

        // Check for unexpected fields
        if (partialValidation?.IgnoreExtraFields != true && !schema.AdditionalPropertiesAllowed)
        {
            foreach (var propName in obj.Select(p => p.Key))
            {
                if (!schema.Properties.ContainsKey(propName))
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
        JsonTypeSchema schema,
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

        if (schema.ItemSchema != null)
        {
            for (int i = 0; i < arr.Count; i++)
            {
                ValidateNode(arr[i], schema.ItemSchema, endpoint, $"{path}[{i}]", violations, partialValidation);
            }
        }
    }

    private void ValidateString(JsonNode node, JsonTypeSchema schema, string endpoint, string path, List<ContractViolation> violations)
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

        // Validate format if specified
        if (!string.IsNullOrEmpty(schema.Format))
        {
            var isValid = schema.Format.ToLowerInvariant() switch
            {
                "email" => IsValidEmail(value),
                "uri" or "url" => Uri.TryCreate(value, UriKind.Absolute, out _),
                "uuid" or "guid" => Guid.TryParse(value, out _),
                "date-time" => DateTime.TryParse(value, out _),
                "date" => DateOnly.TryParse(value, out _),
                "time" => TimeOnly.TryParse(value, out _),
                "ipv4" => System.Net.IPAddress.TryParse(value, out var ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork,
                "ipv6" => System.Net.IPAddress.TryParse(value, out var ip6) && ip6.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6,
                _ => true // Unknown formats pass
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

    private void ValidateInteger(JsonNode node, string endpoint, string path, List<ContractViolation> violations)
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

        // Check if it's actually an integer (no decimal part)
        if (!node.AsValue().TryGetValue<long>(out _))
        {
            var value = node.GetValue<double>();
            if (value != Math.Floor(value))
            {
                violations.Add(new ContractViolation(
                    endpoint,
                    path,
                    "Expected integer but got decimal",
                    ViolationType.InvalidType,
                    "integer",
                    value.ToString()));
            }
        }
    }

    private void ValidateNumber(JsonNode node, string endpoint, string path, List<ContractViolation> violations)
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

        // Basic email validation
        return Regex.IsMatch(value, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
    }

    private object? GenerateSampleValue(JsonTypeSchema schema)
    {
        if (schema.IsNullable)
            return null;

        return schema.SchemaType switch
        {
            JsonSchemaType.Object => GenerateSampleObject(schema),
            JsonSchemaType.Array => GenerateSampleArray(schema),
            JsonSchemaType.String => GenerateSampleString(schema),
            JsonSchemaType.Integer => 1,
            JsonSchemaType.Number => 1.0,
            JsonSchemaType.Boolean => true,
            _ => null
        };
    }

    private object GenerateSampleObject(JsonTypeSchema schema)
    {
        var result = new Dictionary<string, object?>();
        foreach (var (propName, propSchema) in schema.Properties)
        {
            result[propName] = GenerateSampleValue(propSchema.TypeSchema);
        }
        return result;
    }

    private object GenerateSampleArray(JsonTypeSchema schema)
    {
        if (schema.ItemSchema != null)
        {
            return new[] { GenerateSampleValue(schema.ItemSchema) };
        }
        return Array.Empty<object>();
    }

    private string GenerateSampleString(JsonTypeSchema schema)
    {
        return schema.Format?.ToLowerInvariant() switch
        {
            "email" => "user@example.com",
            "uri" or "url" => "https://example.com",
            "uuid" or "guid" => Guid.Empty.ToString(),
            "date-time" => DateTime.UtcNow.ToString("O"),
            "date" => DateOnly.FromDateTime(DateTime.UtcNow).ToString("O"),
            "time" => TimeOnly.FromDateTime(DateTime.UtcNow).ToString("O"),
            "ipv4" => "127.0.0.1",
            "ipv6" => "::1",
            _ => "string"
        };
    }
}
