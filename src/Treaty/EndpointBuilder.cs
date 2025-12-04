using Treaty.Contracts;
using Treaty.Serialization;
using Treaty.Validation;

namespace Treaty;

/// <summary>
/// Builder for defining endpoint expectations.
/// </summary>
public sealed class EndpointBuilder
{
    private readonly ContractBuilder _parent;
    private readonly string _pathTemplate;
    private HttpMethod _method = HttpMethod.Get;
    private RequestExpectationBuilder? _requestBuilder;
    private readonly List<ResponseExpectationBuilder> _responseBuilders = [];
    private readonly Dictionary<string, HeaderExpectation> _headers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, QueryParameterExpectation> _queryParams = new(StringComparer.OrdinalIgnoreCase);

    internal EndpointBuilder(ContractBuilder parent, string pathTemplate)
    {
        _parent = parent;
        _pathTemplate = pathTemplate;
    }

    /// <summary>
    /// Specifies the HTTP method for this endpoint.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <returns>This builder for chaining.</returns>
    public EndpointBuilder WithMethod(HttpMethod method)
    {
        _method = method;
        return this;
    }

    /// <summary>
    /// Specifies that this endpoint expects a request body.
    /// </summary>
    /// <param name="configure">Action to configure request expectations.</param>
    /// <returns>This builder for chaining.</returns>
    public EndpointBuilder ExpectingRequest(Action<RequestExpectationBuilder> configure)
    {
        _requestBuilder = new RequestExpectationBuilder();
        configure(_requestBuilder);
        return this;
    }

    /// <summary>
    /// Specifies the expected response for this endpoint.
    /// </summary>
    /// <param name="configure">Action to configure response expectations.</param>
    /// <returns>This builder for chaining.</returns>
    /// <example>
    /// <code>
    /// .ExpectingResponse(r => r
    ///     .WithStatus(200)
    ///     .WithJsonBody&lt;User&gt;())
    /// </code>
    /// </example>
    public EndpointBuilder ExpectingResponse(Action<ResponseExpectationBuilder> configure)
    {
        var builder = new ResponseExpectationBuilder();
        configure(builder);
        _responseBuilders.Add(builder);
        return this;
    }

    /// <summary>
    /// Specifies a required header for requests to this endpoint.
    /// </summary>
    /// <param name="headerName">The header name.</param>
    /// <returns>This builder for chaining.</returns>
    public EndpointBuilder WithHeader(string headerName)
    {
        _headers[headerName] = HeaderExpectation.Required(headerName);
        return this;
    }

    /// <summary>
    /// Specifies a required header with a specific value for requests to this endpoint.
    /// </summary>
    /// <param name="headerName">The header name.</param>
    /// <param name="value">The expected value.</param>
    /// <returns>This builder for chaining.</returns>
    public EndpointBuilder WithHeader(string headerName, string value)
    {
        _headers[headerName] = HeaderExpectation.RequiredWithValue(headerName, value);
        return this;
    }

    /// <summary>
    /// Specifies a required query parameter for this endpoint.
    /// </summary>
    /// <param name="paramName">The parameter name.</param>
    /// <param name="type">The parameter type.</param>
    /// <returns>This builder for chaining.</returns>
    public EndpointBuilder WithQueryParam(string paramName, QueryParameterType type = QueryParameterType.String)
    {
        _queryParams[paramName] = QueryParameterExpectation.Required(paramName, type);
        return this;
    }

    /// <summary>
    /// Specifies an optional query parameter for this endpoint.
    /// </summary>
    /// <param name="paramName">The parameter name.</param>
    /// <param name="type">The parameter type.</param>
    /// <returns>This builder for chaining.</returns>
    public EndpointBuilder WithOptionalQueryParam(string paramName, QueryParameterType type = QueryParameterType.String)
    {
        _queryParams[paramName] = QueryParameterExpectation.Optional(paramName, type);
        return this;
    }

    /// <summary>
    /// Starts defining another endpoint.
    /// </summary>
    /// <param name="pathTemplate">The path template for the new endpoint.</param>
    /// <returns>A new endpoint builder.</returns>
    public EndpointBuilder ForEndpoint(string pathTemplate)
    {
        return _parent.ForEndpoint(pathTemplate);
    }

    /// <summary>
    /// Builds the final contract.
    /// </summary>
    /// <returns>The built contract.</returns>
    public Contract Build() => _parent.Build();

    internal EndpointContract Build(IJsonSerializer serializer)
    {
        var request = _requestBuilder?.Build(serializer);
        var responses = _responseBuilders.Select(b => b.Build(serializer)).ToList();

        return new EndpointContract(
            _pathTemplate,
            _method,
            request,
            responses,
            _headers,
            _queryParams);
    }
}
