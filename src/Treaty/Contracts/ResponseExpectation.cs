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
    [Obsolete("Use StrictMode instead. Extra fields are now ignored by default (lenient mode). Set StrictMode = true to reject extra fields.")]
    public bool IgnoreExtraFields { get; }

    /// <summary>
    /// Gets whether strict validation is enabled. When true, extra fields not defined in the schema will cause violations.
    /// Default is false (lenient mode - extra fields are ignored for better forward compatibility).
    /// </summary>
    public bool StrictMode { get; }

    /// <summary>
    /// Gets the matcher overrides for specific properties.
    /// </summary>
    public MatcherValidationConfig? MatcherConfig { get; }

    internal PartialValidationConfig(
        IReadOnlyList<string> propertiesToValidate,
        bool ignoreExtraFields,
        MatcherValidationConfig? matcherConfig = null,
        bool strictMode = false)
    {
        PropertiesToValidate = propertiesToValidate;
        IgnoreExtraFields = ignoreExtraFields;
        StrictMode = strictMode;
        MatcherConfig = matcherConfig;
    }
}

/// <summary>
/// Configuration for matcher-based property validation overrides.
/// </summary>
public sealed class MatcherValidationConfig
{
    /// <summary>
    /// Gets the property matchers keyed by property name (CLR name).
    /// </summary>
    public IReadOnlyDictionary<string, Matching.IMatcher> PropertyMatchers { get; }

    internal MatcherValidationConfig(IReadOnlyDictionary<string, Matching.IMatcher> propertyMatchers)
    {
        PropertyMatchers = propertyMatchers;
    }
}
