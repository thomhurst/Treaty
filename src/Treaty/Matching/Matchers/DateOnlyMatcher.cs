using System.Text.Json;
using System.Text.Json.Nodes;
using Treaty.Validation;

namespace Treaty.Matching.Matchers;

/// <summary>
/// Matches a valid ISO 8601 date string (date only, no time).
/// </summary>
internal sealed class DateOnlyMatcher : IMatcher
{
    public MatcherType Type => MatcherType.DateOnly;

    public string Description => "a valid ISO 8601 date (YYYY-MM-DD)";

    public IReadOnlyList<ContractViolation> Validate(JsonNode? node, string endpoint, string path)
    {
        var violations = new List<ContractViolation>();

        if (node == null)
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is null but expected a date",
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
                "string (date)", node.GetValueKind().ToString()));
            return violations;
        }

        var value = node.GetValue<string>();
        if (string.IsNullOrEmpty(value) || !System.DateOnly.TryParse(value, out _))
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is not a valid date",
                ViolationType.InvalidFormat,
                Description, value ?? ""));
        }

        return violations;
    }

    public object GenerateSample() => System.DateOnly.FromDateTime(DateTime.UtcNow).ToString("O");
}
