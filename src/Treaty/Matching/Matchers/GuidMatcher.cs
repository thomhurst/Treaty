using System.Text.Json;
using System.Text.Json.Nodes;
using Treaty.Validation;

namespace Treaty.Matching.Matchers;

/// <summary>
/// Matches any valid GUID/UUID string.
/// </summary>
internal sealed class GuidMatcher : IMatcher
{
    public MatcherType Type => MatcherType.Guid;

    public string Description => "a valid GUID/UUID";

    public IReadOnlyList<ContractViolation> Validate(JsonNode? node, string endpoint, string path)
    {
        var violations = new List<ContractViolation>();

        if (node == null)
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is null but expected a GUID string",
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
                "string (GUID)", node.GetValueKind().ToString()));
            return violations;
        }

        var value = node.GetValue<string>();
        if (!System.Guid.TryParse(value, out _))
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is not a valid GUID",
                ViolationType.InvalidFormat,
                Description, value));
        }

        return violations;
    }

    public object GenerateSample() => System.Guid.NewGuid().ToString();
}
