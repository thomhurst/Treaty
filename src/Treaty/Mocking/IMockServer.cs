namespace Treaty.Mocking;

/// <summary>
/// Represents an in-memory mock server for testing API consumers.
/// </summary>
public interface IMockServer : IAsyncDisposable
{
    /// <summary>
    /// Gets the base URL of the mock server once started.
    /// Returns null if the server has not been started.
    /// </summary>
    string? BaseUrl { get; }

    /// <summary>
    /// Starts the mock server.
    /// </summary>
    /// <param name="port">Optional port number. If not specified, a random available port is used.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StartAsync(int? port = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the mock server.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StopAsync(CancellationToken cancellationToken = default);
}
