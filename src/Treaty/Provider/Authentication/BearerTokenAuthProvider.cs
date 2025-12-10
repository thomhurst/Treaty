using System.Net.Http.Headers;

namespace Treaty.Provider.Authentication;

/// <summary>
/// Provides Bearer token authentication for HTTP requests.
/// </summary>
public sealed class BearerTokenAuthProvider : IAuthenticationProvider
{
    private readonly Func<CancellationToken, Task<string>> _tokenFactory;

    /// <summary>
    /// Initializes a new instance with a static token.
    /// </summary>
    /// <param name="token">The bearer token.</param>
    public BearerTokenAuthProvider(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        _tokenFactory = _ => Task.FromResult(token);
    }

    /// <summary>
    /// Initializes a new instance with a synchronous token factory.
    /// </summary>
    /// <param name="tokenFactory">A function that returns the bearer token.</param>
    public BearerTokenAuthProvider(Func<string> tokenFactory)
    {
        ArgumentNullException.ThrowIfNull(tokenFactory);
        _tokenFactory = _ => Task.FromResult(tokenFactory());
    }

    /// <summary>
    /// Initializes a new instance with an asynchronous token factory.
    /// </summary>
    /// <param name="tokenFactory">A function that returns the bearer token asynchronously.</param>
    public BearerTokenAuthProvider(Func<CancellationToken, Task<string>> tokenFactory)
    {
        _tokenFactory = tokenFactory ?? throw new ArgumentNullException(nameof(tokenFactory));
    }

    /// <inheritdoc />
    public async Task ApplyAuthenticationAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        var token = await _tokenFactory(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
