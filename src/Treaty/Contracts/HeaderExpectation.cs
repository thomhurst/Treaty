namespace Treaty.Contracts;

/// <summary>
/// Represents an expectation for an HTTP header.
/// </summary>
public sealed class HeaderExpectation
{
    /// <summary>
    /// Gets the header name (case-insensitive comparison is used).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets whether this header is required.
    /// </summary>
    public bool IsRequired { get; }

    /// <summary>
    /// Gets the expected value pattern (regex). Null means any value is accepted.
    /// </summary>
    public string? ValuePattern { get; }

    /// <summary>
    /// Gets the exact expected value. Null means any value matching the pattern is accepted.
    /// </summary>
    public string? ExactValue { get; }

    internal HeaderExpectation(string name, bool isRequired, string? valuePattern, string? exactValue)
    {
        Name = name;
        IsRequired = isRequired;
        ValuePattern = valuePattern;
        ExactValue = exactValue;
    }

    /// <summary>
    /// Creates a required header expectation with any value.
    /// </summary>
    public static HeaderExpectation Required(string name) => new(name, true, null, null);

    /// <summary>
    /// Creates a required header expectation with an exact value.
    /// </summary>
    public static HeaderExpectation RequiredWithValue(string name, string value) => new(name, true, null, value);

    /// <summary>
    /// Creates an optional header expectation.
    /// </summary>
    public static HeaderExpectation Optional(string name) => new(name, false, null, null);
}
