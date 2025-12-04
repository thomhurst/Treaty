using System.Linq.Expressions;
using Treaty.Contracts;
using Treaty.Serialization;
using Treaty.Validation;

namespace Treaty;

/// <summary>
/// Builder for response expectations.
/// </summary>
public sealed class ResponseExpectationBuilder
{
    private int _statusCode = 200;
    private string? _contentType;
    private Type? _bodyType;
    private readonly Dictionary<string, HeaderExpectation> _headers = new(StringComparer.OrdinalIgnoreCase);
    private PartialValidationBuilder? _partialValidationBuilder;

    /// <summary>
    /// Specifies the expected HTTP status code.
    /// </summary>
    /// <param name="statusCode">The expected status code.</param>
    /// <returns>This builder for chaining.</returns>
    public ResponseExpectationBuilder WithStatus(int statusCode)
    {
        _statusCode = statusCode;
        return this;
    }

    /// <summary>
    /// Specifies the expected content type.
    /// </summary>
    /// <param name="contentType">The content type (e.g., "application/json").</param>
    /// <returns>This builder for chaining.</returns>
    public ResponseExpectationBuilder WithContentType(string contentType)
    {
        _contentType = contentType;
        return this;
    }

    /// <summary>
    /// Specifies that the response body should match a JSON schema derived from the given type.
    /// </summary>
    /// <typeparam name="T">The expected type of the response body.</typeparam>
    /// <returns>This builder for chaining.</returns>
    /// <example>
    /// <code>
    /// .WithJsonBody&lt;User&gt;()
    /// </code>
    /// </example>
    public ResponseExpectationBuilder WithJsonBody<T>()
    {
        _bodyType = typeof(T);
        _contentType ??= "application/json";
        return this;
    }

    /// <summary>
    /// Specifies that the response body should match a JSON schema derived from the given type,
    /// with partial validation support.
    /// </summary>
    /// <typeparam name="T">The expected type of the response body.</typeparam>
    /// <param name="configure">Action to configure partial validation.</param>
    /// <returns>This builder for chaining.</returns>
    /// <example>
    /// <code>
    /// .WithJsonBody&lt;User&gt;(v => v
    ///     .OnlyValidate(u => u.Id, u => u.Email)
    ///     .IgnoreExtraFields())
    /// </code>
    /// </example>
    public ResponseExpectationBuilder WithJsonBody<T>(Action<PartialValidationBuilder<T>> configure)
    {
        _bodyType = typeof(T);
        _contentType ??= "application/json";

        var builder = new PartialValidationBuilder<T>();
        configure(builder);
        _partialValidationBuilder = builder;
        return this;
    }

    /// <summary>
    /// Specifies that the response body should match a JSON schema derived from the given type.
    /// </summary>
    /// <param name="type">The expected type of the response body.</param>
    /// <returns>This builder for chaining.</returns>
    public ResponseExpectationBuilder WithJsonBody(Type type)
    {
        _bodyType = type;
        _contentType ??= "application/json";
        return this;
    }

    /// <summary>
    /// Specifies a required header in the response.
    /// </summary>
    /// <param name="headerName">The header name.</param>
    /// <returns>This builder for chaining.</returns>
    public ResponseExpectationBuilder WithHeader(string headerName)
    {
        _headers[headerName] = HeaderExpectation.Required(headerName);
        return this;
    }

    /// <summary>
    /// Specifies a required header with a specific value in the response.
    /// </summary>
    /// <param name="headerName">The header name.</param>
    /// <param name="value">The expected value.</param>
    /// <returns>This builder for chaining.</returns>
    public ResponseExpectationBuilder WithHeader(string headerName, string value)
    {
        _headers[headerName] = HeaderExpectation.RequiredWithValue(headerName, value);
        return this;
    }

    internal ResponseExpectation Build(IJsonSerializer serializer)
    {
        ISchemaValidator? validator = null;
        if (_bodyType != null)
        {
            var schema = serializer.GetSchema(_bodyType);
            validator = new TypeSchemaValidator(schema, serializer);
        }

        var partialConfig = _partialValidationBuilder?.Build();
        return new ResponseExpectation(_statusCode, _contentType, validator, _headers, partialConfig);
    }
}

/// <summary>
/// Builder for partial validation configuration.
/// </summary>
public abstract class PartialValidationBuilder
{
    internal abstract PartialValidationConfig Build();
}

/// <summary>
/// Builder for partial validation configuration with type safety.
/// </summary>
/// <typeparam name="T">The type being validated.</typeparam>
public sealed class PartialValidationBuilder<T> : PartialValidationBuilder
{
    private readonly List<string> _propertiesToValidate = [];
    private bool _ignoreExtraFields;

    /// <summary>
    /// Specifies which properties to validate. Only these properties will be checked.
    /// </summary>
    /// <param name="properties">Expressions selecting the properties to validate.</param>
    /// <returns>This builder for chaining.</returns>
    /// <example>
    /// <code>
    /// .OnlyValidate(u => u.Id, u => u.Email)
    /// </code>
    /// </example>
    public PartialValidationBuilder<T> OnlyValidate(params Expression<Func<T, object?>>[] properties)
    {
        foreach (var property in properties)
        {
            var memberExpr = property.Body as MemberExpression
                ?? (property.Body as UnaryExpression)?.Operand as MemberExpression;

            if (memberExpr != null)
            {
                _propertiesToValidate.Add(memberExpr.Member.Name);
            }
        }
        return this;
    }

    /// <summary>
    /// Specifies that extra fields in the response (not defined in the schema) should be ignored.
    /// </summary>
    /// <returns>This builder for chaining.</returns>
    public PartialValidationBuilder<T> IgnoreExtraFields()
    {
        _ignoreExtraFields = true;
        return this;
    }

    internal override PartialValidationConfig Build()
    {
        return new PartialValidationConfig(_propertiesToValidate, _ignoreExtraFields);
    }
}
