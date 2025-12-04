namespace Treaty.Validation;

/// <summary>
/// Represents a single contract violation found during validation.
/// </summary>
public sealed class ContractViolation
{
    /// <summary>
    /// Gets the endpoint where the violation occurred (e.g., "GET /users/123").
    /// </summary>
    public string Endpoint { get; }

    /// <summary>
    /// Gets the JSON path where the violation occurred (e.g., "$.user.email").
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets a human-readable description of the violation.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the expected value or constraint.
    /// </summary>
    public string? Expected { get; }

    /// <summary>
    /// Gets the actual value that was found.
    /// </summary>
    public string? Actual { get; }

    /// <summary>
    /// Gets the type of violation.
    /// </summary>
    public ViolationType Type { get; }

    /// <summary>
    /// Creates a new contract violation.
    /// </summary>
    public ContractViolation(
        string endpoint,
        string path,
        string message,
        ViolationType type,
        string? expected = null,
        string? actual = null)
    {
        Endpoint = endpoint;
        Path = path;
        Message = message;
        Type = type;
        Expected = expected;
        Actual = actual;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var result = $"  - {Message}";
        if (!string.IsNullOrEmpty(Path) && Path != "$")
            result += $" at path '{Path}'";
        if (Expected != null)
            result += $" (expected: {Expected}";
        if (Actual != null)
            result += Expected != null ? $", got: {Actual})" : $" (got: {Actual})";
        else if (Expected != null)
            result += ")";
        return result;
    }
}

/// <summary>
/// The type of contract violation.
/// </summary>
public enum ViolationType
{
    /// <summary>A required field is missing.</summary>
    MissingRequired,

    /// <summary>A field has an invalid type.</summary>
    InvalidType,

    /// <summary>A field value doesn't match the expected format.</summary>
    InvalidFormat,

    /// <summary>A value is outside the allowed range.</summary>
    OutOfRange,

    /// <summary>A value doesn't match the allowed enum values.</summary>
    InvalidEnumValue,

    /// <summary>A value doesn't match the expected pattern.</summary>
    PatternMismatch,

    /// <summary>An unexpected status code was returned.</summary>
    UnexpectedStatusCode,

    /// <summary>A required header is missing.</summary>
    MissingHeader,

    /// <summary>A header has an invalid value.</summary>
    InvalidHeaderValue,

    /// <summary>A null value was found where not allowed.</summary>
    UnexpectedNull,

    /// <summary>An extra field was found when not allowed.</summary>
    UnexpectedField,

    /// <summary>The content type doesn't match expected.</summary>
    InvalidContentType,

    /// <summary>A required query parameter is missing.</summary>
    MissingQueryParameter,

    /// <summary>A query parameter has an invalid value.</summary>
    InvalidQueryParameterValue
}
