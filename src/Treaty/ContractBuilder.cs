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
    private ContractMetadata? _metadata;

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
    /// Specifies metadata for this contract (version, description, contact info, etc.).
    /// </summary>
    /// <param name="configure">Action to configure metadata.</param>
    /// <returns>This builder for chaining.</returns>
    /// <example>
    /// <code>
    /// var contract = Treaty.DefineContract("User API")
    ///     .WithMetadata(m => m
    ///         .Version("1.0.0")
    ///         .Description("API for managing users"))
    ///     .ForEndpoint("/users")
    ///         // ...
    ///     .Build();
    /// </code>
    /// </example>
    public ContractBuilder WithMetadata(Action<ContractMetadataBuilder> configure)
    {
        var builder = new ContractMetadataBuilder();
        configure(builder);
        _metadata = builder.Build();
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
        return new Contract(_name, endpoints, _jsonSerializer, defaults, _metadata);
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

/// <summary>
/// Builder for contract metadata.
/// </summary>
public sealed class ContractMetadataBuilder
{
    private string? _version;
    private string? _description;
    private string? _contactName;
    private string? _contactEmail;
    private string? _contactUrl;
    private string? _licenseName;
    private string? _licenseUrl;
    private string? _termsOfService;

    /// <summary>
    /// Sets the API version.
    /// </summary>
    /// <param name="version">The version string (e.g., "1.0.0").</param>
    /// <returns>This builder for chaining.</returns>
    public ContractMetadataBuilder Version(string version)
    {
        _version = version;
        return this;
    }

    /// <summary>
    /// Sets the API description.
    /// </summary>
    /// <param name="description">A description of the API.</param>
    /// <returns>This builder for chaining.</returns>
    public ContractMetadataBuilder Description(string description)
    {
        _description = description;
        return this;
    }

    /// <summary>
    /// Sets the contact information for the API.
    /// </summary>
    /// <param name="name">Contact name.</param>
    /// <param name="email">Contact email.</param>
    /// <param name="url">Contact URL.</param>
    /// <returns>This builder for chaining.</returns>
    public ContractMetadataBuilder Contact(string? name = null, string? email = null, string? url = null)
    {
        _contactName = name;
        _contactEmail = email;
        _contactUrl = url;
        return this;
    }

    /// <summary>
    /// Sets the license information for the API.
    /// </summary>
    /// <param name="name">License name (e.g., "MIT").</param>
    /// <param name="url">URL to the license.</param>
    /// <returns>This builder for chaining.</returns>
    public ContractMetadataBuilder License(string name, string? url = null)
    {
        _licenseName = name;
        _licenseUrl = url;
        return this;
    }

    /// <summary>
    /// Sets the URL to the Terms of Service.
    /// </summary>
    /// <param name="url">URL to the Terms of Service.</param>
    /// <returns>This builder for chaining.</returns>
    public ContractMetadataBuilder TermsOfService(string url)
    {
        _termsOfService = url;
        return this;
    }

    internal ContractMetadata Build()
    {
        ContractContact? contact = null;
        if (_contactName != null || _contactEmail != null || _contactUrl != null)
        {
            contact = new ContractContact(_contactName, _contactEmail, _contactUrl);
        }

        ContractLicense? license = null;
        if (_licenseName != null)
        {
            license = new ContractLicense(_licenseName, _licenseUrl);
        }

        return new ContractMetadata(_version, _description, contact, license, _termsOfService);
    }
}
