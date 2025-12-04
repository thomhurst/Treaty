using System.Text.Json;
using System.Text.Json.Nodes;
using Treaty.Validation;

namespace Treaty.Matching.Matchers;

/// <summary>
/// Matches a valid ISO 8601 time string (time only, no date).
/// </summary>
internal sealed class TimeOnlyMatcher : IMatcher
{
    public MatcherType Type => MatcherType.TimeOnly;

    public string Description => "a valid ISO 8601 time (HH:mm:ss)";

    public IReadOnlyList<ContractViolation> Validate(JsonNode? node, string endpoint, string path)
    {
        var violations = new List<ContractViolation>();

        if (node == null)
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is null but expected a time",
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
                "string (time)", node.GetValueKind().ToString()));
            return violations;
        }

        var value = node.GetValue<string>();
        if (string.IsNullOrEmpty(value) || !System.TimeOnly.TryParse(value, out _))
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is not a valid time",
                ViolationType.InvalidFormat,
                Description, value ?? ""));
        }

        return violations;
    }

    public object GenerateSample() => System.TimeOnly.FromDateTime(DateTime.UtcNow).ToString("O");
}
