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

    internal EndpointContract(
        string pathTemplate,
        HttpMethod method,
        RequestExpectation? requestExpectation,
        IReadOnlyList<ResponseExpectation> responseExpectations,
        IReadOnlyDictionary<string, HeaderExpectation> expectedHeaders,
        IReadOnlyDictionary<string, QueryParameterExpectation> expectedQueryParameters)
    {
        PathTemplate = pathTemplate;
        Method = method;
        RequestExpectation = requestExpectation;
        ResponseExpectations = responseExpectations;
        ExpectedHeaders = expectedHeaders;
        ExpectedQueryParameters = expectedQueryParameters;

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
