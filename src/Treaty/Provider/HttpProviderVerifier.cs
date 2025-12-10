using Microsoft.Extensions.Logging;
using Treaty.Contracts;
using Treaty.Provider.Authentication;
using Treaty.Provider.Resilience;

namespace Treaty.Provider;

/// <summary>
/// Verifies that a live HTTP API implementation meets contract expectations.
/// </summary>
public sealed class HttpProviderVerifier : ProviderVerifierBase
{
    private readonly HttpClient _client;
    private readonly Uri _baseUri;
    private readonly IAuthenticationProvider? _authProvider;
    private readonly IRetryPolicy? _retryPolicy;
    private readonly bool _ownsClient;

    internal HttpProviderVerifier(
        Uri baseUri,
        ContractDefinition contract,
        ILoggerFactory loggerFactory,
        IStateHandler? stateHandler,
        IAuthenticationProvider? authProvider,
        IRetryPolicy? retryPolicy,
        HttpProviderOptions options,
        HttpClient? httpClient = null)
        : base(contract, loggerFactory, stateHandler)
    {
        _baseUri = baseUri;
        _authProvider = authProvider;
        _retryPolicy = retryPolicy;
        _ownsClient = httpClient == null;

        if (httpClient != null)
        {
            _client = httpClient;
        }
        else
        {
            var handler = new HttpClientHandler();

            if (!options.ValidateCertificates)
            {
                handler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }

            handler.AllowAutoRedirect = options.FollowRedirects;
            handler.MaxAutomaticRedirections = options.MaxRedirects;

            _client = new HttpClient(handler)
            {
                BaseAddress = _baseUri,
                Timeout = options.RequestTimeout
            };
        }
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendRequestAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Ensure absolute URI
        if (request.RequestUri != null && !request.RequestUri.IsAbsoluteUri)
        {
            request.RequestUri = new Uri(_baseUri, request.RequestUri);
        }

        // Apply authentication
        if (_authProvider != null)
        {
            await _authProvider.ApplyAuthenticationAsync(request, cancellationToken);
        }

        // Execute with retry policy if configured
        if (_retryPolicy != null)
        {
            return await _retryPolicy.ExecuteAsync(
                async ct =>
                {
                    // Clone request for retries since HttpRequestMessage can only be sent once
                    using var clonedRequest = await CloneRequestAsync(request);
                    return await _client.SendAsync(clonedRequest, ct);
                },
                cancellationToken);
        }

        return await _client.SendAsync(request, cancellationToken);
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);

        // Copy headers
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // Copy content
        if (request.Content != null)
        {
            var content = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(content);

            // Copy content headers
            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        lock (_disposeLock)
        {
            if (!_disposed)
            {
                if (_ownsClient)
                {
                    _client.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
