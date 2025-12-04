using Treaty.Contracts;
using Treaty.Serialization;
using Treaty.Validation;

namespace Treaty;

/// <summary>
/// Builder for request body expectations.
/// </summary>
public sealed class RequestExpectationBuilder
{
    private string _contentType = "application/json";
    private Type? _bodyType;
    private bool _isRequired = true;

    /// <summary>
    /// Specifies the expected content type.
    /// </summary>
    /// <param name="contentType">The content type (e.g., "application/json").</param>
    /// <returns>This builder for chaining.</returns>
    public RequestExpectationBuilder WithContentType(string contentType)
    {
        _contentType = contentType;
        return this;
    }

    /// <summary>
    /// Specifies that the request body should match a JSON schema derived from the given type.
    /// </summary>
    /// <typeparam name="T">The expected type of the request body.</typeparam>
    /// <returns>This builder for chaining.</returns>
    public RequestExpectationBuilder WithJsonBody<T>()
    {
        _bodyType = typeof(T);
        _contentType = "application/json";
        return this;
    }

    /// <summary>
    /// Specifies that the request body should match a JSON schema derived from the given type.
    /// </summary>
    /// <param name="type">The expected type of the request body.</param>
    /// <returns>This builder for chaining.</returns>
    public RequestExpectationBuilder WithJsonBody(Type type)
    {
        _bodyType = type;
        _contentType = "application/json";
        return this;
    }

    /// <summary>
    /// Specifies that the request body is optional.
    /// </summary>
    /// <returns>This builder for chaining.</returns>
    public RequestExpectationBuilder Optional()
    {
        _isRequired = false;
        return this;
    }

    internal RequestExpectation Build(IJsonSerializer serializer)
    {
        ISchemaValidator? validator = null;
        if (_bodyType != null)
        {
            var schema = serializer.GetSchema(_bodyType);
            validator = new TypeSchemaValidator(schema, serializer);
        }

        return new RequestExpectation(_contentType, validator, _isRequired);
    }
}
