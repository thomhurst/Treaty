using Microsoft.Extensions.Logging;
using Treaty.Consumer;
using Treaty.Contracts;
using Treaty.OpenApi;
using Treaty.Provider;
using Treaty.Serialization;

namespace Treaty;

/// <summary>
/// Main entry point for Treaty - a modern contract testing framework for .NET.
/// </summary>
public static class Treaty
{
    /// <summary>
    /// Starts defining a new contract using the fluent builder API.
    /// </summary>
    /// <returns>A contract builder for defining endpoints.</returns>
    /// <example>
    /// <code>
    /// var contract = Treaty.DefineContract()
    ///     .ForEndpoint("/users/{id}")
    ///         .WithMethod(HttpMethod.Get)
    ///         .ExpectingResponse(r => r
    ///             .WithStatus(200)
    ///             .WithJsonBody&lt;User&gt;())
    ///     .Build();
    /// </code>
    /// </example>
    public static ContractBuilder DefineContract() => new();

    /// <summary>
    /// Starts defining a new contract with a specific name.
    /// </summary>
    /// <param name="name">The name of the contract.</param>
    /// <returns>A contract builder for defining endpoints.</returns>
    public static ContractBuilder DefineContract(string name) => new(name);

    /// <summary>
    /// Creates a contract from an OpenAPI specification file.
    /// </summary>
    /// <param name="specPath">Path to the OpenAPI specification file (YAML or JSON).</param>
    /// <returns>An OpenAPI contract builder for customization.</returns>
    /// <example>
    /// <code>
    /// var contract = Treaty.FromOpenApiSpec("api-spec.yaml")
    ///     .ForEndpoint("/users/{id}")  // optional filtering
    ///     .Build();
    /// </code>
    /// </example>
    public static OpenApiContractBuilder FromOpenApiSpec(string specPath)
        => new(specPath);

    /// <summary>
    /// Creates a contract from an OpenAPI specification stream.
    /// </summary>
    /// <param name="specStream">Stream containing the OpenAPI specification.</param>
    /// <param name="format">The format of the specification (yaml or json).</param>
    /// <returns>An OpenAPI contract builder for customization.</returns>
    public static OpenApiContractBuilder FromOpenApiSpec(Stream specStream, OpenApiFormat format = OpenApiFormat.Yaml)
        => new(specStream, format);

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
    /// Creates a mock server from an OpenAPI specification.
    /// Use this to develop your consumer before the provider API is ready.
    /// </summary>
    /// <param name="specPath">Path to the OpenAPI specification file.</param>
    /// <returns>A mock server builder for configuration.</returns>
    /// <example>
    /// <code>
    /// var mock = Treaty.MockFromOpenApi("api-spec.yaml")
    ///     .ForEndpoint("/users/{id}")
    ///         .When(req => req.PathParam("id") == "0").Return(404)
    ///         .Otherwise().Return(200)
    ///     .Build();
    ///
    /// await mock.StartAsync();
    /// var baseUrl = mock.BaseUrl;
    /// </code>
    /// </example>
    public static MockServerBuilder MockFromOpenApi(string specPath) => new(specPath);

    /// <summary>
    /// Creates a mock server from an OpenAPI specification stream.
    /// </summary>
    /// <param name="specStream">Stream containing the OpenAPI specification.</param>
    /// <param name="format">The format of the specification.</param>
    /// <returns>A mock server builder for configuration.</returns>
    public static MockServerBuilder MockFromOpenApi(Stream specStream, OpenApiFormat format = OpenApiFormat.Yaml)
        => new(specStream, format);
}
