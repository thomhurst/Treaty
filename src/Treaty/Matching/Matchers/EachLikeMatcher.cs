using System.Text.Json;
using System.Text.Json.Nodes;
using Treaty.Validation;

namespace Treaty.Matching.Matchers;

/// <summary>
/// Matches an array where each item matches the example structure.
/// </summary>
internal sealed class EachLikeMatcher : IMatcher
{
    private readonly object _example;
    private readonly int _minCount;
    private readonly MatcherSchema _itemSchema;

    public EachLikeMatcher(object example, int minCount = 1)
    {
        _example = example ?? throw new ArgumentNullException(nameof(example));
        _minCount = minCount;

        // Build schema from example
        _itemSchema = MatcherSchema.FromObject(example);
    }

    public MatcherType Type => MatcherType.EachLike;

    public string Description => _minCount > 0
        ? $"an array with at least {_minCount} item(s) matching the example"
        : "an array where each item matches the example";

    public IReadOnlyList<ContractViolation> Validate(JsonNode? node, string endpoint, string path)
    {
        var violations = new List<ContractViolation>();

        if (node == null)
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is null but expected an array",
                ViolationType.UnexpectedNull,
                Description, "null"));
            return violations;
        }

        if (node.GetValueKind() != JsonValueKind.Array)
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                "Value is not an array",
                ViolationType.InvalidType,
                "array", node.GetValueKind().ToString()));
            return violations;
        }

        var array = node.AsArray();

        // Check minimum count
        if (array.Count < _minCount)
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                $"Array has {array.Count} items but requires at least {_minCount}",
                ViolationType.OutOfRange,
                $"at least {_minCount} items", array.Count.ToString()));
        }

        // Validate each item against the schema
        var validator = new MatcherSchemaValidator(_itemSchema);
        for (int i = 0; i < array.Count; i++)
        {
            var item = array[i];
            var itemPath = $"{path}[{i}]";

            if (item == null)
            {
                violations.Add(new ContractViolation(
                    endpoint, itemPath,
                    "Array item is null",
                    ViolationType.UnexpectedNull,
                    "item matching example", "null"));
                continue;
            }

            // Serialize item and validate
            var itemJson = item.ToJsonString();
            var itemViolations = validator.Validate(itemJson, endpoint, null);

            // Adjust paths to include array index
            foreach (var violation in itemViolations)
            {
                var adjustedPath = violation.Path.StartsWith("$.")
                    ? $"{itemPath}.{violation.Path[2..]}"
                    : violation.Path == "$"
                        ? itemPath
                        : $"{itemPath}{violation.Path}";

                violations.Add(new ContractViolation(
                    violation.Endpoint,
                    adjustedPath,
                    violation.Message,
                    violation.Type,
                    violation.Expected,
                    violation.Actual));
            }
        }

        return violations;
    }

    public object GenerateSample()
    {
        var sample = _itemSchema.GenerateSampleValue();
        return new[] { sample };
    }
}
