using Treaty.Validation;

namespace Treaty.Contracts;

/// <summary>
/// Represents the expected format of a request body.
/// </summary>
public sealed class RequestExpectation
{
    /// <summary>
    /// Gets the expected content type for the request body.
    /// </summary>
    public string? ContentType { get; }

    /// <summary>
    /// Gets the schema validator for the request body.
    /// </summary>
    public ISchemaValidator? BodyValidator { get; }

    /// <summary>
    /// Gets whether the request body is required.
    /// </summary>
    public bool IsRequired { get; }

    /// <summary>
    /// Gets the partial validation configuration, if any.
    /// </summary>
    public PartialValidationConfig? PartialValidation { get; }

    internal RequestExpectation(
        string? contentType,
        ISchemaValidator? bodyValidator,
        bool isRequired,
        PartialValidationConfig? partialValidation = null)
    {
        ContentType = contentType;
        BodyValidator = bodyValidator;
        IsRequired = isRequired;
        PartialValidation = partialValidation;
    }
}
