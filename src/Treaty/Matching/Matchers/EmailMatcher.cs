using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Treaty.Validation;

namespace Treaty.Matching.Matchers;

/// <summary>
/// Matches valid email address format.
/// </summary>
internal sealed partial class EmailMatcher : IMatcher
{
    // Simple email regex that covers most valid email formats
    private static readonly Regex EmailPattern = CreateEmailRegex();

    public MatcherType Type => MatcherType.Email;

    public string Description => "a valid email address";

    public IReadOnlyList<ContractViolation> Validate(JsonNode? node, string endpoint, string path)
    {
        var violations = new List<ContractViolation>();

        if (node == null)
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is null but expected an email address",
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
                "string (email)", node.GetValueKind().ToString()));
            return violations;
        }

        var value = node.GetValue<string>();
        if (string.IsNullOrEmpty(value) || !EmailPattern.IsMatch(value))
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is not a valid email address",
                ViolationType.InvalidFormat,
                Description, value ?? ""));
        }

        return violations;
    }

    public object GenerateSample() => "user@example.com";

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled)]
    private static partial Regex CreateEmailRegex();
}
