using Treaty.Validation;

namespace Treaty.Contracts;

/// <summary>
/// Specifies the direction of validation for readOnly/writeOnly constraint handling.
/// </summary>
public enum ValidationDirection
{
    /// <summary>
    /// Validating a request body. ReadOnly fields should not be present.
    /// </summary>
    Request,

    /// <summary>
    /// Validating a response body. WriteOnly fields should not be present.
    /// </summary>
    Response,

    /// <summary>
    /// Direction-agnostic validation (default). Skips readOnly/writeOnly checks for backward compatibility.
    /// </summary>
    Both
}

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
    /// Gets the sample generator for the response body.
    /// </summary>
    public ISchemaGenerator? BodyGenerator { get; }

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
        ISchemaGenerator? bodyGenerator,
        IReadOnlyDictionary<string, HeaderExpectation> expectedHeaders,
        PartialValidationConfig? partialValidation)
    {
        StatusCode = statusCode;
        ContentType = contentType;
        BodyValidator = bodyValidator;
        BodyGenerator = bodyGenerator;
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
    /// Pre-configured validation for request bodies (readOnly fields should not be present).
    /// </summary>
    public static PartialValidationConfig ForRequest { get; } = new([], false, ValidationDirection.Request);

    /// <summary>
    /// Pre-configured validation for response bodies (writeOnly fields should not be present).
    /// </summary>
    public static PartialValidationConfig ForResponse { get; } = new([], false, ValidationDirection.Response);

    /// <summary>
    /// Gets the property paths to validate. If empty, all properties are validated.
    /// </summary>
    public IReadOnlyList<string> PropertiesToValidate { get; }

    /// <summary>
    /// Gets whether strict validation is enabled. When true, extra fields not defined in the schema will cause violations.
    /// Default is false (lenient mode - extra fields are ignored for better forward compatibility).
    /// </summary>
    public bool StrictMode { get; }

    /// <summary>
    /// Gets the validation direction for readOnly/writeOnly constraint handling.
    /// Default is <see cref="ValidationDirection.Both"/> which skips these checks for backward compatibility.
    /// </summary>
    public ValidationDirection Direction { get; }

    internal PartialValidationConfig(
        IReadOnlyList<string> propertiesToValidate,
        bool strictMode = false,
        ValidationDirection direction = ValidationDirection.Both)
    {
        PropertiesToValidate = propertiesToValidate;
        StrictMode = strictMode;
        Direction = direction;
    }

    /// <summary>
    /// Returns a config with the specified direction, preserving other settings from the original config if provided.
    /// </summary>
    internal PartialValidationConfig WithDirection(ValidationDirection direction)
    {
        if (Direction == direction)
        {
            return this;
        }
        return new PartialValidationConfig(PropertiesToValidate, StrictMode, direction);
    }

    /// <summary>
    /// Ensures the config has the specified direction. If null, returns a new config with the direction.
    /// If the config already has the direction, returns as-is. Otherwise, creates a new config preserving other settings.
    /// </summary>
    internal static PartialValidationConfig EnsureDirection(PartialValidationConfig? existing, ValidationDirection direction)
    {
        if (existing == null)
        {
            return direction == ValidationDirection.Request ? ForRequest : ForResponse;
        }

        return existing.WithDirection(direction);
    }
}
