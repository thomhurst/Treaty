using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Treaty.Contracts;
using Treaty.Mocking;

namespace Treaty.OpenApi;

/// <summary>
/// Builder for creating mock servers from OpenAPI specifications.
/// </summary>
public sealed class MockServerBuilder
{
    private readonly OpenApiContractBuilder _contractBuilder;
    private ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;
    private bool _useHttps;
    private int? _minLatencyMs;
    private int? _maxLatencyMs;
    private AuthConfig? _authConfig;
    private readonly Dictionary<string, Func<object>> _customGenerators = new();
    private readonly Dictionary<string, ContractMockEndpointConfig> _endpointConfigs = new();

    internal MockServerBuilder(string specPath)
    {
        _contractBuilder = Contract.FromOpenApi(specPath);
    }

    internal MockServerBuilder(Stream specStream, OpenApiFormat format)
    {
        _contractBuilder = Contract.FromOpenApi(specStream, format);
    }

    /// <summary>
    /// Specifies a logger factory for diagnostic output.
    /// </summary>
    /// <param name="loggerFactory">The logger factory to use.</param>
    /// <returns>This builder for chaining.</returns>
    public MockServerBuilder WithLogging(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        return this;
    }

    /// <summary>
    /// Enables HTTPS for the mock server.
    /// </summary>
    /// <returns>This builder for chaining.</returns>
    public MockServerBuilder UseHttps()
    {
        _useHttps = true;
        return this;
    }

    /// <summary>
    /// Configures latency simulation for all responses.
    /// </summary>
    /// <param name="min">Minimum latency in milliseconds.</param>
    /// <param name="max">Maximum latency in milliseconds.</param>
    /// <returns>This builder for chaining.</returns>
    public MockServerBuilder WithLatency(int min, int max)
    {
        _minLatencyMs = min;
        _maxLatencyMs = max;
        return this;
    }

    /// <summary>
    /// Configures authentication requirements for the mock server.
    /// </summary>
    /// <param name="configure">Action to configure authentication.</param>
    /// <returns>This builder for chaining.</returns>
    /// <example>
    /// <code>
    /// .WithAuth(auth => auth
    ///     .RequireHeader("Authorization")
    ///     .WhenMissing().Return(401))
    /// </code>
    /// </example>
    public MockServerBuilder WithAuth(Action<AuthConfigBuilder> configure)
    {
        var builder = new AuthConfigBuilder();
        configure(builder);
        _authConfig = builder.Build();
        return this;
    }

    /// <summary>
    /// Registers a custom value generator for a specific field name.
    /// </summary>
    /// <param name="fieldName">The field name to match.</param>
    /// <param name="generator">The function that generates values.</param>
    /// <returns>This builder for chaining.</returns>
    /// <example>
    /// <code>
    /// .WithCustomGenerator("correlationId", () => Guid.NewGuid().ToString())
    /// </code>
    /// </example>
    public MockServerBuilder WithCustomGenerator(string fieldName, Func<object> generator)
    {
        _customGenerators[fieldName] = generator;
        return this;
    }

    /// <summary>
    /// Starts configuring response scenarios for a specific endpoint.
    /// </summary>
    /// <param name="pathTemplate">The path template (e.g., "/users/{id}").</param>
    /// <returns>A builder for configuring endpoint-specific responses.</returns>
    public MockEndpointBuilder ForEndpoint(string pathTemplate)
    {
        var config = new ContractMockEndpointConfig();
        _endpointConfigs[pathTemplate] = config;
        return new MockEndpointBuilder(this, config);
    }

    /// <summary>
    /// Builds the mock server asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The configured mock server.</returns>
    public async Task<IMockServer> BuildAsync(CancellationToken cancellationToken = default)
    {
        var contract = await _contractBuilder.BuildAsync(cancellationToken).ConfigureAwait(false);
        return new ContractMockServer(
            contract,
            _loggerFactory,
            _useHttps,
            _minLatencyMs,
            _maxLatencyMs,
            _authConfig,
            _customGenerators,
            _endpointConfigs);
    }
}

/// <summary>
/// Builder for configuring mock endpoint responses.
/// </summary>
public sealed class MockEndpointBuilder
{
    private readonly MockServerBuilder _parent;
    private readonly ContractMockEndpointConfig _config;

    internal MockEndpointBuilder(MockServerBuilder parent, ContractMockEndpointConfig config)
    {
        _parent = parent;
        _config = config;
    }

    /// <summary>
    /// Configures latency simulation for this endpoint.
    /// </summary>
    /// <param name="minMs">Minimum latency in milliseconds.</param>
    /// <param name="maxMs">Maximum latency in milliseconds.</param>
    /// <returns>This builder for chaining.</returns>
    public MockEndpointBuilder WithLatency(int minMs, int maxMs)
    {
        _config.MinLatencyMs = minMs;
        _config.MaxLatencyMs = maxMs;
        return this;
    }

    /// <summary>
    /// Defines a condition for returning a specific response.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <returns>A builder for specifying the response.</returns>
    public MockResponseBuilder When(Func<MockRequestContext, bool> condition)
    {
        return new MockResponseBuilder(this, _config, condition);
    }

    /// <summary>
    /// Defines the default response when no conditions match.
    /// </summary>
    /// <returns>A builder for specifying the response.</returns>
    public MockResponseBuilder Otherwise()
    {
        return new MockResponseBuilder(this, _config, _ => true);
    }

    /// <summary>
    /// Starts configuring another endpoint.
    /// </summary>
    /// <param name="pathTemplate">The path template.</param>
    /// <returns>A builder for the new endpoint.</returns>
    public MockEndpointBuilder ForEndpoint(string pathTemplate) => _parent.ForEndpoint(pathTemplate);

    /// <summary>
    /// Builds the mock server asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The configured mock server.</returns>
    public Task<IMockServer> BuildAsync(CancellationToken cancellationToken = default) => _parent.BuildAsync(cancellationToken);
}

/// <summary>
/// Builder for configuring mock responses.
/// </summary>
public sealed class MockResponseBuilder
{
    private readonly MockEndpointBuilder _parent;
    private readonly ContractMockEndpointConfig _config;
    private readonly Func<MockRequestContext, bool> _condition;

    internal MockResponseBuilder(MockEndpointBuilder parent, ContractMockEndpointConfig config, Func<MockRequestContext, bool> condition)
    {
        _parent = parent;
        _config = config;
        _condition = condition;
    }

    /// <summary>
    /// Specifies the status code to return when the condition matches.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <returns>The parent endpoint builder.</returns>
    public MockEndpointBuilder Return(int statusCode)
    {
        _config.ResponseRules.Add(new ContractMockResponseRule(_condition, statusCode, null));
        return _parent;
    }

    /// <summary>
    /// Specifies the status code and body to return when the condition matches.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="body">The response body.</param>
    /// <returns>The parent endpoint builder.</returns>
    public MockEndpointBuilder Return(int statusCode, object body)
    {
        _config.ResponseRules.Add(new ContractMockResponseRule(_condition, statusCode, body));
        return _parent;
    }

    /// <summary>
    /// Specifies a sequence of responses to return on successive calls.
    /// The last response in the sequence will be repeated for any subsequent calls.
    /// </summary>
    /// <param name="responses">The sequence of responses (status code and optional body).</param>
    /// <returns>The parent endpoint builder.</returns>
    /// <example>
    /// <code>
    /// .ReturnSequence(
    ///     new MockSequenceResponse(503),           // First call returns 503
    ///     new MockSequenceResponse(503),           // Second call returns 503
    ///     new MockSequenceResponse(200, result))   // Third+ calls return 200
    /// </code>
    /// </example>
    public MockEndpointBuilder ReturnSequence(params MockSequenceResponse[] responses)
    {
        if (responses.Length == 0)
            throw new ArgumentException("At least one response is required.", nameof(responses));

        _config.ResponseRules.Add(new ContractMockResponseRule(_condition, responses));
        return _parent;
    }

    /// <summary>
    /// Injects a fault when the condition matches.
    /// </summary>
    /// <param name="fault">The type of fault to inject.</param>
    /// <returns>The parent endpoint builder.</returns>
    /// <example>
    /// <code>
    /// .When(ctx => ctx.Header("X-Fail") == "true")
    ///     .ReturnFault(FaultType.ConnectionReset)
    /// </code>
    /// </example>
    public MockEndpointBuilder ReturnFault(FaultType fault)
    {
        _config.ResponseRules.Add(new ContractMockResponseRule(_condition, fault));
        return _parent;
    }
}

/// <summary>
/// Context for evaluating mock request conditions.
/// </summary>
public sealed class MockRequestContext
{
    private readonly Dictionary<string, string> _pathParams;
    private readonly Dictionary<string, string> _queryParams;
    private readonly Dictionary<string, string> _headers;
    private readonly string? _body;
    private System.Text.Json.JsonElement? _bodyJson;
    private bool _bodyJsonParsed;

    internal MockRequestContext(
        Dictionary<string, string> pathParams,
        Dictionary<string, string> queryParams,
        Dictionary<string, string> headers,
        string? body = null)
    {
        _pathParams = pathParams;
        _queryParams = queryParams;
        _headers = headers;
        _body = body;
    }

    /// <summary>
    /// Gets a path parameter value.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <returns>The parameter value, or null if not found.</returns>
    public string? PathParam(string name) =>
        _pathParams.TryGetValue(name, out var value) ? value : null;

    /// <summary>
    /// Gets a query parameter value.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <returns>The parameter value, or null if not found.</returns>
    public string? QueryParam(string name) =>
        _queryParams.TryGetValue(name, out var value) ? value : null;

    /// <summary>
    /// Gets a header value.
    /// </summary>
    /// <param name="name">The header name.</param>
    /// <returns>The header value, or null if not found.</returns>
    public string? Header(string name) =>
        _headers.TryGetValue(name, out var value) ? value : null;

    /// <summary>
    /// Gets the raw request body as a string.
    /// </summary>
    /// <returns>The request body, or null if not available.</returns>
    public string? Body => _body;

    /// <summary>
    /// Gets the request body parsed as JSON.
    /// </summary>
    /// <returns>The parsed JSON element, or null if the body is empty or not valid JSON.</returns>
    public System.Text.Json.JsonElement? BodyAsJson()
    {
        if (_bodyJsonParsed)
            return _bodyJson;

        _bodyJsonParsed = true;
        if (string.IsNullOrEmpty(_body))
            return null;

        try
        {
            _bodyJson = System.Text.Json.JsonDocument.Parse(_body).RootElement;
        }
        catch (System.Text.Json.JsonException)
        {
            _bodyJson = null;
        }
        return _bodyJson;
    }

    /// <summary>
    /// Deserializes the request body to a strongly-typed object.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <returns>The deserialized object, or default if the body is empty or cannot be deserialized.</returns>
    public T? BodyAs<T>() where T : class
    {
        if (string.IsNullOrEmpty(_body))
            return default;

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<T>(_body);
        }
        catch (System.Text.Json.JsonException)
        {
            return default;
        }
    }
}

/// <summary>
/// Builder for authentication configuration.
/// </summary>
public sealed class AuthConfigBuilder
{
    private string? _requiredHeader;
    private int _missingStatusCode = 401;

    /// <summary>
    /// Specifies a required authentication header.
    /// </summary>
    /// <param name="headerName">The header name (e.g., "Authorization").</param>
    /// <returns>This builder for chaining.</returns>
    public AuthConfigBuilder RequireHeader(string headerName)
    {
        _requiredHeader = headerName;
        return this;
    }

    /// <summary>
    /// Specifies the response when authentication is missing.
    /// </summary>
    /// <returns>A builder for configuring the missing auth response.</returns>
    public AuthMissingBuilder WhenMissing()
    {
        return new AuthMissingBuilder(this);
    }

    internal void SetMissingStatusCode(int statusCode)
    {
        _missingStatusCode = statusCode;
    }

    internal AuthConfig Build()
    {
        return new AuthConfig(_requiredHeader, _missingStatusCode);
    }
}

/// <summary>
/// Builder for missing authentication response.
/// </summary>
public sealed class AuthMissingBuilder
{
    private readonly AuthConfigBuilder _parent;

    internal AuthMissingBuilder(AuthConfigBuilder parent)
    {
        _parent = parent;
    }

    /// <summary>
    /// Specifies the status code to return when authentication is missing.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <returns>The parent auth builder.</returns>
    public AuthConfigBuilder Return(int statusCode)
    {
        _parent.SetMissingStatusCode(statusCode);
        return _parent;
    }
}

internal sealed class AuthConfig
{
    public string? RequiredHeader { get; }
    public int MissingStatusCode { get; }

    public AuthConfig(string? requiredHeader, int missingStatusCode)
    {
        RequiredHeader = requiredHeader;
        MissingStatusCode = missingStatusCode;
    }
}
