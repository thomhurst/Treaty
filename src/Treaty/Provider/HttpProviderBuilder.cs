using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Treaty.Contracts;
using Treaty.Provider.Authentication;
using Treaty.Provider.Resilience;

namespace Treaty.Provider;

/// <summary>
/// Builder for creating HTTP provider verifiers.
/// </summary>
public sealed class HttpProviderBuilder
{
    private Uri? _baseUri;
    private ContractDefinition? _contract;
    private ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;
    private IStateHandler? _stateHandler;
    private IAuthenticationProvider? _authProvider;
    private IRetryPolicy? _retryPolicy;
    private HttpProviderOptions _httpOptions = HttpProviderOptions.Default;
    private HttpClient? _httpClient;

    /// <summary>
    /// Creates a new HTTP provider builder.
    /// </summary>
    public HttpProviderBuilder() { }

    /// <summary>
    /// Specifies the base URL of the API to verify.
    /// </summary>
    /// <param name="baseUrl">The base URL (e.g., "https://api.example.com").</param>
    public HttpProviderBuilder WithBaseUrl(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        // Ensure trailing slash for proper URI combination
        var url = baseUrl.TrimEnd('/') + "/";
        _baseUri = new Uri(url);
        return this;
    }

    /// <summary>
    /// Specifies the base URI of the API to verify.
    /// </summary>
    /// <param name="baseUri">The base URI.</param>
    public HttpProviderBuilder WithBaseUrl(Uri baseUri)
    {
        _baseUri = baseUri ?? throw new ArgumentNullException(nameof(baseUri));
        return this;
    }

    /// <summary>
    /// Specifies the contract to verify against.
    /// </summary>
    /// <param name="contract">The contract.</param>
    public HttpProviderBuilder WithContract(ContractDefinition contract)
    {
        _contract = contract ?? throw new ArgumentNullException(nameof(contract));
        return this;
    }

    /// <summary>
    /// Specifies a logger factory for diagnostic output.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    public HttpProviderBuilder WithLogging(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        return this;
    }

    /// <summary>
    /// Specifies a state handler for setting up provider states.
    /// </summary>
    /// <param name="stateHandler">The state handler.</param>
    public HttpProviderBuilder WithStateHandler(IStateHandler stateHandler)
    {
        _stateHandler = stateHandler ?? throw new ArgumentNullException(nameof(stateHandler));
        return this;
    }

    /// <summary>
    /// Configures state handlers using a fluent builder.
    /// </summary>
    /// <param name="configure">The configuration action.</param>
    public HttpProviderBuilder WithStateHandler(Action<StateHandlerBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new StateHandlerBuilder();
        configure(builder);
        _stateHandler = builder.Build();
        return this;
    }

    /// <summary>
    /// Configures Bearer token authentication with a static token.
    /// </summary>
    /// <param name="token">The bearer token.</param>
    public HttpProviderBuilder WithBearerToken(string token)
    {
        _authProvider = new BearerTokenAuthProvider(token);
        return this;
    }

    /// <summary>
    /// Configures Bearer token authentication with a token factory.
    /// </summary>
    /// <param name="tokenFactory">A function that returns the bearer token asynchronously.</param>
    public HttpProviderBuilder WithBearerToken(Func<CancellationToken, Task<string>> tokenFactory)
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
    public HttpProviderBuilder WithApiKey(
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
    public HttpProviderBuilder WithBasicAuth(string username, string password)
    {
        _authProvider = new BasicAuthProvider(username, password);
        return this;
    }

    /// <summary>
    /// Configures custom header authentication.
    /// </summary>
    /// <param name="headers">The headers to add to each request.</param>
    public HttpProviderBuilder WithCustomHeaders(IDictionary<string, string> headers)
    {
        _authProvider = new CustomHeadersAuthProvider(headers);
        return this;
    }

    /// <summary>
    /// Configures a custom authentication provider.
    /// </summary>
    /// <param name="authProvider">The authentication provider.</param>
    public HttpProviderBuilder WithAuthentication(IAuthenticationProvider authProvider)
    {
        _authProvider = authProvider ?? throw new ArgumentNullException(nameof(authProvider));
        return this;
    }

    /// <summary>
    /// Configures retry policy with default options.
    /// </summary>
    public HttpProviderBuilder WithRetryPolicy()
    {
        _retryPolicy = new RetryPolicy(null, _loggerFactory.CreateLogger<RetryPolicy>());
        return this;
    }

    /// <summary>
    /// Configures retry policy with specified options.
    /// </summary>
    /// <param name="options">The retry policy options.</param>
    public HttpProviderBuilder WithRetryPolicy(RetryPolicyOptions options)
    {
        _retryPolicy = new RetryPolicy(options, _loggerFactory.CreateLogger<RetryPolicy>());
        return this;
    }

    /// <summary>
    /// Configures a custom retry policy.
    /// </summary>
    /// <param name="retryPolicy">The retry policy.</param>
    public HttpProviderBuilder WithRetryPolicy(IRetryPolicy retryPolicy)
    {
        _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
        return this;
    }

    /// <summary>
    /// Configures HTTP options.
    /// </summary>
    /// <param name="options">The HTTP options.</param>
    public HttpProviderBuilder WithHttpOptions(HttpProviderOptions options)
    {
        _httpOptions = options ?? throw new ArgumentNullException(nameof(options));
        return this;
    }

    /// <summary>
    /// Configures HTTP options using a builder action.
    /// </summary>
    /// <param name="configure">The configuration action.</param>
    public HttpProviderBuilder WithHttpOptions(Action<HttpProviderOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new HttpProviderOptionsBuilder();
        configure(builder);
        _httpOptions = builder.Build();
        return this;
    }

    /// <summary>
    /// Uses an existing HttpClient instead of creating one.
    /// The client will not be disposed when the verifier is disposed.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use.</param>
    public HttpProviderBuilder WithHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        return this;
    }

    /// <summary>
    /// Builds the HTTP provider verifier.
    /// </summary>
    /// <returns>A new HTTP provider verifier instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when required options are not specified.</exception>
    public HttpProviderVerifier Build()
    {
        if (_baseUri == null)
            throw new InvalidOperationException("A base URL must be specified using WithBaseUrl().");
        if (_contract == null)
            throw new InvalidOperationException("A contract must be specified using WithContract().");

        return new HttpProviderVerifier(
            _baseUri,
            _contract,
            _loggerFactory,
            _stateHandler,
            _authProvider,
            _retryPolicy,
            _httpOptions,
            _httpClient);
    }
}
