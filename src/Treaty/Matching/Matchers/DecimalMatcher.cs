using System.Text.Json;
using System.Text.Json.Nodes;
using Treaty.Validation;

namespace Treaty.Matching.Matchers;

/// <summary>
/// Matches decimal/floating-point values, optionally within a range.
/// </summary>
internal sealed class DecimalMatcher : IMatcher
{
    private readonly decimal? _min;
    private readonly decimal? _max;

    public DecimalMatcher(decimal? min = null, decimal? max = null)
    {
        _min = min;
        _max = max;
    }

    public MatcherType Type => MatcherType.Decimal;

    public string Description
    {
        get
        {
            if (_min.HasValue && _max.HasValue)
                return $"a number between {_min} and {_max}";
            if (_min.HasValue)
                return $"a number >= {_min}";
            if (_max.HasValue)
                return $"a number <= {_max}";
            return "a number";
        }
    }

    public IReadOnlyList<ContractViolation> Validate(JsonNode? node, string endpoint, string path)
    {
        var violations = new List<ContractViolation>();

        if (node == null)
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is null but expected a number",
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
                "number", node.GetValueKind().ToString()));
            return violations;
        }

        decimal value;
        try
        {
            value = node.GetValue<decimal>();
        }
        catch
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value cannot be converted to decimal",
                ViolationType.InvalidType,
                "decimal number", node.ToJsonString()));
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
        return 1.0m;
    }
}
