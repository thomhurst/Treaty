using Treaty.Contracts;
using Treaty.Serialization;

namespace Treaty;

/// <summary>
/// Builder for creating contract definitions using a fluent API.
/// </summary>
public sealed class ContractBuilder
{
    private readonly string _name;
    private IJsonSerializer _jsonSerializer = new SystemTextJsonSerializer();
    private readonly List<EndpointBuilder> _endpointBuilders = [];
    private ContractDefaultsBuilder? _defaultsBuilder;

    internal ContractBuilder(string name = "Contract")
    {
        _name = name;
    }

    /// <summary>
    /// Specifies a custom JSON serializer for this contract.
    /// </summary>
    /// <param name="serializer">The JSON serializer to use.</param>
    /// <returns>This builder for chaining.</returns>
    /// <example>
    /// <code>
    /// var contract = Treaty.DefineContract()
    ///     .WithJsonSerializer(new SystemTextJsonSerializer(new JsonSerializerOptions {
    ///         PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    ///     }))
    ///     .ForEndpoint("/users")
    ///         // ...
    ///     .Build();
    /// </code>
    /// </example>
    public ContractBuilder WithJsonSerializer(IJsonSerializer serializer)
    {
        _jsonSerializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        return this;
    }

    /// <summary>
    /// Configures default expectations that apply to all endpoints.
    /// </summary>
    /// <param name="configure">Action to configure defaults.</param>
    /// <returns>This builder for chaining.</returns>
    /// <example>
    /// <code>
    /// var contract = Treaty.DefineContract()
    ///     .WithDefaults(d => d.AllResponsesHaveHeader("X-Request-Id"))
    ///     .ForEndpoint("/users")
    ///         // ...
    ///     .Build();
    /// </code>
    /// </example>
    public ContractBuilder WithDefaults(Action<ContractDefaultsBuilder> configure)
    {
        _defaultsBuilder = new ContractDefaultsBuilder();
        configure(_defaultsBuilder);
        return this;
    }

    /// <summary>
    /// Starts defining expectations for an endpoint.
    /// </summary>
    /// <param name="pathTemplate">The path template (e.g., "/users/{id}").</param>
    /// <returns>An endpoint builder for further configuration.</returns>
    public EndpointBuilder ForEndpoint(string pathTemplate)
    {
        var builder = new EndpointBuilder(this, pathTemplate);
        _endpointBuilders.Add(builder);
        return builder;
    }

    /// <summary>
    /// Builds the contract with all defined endpoints.
    /// </summary>
    /// <returns>The built contract.</returns>
    public Contract Build()
    {
        var endpoints = _endpointBuilders.Select(b => b.Build(_jsonSerializer)).ToList();
        var defaults = _defaultsBuilder?.Build();
        return new Contract(_name, endpoints, _jsonSerializer, defaults);
    }
}

/// <summary>
/// Builder for contract-wide default expectations.
/// </summary>
public sealed class ContractDefaultsBuilder
{
    private readonly Dictionary<string, HeaderExpectation> _responseHeaders = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HeaderExpectation> _requestHeaders = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Specifies that all responses must have a specific header.
    /// </summary>
    /// <param name="headerName">The header name.</param>
    /// <returns>This builder for chaining.</returns>
    public ContractDefaultsBuilder AllResponsesHaveHeader(string headerName)
    {
        _responseHeaders[headerName] = HeaderExpectation.Required(headerName);
        return this;
    }

    /// <summary>
    /// Specifies that all responses must have a specific header with a specific value.
    /// </summary>
    /// <param name="headerName">The header name.</param>
    /// <param name="value">The expected header value.</param>
    /// <returns>This builder for chaining.</returns>
    public ContractDefaultsBuilder AllResponsesHaveHeader(string headerName, string value)
    {
        _responseHeaders[headerName] = HeaderExpectation.RequiredWithValue(headerName, value);
        return this;
    }

    /// <summary>
    /// Specifies that all requests must have a specific header.
    /// </summary>
    /// <param name="headerName">The header name.</param>
    /// <returns>This builder for chaining.</returns>
    public ContractDefaultsBuilder AllRequestsHaveHeader(string headerName)
    {
        _requestHeaders[headerName] = HeaderExpectation.Required(headerName);
        return this;
    }

    internal ContractDefaults Build()
    {
        return new ContractDefaults(_responseHeaders, _requestHeaders);
    }
}
