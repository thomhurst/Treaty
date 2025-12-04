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

    internal RequestExpectation(string? contentType, ISchemaValidator? bodyValidator, bool isRequired)
    {
        ContentType = contentType;
        BodyValidator = bodyValidator;
        IsRequired = isRequired;
    }
}
