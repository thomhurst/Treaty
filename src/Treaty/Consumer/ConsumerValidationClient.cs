using Microsoft.Extensions.Logging;
using Treaty.Contracts;
using Treaty.Provider;
using Treaty.Provider.Authentication;
using Treaty.Provider.Resilience;

namespace Treaty.Consumer;

/// <summary>
/// Validates that consumer HTTP client code makes requests that conform to contracts.
/// </summary>
public sealed class ConsumerValidationClient
{
    private readonly ContractDefinition _contract;
    private readonly string _baseUrl;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IAuthenticationProvider? _authProvider;
    private readonly IRetryPolicy? _retryPolicy;
    private readonly HttpProviderOptions _httpOptions;
    private readonly HttpMessageHandler? _innerHandler;

    internal ConsumerValidationClient(
        ContractDefinition contract,
        string baseUrl,
        ILoggerFactory loggerFactory,
        IAuthenticationProvider? authProvider = null,
        IRetryPolicy? retryPolicy = null,
        HttpProviderOptions? httpOptions = null,
        HttpMessageHandler? innerHandler = null)
    {
        _contract = contract;
        _baseUrl = baseUrl;
        _loggerFactory = loggerFactory;
        _authProvider = authProvider;
        _retryPolicy = retryPolicy;
        _httpOptions = httpOptions ?? HttpProviderOptions.Default;
        _innerHandler = innerHandler;
    }

    /// <summary>
    /// Creates an HttpClient that validates all requests against the contract.
    /// </summary>
    /// <returns>An HttpClient with contract validation enabled.</returns>
    public HttpClient CreateHttpClient()
    {
        var handler = CreateHandler();
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = _httpOptions.RequestTimeout
        };

        return client;
    }

    /// <summary>
    /// Creates a DelegatingHandler that validates requests against the contract.
    /// Use this when you need to integrate with existing HttpClient configurations.
    /// </summary>
    /// <param name="innerHandler">The inner handler to delegate to. If not specified, uses the configured inner handler or creates a new HttpClientHandler.</param>
    /// <returns>A DelegatingHandler with contract validation.</returns>
    public DelegatingHandler CreateHandler(HttpMessageHandler? innerHandler = null)
    {
        var baseHandler = innerHandler ?? _innerHandler ?? CreateBaseHandler();

        // Build handler chain: Auth -> Retry -> Validation -> Base
        HttpMessageHandler currentHandler = baseHandler;

        // Add retry policy if configured
        if (_retryPolicy != null)
        {
            var retryHandler = new RetryDelegatingHandler(_retryPolicy)
            {
                InnerHandler = currentHandler
            };
            currentHandler = retryHandler;
        }

        // Add auth if configured
        if (_authProvider != null)
        {
            var authHandler = new AuthenticationDelegatingHandler(_authProvider)
            {
                InnerHandler = currentHandler
            };
            currentHandler = authHandler;
        }

        // Add contract validation at the outer layer
        return new ContractValidatingHandler(_contract, _loggerFactory)
        {
            InnerHandler = currentHandler
        };
    }

    private HttpClientHandler CreateBaseHandler()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = _httpOptions.FollowRedirects,
            MaxAutomaticRedirections = _httpOptions.MaxRedirects
        };

        if (!_httpOptions.ValidateCertificates)
        {
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        }

        return handler;
    }
}

/// <summary>
/// DelegatingHandler that applies authentication to requests.
/// </summary>
internal sealed class AuthenticationDelegatingHandler(IAuthenticationProvider authProvider) : DelegatingHandler
{
    private readonly IAuthenticationProvider _authProvider = authProvider;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await _authProvider.ApplyAuthenticationAsync(request, cancellationToken);
        return await base.SendAsync(request, cancellationToken);
    }
}

/// <summary>
/// DelegatingHandler that applies retry policy to requests.
/// </summary>
internal sealed class RetryDelegatingHandler(IRetryPolicy retryPolicy) : DelegatingHandler
{
    private readonly IRetryPolicy _retryPolicy = retryPolicy;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return await _retryPolicy.ExecuteAsync(
            async ct => await base.SendAsync(CloneRequest(request), ct),
            cancellationToken);
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version
        };

        // Copy headers
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // Copy content if present
        if (request.Content != null)
        {
            // For retry, we need to buffer the content so it can be re-read
            var content = request.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            clone.Content = new ByteArrayContent(content);

            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        // Copy options
        foreach (var option in request.Options)
        {
            clone.Options.TryAdd(option.Key, option.Value);
        }

        return clone;
    }
}
