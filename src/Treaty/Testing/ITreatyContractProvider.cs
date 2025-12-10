using Treaty.Contracts;

namespace Treaty.Testing;

/// <summary>
/// Interface for providing contracts for testing.
/// Implement this interface to expose contracts to TUnit data sources.
/// </summary>
public interface ITreatyContractProvider
{
    /// <summary>
    /// Gets the contract to be verified.
    /// </summary>
    /// <returns>The contract instance.</returns>
    ApiContract GetContract();
}
