using Treaty.Contracts;

namespace Treaty;

/// <summary>
/// Builder for specifying example data for an endpoint.
/// Example data is used for automatic request generation during bulk verification.
/// </summary>
public sealed class ExampleDataBuilder
{
    private readonly Dictionary<string, object> _pathParams = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object> _queryParams = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _headers = new(StringComparer.OrdinalIgnoreCase);
    private object? _requestBody;

    /// <summary>
    /// Specifies an example value for a path parameter.
    /// </summary>
    /// <param name="name">The parameter name (without braces).</param>
    /// <param name="value">The example value.</param>
    /// <returns>This builder for chaining.</returns>
    /// <example>
    /// <code>
    /// .WithExampleData(e => e.WithPathParam("id", 123))
    /// </code>
    /// </example>
    public ExampleDataBuilder WithPathParam(string name, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        _pathParams[name] = value;
        return this;
    }

    /// <summary>
    /// Specifies example values for multiple path parameters using an anonymous object.
    /// </summary>
    /// <param name="pathParams">An anonymous object containing path parameters.</param>
    /// <returns>This builder for chaining.</returns>
    /// <example>
    /// <code>
    /// .WithExampleData(e => e.WithPathParams(new { id = 123, version = "v1" }))
    /// </code>
    /// </example>
    public ExampleDataBuilder WithPathParams(object pathParams)
    {
        ArgumentNullException.ThrowIfNull(pathParams);
        foreach (var prop in pathParams.GetType().GetProperties())
        {
            var value = prop.GetValue(pathParams);
            if (value != null)
            {
                _pathParams[prop.Name] = value;
            }
        }
        return this;
    }

    /// <summary>
    /// Specifies an example value for a query parameter.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The example value.</param>
    /// <returns>This builder for chaining.</returns>
    public ExampleDataBuilder WithQueryParam(string name, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        _queryParams[name] = value;
        return this;
    }

    /// <summary>
    /// Specifies example values for multiple query parameters using an anonymous object.
    /// </summary>
    /// <param name="queryParams">An anonymous object containing query parameters.</param>
    /// <returns>This builder for chaining.</returns>
    public ExampleDataBuilder WithQueryParams(object queryParams)
    {
        ArgumentNullException.ThrowIfNull(queryParams);
        foreach (var prop in queryParams.GetType().GetProperties())
        {
            var value = prop.GetValue(queryParams);
            if (value != null)
            {
                _queryParams[prop.Name] = value;
            }
        }
        return this;
    }

    /// <summary>
    /// Specifies an example value for a request header.
    /// </summary>
    /// <param name="name">The header name.</param>
    /// <param name="value">The example value.</param>
    /// <returns>This builder for chaining.</returns>
    public ExampleDataBuilder WithHeader(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        _headers[name] = value;
        return this;
    }

    /// <summary>
    /// Specifies an example request body.
    /// </summary>
    /// <param name="body">The example request body object.</param>
    /// <returns>This builder for chaining.</returns>
    public ExampleDataBuilder WithBody(object body)
    {
        ArgumentNullException.ThrowIfNull(body);
        _requestBody = body;
        return this;
    }

    /// <summary>
    /// Specifies an example request body of a specific type.
    /// </summary>
    /// <typeparam name="T">The type of the request body.</typeparam>
    /// <param name="body">The example request body.</param>
    /// <returns>This builder for chaining.</returns>
    public ExampleDataBuilder WithBody<T>(T body) where T : class
    {
        ArgumentNullException.ThrowIfNull(body);
        _requestBody = body;
        return this;
    }

    internal ExampleData Build()
    {
        return new ExampleData(_pathParams, _queryParams, _headers, _requestBody);
    }
}
