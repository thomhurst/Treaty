using System.Text.Json.Nodes;
using Treaty.Validation;

namespace Treaty.Matching.Matchers;

/// <summary>
/// Matches only null values.
/// </summary>
internal sealed class NullMatcher : IMatcher
{
    public MatcherType Type => MatcherType.Null;

    public string Description => "null";

    public IReadOnlyList<ContractViolation> Validate(JsonNode? node, string endpoint, string path)
    {
        var violations = new List<ContractViolation>();

        if (node != null)
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is not null but expected null",
                ViolationType.InvalidType,
                Description, node.GetValueKind().ToString()));
        }

        return violations;
    }

    public object? GenerateSample() => null;
}
