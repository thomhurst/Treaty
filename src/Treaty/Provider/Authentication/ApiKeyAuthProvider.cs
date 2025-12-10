namespace Treaty.Provider.Authentication;

/// <summary>
/// Location where the API key should be placed.
/// </summary>
public enum ApiKeyLocation
{
    /// <summary>
    /// Place the API key in a header.
    /// </summary>
    Header,

    /// <summary>
    /// Place the API key in the query string.
    /// </summary>
    QueryString
}

/// <summary>
/// Provides API key authentication for HTTP requests.
/// </summary>
public sealed class ApiKeyAuthProvider : IAuthenticationProvider
{
    private readonly string _apiKey;
    private readonly string _parameterName;
    private readonly ApiKeyLocation _location;

    /// <summary>
    /// Initializes a new instance with an API key.
    /// </summary>
    /// <param name="apiKey">The API key value.</param>
    /// <param name="parameterName">The name of the header or query parameter. Default is "X-API-Key".</param>
    /// <param name="location">Where to place the API key. Default is Header.</param>
    public ApiKeyAuthProvider(
        string apiKey,
        string parameterName = "X-API-Key",
        ApiKeyLocation location = ApiKeyLocation.Header)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterName);

        _apiKey = apiKey;
        _parameterName = parameterName;
        _location = location;
    }

    /// <inheritdoc />
    public Task ApplyAuthenticationAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        if (_location == ApiKeyLocation.Header)
        {
            request.Headers.TryAddWithoutValidation(_parameterName, _apiKey);
        }
        else
        {
            var uri = request.RequestUri!;
            var uriString = uri.IsAbsoluteUri ? uri.AbsoluteUri : uri.ToString();
            var separator = uriString.Contains('?') ? "&" : "?";
            var newUri = $"{uriString}{separator}{Uri.EscapeDataString(_parameterName)}={Uri.EscapeDataString(_apiKey)}";
            request.RequestUri = new Uri(newUri, uri.IsAbsoluteUri ? UriKind.Absolute : UriKind.Relative);
        }

        return Task.CompletedTask;
    }
}
