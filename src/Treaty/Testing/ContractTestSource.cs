using Treaty.Contracts;

namespace Treaty.Testing;

/// <summary>
/// Provides test data from contracts for use with TUnit's [MethodDataSource] attribute.
/// </summary>
/// <example>
/// <code>
/// public class UserApiTests
/// {
///     private static readonly Contract _contract = new ContractBuilder("UserApiConsumer")
///         .ForEndpoint("/users/{id}", HttpMethod.Get)
///             .WithExamplePathParams(new { id = 123 })
///             .ExpectingResponse(200)
///                 .WithBody&lt;UserDto&gt;()
///                 .Done()
///             .Done()
///         .Build();
///
///     public static IEnumerable&lt;EndpointTest&gt; GetEndpoints()
///         =&gt; ContractTestSource.GetEndpointTests(_contract);
///
///     [Test]
///     [MethodDataSource(nameof(GetEndpoints))]
///     public async Task VerifyEndpoint(EndpointTest test)
///     {
///         using var verifier = ProviderBuilder.ForProvider&lt;ApiStartup&gt;(_contract).Build();
///         await test.VerifyAsync(verifier);
///     }
/// }
/// </code>
/// </example>
public static class ContractTestSource
{
    /// <summary>
    /// Gets all endpoint tests from a contract.
    /// </summary>
    /// <param name="contract">The contract to get tests from.</param>
    /// <param name="includeEndpointsWithoutExampleData">
    /// Whether to include endpoints without example data. Default is false.
    /// </param>
    /// <returns>Enumerable of endpoint tests.</returns>
    public static IEnumerable<EndpointTest> GetEndpointTests(
        Contract contract,
        bool includeEndpointsWithoutExampleData = false)
    {
        return contract.Endpoints
            .Where(e => includeEndpointsWithoutExampleData || e.HasExampleData)
            .Select(e => new EndpointTest(e, contract));
    }

    /// <summary>
    /// Gets endpoint tests from a contract provider.
    /// </summary>
    /// <typeparam name="TProvider">The contract provider type.</typeparam>
    /// <param name="includeEndpointsWithoutExampleData">
    /// Whether to include endpoints without example data. Default is false.
    /// </param>
    /// <returns>Enumerable of endpoint tests.</returns>
    public static IEnumerable<EndpointTest> GetEndpointTests<TProvider>(
        bool includeEndpointsWithoutExampleData = false)
        where TProvider : ITreatyContractProvider, new()
    {
        var provider = new TProvider();
        return GetEndpointTests(provider.GetContract(), includeEndpointsWithoutExampleData);
    }

    /// <summary>
    /// Gets endpoint tests filtered by HTTP method.
    /// </summary>
    /// <param name="contract">The contract to get tests from.</param>
    /// <param name="method">The HTTP method to filter by.</param>
    /// <returns>Enumerable of endpoint tests.</returns>
    public static IEnumerable<EndpointTest> GetEndpointTestsByMethod(
        Contract contract,
        HttpMethod method)
    {
        return contract.Endpoints
            .Where(e => e.HasExampleData && e.Method == method)
            .Select(e => new EndpointTest(e, contract));
    }

    /// <summary>
    /// Gets endpoint tests filtered by path prefix.
    /// </summary>
    /// <param name="contract">The contract to get tests from.</param>
    /// <param name="pathPrefix">The path prefix to filter by.</param>
    /// <returns>Enumerable of endpoint tests.</returns>
    public static IEnumerable<EndpointTest> GetEndpointTestsByPath(
        Contract contract,
        string pathPrefix)
    {
        return contract.Endpoints
            .Where(e => e.HasExampleData && e.PathTemplate.StartsWith(pathPrefix, StringComparison.OrdinalIgnoreCase))
            .Select(e => new EndpointTest(e, contract));
    }

    /// <summary>
    /// Gets endpoint tests with a custom filter.
    /// </summary>
    /// <param name="contract">The contract to get tests from.</param>
    /// <param name="filter">The filter predicate.</param>
    /// <returns>Enumerable of endpoint tests.</returns>
    public static IEnumerable<EndpointTest> GetEndpointTests(
        Contract contract,
        Func<EndpointContract, bool> filter)
    {
        return contract.Endpoints
            .Where(filter)
            .Select(e => new EndpointTest(e, contract));
    }
}
