namespace Treaty.Validation;

/// <summary>
/// Represents a single contract violation found during validation.
/// </summary>
/// <param name="Endpoint">The endpoint where the violation occurred (e.g., "GET /users/123").</param>
/// <param name="Path">The JSON path where the violation occurred (e.g., "$.user.email").</param>
/// <param name="Message">A human-readable description of the violation.</param>
/// <param name="Type">The type of violation.</param>
/// <param name="Expected">The expected value or constraint.</param>
/// <param name="Actual">The actual value that was found.</param>
public sealed record ContractViolation(
    string Endpoint,
    string Path,
    string Message,
    ViolationType Type,
    string? Expected = null,
    string? Actual = null)
{
    /// <inheritdoc/>
    public override string ToString()
    {
        var result = $"  - {Message}";
        if (!string.IsNullOrEmpty(Path) && Path != "$")
        {
            result += $" at path '{Path}'";
        }
        if (Expected != null)
        {
            result += $" (expected: {Expected}";
        }
        if (Actual != null)
        {
            result += Expected != null ? $", got: {Actual})" : $" (got: {Actual})";
        }
        else if (Expected != null)
        {
            result += ")";
        }
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
    InvalidQueryParameterValue,

    /// <summary>Verification timed out.</summary>
    Timeout,

    /// <summary>The discriminator value doesn't match any known schema.</summary>
    DiscriminatorMismatch
}
