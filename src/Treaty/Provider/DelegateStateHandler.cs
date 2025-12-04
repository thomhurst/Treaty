using Treaty.Contracts;

namespace Treaty.Provider;

/// <summary>
/// A state handler implementation that uses delegates for setup and teardown.
/// This provides a flexible, fluent way to configure state handlers without implementing a full class.
/// </summary>
public sealed class DelegateStateHandler : IStateHandler
{
    private readonly Dictionary<string, Func<ProviderState, CancellationToken, Task>> _setupHandlers
        = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, Func<ProviderState, CancellationToken, Task>> _teardownHandlers
        = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a setup handler for the specified state name.
    /// </summary>
    /// <param name="stateName">The state name to handle.</param>
    /// <param name="setup">The async setup function.</param>
    /// <returns>This handler for chaining.</returns>
    public DelegateStateHandler OnState(string stateName, Func<ProviderState, CancellationToken, Task> setup)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);
        ArgumentNullException.ThrowIfNull(setup);
        _setupHandlers[stateName] = setup;
        return this;
    }

    /// <summary>
    /// Registers a setup handler for the specified state name.
    /// </summary>
    /// <param name="stateName">The state name to handle.</param>
    /// <param name="setup">The async setup function.</param>
    /// <returns>This handler for chaining.</returns>
    public DelegateStateHandler OnState(string stateName, Func<ProviderState, Task> setup)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);
        ArgumentNullException.ThrowIfNull(setup);
        _setupHandlers[stateName] = (state, _) => setup(state);
        return this;
    }

    /// <summary>
    /// Registers a setup handler for the specified state name using the parameters dictionary directly.
    /// </summary>
    /// <param name="stateName">The state name to handle.</param>
    /// <param name="setup">The async setup function receiving the parameters.</param>
    /// <returns>This handler for chaining.</returns>
    public DelegateStateHandler OnState(string stateName, Func<IReadOnlyDictionary<string, object>, Task> setup)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);
        ArgumentNullException.ThrowIfNull(setup);
        _setupHandlers[stateName] = (state, _) => setup(state.Parameters);
        return this;
    }

    /// <summary>
    /// Registers a synchronous setup handler for the specified state name.
    /// </summary>
    /// <param name="stateName">The state name to handle.</param>
    /// <param name="setup">The setup action.</param>
    /// <returns>This handler for chaining.</returns>
    public DelegateStateHandler OnState(string stateName, Action<ProviderState> setup)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);
        ArgumentNullException.ThrowIfNull(setup);
        _setupHandlers[stateName] = (state, _) =>
        {
            setup(state);
            return Task.CompletedTask;
        };
        return this;
    }

    /// <summary>
    /// Registers a synchronous setup handler for the specified state name using the parameters dictionary.
    /// </summary>
    /// <param name="stateName">The state name to handle.</param>
    /// <param name="setup">The setup action receiving the parameters.</param>
    /// <returns>This handler for chaining.</returns>
    public DelegateStateHandler OnState(string stateName, Action<IReadOnlyDictionary<string, object>> setup)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);
        ArgumentNullException.ThrowIfNull(setup);
        _setupHandlers[stateName] = (state, _) =>
        {
            setup(state.Parameters);
            return Task.CompletedTask;
        };
        return this;
    }

    /// <summary>
    /// Registers a parameterless synchronous setup handler for the specified state name.
    /// </summary>
    /// <param name="stateName">The state name to handle.</param>
    /// <param name="setup">The setup action.</param>
    /// <returns>This handler for chaining.</returns>
    public DelegateStateHandler OnState(string stateName, Action setup)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);
        ArgumentNullException.ThrowIfNull(setup);
        _setupHandlers[stateName] = (_, _) =>
        {
            setup();
            return Task.CompletedTask;
        };
        return this;
    }

    /// <summary>
    /// Registers a teardown handler for the specified state name.
    /// </summary>
    /// <param name="stateName">The state name to handle.</param>
    /// <param name="teardown">The async teardown function.</param>
    /// <returns>This handler for chaining.</returns>
    public DelegateStateHandler WithTeardown(string stateName, Func<ProviderState, CancellationToken, Task> teardown)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);
        ArgumentNullException.ThrowIfNull(teardown);
        _teardownHandlers[stateName] = teardown;
        return this;
    }

    /// <summary>
    /// Registers a teardown handler for the specified state name.
    /// </summary>
    /// <param name="stateName">The state name to handle.</param>
    /// <param name="teardown">The async teardown function.</param>
    /// <returns>This handler for chaining.</returns>
    public DelegateStateHandler WithTeardown(string stateName, Func<ProviderState, Task> teardown)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);
        ArgumentNullException.ThrowIfNull(teardown);
        _teardownHandlers[stateName] = (state, _) => teardown(state);
        return this;
    }

    /// <summary>
    /// Registers a synchronous teardown handler for the specified state name.
    /// </summary>
    /// <param name="stateName">The state name to handle.</param>
    /// <param name="teardown">The teardown action.</param>
    /// <returns>This handler for chaining.</returns>
    public DelegateStateHandler WithTeardown(string stateName, Action<ProviderState> teardown)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);
        ArgumentNullException.ThrowIfNull(teardown);
        _teardownHandlers[stateName] = (state, _) =>
        {
            teardown(state);
            return Task.CompletedTask;
        };
        return this;
    }

    /// <inheritdoc/>
    public bool CanHandle(string stateName)
    {
        return _setupHandlers.ContainsKey(stateName);
    }

    /// <inheritdoc/>
    public async Task SetupAsync(ProviderState state, CancellationToken cancellationToken = default)
    {
        if (_setupHandlers.TryGetValue(state.Name, out var handler))
        {
            await handler(state, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task TeardownAsync(ProviderState state, CancellationToken cancellationToken = default)
    {
        if (_teardownHandlers.TryGetValue(state.Name, out var handler))
        {
            await handler(state, cancellationToken);
        }
    }

    /// <summary>
    /// Gets the names of all registered states.
    /// </summary>
    public IReadOnlyCollection<string> RegisteredStates => _setupHandlers.Keys;
}
