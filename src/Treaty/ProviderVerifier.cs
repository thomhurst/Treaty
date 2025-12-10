using Treaty.Provider;

namespace Treaty;

/// <summary>
/// Entry point for creating provider verifiers to test API implementations against contracts.
/// </summary>
public static class ProviderVerifier
{
    /// <summary>
    /// Creates a provider verifier for testing your API implementation against a contract using TestServer.
    /// Use this when you are the API provider and want to verify your API meets the contract.
    /// </summary>
    /// <typeparam name="TStartup">The startup class of your API.</typeparam>
    /// <returns>A provider builder for configuration.</returns>
    /// <example>
    /// <code>
    /// var contract = Contract.FromOpenApi("api-spec.yaml").Build();
    ///
    /// using var provider = ProviderVerifier.ForTestServer&lt;MyApiStartup&gt;()
    ///     .WithContract(contract)
    ///     .Build();
    ///
    /// await provider.VerifyAsync("/users/123", HttpMethod.Get);
    /// </code>
    /// </example>
    public static ProviderBuilder<TStartup> ForTestServer<TStartup>() where TStartup : class
        => new();

    /// <summary>
    /// Creates an HTTP provider verifier for testing a live API against a contract.
    /// Use this when you need to verify an external API or deployed service.
    /// </summary>
    /// <returns>An HTTP provider builder for configuration.</returns>
    /// <example>
    /// <code>
    /// var contract = Contract.FromOpenApi("api-spec.yaml").Build();
    ///
    /// using var provider = ProviderVerifier.ForHttpClient()
    ///     .WithBaseUrl("https://api.staging.example.com")
    ///     .WithContract(contract)
    ///     .WithBearerToken("my-token")
    ///     .WithRetryPolicy()
    ///     .Build();
    ///
    /// await provider.VerifyAsync("/users/123", HttpMethod.Get);
    /// </code>
    /// </example>
    public static HttpProviderBuilder ForHttpClient() => new();
}
