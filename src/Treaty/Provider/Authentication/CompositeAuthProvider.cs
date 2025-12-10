namespace Treaty.Provider.Authentication;

/// <summary>
/// Combines multiple authentication providers into one.
/// </summary>
public sealed class CompositeAuthProvider : IAuthenticationProvider
{
    private readonly IReadOnlyList<IAuthenticationProvider> _providers;

    /// <summary>
    /// Initializes a new instance with multiple authentication providers.
    /// </summary>
    /// <param name="providers">The providers to combine.</param>
    public CompositeAuthProvider(params IAuthenticationProvider[] providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _providers = providers;
    }

    /// <summary>
    /// Initializes a new instance with multiple authentication providers.
    /// </summary>
    /// <param name="providers">The providers to combine.</param>
    public CompositeAuthProvider(IEnumerable<IAuthenticationProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _providers = providers.ToList();
    }

    /// <inheritdoc />
    public async Task ApplyAuthenticationAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        foreach (var provider in _providers)
        {
            await provider.ApplyAuthenticationAsync(request, cancellationToken);
        }
    }
}
