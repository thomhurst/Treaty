using System.Text.Json;
using System.Text.Json.Nodes;
using Treaty.Validation;

namespace Treaty.Matching.Matchers;

/// <summary>
/// Matches any value of a specific CLR type.
/// </summary>
internal sealed class TypeMatcher : IMatcher
{
    private readonly Type _expectedType;
    private readonly object? _example;

    public TypeMatcher(Type expectedType, object? example = null)
    {
        _expectedType = expectedType ?? throw new ArgumentNullException(nameof(expectedType));
        _example = example;
    }

    public MatcherType Type => MatcherType.Type;

    public string Description => $"a value of type {GetTypeName(_expectedType)}";

    public IReadOnlyList<ContractViolation> Validate(JsonNode? node, string endpoint, string path)
    {
        var violations = new List<ContractViolation>();

        // Handle null
        if (node == null)
        {
            if (IsNullableType(_expectedType))
            {
                return violations; // Null is valid for nullable types
            }

            violations.Add(new ContractViolation(
                endpoint, path,
                $"Value is null but expected {GetTypeName(_expectedType)}",
                ViolationType.UnexpectedNull,
                Description, "null"));
            return violations;
        }

        var kind = node.GetValueKind();
        var expectedKind = GetExpectedJsonKind(_expectedType);

        // Special handling for numbers - both Integer and Number match numeric types
        if (expectedKind == JsonValueKind.Number && kind == JsonValueKind.Number)
        {
            return violations; // Valid
        }

        // Check if JSON kind matches expected
        if (kind != expectedKind && !(expectedKind == JsonValueKind.True && (kind == JsonValueKind.True || kind == JsonValueKind.False)))
        {
            violations.Add(new ContractViolation(
                endpoint, path,
                $"Value type mismatch: expected {GetTypeName(_expectedType)}",
                ViolationType.InvalidType,
                GetTypeName(_expectedType), kind.ToString()));
        }

        return violations;
    }

    public object? GenerateSample()
    {
        if (_example != null)
            return _example;

        // Generate sample based on type
        if (_expectedType == typeof(string)) return "string";
        if (_expectedType == typeof(int) || _expectedType == typeof(int?)) return 1;
        if (_expectedType == typeof(long) || _expectedType == typeof(long?)) return 1L;
        if (_expectedType == typeof(double) || _expectedType == typeof(double?)) return 1.0;
        if (_expectedType == typeof(decimal) || _expectedType == typeof(decimal?)) return 1.0m;
        if (_expectedType == typeof(bool) || _expectedType == typeof(bool?)) return true;
        if (_expectedType == typeof(Guid) || _expectedType == typeof(Guid?)) return Guid.NewGuid().ToString();
        if (_expectedType == typeof(DateTime) || _expectedType == typeof(DateTime?)) return DateTime.UtcNow.ToString("O");
        if (_expectedType == typeof(DateTimeOffset) || _expectedType == typeof(DateTimeOffset?)) return DateTimeOffset.UtcNow.ToString("O");

        return null;
    }

    private static JsonValueKind GetExpectedJsonKind(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (underlying == typeof(string) || underlying == typeof(Guid) ||
            underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset) ||
            underlying == typeof(DateOnly) || underlying == typeof(TimeOnly) ||
            underlying == typeof(Uri))
            return JsonValueKind.String;

        if (underlying == typeof(int) || underlying == typeof(long) ||
            underlying == typeof(short) || underlying == typeof(byte) ||
            underlying == typeof(uint) || underlying == typeof(ulong) ||
            underlying == typeof(ushort) || underlying == typeof(sbyte) ||
            underlying == typeof(double) || underlying == typeof(float) ||
            underlying == typeof(decimal))
            return JsonValueKind.Number;

        if (underlying == typeof(bool))
            return JsonValueKind.True;

        if (underlying.IsArray || (underlying.IsGenericType &&
            (underlying.GetGenericTypeDefinition() == typeof(List<>) ||
             underlying.GetGenericTypeDefinition() == typeof(IEnumerable<>) ||
             underlying.GetGenericTypeDefinition() == typeof(IList<>) ||
             underlying.GetGenericTypeDefinition() == typeof(ICollection<>))))
            return JsonValueKind.Array;

        return JsonValueKind.Object;
    }

    private static bool IsNullableType(Type type)
    {
        return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
    }

    private static string GetTypeName(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
            return $"{underlying.Name}?";

        return type.Name;
    }
}
