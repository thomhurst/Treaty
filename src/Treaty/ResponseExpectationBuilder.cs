using System.Linq.Expressions;
using Treaty.Contracts;
using Treaty.Matching;
using Treaty.Serialization;
using Treaty.Validation;

using MatcherValidationConfig = Treaty.Contracts.MatcherValidationConfig;

namespace Treaty;

/// <summary>
/// Builder for response expectations.
/// </summary>
public sealed class ResponseExpectationBuilder
{
    private int _statusCode = 200;
    private string? _contentType;
    private Type? _bodyType;
    private object? _matcherSchema;
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
    /// Specifies that the response body should match a schema defined by matchers.
    /// Use Match.* methods to define flexible matching rules.
    /// </summary>
    /// <param name="matcherSchema">An anonymous object with Match.* matchers defining the expected structure.</param>
    /// <returns>This builder for chaining.</returns>
    /// <example>
    /// <code>
    /// .WithMatcherSchema(new {
    ///     id = Match.Guid(),
    ///     name = Match.NonEmptyString(),
    ///     email = Match.Email(),
    ///     status = Match.OneOf("active", "inactive"),
    ///     createdAt = Match.DateTime()
    /// })
    /// </code>
    /// </example>
    public ResponseExpectationBuilder WithMatcherSchema(object matcherSchema)
    {
        _matcherSchema = matcherSchema ?? throw new ArgumentNullException(nameof(matcherSchema));
        _bodyType = null; // Clear any type-based schema
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

        if (_matcherSchema != null)
        {
            // Build matcher-based validator
            var schema = MatcherSchema.FromObject(_matcherSchema);
            validator = new MatcherSchemaValidator(schema);
        }
        else if (_bodyType != null)
        {
            // Build type-based validator
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
    private readonly Dictionary<string, IMatcher> _propertyMatchers = new();
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

    /// <summary>
    /// Specifies a matcher for a specific property, overriding the type-based validation.
    /// </summary>
    /// <typeparam name="TProp">The property type.</typeparam>
    /// <param name="property">Expression selecting the property.</param>
    /// <param name="matcher">The matcher to use for this property.</param>
    /// <returns>This builder for chaining.</returns>
    /// <example>
    /// <code>
    /// .WithMatcher(u => u.Id, Match.Guid())
    /// .WithMatcher(u => u.CreatedAt, Match.DateTime())
    /// </code>
    /// </example>
    public PartialValidationBuilder<T> WithMatcher<TProp>(Expression<Func<T, TProp>> property, IMatcher matcher)
    {
        var memberExpr = property.Body as MemberExpression
            ?? (property.Body as UnaryExpression)?.Operand as MemberExpression;

        if (memberExpr != null)
        {
            _propertyMatchers[memberExpr.Member.Name] = matcher;
        }

        return this;
    }

    internal override PartialValidationConfig Build()
    {
        MatcherValidationConfig? matcherConfig = null;
        if (_propertyMatchers.Count > 0)
        {
            matcherConfig = new MatcherValidationConfig(_propertyMatchers);
        }

        return new PartialValidationConfig(_propertiesToValidate, _ignoreExtraFields, matcherConfig);
    }
}
