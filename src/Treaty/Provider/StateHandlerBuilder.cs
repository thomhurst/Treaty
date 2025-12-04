using Treaty.Contracts;

namespace Treaty.Provider;

/// <summary>
/// Fluent builder for configuring state handlers.
/// </summary>
/// <example>
/// <code>
/// .WithStateHandler(states => states
///     .ForState("a user exists", async p =>
///     {
///         var id = (int)p["id"];
///         await _db.CreateUser(id, "Test User");
///     })
///     .ForState("the cart is empty", () =>
///     {
///         _cart.Clear();
///     }))
/// </code>
/// </example>
public sealed class StateHandlerBuilder
{
    private readonly DelegateStateHandler _handler = new();

    /// <summary>
    /// Registers an async setup handler for the specified state name.
    /// </summary>
    /// <param name="stateName">The state name to handle.</param>
    /// <param name="setup">The async setup function receiving the full provider state.</param>
    /// <returns>This builder for chaining.</returns>
    public StateHandlerBuilder ForState(string stateName, Func<ProviderState, CancellationToken, Task> setup)
    {
        _handler.OnState(stateName, setup);
        return this;
    }

    /// <summary>
    /// Registers an async setup handler for the specified state name.
    /// </summary>
    /// <param name="stateName">The state name to handle.</param>
    /// <param name="setup">The async setup function receiving the full provider state.</param>
    /// <returns>This builder for chaining.</returns>
    public StateHandlerBuilder ForState(string stateName, Func<ProviderState, Task> setup)
    {
        _handler.OnState(stateName, setup);
        return this;
    }

    /// <summary>
    /// Registers an async setup handler for the specified state name.
    /// </summary>
    /// <param name="stateName">The state name to handle.</param>
    /// <param name="setup">The async setup function receiving the parameters dictionary.</param>
    /// <returns>This builder for chaining.</returns>
    public StateHandlerBuilder ForState(string stateName, Func<IReadOnlyDictionary<string, object>, Task> setup)
    {
        _handler.OnState(stateName, setup);
        return this;
    }

    /// <summary>
    /// Registers a synchronous setup handler for the specified state name.
    /// </summary>
    /// <param name="stateName">The state name to handle.</param>
    /// <param name="setup">The setup action receiving the full provider state.</param>
    /// <returns>This builder for chaining.</returns>
    public StateHandlerBuilder ForState(string stateName, Action<ProviderState> setup)
    {
        _handler.OnState(stateName, setup);
        return this;
    }

    /// <summary>
    /// Registers a synchronous setup handler for the specified state name.
    /// </summary>
    /// <param name="stateName">The state name to handle.</param>
    /// <param name="setup">The setup action receiving the parameters dictionary.</param>
    /// <returns>This builder for chaining.</returns>
    public StateHandlerBuilder ForState(string stateName, Action<IReadOnlyDictionary<string, object>> setup)
    {
        _handler.OnState(stateName, setup);
        return this;
    }

    /// <summary>
    /// Registers a parameterless synchronous setup handler for the specified state name.
    /// </summary>
    /// <param name="stateName">The state name to handle.</param>
    /// <param name="setup">The setup action.</param>
    /// <returns>This builder for chaining.</returns>
    public StateHandlerBuilder ForState(string stateName, Action setup)
    {
        _handler.OnState(stateName, setup);
        return this;
    }

    /// <summary>
    /// Registers a teardown handler for the specified state name.
    /// Teardown is called after the endpoint verification, regardless of success or failure.
    /// </summary>
    /// <param name="stateName">The state name to handle.</param>
    /// <param name="teardown">The async teardown function.</param>
    /// <returns>This builder for chaining.</returns>
    public StateHandlerBuilder WithTeardown(string stateName, Func<ProviderState, CancellationToken, Task> teardown)
    {
        _handler.WithTeardown(stateName, teardown);
        return this;
    }

    /// <summary>
    /// Registers a teardown handler for the specified state name.
    /// </summary>
    /// <param name="stateName">The state name to handle.</param>
    /// <param name="teardown">The async teardown function.</param>
    /// <returns>This builder for chaining.</returns>
    public StateHandlerBuilder WithTeardown(string stateName, Func<ProviderState, Task> teardown)
    {
        _handler.WithTeardown(stateName, teardown);
        return this;
    }

    /// <summary>
    /// Registers a synchronous teardown handler for the specified state name.
    /// </summary>
    /// <param name="stateName">The state name to handle.</param>
    /// <param name="teardown">The teardown action.</param>
    /// <returns>This builder for chaining.</returns>
    public StateHandlerBuilder WithTeardown(string stateName, Action<ProviderState> teardown)
    {
        _handler.WithTeardown(stateName, teardown);
        return this;
    }

    /// <summary>
    /// Builds the state handler.
    /// </summary>
    /// <returns>The configured state handler.</returns>
    internal IStateHandler Build() => _handler;
}
