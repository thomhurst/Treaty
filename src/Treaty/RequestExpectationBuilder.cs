using System.Linq.Expressions;
using Treaty.Contracts;
using Treaty.Matching;
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
    private object? _matcherSchema;
    private bool _isRequired = true;
    private RequestPartialValidationBuilder? _partialValidationBuilder;

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
    /// Specifies that the request body should match a JSON schema derived from the given type,
    /// with partial validation support.
    /// </summary>
    /// <typeparam name="T">The expected type of the request body.</typeparam>
    /// <param name="configure">Action to configure partial validation.</param>
    /// <returns>This builder for chaining.</returns>
    /// <example>
    /// <code>
    /// .WithJsonBody&lt;CreateUserRequest&gt;(v => v
    ///     .WithMatcher(r => r.RequestId, Match.Guid())
    ///     .WithMatcher(r => r.Timestamp, Match.DateTime()))
    /// </code>
    /// </example>
    public RequestExpectationBuilder WithJsonBody<T>(Action<RequestPartialValidationBuilder<T>> configure)
    {
        _bodyType = typeof(T);
        _contentType = "application/json";

        var builder = new RequestPartialValidationBuilder<T>();
        configure(builder);
        _partialValidationBuilder = builder;
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
    /// Specifies that the request body should match a schema defined by matchers.
    /// Use Match.* methods to define flexible matching rules.
    /// </summary>
    /// <param name="matcherSchema">An anonymous object with Match.* matchers defining the expected structure.</param>
    /// <returns>This builder for chaining.</returns>
    /// <example>
    /// <code>
    /// .WithMatcherSchema(new {
    ///     requestId = Match.Guid(),
    ///     userId = Match.Integer(min: 1),
    ///     timestamp = Match.DateTime(),
    ///     action = Match.OneOf("create", "update", "delete")
    /// })
    /// </code>
    /// </example>
    public RequestExpectationBuilder WithMatcherSchema(object matcherSchema)
    {
        _matcherSchema = matcherSchema ?? throw new ArgumentNullException(nameof(matcherSchema));
        _bodyType = null; // Clear any type-based schema
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
        PartialValidationConfig? partialConfig = null;

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
            partialConfig = _partialValidationBuilder?.Build();
        }

        return new RequestExpectation(_contentType, validator, _isRequired, partialConfig);
    }
}

/// <summary>
/// Builder for request partial validation configuration.
/// </summary>
public abstract class RequestPartialValidationBuilder
{
    internal abstract PartialValidationConfig Build();
}

/// <summary>
/// Builder for request partial validation configuration with type safety.
/// </summary>
/// <typeparam name="T">The type being validated.</typeparam>
public sealed class RequestPartialValidationBuilder<T> : RequestPartialValidationBuilder
{
    private readonly List<string> _propertiesToValidate = [];
    private readonly Dictionary<string, IMatcher> _propertyMatchers = new();
    private bool _ignoreExtraFields;
    private bool _strictMode;

    /// <summary>
    /// Specifies which properties to validate. Only these properties will be checked.
    /// </summary>
    /// <param name="properties">Expressions selecting the properties to validate.</param>
    /// <returns>This builder for chaining.</returns>
    /// <example>
    /// <code>
    /// .OnlyValidate(r => r.UserId, r => r.Action)
    /// </code>
    /// </example>
    public RequestPartialValidationBuilder<T> OnlyValidate(params Expression<Func<T, object?>>[] properties)
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
    /// Enables strict validation mode. Extra fields not defined in the schema will cause violations.
    /// By default, Treaty uses lenient mode where extra fields are ignored for better forward compatibility.
    /// </summary>
    /// <returns>This builder for chaining.</returns>
    /// <example>
    /// <code>
    /// .WithJsonBody&lt;CreateUserRequest&gt;(v => v.StrictMode())
    /// </code>
    /// </example>
    public RequestPartialValidationBuilder<T> StrictMode()
    {
        _strictMode = true;
        return this;
    }

    /// <summary>
    /// Specifies that extra fields in the request (not defined in the schema) should be ignored.
    /// This is now the default behavior. This method is kept for backward compatibility.
    /// </summary>
    /// <returns>This builder for chaining.</returns>
    [Obsolete("Extra fields are now ignored by default (lenient mode). This method is no longer needed. Use StrictMode() to reject extra fields.")]
    public RequestPartialValidationBuilder<T> IgnoreExtraFields()
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
    /// .WithMatcher(r => r.RequestId, Match.Guid())
    /// .WithMatcher(r => r.Timestamp, Match.DateTime())
    /// </code>
    /// </example>
    public RequestPartialValidationBuilder<T> WithMatcher<TProp>(Expression<Func<T, TProp>> property, IMatcher matcher)
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

        return new PartialValidationConfig(_propertiesToValidate, _ignoreExtraFields, matcherConfig, _strictMode);
    }
}
