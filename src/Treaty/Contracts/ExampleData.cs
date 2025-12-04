namespace Treaty.Contracts;

/// <summary>
/// Contains example values for path parameters, query parameters, headers, and request body.
/// Used for automatic request generation during bulk verification.
/// </summary>
public sealed class ExampleData
{
    /// <summary>
    /// Gets the example values for path parameters.
    /// </summary>
    public IReadOnlyDictionary<string, object> PathParameters { get; }

    /// <summary>
    /// Gets the example values for query parameters.
    /// </summary>
    public IReadOnlyDictionary<string, object> QueryParameters { get; }

    /// <summary>
    /// Gets the example values for request headers.
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>
    /// Gets the example request body.
    /// </summary>
    public object? RequestBody { get; }

    internal ExampleData(
        IReadOnlyDictionary<string, object> pathParameters,
        IReadOnlyDictionary<string, object> queryParameters,
        IReadOnlyDictionary<string, string> headers,
        object? requestBody)
    {
        PathParameters = pathParameters;
        QueryParameters = queryParameters;
        Headers = headers;
        RequestBody = requestBody;
    }

    /// <summary>
    /// Creates an empty example data instance.
    /// </summary>
    public static ExampleData Empty { get; } = new ExampleData(
        new Dictionary<string, object>(),
        new Dictionary<string, object>(),
        new Dictionary<string, string>(),
        null);

    /// <summary>
    /// Gets a value indicating whether this example data has any values defined.
    /// </summary>
    public bool HasValues =>
        PathParameters.Count > 0 ||
        QueryParameters.Count > 0 ||
        Headers.Count > 0 ||
        RequestBody != null;
}
