using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Treaty.Contracts;

namespace Treaty.Provider;

/// <summary>
/// Builder for creating provider verifiers.
/// </summary>
/// <typeparam name="TStartup">The startup class of the API being verified.</typeparam>
public sealed class ProviderBuilder<TStartup> where TStartup : class
{
    private Contract? _contract;
    private ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;
    private IStateHandler? _stateHandler;

    /// <summary>
    /// Creates a new provider builder.
    /// </summary>
    public ProviderBuilder() { }

    /// <summary>
    /// Specifies the contract to verify against.
    /// </summary>
    /// <param name="contract">The contract to use for verification.</param>
    /// <returns>This builder for chaining.</returns>
    public ProviderBuilder<TStartup> WithContract(Contract contract)
    {
        _contract = contract ?? throw new ArgumentNullException(nameof(contract));
        return this;
    }

    /// <summary>
    /// Specifies a logger factory for diagnostic output.
    /// </summary>
    /// <param name="loggerFactory">The logger factory to use.</param>
    /// <returns>This builder for chaining.</returns>
    public ProviderBuilder<TStartup> WithLogging(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        return this;
    }

    /// <summary>
    /// Specifies a state handler for setting up provider states before verification.
    /// </summary>
    /// <param name="stateHandler">The state handler implementation.</param>
    /// <returns>This builder for chaining.</returns>
    public ProviderBuilder<TStartup> WithStateHandler(IStateHandler stateHandler)
    {
        _stateHandler = stateHandler ?? throw new ArgumentNullException(nameof(stateHandler));
        return this;
    }

    /// <summary>
    /// Configures state handlers using a fluent builder.
    /// </summary>
    /// <param name="configure">Action to configure state handlers.</param>
    /// <returns>This builder for chaining.</returns>
    /// <example>
    /// <code>
    /// .WithStateHandler(states => states
    ///     .ForState("a user exists", async p =>
    ///     {
    ///         var id = (int)p["id"];
    ///         await _db.CreateUser(id, "Test");
    ///     })
    ///     .ForState("the cart is empty", () => _cart.Clear()))
    /// </code>
    /// </example>
    public ProviderBuilder<TStartup> WithStateHandler(Action<StateHandlerBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new StateHandlerBuilder();
        configure(builder);
        _stateHandler = builder.Build();
        return this;
    }

    /// <summary>
    /// Builds the provider verifier.
    /// </summary>
    /// <returns>The configured provider verifier.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no contract was specified.</exception>
    public ProviderVerifier<TStartup> Build()
    {
        if (_contract == null)
            throw new InvalidOperationException("A contract must be specified using WithContract().");

        return new ProviderVerifier<TStartup>(_contract, _loggerFactory, _stateHandler);
    }
}
