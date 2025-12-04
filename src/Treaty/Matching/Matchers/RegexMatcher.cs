using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Treaty.Validation;

namespace Treaty.Matching.Matchers;

/// <summary>
/// Matches a string value against a regex pattern.
/// </summary>
internal sealed class RegexMatcher : IMatcher
{
    private readonly string _pattern;
    private readonly Regex _regex;

    public RegexMatcher(string pattern)
    {
        _pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
        _regex = new Regex(pattern, RegexOptions.Compiled);
    }

    public MatcherType Type => MatcherType.Regex;

    public string Description => $"a string matching pattern '{_pattern}'";

    public IReadOnlyList<ContractViolation> Validate(JsonNode? node, string endpoint, string path)
    {
        var violations = new List<ContractViolation>();

        if (node == null)
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is null but expected a string matching pattern",
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
        if (!_regex.IsMatch(value ?? ""))
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                $"Value does not match pattern '{_pattern}'",
                ViolationType.PatternMismatch,
                _pattern, value ?? ""));
        }

        return violations;
    }

    public object GenerateSample() => $"<matches {_pattern}>";
}
