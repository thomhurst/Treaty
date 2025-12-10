namespace Treaty.Provider.Authentication;

/// <summary>
/// Provides authentication via custom headers for HTTP requests.
/// </summary>
public sealed class CustomHeadersAuthProvider : IAuthenticationProvider
{
    private readonly IReadOnlyDictionary<string, string> _headers;

    /// <summary>
    /// Initializes a new instance with custom headers.
    /// </summary>
    /// <param name="headers">The headers to apply to each request.</param>
    public CustomHeadersAuthProvider(IDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        _headers = new Dictionary<string, string>(headers);
    }

    /// <inheritdoc />
    public Task ApplyAuthenticationAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        foreach (var (name, value) in _headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        return Task.CompletedTask;
    }
}
