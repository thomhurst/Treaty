namespace Treaty.Provider.Resilience;

/// <summary>
/// Interface for retry policies that handle transient failures.
/// </summary>
public interface IRetryPolicy
{
    /// <summary>
    /// Executes the operation with retry logic.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}
