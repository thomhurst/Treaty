using System.Text.Json;
using System.Text.Json.Nodes;
using Treaty.Validation;

namespace Treaty.Matching.Matchers;

/// <summary>
/// Matches integer values, optionally within a range.
/// </summary>
internal sealed class IntegerMatcher : IMatcher
{
    private readonly long? _min;
    private readonly long? _max;

    public IntegerMatcher(long? min = null, long? max = null)
    {
        _min = min;
        _max = max;
    }

    public MatcherType Type => MatcherType.Integer;

    public string Description
    {
        get
        {
            if (_min.HasValue && _max.HasValue)
                return $"an integer between {_min} and {_max}";
            if (_min.HasValue)
                return $"an integer >= {_min}";
            if (_max.HasValue)
                return $"an integer <= {_max}";
            return "an integer";
        }
    }

    public IReadOnlyList<ContractViolation> Validate(JsonNode? node, string endpoint, string path)
    {
        var violations = new List<ContractViolation>();

        if (node == null)
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is null but expected an integer",
                ViolationType.UnexpectedNull,
                Description, "null"));
            return violations;
        }

        if (node.GetValueKind() != JsonValueKind.Number)
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is not a number",
                ViolationType.InvalidType,
                "integer", node.GetValueKind().ToString()));
            return violations;
        }

        // Try to get as long
        long value;
        try
        {
            value = node.GetValue<long>();
        }
        catch
        {
            // If it can't be converted to long, it might be a decimal
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is not an integer",
                ViolationType.InvalidType,
                "integer", node.ToJsonString()));
            return violations;
        }

        // Check the original JSON to ensure it's not a decimal
        var jsonString = node.ToJsonString();
        if (jsonString.Contains('.'))
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is a decimal, not an integer",
                ViolationType.InvalidType,
                "integer", jsonString));
            return violations;
        }

        // Check range constraints
        if (_min.HasValue && value < _min.Value)
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                $"Value {value} is less than minimum {_min}",
                ViolationType.OutOfRange,
                $">= {_min}", value.ToString()));
        }

        if (_max.HasValue && value > _max.Value)
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                $"Value {value} is greater than maximum {_max}",
                ViolationType.OutOfRange,
                $"<= {_max}", value.ToString()));
        }

        return violations;
    }

    public object GenerateSample()
    {
        if (_min.HasValue && _max.HasValue)
            return (_min.Value + _max.Value) / 2;
        if (_min.HasValue)
            return _min.Value;
        if (_max.HasValue)
            return _max.Value;
        return 1;
    }
}
