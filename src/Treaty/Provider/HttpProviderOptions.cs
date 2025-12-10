namespace Treaty.Provider;

/// <summary>
/// Options specific to HTTP provider verification.
/// </summary>
public sealed class HttpProviderOptions
{
    /// <summary>
    /// Timeout for individual HTTP requests. Default is 30 seconds.
    /// </summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to follow redirects. Default is true.
    /// </summary>
    public bool FollowRedirects { get; init; } = true;

    /// <summary>
    /// Maximum number of redirects to follow. Default is 5.
    /// </summary>
    public int MaxRedirects { get; init; } = 5;

    /// <summary>
    /// Whether to validate SSL certificates. Default is true.
    /// Set to false for self-signed certificates in development.
    /// </summary>
    public bool ValidateCertificates { get; init; } = true;

    /// <summary>
    /// Default HTTP options.
    /// </summary>
    public static HttpProviderOptions Default { get; } = new();
}

/// <summary>
/// Fluent builder for HTTP provider options.
/// </summary>
public sealed class HttpProviderOptionsBuilder
{
    private TimeSpan _requestTimeout = TimeSpan.FromSeconds(30);
    private bool _followRedirects = true;
    private int _maxRedirects = 5;
    private bool _validateCertificates = true;

    /// <summary>
    /// Sets the request timeout.
    /// </summary>
    /// <param name="timeout">The timeout duration.</param>
    public HttpProviderOptionsBuilder WithTimeout(TimeSpan timeout)
    {
        _requestTimeout = timeout;
        return this;
    }

    /// <summary>
    /// Sets the request timeout in seconds.
    /// </summary>
    /// <param name="seconds">The timeout in seconds.</param>
    public HttpProviderOptionsBuilder WithTimeout(int seconds)
    {
        _requestTimeout = TimeSpan.FromSeconds(seconds);
        return this;
    }

    /// <summary>
    /// Configures redirect behavior.
    /// </summary>
    /// <param name="follow">Whether to follow redirects.</param>
    /// <param name="maxRedirects">Maximum number of redirects to follow.</param>
    public HttpProviderOptionsBuilder FollowRedirects(bool follow = true, int maxRedirects = 5)
    {
        _followRedirects = follow;
        _maxRedirects = maxRedirects;
        return this;
    }

    /// <summary>
    /// Disables SSL certificate validation.
    /// Use with caution, primarily for development with self-signed certificates.
    /// </summary>
    public HttpProviderOptionsBuilder SkipCertificateValidation()
    {
        _validateCertificates = false;
        return this;
    }

    /// <summary>
    /// Builds the HTTP provider options.
    /// </summary>
    internal HttpProviderOptions Build() => new()
    {
        RequestTimeout = _requestTimeout,
        FollowRedirects = _followRedirects,
        MaxRedirects = _maxRedirects,
        ValidateCertificates = _validateCertificates
    };
}
