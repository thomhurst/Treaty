using Treaty.Serialization;

namespace Treaty.Contracts;

/// <summary>
/// Represents a contract definition containing endpoint expectations.
/// Contracts are immutable once built and define the expected behavior of an API.
/// </summary>
public sealed class Contract
{
    /// <summary>
    /// Gets the name of the contract.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the endpoint definitions in this contract.
    /// </summary>
    public IReadOnlyList<EndpointContract> Endpoints { get; }

    /// <summary>
    /// Gets the JSON serializer used for this contract.
    /// </summary>
    public IJsonSerializer JsonSerializer { get; }

    /// <summary>
    /// Gets the default response expectations applied to all endpoints.
    /// </summary>
    public ContractDefaults? Defaults { get; }

    internal Contract(
        string name,
        IReadOnlyList<EndpointContract> endpoints,
        IJsonSerializer jsonSerializer,
        ContractDefaults? defaults)
    {
        Name = name;
        Endpoints = endpoints;
        JsonSerializer = jsonSerializer;
        Defaults = defaults;
    }

    /// <summary>
    /// Finds an endpoint contract matching the given path and method.
    /// </summary>
    /// <param name="path">The request path.</param>
    /// <param name="method">The HTTP method.</param>
    /// <returns>The matching endpoint contract, or null if not found.</returns>
    public EndpointContract? FindEndpoint(string path, HttpMethod method)
    {
        return Endpoints.FirstOrDefault(e => e.Matches(path, method));
    }
}
