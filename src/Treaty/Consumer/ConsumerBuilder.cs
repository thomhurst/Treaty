using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Treaty.Contracts;

namespace Treaty.Consumer;

/// <summary>
/// Builder for creating consumer verifiers.
/// </summary>
public sealed class ConsumerBuilder
{
    private Contract? _contract;
    private ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;
    private string _baseUrl = "http://localhost";

    internal ConsumerBuilder() { }

    /// <summary>
    /// Specifies the contract to verify against.
    /// </summary>
    /// <param name="contract">The contract to use for verification.</param>
    /// <returns>This builder for chaining.</returns>
    public ConsumerBuilder WithContract(Contract contract)
    {
        _contract = contract ?? throw new ArgumentNullException(nameof(contract));
        return this;
    }

    /// <summary>
    /// Specifies the base URL for the API.
    /// </summary>
    /// <param name="baseUrl">The base URL.</param>
    /// <returns>This builder for chaining.</returns>
    public ConsumerBuilder WithBaseUrl(string baseUrl)
    {
        _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        return this;
    }

    /// <summary>
    /// Specifies a logger factory for diagnostic output.
    /// </summary>
    /// <param name="loggerFactory">The logger factory to use.</param>
    /// <returns>This builder for chaining.</returns>
    public ConsumerBuilder WithLogging(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        return this;
    }

    /// <summary>
    /// Builds the consumer verifier.
    /// </summary>
    /// <returns>The configured consumer verifier.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no contract was specified.</exception>
    public ConsumerVerifier Build()
    {
        if (_contract == null)
            throw new InvalidOperationException("A contract must be specified using WithContract().");

        return new ConsumerVerifier(_contract, _baseUrl, _loggerFactory);
    }
}
