using Treaty.Contracts;
using Treaty.Provider;
using Treaty.Validation;

namespace Treaty.Testing;

/// <summary>
/// Represents a single endpoint test case generated from a contract.
/// Used with TUnit data sources to run parameterized contract tests.
/// </summary>
public sealed class EndpointTest
{
    /// <summary>
    /// Gets the endpoint contract being tested.
    /// </summary>
    public EndpointContract Endpoint { get; }

    /// <summary>
    /// Gets the full contract containing this endpoint.
    /// </summary>
    public Contract Contract { get; }

    /// <summary>
    /// Gets the display name for this test case.
    /// </summary>
    public string DisplayName => Endpoint.ToString();

    /// <summary>
    /// Creates a new endpoint test case.
    /// </summary>
    /// <param name="endpoint">The endpoint contract.</param>
    /// <param name="contract">The parent contract.</param>
    public EndpointTest(EndpointContract endpoint, Contract contract)
    {
        Endpoint = endpoint;
        Contract = contract;
    }

    /// <summary>
    /// Verifies this endpoint against a provider using the specified verifier.
    /// </summary>
    /// <typeparam name="TStartup">The provider's startup class.</typeparam>
    /// <param name="verifier">The provider verifier instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    /// <exception cref="ContractViolationException">Thrown when validation fails.</exception>
    public async Task VerifyAsync<TStartup>(
        ProviderVerifier<TStartup> verifier,
        CancellationToken cancellationToken = default) where TStartup : class
    {
        var path = Endpoint.GetExampleUrl();
        await verifier.VerifyAsync(
            path,
            Endpoint.Method,
            Endpoint.ExampleData?.RequestBody,
            Endpoint.ExampleData?.Headers.Count > 0
                ? new Dictionary<string, string>(Endpoint.ExampleData.Headers)
                : null,
            cancellationToken);
    }

    /// <summary>
    /// Verifies this endpoint and returns the result without throwing.
    /// </summary>
    /// <typeparam name="TStartup">The provider's startup class.</typeparam>
    /// <param name="verifier">The provider verifier instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    public async Task<ValidationResult> TryVerifyAsync<TStartup>(
        ProviderVerifier<TStartup> verifier,
        CancellationToken cancellationToken = default) where TStartup : class
    {
        var path = Endpoint.GetExampleUrl();
        return await verifier.TryVerifyAsync(
            path,
            Endpoint.Method,
            Endpoint.ExampleData?.RequestBody,
            Endpoint.ExampleData?.Headers.Count > 0
                ? new Dictionary<string, string>(Endpoint.ExampleData.Headers)
                : null,
            cancellationToken);
    }

    /// <inheritdoc/>
    public override string ToString() => DisplayName;
}
