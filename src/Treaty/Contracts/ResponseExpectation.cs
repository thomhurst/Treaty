using Treaty.Validation;

namespace Treaty.Contracts;

/// <summary>
/// Represents the expected format of a response.
/// </summary>
public sealed class ResponseExpectation
{
    /// <summary>
    /// Gets the expected HTTP status code.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Gets the expected content type for the response body.
    /// </summary>
    public string? ContentType { get; }

    /// <summary>
    /// Gets the schema validator for the response body.
    /// </summary>
    public ISchemaValidator? BodyValidator { get; }

    /// <summary>
    /// Gets the expected headers in the response.
    /// </summary>
    public IReadOnlyDictionary<string, HeaderExpectation> ExpectedHeaders { get; }

    /// <summary>
    /// Gets the partial validation configuration, if any.
    /// </summary>
    public PartialValidationConfig? PartialValidation { get; }

    internal ResponseExpectation(
        int statusCode,
        string? contentType,
        ISchemaValidator? bodyValidator,
        IReadOnlyDictionary<string, HeaderExpectation> expectedHeaders,
        PartialValidationConfig? partialValidation)
    {
        StatusCode = statusCode;
        ContentType = contentType;
        BodyValidator = bodyValidator;
        ExpectedHeaders = expectedHeaders;
        PartialValidation = partialValidation;
    }
}

/// <summary>
/// Configuration for partial validation of response bodies.
/// </summary>
public sealed class PartialValidationConfig
{
    /// <summary>
    /// Gets the property paths to validate. If empty, all properties are validated.
    /// </summary>
    public IReadOnlyList<string> PropertiesToValidate { get; }

    /// <summary>
    /// Gets whether to ignore extra fields not defined in the schema.
    /// </summary>
    public bool IgnoreExtraFields { get; }

    internal PartialValidationConfig(IReadOnlyList<string> propertiesToValidate, bool ignoreExtraFields)
    {
        PropertiesToValidate = propertiesToValidate;
        IgnoreExtraFields = ignoreExtraFields;
    }
}
