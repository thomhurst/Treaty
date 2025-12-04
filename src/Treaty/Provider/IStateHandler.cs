using Treaty.Contracts;

namespace Treaty.Provider;

/// <summary>
/// Interface for handling provider state setup and teardown during verification.
/// Implement this interface to set up test data or system state before endpoint verification.
/// </summary>
public interface IStateHandler
{
    /// <summary>
    /// Sets up the provider state before verification.
    /// This method is called before each endpoint verification that declares the state.
    /// </summary>
    /// <param name="state">The provider state to set up.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public async Task SetupAsync(ProviderState state, CancellationToken cancellationToken)
    /// {
    ///     switch (state.Name)
    ///     {
    ///         case "a user exists":
    ///             var id = state.GetParameter&lt;int&gt;("id");
    ///             await _db.CreateUser(id, "Test User");
    ///             break;
    ///     }
    /// }
    /// </code>
    /// </example>
    Task SetupAsync(ProviderState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tears down the provider state after verification.
    /// This method is called after each endpoint verification that declares the state.
    /// </summary>
    /// <param name="state">The provider state to tear down.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task TeardownAsync(ProviderState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a value indicating whether this handler can handle the specified state.
    /// </summary>
    /// <param name="stateName">The state name to check.</param>
    /// <returns>True if this handler can handle the state.</returns>
    bool CanHandle(string stateName);
}
