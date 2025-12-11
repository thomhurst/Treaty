using Treaty.Contracts;
using Treaty.Provider;
using Treaty.Validation;

namespace Treaty.Testing;

/// <summary>
/// Represents a single endpoint test case generated from a contract.
/// Used with TUnit data sources to run parameterized contract tests.
/// </summary>
/// <remarks>
/// Creates a new endpoint test case.
/// </remarks>
/// <param name="endpoint">The endpoint contract.</param>
/// <param name="contract">The parent contract.</param>
public sealed class EndpointTest(EndpointContract endpoint, ContractDefinition contract)
{
    /// <summary>
    /// Gets the endpoint contract being tested.
    /// </summary>
    public EndpointContract Endpoint { get; } = endpoint;

    /// <summary>
    /// Gets the full contract containing this endpoint.
    /// </summary>
    public ContractDefinition Contract { get; } = contract;

    /// <summary>
    /// Gets the display name for this test case.
    /// </summary>
    public string DisplayName => Endpoint.ToString();

    /// <summary>
    /// Verifies this endpoint against a provider using the specified verifier.
    /// </summary>
    /// <typeparam name="TEntryPoint">The provider's entry point class (Program or Startup).</typeparam>
    /// <param name="verifier">The provider verifier instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    /// <exception cref="ContractViolationException">Thrown when validation fails.</exception>
    public async Task VerifyAsync<TEntryPoint>(
        ProviderVerifier<TEntryPoint> verifier,
        CancellationToken cancellationToken = default) where TEntryPoint : class
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
    /// <typeparam name="TEntryPoint">The provider's entry point class (Program or Startup).</typeparam>
    /// <param name="verifier">The provider verifier instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    public async Task<ValidationResult> TryVerifyAsync<TEntryPoint>(
        ProviderVerifier<TEntryPoint> verifier,
        CancellationToken cancellationToken = default) where TEntryPoint : class
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
