using Treaty.Consumer;

namespace Treaty;

/// <summary>
/// Entry point for creating consumer verifiers to validate client HTTP calls against contracts.
/// </summary>
public static class ConsumerVerifier
{
    /// <summary>
    /// Creates a consumer verifier for testing your HTTP client code against a contract.
    /// Use this when you are consuming an API and want to verify your client calls are correct.
    /// </summary>
    /// <returns>A consumer builder for configuration.</returns>
    /// <example>
    /// <code>
    /// var contract = Contract.FromOpenApi("api-spec.yaml").Build();
    ///
    /// var consumer = ConsumerVerifier.Create()
    ///     .WithContract(contract)
    ///     .Build();
    ///
    /// var httpClient = consumer.CreateHttpClient();
    /// // Use httpClient - all requests are validated against contract
    /// </code>
    /// </example>
    public static ConsumerBuilder Create() => new();
}
