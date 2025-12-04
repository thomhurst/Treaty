using System.Text.Json;
using System.Text.Json.Nodes;
using Treaty.Validation;

namespace Treaty.Matching.Matchers;

/// <summary>
/// Matches a valid absolute URI.
/// </summary>
internal sealed class UriMatcher : IMatcher
{
    public MatcherType Type => MatcherType.Uri;

    public string Description => "a valid URI";

    public IReadOnlyList<ContractViolation> Validate(JsonNode? node, string endpoint, string path)
    {
        var violations = new List<ContractViolation>();

        if (node == null)
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is null but expected a URI",
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
                "string (URI)", node.GetValueKind().ToString()));
            return violations;
        }

        var value = node.GetValue<string>();
        if (string.IsNullOrEmpty(value) || !System.Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is not a valid absolute URI",
                ViolationType.InvalidFormat,
                Description, value ?? ""));
        }

        return violations;
    }

    public object GenerateSample() => "https://example.com";
}
