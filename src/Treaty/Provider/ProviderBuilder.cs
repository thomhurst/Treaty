using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    private ContractDefinition? _contract;
    private ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;
    private IStateHandler? _stateHandler;
    private readonly List<Action<IServiceCollection>> _serviceConfigurations = [];
    private readonly List<Action<IConfigurationBuilder>> _configurationActions = [];
    private readonly List<Action<IWebHostBuilder>> _webHostConfigurations = [];
    private string? _environment;

    /// <summary>
    /// Creates a new provider builder.
    /// </summary>
    public ProviderBuilder() { }

    /// <summary>
    /// Specifies the contract to verify against.
    /// </summary>
    /// <param name="contract">The contract to use for verification.</param>
    /// <returns>This builder for chaining.</returns>
    public ProviderBuilder<TStartup> WithContract(ContractDefinition contract)
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
    /// Configures services for the test server. Use this to replace real services with test doubles.
    /// </summary>
    /// <param name="configure">Action to configure services.</param>
    /// <returns>This builder for chaining.</returns>
    /// <example>
    /// <code>
    /// .ConfigureServices(services =>
    /// {
    ///     // Replace real database with in-memory version
    ///     services.RemoveAll&lt;IUserRepository&gt;();
    ///     services.AddSingleton&lt;IUserRepository, InMemoryUserRepository&gt;();
    ///
    ///     // Replace external API client with mock
    ///     services.AddSingleton&lt;IPaymentClient&gt;(new MockPaymentClient());
    /// })
    /// </code>
    /// </example>
    public ProviderBuilder<TStartup> ConfigureServices(Action<IServiceCollection> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _serviceConfigurations.Add(configure);
        return this;
    }

    /// <summary>
    /// Configures application configuration for the test server. Use this to override settings like connection strings or feature flags.
    /// </summary>
    /// <param name="configure">Action to configure the configuration builder.</param>
    /// <returns>This builder for chaining.</returns>
    /// <example>
    /// <code>
    /// .ConfigureAppConfiguration(config =>
    /// {
    ///     config.AddInMemoryCollection(new Dictionary&lt;string, string?&gt;
    ///     {
    ///         ["ConnectionStrings:Default"] = "Server=localhost;Database=TestDb",
    ///         ["Features:NewCheckout"] = "true"
    ///     });
    /// })
    /// </code>
    /// </example>
    public ProviderBuilder<TStartup> ConfigureAppConfiguration(Action<IConfigurationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _configurationActions.Add(configure);
        return this;
    }

    /// <summary>
    /// Sets the hosting environment for the test server.
    /// </summary>
    /// <param name="environment">The environment name (e.g., "Development", "Testing", "Production").</param>
    /// <returns>This builder for chaining.</returns>
    /// <example>
    /// <code>
    /// .UseEnvironment("Testing")
    /// </code>
    /// </example>
    public ProviderBuilder<TStartup> UseEnvironment(string environment)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        return this;
    }

    /// <summary>
    /// Provides full control over web host configuration. Use this as an escape hatch for advanced scenarios.
    /// </summary>
    /// <param name="configure">Action to configure the web host builder.</param>
    /// <returns>This builder for chaining.</returns>
    /// <example>
    /// <code>
    /// .ConfigureWebHost(webBuilder =>
    /// {
    ///     webBuilder.ConfigureLogging(logging =>
    ///         logging.SetMinimumLevel(LogLevel.Warning));
    /// })
    /// </code>
    /// </example>
    public ProviderBuilder<TStartup> ConfigureWebHost(Action<IWebHostBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _webHostConfigurations.Add(configure);
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

        return new ProviderVerifier<TStartup>(
            _contract,
            _loggerFactory,
            _stateHandler,
            _serviceConfigurations,
            _configurationActions,
            _webHostConfigurations,
            _environment);
    }
}
