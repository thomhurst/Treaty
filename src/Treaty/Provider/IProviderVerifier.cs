using Treaty.Contracts;
using Treaty.Validation;

namespace Treaty.Provider;

/// <summary>
/// Interface for provider verifiers that test API implementations against contracts.
/// </summary>
public interface IProviderVerifier : IDisposable
{
    /// <summary>
    /// Gets the state handler configured for this verifier.
    /// </summary>
    IStateHandler? StateHandler { get; }

    /// <summary>
    /// Verifies that the endpoint at the specified path meets contract expectations.
    /// Throws <see cref="ContractViolationException"/> if validation fails.
    /// </summary>
    /// <param name="path">The request path (e.g., "/users/123").</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="body">Optional request body.</param>
    /// <param name="headers">Optional request headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ContractViolationException">Thrown when contract violations are detected.</exception>
    Task VerifyAsync(
        string path,
        HttpMethod method,
        object? body = null,
        Dictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies that the endpoint meets contract expectations without throwing.
    /// </summary>
    /// <param name="path">The request path (e.g., "/users/123").</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="body">Optional request body.</param>
    /// <param name="headers">Optional request headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A validation result indicating success or failure with violations.</returns>
    Task<ValidationResult> TryVerifyAsync(
        string path,
        HttpMethod method,
        object? body = null,
        Dictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies all endpoints in the contract that have example data.
    /// </summary>
    /// <param name="options">Optional verification options.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A bulk verification result containing all endpoint results.</returns>
    Task<BulkVerificationResult> VerifyAllAsync(
        VerificationOptions? options = null,
        IProgress<VerificationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies endpoints that match the specified filter.
    /// </summary>
    /// <param name="filter">A predicate to filter which endpoints to verify.</param>
    /// <param name="options">Optional verification options.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A bulk verification result containing all endpoint results.</returns>
    Task<BulkVerificationResult> VerifyAsync(
        Func<EndpointContract, bool> filter,
        VerificationOptions? options = null,
        IProgress<VerificationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
