namespace Treaty.Mocking;

/// <summary>
/// Represents a recorded HTTP request received by the mock server.
/// </summary>
public sealed record RecordedRequest
{
    /// <summary>
    /// The timestamp when the request was received.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// The HTTP method (GET, POST, PUT, DELETE, etc.).
    /// </summary>
    public string Method { get; init; } = string.Empty;

    /// <summary>
    /// The request path.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// The request body, or null if no body was sent.
    /// </summary>
    public string? Body { get; init; }

    /// <summary>
    /// The request headers.
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// The query string parameters.
    /// </summary>
    public IReadOnlyDictionary<string, string> QueryParams { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// The path parameters extracted from the URL template.
    /// </summary>
    public IReadOnlyDictionary<string, string> PathParams { get; init; } = new Dictionary<string, string>();
}
