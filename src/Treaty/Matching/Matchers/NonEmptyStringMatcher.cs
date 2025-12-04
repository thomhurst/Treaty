using System.Text.Json;
using System.Text.Json.Nodes;
using Treaty.Validation;

namespace Treaty.Matching.Matchers;

/// <summary>
/// Matches any non-null, non-empty string.
/// </summary>
internal sealed class NonEmptyStringMatcher : IMatcher
{
    public MatcherType Type => MatcherType.NonEmptyString;

    public string Description => "a non-empty string";

    public IReadOnlyList<ContractViolation> Validate(JsonNode? node, string endpoint, string path)
    {
        var violations = new List<ContractViolation>();

        if (node == null)
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is null but expected a non-empty string",
                ViolationType.UnexpectedNull,
                Description, "null"));
            return violations;
        }

        if (node.GetValueKind() != JsonValueKind.String)
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is not a string",
                ViolationType.InvalidType,
                "string", node.GetValueKind().ToString()));
            return violations;
        }

        var value = node.GetValue<string>();
        if (string.IsNullOrEmpty(value))
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "String is empty but must be non-empty",
                ViolationType.InvalidFormat,
                Description, "\"\""));
        }

        return violations;
    }

    public object GenerateSample() => "string";
}
