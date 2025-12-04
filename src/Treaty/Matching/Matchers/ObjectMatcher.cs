using System.Text.Json;
using System.Text.Json.Nodes;
using Treaty.Validation;

namespace Treaty.Matching.Matchers;

/// <summary>
/// Matches a nested object with its own matcher schema.
/// </summary>
internal sealed class ObjectMatcher : IMatcher
{
    private readonly object _schema;
    private readonly MatcherSchema _matcherSchema;

    public ObjectMatcher(object schema)
    {
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        _matcherSchema = MatcherSchema.FromObject(schema);
    }

    public MatcherType Type => MatcherType.Object;

    public string Description => "an object matching the schema";

    public IReadOnlyList<ContractViolation> Validate(JsonNode? node, string endpoint, string path)
    {
        var violations = new List<ContractViolation>();

        if (node == null)
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is null but expected an object",
                ViolationType.UnexpectedNull,
                Description, "null"));
            return violations;
        }

        if (node.GetValueKind() != JsonValueKind.Object)
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is not an object",
                ViolationType.InvalidType,
                "object", node.GetValueKind().ToString()));
            return violations;
        }

        // Validate against the nested schema
        var validator = new MatcherSchemaValidator(_matcherSchema);
        var json = node.ToJsonString();
        var nestedViolations = validator.Validate(json, endpoint, null);

        // Adjust paths to include current path
        foreach (var violation in nestedViolations)
        {
            var adjustedPath = violation.Path.StartsWith("$.")
                ? $"{path}.{violation.Path[2..]}"
                : violation.Path == "$"
                    ? path
                    : $"{path}{violation.Path}";

            violations.Add(new ContractViolation(
                violation.Endpoint,
                adjustedPath,
                violation.Message,
                violation.Type,
                violation.Expected,
                violation.Actual));
        }

        return violations;
    }

    public object? GenerateSample() => _matcherSchema.GenerateSampleValue();
}
