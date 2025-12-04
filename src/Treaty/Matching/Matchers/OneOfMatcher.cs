using System.Text.Json;
using System.Text.Json.Nodes;
using Treaty.Validation;

namespace Treaty.Matching.Matchers;

/// <summary>
/// Matches one of a set of specified values (like an enum).
/// </summary>
internal sealed class OneOfMatcher : IMatcher
{
    private readonly object[] _allowedValues;
    private readonly HashSet<string> _stringValues;

    public OneOfMatcher(params object[] values)
    {
        if (values == null || values.Length == 0)
            throw new ArgumentException("At least one value must be provided", nameof(values));

        _allowedValues = values;
        _stringValues = values.Select(v => JsonSerializer.Serialize(v)).ToHashSet();
    }

    public MatcherType Type => MatcherType.OneOf;

    public string Description => $"one of: {string.Join(", ", _allowedValues.Select(FormatValue))}";

    public IReadOnlyList<ContractViolation> Validate(JsonNode? node, string endpoint, string path)
    {
        var violations = new List<ContractViolation>();

        if (node == null)
        {
            // Check if null is one of the allowed values
            if (_allowedValues.Any(v => v == null))
                return violations;

            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is null but not in allowed values",
                ViolationType.InvalidEnumValue,
                Description, "null"));
            return violations;
        }

        // Serialize the node value and compare with allowed values
        var nodeJson = node.ToJsonString();

        if (!_stringValues.Contains(nodeJson))
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is not one of the allowed values",
                ViolationType.InvalidEnumValue,
                Description, GetDisplayValue(node)));
        }

        return violations;
    }

    public object? GenerateSample() => _allowedValues.FirstOrDefault();

    private static string FormatValue(object? value)
    {
        if (value == null) return "null";
        if (value is string s) return $"\"{s}\"";
        return value.ToString() ?? "null";
    }

    private static string GetDisplayValue(JsonNode node)
    {
        return node.GetValueKind() switch
        {
            JsonValueKind.String => node.GetValue<string>(),
            _ => node.ToJsonString()
        };
    }
}
