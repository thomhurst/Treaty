namespace Treaty.Diagnostics;

/// <summary>
/// Represents a difference between expected and actual JSON at a specific path.
/// </summary>
public sealed class JsonDiff
{
    /// <summary>
    /// Gets the JSON path where the difference occurred (e.g., "$.user.address.street").
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the type of difference.
    /// </summary>
    public DiffType Type { get; }

    /// <summary>
    /// Gets the expected value (from the contract schema).
    /// </summary>
    public string? Expected { get; }

    /// <summary>
    /// Gets the actual value (from the response).
    /// </summary>
    public string? Actual { get; }

    /// <summary>
    /// Gets an optional description of the difference.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Creates a new JSON diff entry.
    /// </summary>
    public JsonDiff(string path, DiffType type, string? expected, string? actual, string? description = null)
    {
        Path = path;
        Type = type;
        Expected = expected;
        Actual = actual;
        Description = description;
    }

    /// <summary>
    /// Creates a diff for a field that is present in actual but not expected.
    /// </summary>
    public static JsonDiff Added(string path, string actualValue)
        => new(path, DiffType.Added, null, actualValue, "Field present in response but not in contract schema");

    /// <summary>
    /// Creates a diff for a field that is expected but missing from actual.
    /// </summary>
    public static JsonDiff Removed(string path, string expectedValue)
        => new(path, DiffType.Removed, expectedValue, null, "Required field missing from response");

    /// <summary>
    /// Creates a diff for a field with different values.
    /// </summary>
    public static JsonDiff Changed(string path, string expectedValue, string actualValue)
        => new(path, DiffType.Changed, expectedValue, actualValue, "Value differs from expected");

    /// <summary>
    /// Creates a diff for a field with a type mismatch.
    /// </summary>
    public static JsonDiff TypeMismatch(string path, string expectedType, string actualType)
        => new(path, DiffType.TypeMismatch, expectedType, actualType, $"Expected type '{expectedType}', got '{actualType}'");

    /// <inheritdoc/>
    public override string ToString()
    {
        return Type switch
        {
            DiffType.Added => $"+ {Path}: {Actual}",
            DiffType.Removed => $"- {Path}: {Expected}",
            DiffType.Changed => $"~ {Path}: {Expected} → {Actual}",
            DiffType.TypeMismatch => $"! {Path}: expected {Expected}, got {Actual}",
            _ => $"{Path}: {Description}"
        };
    }
}

/// <summary>
/// Type of difference between expected and actual JSON.
/// </summary>
public enum DiffType
{
    /// <summary>
    /// Field is present in actual but not in expected schema.
    /// </summary>
    Added,

    /// <summary>
    /// Field is expected but missing from actual.
    /// </summary>
    Removed,

    /// <summary>
    /// Field value differs between expected and actual.
    /// </summary>
    Changed,

    /// <summary>
    /// Field type differs between expected and actual.
    /// </summary>
    TypeMismatch
}
