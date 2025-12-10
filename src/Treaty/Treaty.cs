using Treaty.Consumer;
using Treaty.Contracts;
using Treaty.Mocking;
using Treaty.OpenApi;
using Treaty.Provider;

namespace Treaty;

/// <summary>
/// Main entry point for Treaty - a modern contract testing framework for .NET.
/// Treaty works exclusively with OpenAPI specifications to define API contracts.
/// </summary>
public static class Treaty
{
    /// <summary>
    /// Creates a contract from an OpenAPI specification file.
    /// </summary>
    /// <param name="specPath">Path to the OpenAPI specification file (YAML or JSON).</param>
    /// <returns>An OpenAPI contract builder for customization.</returns>
    /// <example>
    /// <code>
    /// var contract = Treaty.OpenApi("api-spec.yaml")
    ///     .ForEndpoint("/users/{id}")  // optional filtering
    ///     .Build();
    /// </code>
    /// </example>
    public static OpenApiContractBuilder OpenApi(string specPath)
        => new(specPath);

    /// <summary>
    /// Creates a contract from an OpenAPI specification stream.
    /// </summary>
    /// <param name="specStream">Stream containing the OpenAPI specification.</param>
    /// <param name="format">The format of the specification (yaml or json).</param>
    /// <returns>An OpenAPI contract builder for customization.</returns>
    public static OpenApiContractBuilder OpenApi(Stream specStream, OpenApiFormat format = OpenApiFormat.Yaml)
        => new(specStream, format);

    /// <summary>
    /// Creates a mock server from an OpenAPI specification file.
    /// Use this to develop your consumer before the provider API is ready.
    /// </summary>
    /// <param name="specPath">Path to the OpenAPI specification file.</param>
    /// <returns>A mock server builder for configuration.</returns>
    /// <example>
    /// <code>
    /// var mock = Treaty.MockServer("api-spec.yaml")
    ///     .ForEndpoint("/users/{id}")
    ///         .When(req => req.PathParam("id") == "0").Return(404)
    ///         .Otherwise().Return(200)
    ///     .Build();
    ///
    /// await mock.StartAsync();
    /// var baseUrl = mock.BaseUrl;
    /// </code>
    /// </example>
    public static MockServerBuilder MockServer(string specPath) => new(specPath);

    /// <summary>
    /// Creates a mock server from an OpenAPI specification stream.
    /// </summary>
    /// <param name="specStream">Stream containing the OpenAPI specification.</param>
    /// <param name="format">The format of the specification.</param>
    /// <returns>A mock server builder for configuration.</returns>
    public static MockServerBuilder MockServer(Stream specStream, OpenApiFormat format = OpenApiFormat.Yaml)
        => new(specStream, format);

    /// <summary>
    /// Creates a mock server from a Treaty contract.
    /// Use this when you have already loaded a contract and want to create a mock server from it.
    /// </summary>
    /// <param name="contract">The contract to use for generating mock responses.</param>
    /// <returns>A mock server builder for configuration.</returns>
    /// <example>
    /// <code>
    /// var contract = Treaty.OpenApi("api-spec.yaml").Build();
    ///
    /// var mock = Treaty.MockServer(contract)
    ///     .ForEndpoint("/users/{id}")
    ///         .When(req => req.PathParam("id") == "0").Return(404)
    ///         .Otherwise().Return(200)
    ///     .Build();
    ///
    /// await mock.StartAsync();
    /// var baseUrl = mock.BaseUrl;
    /// </code>
    /// </example>
    public static ContractMockServerBuilder MockServer(Contract contract)
        => new(contract);

    /// <summary>
    /// Creates a provider verifier for testing your API implementation against a contract.
    /// Use this when you are the API provider and want to verify your API meets the contract.
    /// </summary>
    /// <typeparam name="TStartup">The startup class of your API.</typeparam>
    /// <returns>A provider builder for configuration.</returns>
    /// <example>
    /// <code>
    /// var provider = Treaty.ForProvider&lt;MyApiStartup&gt;()
    ///     .WithContract(contract)
    ///     .Build();
    ///
    /// await provider.VerifyAsync("/users/123", HttpMethod.Get);
    /// </code>
    /// </example>
    public static ProviderBuilder<TStartup> ForProvider<TStartup>() where TStartup : class
        => new();

    /// <summary>
    /// Creates a consumer verifier for testing your HTTP client code against a contract.
    /// Use this when you are consuming an API and want to verify your client calls are correct.
    /// </summary>
    /// <returns>A consumer builder for configuration.</returns>
    /// <example>
    /// <code>
    /// var consumer = Treaty.ForConsumer()
    ///     .WithContract(contract)
    ///     .Build();
    ///
    /// var httpClient = consumer.CreateHttpClient();
    /// // Use httpClient - all requests are validated against contract
    /// </code>
    /// </example>
    public static ConsumerBuilder ForConsumer() => new();

    /// <summary>
    /// Compares two contracts and returns a diff describing all changes.
    /// Use this to detect breaking changes between contract versions.
    /// </summary>
    /// <param name="oldContract">The baseline (old) contract.</param>
    /// <param name="newContract">The new contract to compare.</param>
    /// <returns>A diff containing all detected changes with severity levels.</returns>
    /// <example>
    /// <code>
    /// var oldContract = Treaty.OpenApi("api-v1.yaml").Build();
    /// var newContract = Treaty.OpenApi("api-v2.yaml").Build();
    ///
    /// var diff = Treaty.CompareContracts(oldContract, newContract);
    ///
    /// if (diff.HasBreakingChanges)
    /// {
    ///     Console.WriteLine("Breaking changes detected!");
    ///     foreach (var change in diff.BreakingChanges)
    ///     {
    ///         Console.WriteLine($"  - {change.Description}");
    ///     }
    /// }
    ///
    /// // Or throw if breaking changes exist
    /// diff.ThrowIfBreaking();
    /// </code>
    /// </example>
    public static ContractDiff CompareContracts(Contract oldContract, Contract newContract)
        => ContractComparer.Compare(oldContract, newContract);

    #region Legacy API (to be removed)

    /// <summary>
    /// Creates a contract from an OpenAPI specification file.
    /// </summary>
    /// <param name="specPath">Path to the OpenAPI specification file (YAML or JSON).</param>
    /// <returns>An OpenAPI contract builder for customization.</returns>
    [Obsolete("Use Treaty.OpenApi() instead. This method will be removed in the next major version.")]
    public static OpenApiContractBuilder FromOpenApiSpec(string specPath)
        => OpenApi(specPath);

    /// <summary>
    /// Creates a contract from an OpenAPI specification stream.
    /// </summary>
    /// <param name="specStream">Stream containing the OpenAPI specification.</param>
    /// <param name="format">The format of the specification (yaml or json).</param>
    /// <returns>An OpenAPI contract builder for customization.</returns>
    [Obsolete("Use Treaty.OpenApi() instead. This method will be removed in the next major version.")]
    public static OpenApiContractBuilder FromOpenApiSpec(Stream specStream, OpenApiFormat format = OpenApiFormat.Yaml)
        => OpenApi(specStream, format);

    /// <summary>
    /// Creates a mock server from an OpenAPI specification file.
    /// </summary>
    /// <param name="specPath">Path to the OpenAPI specification file.</param>
    /// <returns>A mock server builder for configuration.</returns>
    [Obsolete("Use Treaty.MockServer() instead. This method will be removed in the next major version.")]
    public static MockServerBuilder MockFromOpenApi(string specPath) => MockServer(specPath);

    /// <summary>
    /// Creates a mock server from an OpenAPI specification stream.
    /// </summary>
    /// <param name="specStream">Stream containing the OpenAPI specification.</param>
    /// <param name="format">The format of the specification.</param>
    /// <returns>A mock server builder for configuration.</returns>
    [Obsolete("Use Treaty.MockServer() instead. This method will be removed in the next major version.")]
    public static MockServerBuilder MockFromOpenApi(Stream specStream, OpenApiFormat format = OpenApiFormat.Yaml)
        => MockServer(specStream, format);

    /// <summary>
    /// Creates a mock server from a Treaty contract.
    /// </summary>
    /// <param name="contract">The contract to use for generating mock responses.</param>
    /// <returns>A mock server builder for configuration.</returns>
    [Obsolete("Use Treaty.MockServer(contract) instead. This method will be removed in the next major version.")]
    public static ContractMockServerBuilder MockFromContract(Contract contract)
        => MockServer(contract);

    #endregion
}
