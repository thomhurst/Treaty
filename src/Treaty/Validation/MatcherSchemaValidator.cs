using System.Text.Json;
using System.Text.Json.Nodes;
using Treaty.Contracts;
using Treaty.Matching;

namespace Treaty.Validation;

/// <summary>
/// Validates JSON content against a matcher-based schema.
/// </summary>
internal sealed class MatcherSchemaValidator : ISchemaValidator
{
    private readonly MatcherSchema _schema;

    public MatcherSchemaValidator(MatcherSchema schema)
    {
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
    }

    public Type? ExpectedType => null;

    public IReadOnlyList<ContractViolation> Validate(string json, string endpoint, PartialValidationConfig? partialValidation = null)
    {
        var violations = new List<ContractViolation>();

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            violations.Add(new ContractViolation(
                endpoint, "$",
                $"Invalid JSON: {ex.Message}",
                ViolationType.InvalidFormat));
            return violations;
        }

        ValidateNode(root, _schema, endpoint, "$", violations, partialValidation);
        return violations;
    }

    public string GenerateSample() => _schema.GenerateSample();

    private void ValidateNode(
        JsonNode? node,
        MatcherSchema schema,
        string endpoint,
        string path,
        List<ContractViolation> violations,
        PartialValidationConfig? partialValidation)
    {
        // If schema has a matcher, use it directly
        if (schema.Matcher != null)
        {
            var matcherViolations = schema.Matcher.Validate(node, endpoint, path);
            violations.AddRange(matcherViolations);
            return;
        }

        // Handle array schema
        if (schema.IsArray)
        {
            ValidateArray(node, schema, endpoint, path, violations, partialValidation);
            return;
        }

        // Handle object schema
        if (schema.IsObject)
        {
            ValidateObject(node, schema, endpoint, path, violations, partialValidation);
            return;
        }
    }

    private void ValidateArray(
        JsonNode? node,
        MatcherSchema schema,
        string endpoint,
        string path,
        List<ContractViolation> violations,
        PartialValidationConfig? partialValidation)
    {
        if (node == null)
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is null but expected an array",
                ViolationType.UnexpectedNull,
                "array", "null"));
            return;
        }

        if (node.GetValueKind() != JsonValueKind.Array)
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is not an array",
                ViolationType.InvalidType,
                "array", node.GetValueKind().ToString()));
            return;
        }

        var array = node.AsArray();

        // Validate each item if we have an item schema
        if (schema.ItemSchema != null)
        {
            for (int i = 0; i < array.Count; i++)
            {
                var itemPath = $"{path}[{i}]";
                ValidateNode(array[i], schema.ItemSchema, endpoint, itemPath, violations, partialValidation);
            }
        }
    }

    private void ValidateObject(
        JsonNode? node,
        MatcherSchema schema,
        string endpoint,
        string path,
        List<ContractViolation> violations,
        PartialValidationConfig? partialValidation)
    {
        if (node == null)
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is null but expected an object",
                ViolationType.UnexpectedNull,
                "object", "null"));
            return;
        }

        if (node.GetValueKind() != JsonValueKind.Object)
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is not an object",
                ViolationType.InvalidType,
                "object", node.GetValueKind().ToString()));
            return;
        }

        var obj = node.AsObject();

        // Check required properties
        foreach (var requiredProp in schema.Required)
        {
            // Skip if partial validation is active and property not in list
            if (partialValidation?.PropertiesToValidate.Count > 0 &&
                !partialValidation.PropertiesToValidate.Any(p =>
                    p.Equals(requiredProp, StringComparison.OrdinalIgnoreCase) ||
                    schema.Properties.TryGetValue(requiredProp, out var prop) &&
                    p.Equals(prop.ClrName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (!obj.ContainsKey(requiredProp))
            {
                violations.Add(new ContractViolation(
                    endpoint, $"{path}.{requiredProp}",
                    $"Missing required property '{requiredProp}'",
                    ViolationType.MissingRequired));
            }
        }

        // Validate each property
        foreach (var (propName, propSchema) in schema.Properties)
        {
            // Skip if partial validation is active and property not in list
            if (partialValidation?.PropertiesToValidate.Count > 0 &&
                !partialValidation.PropertiesToValidate.Any(p =>
                    p.Equals(propName, StringComparison.OrdinalIgnoreCase) ||
                    p.Equals(propSchema.ClrName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (obj.TryGetPropertyValue(propName, out var propValue))
            {
                var propPath = $"{path}.{propName}";
                ValidateNode(propValue, propSchema.Schema, endpoint, propPath, violations, partialValidation);
            }
        }

        // Check for unexpected properties (unless IgnoreExtraFields is set)
        if (partialValidation?.IgnoreExtraFields != true)
        {
            foreach (var (actualProp, _) in obj)
            {
                if (!schema.Properties.ContainsKey(actualProp))
                {
                    violations.Add(new ContractViolation(
                        endpoint, $"{path}.{actualProp}",
                        $"Unexpected property '{actualProp}'",
                        ViolationType.UnexpectedField));
                }
            }
        }
    }
}
