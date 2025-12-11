using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Treaty.Contracts;
using Treaty.Provider.Authentication;
using Treaty.Provider.Resilience;
using Treaty.Provider;

namespace Treaty.Consumer;

/// <summary>
/// Builder for creating consumer verifiers.
/// </summary>
public sealed class ConsumerBuilder
{
    private ContractDefinition? _contract;
    private ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;
    private string _baseUrl = "http://localhost";
    private IAuthenticationProvider? _authProvider;
    private IRetryPolicy? _retryPolicy;
    private HttpProviderOptions _httpOptions = HttpProviderOptions.Default;
    private HttpMessageHandler? _innerHandler;

    internal ConsumerBuilder() { }

    /// <summary>
    /// Specifies the contract to verify against.
    /// </summary>
    /// <param name="contract">The contract to use for verification.</param>
    /// <returns>This builder for chaining.</returns>
    public ConsumerBuilder WithContract(ContractDefinition contract)
    {
        _contract = contract ?? throw new ArgumentNullException(nameof(contract));
        return this;
    }

    /// <summary>
    /// Specifies the base URL for the API.
    /// </summary>
    /// <param name="baseUrl">The base URL.</param>
    /// <returns>This builder for chaining.</returns>
    public ConsumerBuilder WithBaseUrl(string baseUrl)
    {
        _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        return this;
    }

    /// <summary>
    /// Specifies a logger factory for diagnostic output.
    /// </summary>
    /// <param name="loggerFactory">The logger factory to use.</param>
    /// <returns>This builder for chaining.</returns>
    public ConsumerBuilder WithLogging(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        return this;
    }

    /// <summary>
    /// Configures Bearer token authentication with a static token.
    /// </summary>
    /// <param name="token">The bearer token.</param>
    /// <returns>This builder for chaining.</returns>
    public ConsumerBuilder WithBearerToken(string token)
    {
        _authProvider = new BearerTokenAuthProvider(token);
        return this;
    }

    /// <summary>
    /// Configures Bearer token authentication with a token factory.
    /// </summary>
    /// <param name="tokenFactory">A function that returns the bearer token asynchronously.</param>
    /// <returns>This builder for chaining.</returns>
    public ConsumerBuilder WithBearerToken(Func<CancellationToken, Task<string>> tokenFactory)
    {
        _authProvider = new BearerTokenAuthProvider(tokenFactory);
        return this;
    }

    /// <summary>
    /// Configures API key authentication.
    /// </summary>
    /// <param name="apiKey">The API key value.</param>
    /// <param name="parameterName">The name of the header or query parameter. Default is "X-API-Key".</param>
    /// <param name="location">Where to place the API key. Default is Header.</param>
    /// <returns>This builder for chaining.</returns>
    public ConsumerBuilder WithApiKey(
        string apiKey,
        string parameterName = "X-API-Key",
        ApiKeyLocation location = ApiKeyLocation.Header)
    {
        _authProvider = new ApiKeyAuthProvider(apiKey, parameterName, location);
        return this;
    }

    /// <summary>
    /// Configures HTTP Basic authentication.
    /// </summary>
    /// <param name="username">The username.</param>
    /// <param name="password">The password.</param>
    /// <returns>This builder for chaining.</returns>
    public ConsumerBuilder WithBasicAuth(string username, string password)
    {
        _authProvider = new BasicAuthProvider(username, password);
        return this;
    }

    /// <summary>
    /// Configures custom header authentication.
    /// </summary>
    /// <param name="headers">The headers to add to each request.</param>
    /// <returns>This builder for chaining.</returns>
    public ConsumerBuilder WithCustomHeaders(IDictionary<string, string> headers)
    {
        _authProvider = new CustomHeadersAuthProvider(headers);
        return this;
    }

    /// <summary>
    /// Configures a custom authentication provider.
    /// </summary>
    /// <param name="authProvider">The authentication provider.</param>
    /// <returns>This builder for chaining.</returns>
    public ConsumerBuilder WithAuthentication(IAuthenticationProvider authProvider)
    {
        _authProvider = authProvider ?? throw new ArgumentNullException(nameof(authProvider));
        return this;
    }

    /// <summary>
    /// Configures retry policy with default options.
    /// </summary>
    /// <returns>This builder for chaining.</returns>
    public ConsumerBuilder WithRetryPolicy()
    {
        _retryPolicy = new RetryPolicy(null, _loggerFactory.CreateLogger<RetryPolicy>());
        return this;
    }

    /// <summary>
    /// Configures retry policy with specified options.
    /// </summary>
    /// <param name="options">The retry policy options.</param>
    /// <returns>This builder for chaining.</returns>
    public ConsumerBuilder WithRetryPolicy(RetryPolicyOptions options)
    {
        _retryPolicy = new RetryPolicy(options, _loggerFactory.CreateLogger<RetryPolicy>());
        return this;
    }

    /// <summary>
    /// Configures a custom retry policy.
    /// </summary>
    /// <param name="retryPolicy">The retry policy.</param>
    /// <returns>This builder for chaining.</returns>
    public ConsumerBuilder WithRetryPolicy(IRetryPolicy retryPolicy)
    {
        _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
        return this;
    }

    /// <summary>
    /// Configures HTTP options.
    /// </summary>
    /// <param name="options">The HTTP options.</param>
    /// <returns>This builder for chaining.</returns>
    public ConsumerBuilder WithHttpOptions(HttpProviderOptions options)
    {
        _httpOptions = options ?? throw new ArgumentNullException(nameof(options));
        return this;
    }

    /// <summary>
    /// Configures HTTP options using a builder action.
    /// </summary>
    /// <param name="configure">The configuration action.</param>
    /// <returns>This builder for chaining.</returns>
    public ConsumerBuilder WithHttpOptions(Action<HttpProviderOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new HttpProviderOptionsBuilder();
        configure(builder);
        _httpOptions = builder.Build();
        return this;
    }

    /// <summary>
    /// Specifies a custom inner handler for the HttpClient.
    /// </summary>
    /// <param name="handler">The inner handler to use.</param>
    /// <returns>This builder for chaining.</returns>
    public ConsumerBuilder WithInnerHandler(HttpMessageHandler handler)
    {
        _innerHandler = handler ?? throw new ArgumentNullException(nameof(handler));
        return this;
    }

    /// <summary>
    /// Builds the consumer validation client.
    /// </summary>
    /// <returns>The configured consumer validation client.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no contract was specified.</exception>
    public Task<ConsumerValidationClient> BuildAsync()
    {
        if (_contract == null)
        {
            throw new InvalidOperationException("A contract must be specified using WithContract().");
        }

        var client = new ConsumerValidationClient(
            _contract,
            _baseUrl,
            _loggerFactory,
            _authProvider,
            _retryPolicy,
            _httpOptions,
            _innerHandler);
        return Task.FromResult(client);
    }
}
