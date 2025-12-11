namespace Treaty.Mocking;

/// <summary>
/// Types of faults that can be injected into mock server responses.
/// </summary>
public enum FaultType
{
    /// <summary>
    /// Simulates a connection reset by closing the connection abruptly.
    /// </summary>
    ConnectionReset,

    /// <summary>
    /// Simulates a timeout by delaying the response for 30 seconds.
    /// </summary>
    Timeout,

    /// <summary>
    /// Returns a malformed JSON response that cannot be parsed.
    /// </summary>
    MalformedResponse,

    /// <summary>
    /// Returns an empty response body regardless of the expected content.
    /// </summary>
    EmptyResponse
}
