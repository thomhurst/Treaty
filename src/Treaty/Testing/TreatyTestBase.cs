using Microsoft.Extensions.Logging;
using Treaty.Contracts;
using Treaty.Provider;

namespace Treaty.Testing;

/// <summary>
/// Base class for Treaty contract verification tests.
/// Provides automatic setup and teardown of the provider verifier.
/// </summary>
/// <typeparam name="TStartup">The startup class of the API being verified.</typeparam>
public abstract class TreatyTestBase<TStartup> : IDisposable where TStartup : class
{
    private ProviderVerifier<TStartup>? _provider;
    private bool _disposed;

    /// <summary>
    /// Gets the provider verifier instance.
    /// The verifier is lazily created when first accessed.
    /// </summary>
    protected ProviderVerifier<TStartup> Provider => _provider ??= CreateVerifier();

    /// <summary>
    /// Gets the contract to be verified.
    /// Override this to return your contract definition.
    /// </summary>
    protected abstract ApiContract Contract { get; }

    /// <summary>
    /// Gets the state handler for provider states, if any.
    /// Override this to provide state setup/teardown logic.
    /// </summary>
    protected virtual IStateHandler? StateHandler => null;

    /// <summary>
    /// Gets the logger factory for the verifier.
    /// Override to customize logging.
    /// </summary>
    protected virtual ILoggerFactory LoggerFactory => Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
    {
        builder.AddConsole();
        builder.SetMinimumLevel(LogLevel.Debug);
    });

    /// <summary>
    /// Creates the provider verifier.
    /// Override to customize verifier creation.
    /// </summary>
    protected virtual ProviderVerifier<TStartup> CreateVerifier()
    {
        var builder = new ProviderBuilder<TStartup>()
            .WithContract(Contract)
            .WithLogging(LoggerFactory);

        if (StateHandler != null)
        {
            builder.WithStateHandler(StateHandler);
        }

        ConfigureProvider(builder);

        return builder.Build();
    }

    /// <summary>
    /// Configures the provider builder before creating the verifier.
    /// Override to add custom configuration.
    /// </summary>
    /// <param name="builder">The provider builder.</param>
    protected virtual void ConfigureProvider(ProviderBuilder<TStartup> builder)
    {
        // Override to add custom configuration
    }

    /// <summary>
    /// Verifies all endpoints in the contract.
    /// </summary>
    /// <param name="options">Optional verification options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The bulk verification result.</returns>
    protected async Task<BulkVerificationResult> VerifyAllAsync(
        VerificationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return await Provider.VerifyAllAsync(options, null, cancellationToken);
    }

    /// <summary>
    /// Verifies all endpoints and throws if any fail.
    /// </summary>
    /// <param name="options">Optional verification options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected async Task VerifyAllAndThrowAsync(
        VerificationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var result = await VerifyAllAsync(options, cancellationToken);
        result.ThrowIfAnyFailed();
    }

    /// <summary>
    /// Verifies endpoints matching a filter.
    /// </summary>
    /// <param name="filter">The filter predicate.</param>
    /// <param name="options">Optional verification options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The bulk verification result.</returns>
    protected async Task<BulkVerificationResult> VerifyAsync(
        Func<EndpointContract, bool> filter,
        VerificationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return await Provider.VerifyAsync(filter, options, null, cancellationToken);
    }

    /// <summary>
    /// Gets all endpoint test cases from the contract.
    /// Useful for parameterized tests.
    /// </summary>
    /// <returns>Enumerable of endpoint tests.</returns>
    protected IEnumerable<EndpointTest> GetEndpointTests()
    {
        return Contract.Endpoints
            .Where(e => e.HasExampleData)
            .Select(e => new EndpointTest(e, Contract));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    /// <param name="disposing">Whether disposing managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _provider?.Dispose();
            }
            _disposed = true;
        }
    }
}
