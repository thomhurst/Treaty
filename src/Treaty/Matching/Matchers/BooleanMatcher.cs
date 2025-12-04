using System.Text.Json;
using System.Text.Json.Nodes;
using Treaty.Validation;

namespace Treaty.Matching.Matchers;

/// <summary>
/// Matches any boolean value (true or false).
/// </summary>
internal sealed class BooleanMatcher : IMatcher
{
    public MatcherType Type => MatcherType.Boolean;

    public string Description => "a boolean (true or false)";

    public IReadOnlyList<ContractViolation> Validate(JsonNode? node, string endpoint, string path)
    {
        var violations = new List<ContractViolation>();

        if (node == null)
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is null but expected a boolean",
                ViolationType.UnexpectedNull,
                Description, "null"));
            return violations;
        }

        var kind = node.GetValueKind();
        if (kind != JsonValueKind.True && kind != JsonValueKind.False)
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is not a boolean",
                ViolationType.InvalidType,
                "boolean", kind.ToString()));
        }

        return violations;
    }

    public object GenerateSample() => true;
}
