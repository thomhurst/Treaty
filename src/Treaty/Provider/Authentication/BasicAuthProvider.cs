using System.Net.Http.Headers;
using System.Text;

namespace Treaty.Provider.Authentication;

/// <summary>
/// Provides HTTP Basic authentication for HTTP requests.
/// </summary>
public sealed class BasicAuthProvider : IAuthenticationProvider
{
    private readonly string _credentials;

    /// <summary>
    /// Initializes a new instance with username and password.
    /// </summary>
    /// <param name="username">The username.</param>
    /// <param name="password">The password.</param>
    public BasicAuthProvider(string username, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(password);

        _credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
    }

    /// <inheritdoc />
    public Task ApplyAuthenticationAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", _credentials);
        return Task.CompletedTask;
    }
}
