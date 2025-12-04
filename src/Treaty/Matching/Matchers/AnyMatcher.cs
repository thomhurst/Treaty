using System.Text.Json.Nodes;
using Treaty.Validation;

namespace Treaty.Matching.Matchers;

/// <summary>
/// Matches any value (always passes validation).
/// Use this for properties you don't care about validating.
/// </summary>
internal sealed class AnyMatcher : IMatcher
{
    public MatcherType Type => MatcherType.Any;

    public string Description => "any value";

    public IReadOnlyList<ContractViolation> Validate(JsonNode? node, string endpoint, string path)
    {
        // Always passes - accepts any value including null
        return [];
    }

    public object? GenerateSample() => null;
}
