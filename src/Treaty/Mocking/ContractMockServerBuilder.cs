using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Treaty.Contracts;
using Treaty.OpenApi;

namespace Treaty.Mocking;

/// <summary>
/// Builder for creating mock servers from Treaty contracts.
/// </summary>
public sealed class ContractMockServerBuilder
{
    private readonly ContractDefinition _contract;
    private ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;
    private bool _useHttps;
    private int? _minLatencyMs;
    private int? _maxLatencyMs;
    private AuthConfig? _authConfig;
    private readonly Dictionary<string, Func<object>> _customGenerators = [];
    private readonly Dictionary<string, ContractMockEndpointConfig> _endpointConfigs = [];

    internal ContractMockServerBuilder(ContractDefinition contract)
    {
        _contract = contract ?? throw new ArgumentNullException(nameof(contract));
    }

    /// <summary>
    /// Specifies a logger factory for diagnostic output.
    /// </summary>
    /// <param name="loggerFactory">The logger factory to use.</param>
    /// <returns>This builder for chaining.</returns>
    public ContractMockServerBuilder WithLogging(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        return this;
    }

    /// <summary>
    /// Enables HTTPS for the mock server.
    /// </summary>
    /// <returns>This builder for chaining.</returns>
    public ContractMockServerBuilder UseHttps()
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
    public ContractMockServerBuilder WithLatency(int min, int max)
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
    public ContractMockServerBuilder WithAuth(Action<AuthConfigBuilder> configure)
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
    public ContractMockServerBuilder WithCustomGenerator(string fieldName, Func<object> generator)
    {
        _customGenerators[fieldName] = generator;
        return this;
    }

    /// <summary>
    /// Starts configuring response scenarios for a specific endpoint.
    /// </summary>
    /// <param name="pathTemplate">The path template (e.g., "/users/{id}").</param>
    /// <returns>A builder for configuring endpoint-specific responses.</returns>
    public ContractMockEndpointBuilder ForEndpoint(string pathTemplate)
    {
        var config = new ContractMockEndpointConfig();
        _endpointConfigs[pathTemplate] = config;
        return new ContractMockEndpointBuilder(this, config);
    }

    /// <summary>
    /// Builds the mock server.
    /// </summary>
    /// <returns>The configured mock server.</returns>
    public Task<IMockServer> BuildAsync()
    {
        var server = new ContractMockServer(
            _contract,
            _loggerFactory,
            _useHttps,
            _minLatencyMs,
            _maxLatencyMs,
            _authConfig,
            _customGenerators,
            _endpointConfigs);
        return Task.FromResult<IMockServer>(server);
    }
}

/// <summary>
/// Builder for configuring mock endpoint responses.
/// </summary>
public sealed class ContractMockEndpointBuilder
{
    private readonly ContractMockServerBuilder _parent;
    private readonly ContractMockEndpointConfig _config;

    internal ContractMockEndpointBuilder(ContractMockServerBuilder parent, ContractMockEndpointConfig config)
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
    public ContractMockEndpointBuilder WithLatency(int minMs, int maxMs)
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
    public ContractMockResponseBuilder When(Func<MockRequestContext, bool> condition)
    {
        return new ContractMockResponseBuilder(this, _config, condition);
    }

    /// <summary>
    /// Defines the default response when no conditions match.
    /// </summary>
    /// <returns>A builder for specifying the response.</returns>
    public ContractMockResponseBuilder Otherwise()
    {
        return new ContractMockResponseBuilder(this, _config, _ => true);
    }

    /// <summary>
    /// Starts configuring another endpoint.
    /// </summary>
    /// <param name="pathTemplate">The path template.</param>
    /// <returns>A builder for the new endpoint.</returns>
    public ContractMockEndpointBuilder ForEndpoint(string pathTemplate) => _parent.ForEndpoint(pathTemplate);

    /// <summary>
    /// Builds the mock server.
    /// </summary>
    /// <returns>The configured mock server.</returns>
    public Task<IMockServer> BuildAsync() => _parent.BuildAsync();
}

/// <summary>
/// Builder for configuring mock responses.
/// </summary>
public sealed class ContractMockResponseBuilder
{
    private readonly ContractMockEndpointBuilder _parent;
    private readonly ContractMockEndpointConfig _config;
    private readonly Func<MockRequestContext, bool> _condition;

    internal ContractMockResponseBuilder(
        ContractMockEndpointBuilder parent,
        ContractMockEndpointConfig config,
        Func<MockRequestContext, bool> condition)
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
    public ContractMockEndpointBuilder Return(int statusCode)
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
    public ContractMockEndpointBuilder Return(int statusCode, object body)
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
    public ContractMockEndpointBuilder ReturnSequence(params MockSequenceResponse[] responses)
    {
        if (responses.Length == 0)
        {
            throw new ArgumentException("At least one response is required.", nameof(responses));
        }

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
    public ContractMockEndpointBuilder ReturnFault(FaultType fault)
    {
        _config.ResponseRules.Add(new ContractMockResponseRule(_condition, fault));
        return _parent;
    }
}

internal sealed class ContractMockEndpointConfig
{
    public List<ContractMockResponseRule> ResponseRules { get; } = [];
    public int? MinLatencyMs { get; set; }
    public int? MaxLatencyMs { get; set; }
}

internal sealed class ContractMockResponseRule
{
    public Func<MockRequestContext, bool> Condition { get; }
    public int StatusCode { get; }
    public object? Body { get; }
    public IReadOnlyList<MockSequenceResponse>? Sequence { get; }
    public FaultType? Fault { get; }

    public ContractMockResponseRule(Func<MockRequestContext, bool> condition, int statusCode, object? body)
    {
        Condition = condition;
        StatusCode = statusCode;
        Body = body;
        Sequence = null;
        Fault = null;
    }

    public ContractMockResponseRule(Func<MockRequestContext, bool> condition, IReadOnlyList<MockSequenceResponse> sequence)
    {
        Condition = condition;
        StatusCode = sequence[0].StatusCode;
        Body = sequence[0].Body;
        Sequence = sequence;
        Fault = null;
    }

    public ContractMockResponseRule(Func<MockRequestContext, bool> condition, FaultType fault)
    {
        Condition = condition;
        StatusCode = 0; // Not used for faults
        Body = null;
        Sequence = null;
        Fault = fault;
    }
}

/// <summary>
/// Represents a single response in a sequence.
/// </summary>
/// <remarks>
/// Creates a new sequence response.
/// </remarks>
/// <param name="statusCode">The HTTP status code.</param>
/// <param name="body">The response body, or null for no body.</param>
public sealed class MockSequenceResponse(int statusCode, object? body = null)
{
    /// <summary>
    /// The HTTP status code.
    /// </summary>
    public int StatusCode { get; } = statusCode;

    /// <summary>
    /// The response body, or null for no body.
    /// </summary>
    public object? Body { get; } = body;
}
