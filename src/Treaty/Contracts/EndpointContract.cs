using System.Text.RegularExpressions;

namespace Treaty.Contracts;

/// <summary>
/// Represents the contract for a single API endpoint.
/// </summary>
public sealed class EndpointContract
{
    private readonly Regex _pathPattern;
    private readonly List<string> _pathParameterNames;

    /// <summary>
    /// Gets the path template for this endpoint (e.g., "/users/{id}").
    /// </summary>
    public string PathTemplate { get; }

    /// <summary>
    /// Gets the HTTP method for this endpoint.
    /// </summary>
    public HttpMethod Method { get; }

    /// <summary>
    /// Gets the expected request specification.
    /// </summary>
    public RequestExpectation? RequestExpectation { get; }

    /// <summary>
    /// Gets the expected response specifications.
    /// </summary>
    public IReadOnlyList<ResponseExpectation> ResponseExpectations { get; }

    /// <summary>
    /// Gets the expected headers for requests to this endpoint.
    /// </summary>
    public IReadOnlyDictionary<string, HeaderExpectation> ExpectedHeaders { get; }

    /// <summary>
    /// Gets the expected query parameters for requests to this endpoint.
    /// </summary>
    public IReadOnlyDictionary<string, QueryParameterExpectation> ExpectedQueryParameters { get; }

    /// <summary>
    /// Gets the example data for this endpoint, used for automatic request generation.
    /// </summary>
    public ExampleData? ExampleData { get; }

    /// <summary>
    /// Gets the provider states that must be established before this endpoint can be tested.
    /// </summary>
    public IReadOnlyList<ProviderState> ProviderStates { get; }

    internal EndpointContract(
        string pathTemplate,
        HttpMethod method,
        RequestExpectation? requestExpectation,
        IReadOnlyList<ResponseExpectation> responseExpectations,
        IReadOnlyDictionary<string, HeaderExpectation> expectedHeaders,
        IReadOnlyDictionary<string, QueryParameterExpectation> expectedQueryParameters,
        ExampleData? exampleData = null,
        IReadOnlyList<ProviderState>? providerStates = null)
    {
        PathTemplate = pathTemplate;
        Method = method;
        RequestExpectation = requestExpectation;
        ResponseExpectations = responseExpectations;
        ExpectedHeaders = expectedHeaders;
        ExpectedQueryParameters = expectedQueryParameters;
        ExampleData = exampleData;
        ProviderStates = providerStates ?? [];

        (_pathPattern, _pathParameterNames) = BuildPathPattern(pathTemplate);
    }

    /// <summary>
    /// Checks if this endpoint matches the given path and method.
    /// </summary>
    /// <param name="path">The request path.</param>
    /// <param name="method">The HTTP method.</param>
    /// <returns>True if this endpoint matches.</returns>
    public bool Matches(string path, HttpMethod method)
    {
        if (Method != method)
            return false;

        // Remove query string if present
        var pathWithoutQuery = path.Split('?')[0];
        return _pathPattern.IsMatch(pathWithoutQuery);
    }

    /// <summary>
    /// Extracts path parameters from the given path.
    /// </summary>
    /// <param name="path">The request path.</param>
    /// <returns>A dictionary of parameter names to values.</returns>
    public IReadOnlyDictionary<string, string> ExtractPathParameters(string path)
    {
        var pathWithoutQuery = path.Split('?')[0];
        var match = _pathPattern.Match(pathWithoutQuery);
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (match.Success)
        {
            for (int i = 0; i < _pathParameterNames.Count; i++)
            {
                var paramName = _pathParameterNames[i];
                var groupName = $"param{i}";
                if (match.Groups[groupName].Success)
                {
                    parameters[paramName] = match.Groups[groupName].Value;
                }
            }
        }

        return parameters;
    }

    /// <summary>
    /// Gets a display string for this endpoint (e.g., "GET /users/{id}").
    /// </summary>
    public override string ToString() => $"{Method.Method} {PathTemplate}";

    /// <summary>
    /// Gets a value indicating whether this endpoint has example data defined
    /// that can be used for automatic request generation.
    /// </summary>
    public bool HasExampleData => ExampleData?.HasValues == true ||
                                   _pathParameterNames.Count == 0;

    /// <summary>
    /// Generates a concrete path by replacing path parameters with example values.
    /// </summary>
    /// <returns>The concrete path with parameters replaced, or the template if no example data is available.</returns>
    /// <exception cref="InvalidOperationException">Thrown when example data is missing for required path parameters.</exception>
    public string GetExamplePath()
    {
        if (_pathParameterNames.Count == 0)
            return PathTemplate;

        if (ExampleData == null)
            throw new InvalidOperationException(
                $"Cannot generate example path for '{PathTemplate}': No example data provided. " +
                $"Use WithExampleData() or WithExamplePathParams() to specify values for path parameters: {string.Join(", ", _pathParameterNames)}");

        var path = PathTemplate;
        foreach (var paramName in _pathParameterNames)
        {
            if (!ExampleData.PathParameters.TryGetValue(paramName, out var value))
            {
                throw new InvalidOperationException(
                    $"Cannot generate example path for '{PathTemplate}': Missing example value for path parameter '{paramName}'. " +
                    $"Use WithExampleData(e => e.WithPathParam(\"{paramName}\", value)) to specify a value.");
            }

            path = path.Replace($"{{{paramName}}}", value.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        return path;
    }

    /// <summary>
    /// Generates a concrete URL by replacing path parameters with example values
    /// and appending query parameters.
    /// </summary>
    /// <returns>The concrete URL with parameters replaced and query string appended.</returns>
    /// <exception cref="InvalidOperationException">Thrown when example data is missing for required path parameters.</exception>
    public string GetExampleUrl()
    {
        var path = GetExamplePath();

        if (ExampleData?.QueryParameters.Count > 0)
        {
            var queryParams = string.Join("&",
                ExampleData.QueryParameters.Select(kv =>
                    $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value.ToString() ?? "")}"));
            path = $"{path}?{queryParams}";
        }

        return path;
    }

    /// <summary>
    /// Gets the names of path parameters in this endpoint's path template.
    /// </summary>
    public IReadOnlyList<string> PathParameterNames => _pathParameterNames;

    private static (Regex pattern, List<string> parameterNames) BuildPathPattern(string pathTemplate)
    {
        var parameterNames = new List<string>();
        // First extract parameter names, then build the regex pattern
        var escaped = Regex.Escape(pathTemplate);
        // After Regex.Escape, {id} becomes \{id} (only { is escaped, not })
        // So we match \{ followed by anything except }, then }
        var pattern = "^" + Regex.Replace(
            escaped,
            @"\\{([^}]+)}",
            match =>
            {
                parameterNames.Add(match.Groups[1].Value);
                return $"(?<param{parameterNames.Count - 1}>[^/]+)";
            }) + "$";

        return (new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase), parameterNames);
    }
}
