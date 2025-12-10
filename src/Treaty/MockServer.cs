using Treaty.Contracts;
using Treaty.Mocking;
using Treaty.OpenApi;

namespace Treaty;

/// <summary>
/// Entry point for creating mock servers from API contracts.
/// </summary>
public static class MockServer
{
    /// <summary>
    /// Creates a mock server from an OpenAPI specification file.
    /// Use this to develop your consumer before the provider API is ready.
    /// </summary>
    /// <param name="specPath">Path to the OpenAPI specification file.</param>
    /// <returns>A mock server builder for configuration.</returns>
    /// <example>
    /// <code>
    /// var mock = MockServer.FromOpenApi("api-spec.yaml")
    ///     .ForEndpoint("/users/{id}")
    ///         .When(req => req.PathParam("id") == "0").Return(404)
    ///         .Otherwise().Return(200)
    ///     .Build();
    ///
    /// await mock.StartAsync();
    /// var baseUrl = mock.BaseUrl;
    /// </code>
    /// </example>
    public static MockServerBuilder FromOpenApi(string specPath) => new(specPath);

    /// <summary>
    /// Creates a mock server from an OpenAPI specification stream.
    /// </summary>
    /// <param name="specStream">Stream containing the OpenAPI specification.</param>
    /// <param name="format">The format of the specification.</param>
    /// <returns>A mock server builder for configuration.</returns>
    public static MockServerBuilder FromOpenApi(Stream specStream, OpenApiFormat format = OpenApiFormat.Yaml)
        => new(specStream, format);

    /// <summary>
    /// Creates a mock server from a Treaty contract.
    /// Use this when you have already loaded a contract and want to create a mock server from it.
    /// </summary>
    /// <param name="contract">The contract to use for generating mock responses.</param>
    /// <returns>A mock server builder for configuration.</returns>
    /// <example>
    /// <code>
    /// var contract = Contract.FromOpenApi("api-spec.yaml").Build();
    ///
    /// var mock = MockServer.FromContract(contract)
    ///     .ForEndpoint("/users/{id}")
    ///         .When(req => req.PathParam("id") == "0").Return(404)
    ///         .Otherwise().Return(200)
    ///     .Build();
    ///
    /// await mock.StartAsync();
    /// var baseUrl = mock.BaseUrl;
    /// </code>
    /// </example>
    public static ContractMockServerBuilder FromContract(ContractDefinition contract)
        => new(contract);
}
