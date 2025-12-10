namespace Treaty.Provider.Authentication;

/// <summary>
/// Interface for providing authentication to HTTP requests.
/// </summary>
public interface IAuthenticationProvider
{
    /// <summary>
    /// Applies authentication to the HTTP request.
    /// </summary>
    /// <param name="request">The request to authenticate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ApplyAuthenticationAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default);
}
