using System.Text.Json;
using System.Text.Json.Nodes;
using Treaty.Validation;

namespace Treaty.Matching.Matchers;

/// <summary>
/// Matches a valid ISO 8601 date-time string.
/// </summary>
internal sealed class DateTimeMatcher : IMatcher
{
    public MatcherType Type => MatcherType.DateTime;

    public string Description => "a valid ISO 8601 date-time";

    public IReadOnlyList<ContractViolation> Validate(JsonNode? node, string endpoint, string path)
    {
        var violations = new List<ContractViolation>();

        if (node == null)
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is null but expected a date-time",
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
                "string (date-time)", node.GetValueKind().ToString()));
            return violations;
        }

        var value = node.GetValue<string>();
        if (string.IsNullOrEmpty(value) ||
            (!DateTime.TryParse(value, out _) && !DateTimeOffset.TryParse(value, out _)))
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is not a valid date-time",
                ViolationType.InvalidFormat,
                Description, value ?? ""));
        }

        return violations;
    }

    public object GenerateSample() => DateTime.UtcNow.ToString("O");
}
