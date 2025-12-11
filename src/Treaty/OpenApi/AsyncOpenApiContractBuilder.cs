using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;
using Treaty.Contracts;
using Treaty.Serialization;
using Treaty.Validation;

namespace Treaty.OpenApi;

/// <summary>
/// Async builder for creating contracts from OpenAPI specifications.
/// Use this builder when you want to avoid blocking I/O during document loading.
/// </summary>
public sealed class AsyncOpenApiContractBuilder
{
    private readonly Func<CancellationToken, Task<OpenApiDocument>> _documentLoader;
    private readonly CancellationToken _cancellationToken;
    private readonly ILogger _logger;
    private IJsonSerializer _jsonSerializer = new SystemTextJsonSerializer();
    private readonly HashSet<string> _includedEndpoints = [];
    private readonly HashSet<string> _excludedEndpoints = [];

    internal AsyncOpenApiContractBuilder(string specPath, CancellationToken cancellationToken)
    {
        _logger = NullLogger.Instance;
        _cancellationToken = cancellationToken;

        _documentLoader = async ct =>
        {
            var settings = new OpenApiReaderSettings();
            settings.AddYamlReader();

            var result = await OpenApiDocument.LoadAsync(specPath, settings, ct).ConfigureAwait(false);
            LogDiagnostics(result.Diagnostic);
            return result.Document ?? throw new InvalidOperationException("Failed to load OpenAPI document");
        };
    }

    internal AsyncOpenApiContractBuilder(Stream specStream, OpenApiFormat format, CancellationToken cancellationToken)
    {
        _logger = NullLogger.Instance;
        _cancellationToken = cancellationToken;

        _documentLoader = async ct =>
        {
            var settings = new OpenApiReaderSettings();
            settings.AddYamlReader();

            var result = await OpenApiDocument.LoadAsync(specStream, format == OpenApiFormat.Json ? "json" : "yaml", settings, ct).ConfigureAwait(false);
            LogDiagnostics(result.Diagnostic);
            return result.Document ?? throw new InvalidOperationException("Failed to load OpenAPI document");
        };
    }

    private void LogDiagnostics(OpenApiDiagnostic? diagnostic)
    {
        if (diagnostic == null) return;

        if (diagnostic.Errors.Count > 0)
        {
            foreach (var error in diagnostic.Errors)
            {
                _logger.LogWarning("[Treaty] OpenAPI parsing error: {Message}", error.Message);
            }
        }

        if (diagnostic.Warnings.Count > 0)
        {
            foreach (var warning in diagnostic.Warnings)
            {
                _logger.LogWarning("[Treaty] OpenAPI parsing warning: {Message}", warning.Message);
            }
        }
    }

    /// <summary>
    /// Specifies a custom JSON serializer for this contract.
    /// </summary>
    /// <param name="serializer">The JSON serializer to use.</param>
    /// <returns>This builder for chaining.</returns>
    public AsyncOpenApiContractBuilder WithJsonSerializer(IJsonSerializer serializer)
    {
        _jsonSerializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        return this;
    }

    /// <summary>
    /// Includes only the specified endpoint in the contract.
    /// </summary>
    /// <param name="pathTemplate">The path template to include.</param>
    /// <returns>This builder for chaining.</returns>
    public AsyncOpenApiContractBuilder ForEndpoint(string pathTemplate)
    {
        _includedEndpoints.Add(pathTemplate);
        return this;
    }

    /// <summary>
    /// Excludes the specified endpoint from the contract.
    /// </summary>
    /// <param name="pathTemplate">The path template to exclude.</param>
    /// <returns>This builder for chaining.</returns>
    public AsyncOpenApiContractBuilder ExcludeEndpoint(string pathTemplate)
    {
        _excludedEndpoints.Add(pathTemplate);
        return this;
    }

    /// <summary>
    /// Asynchronously builds the contract from the OpenAPI specification.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token to override the one provided at construction.</param>
    /// <returns>The built contract.</returns>
    public async Task<ContractDefinition> BuildAsync(CancellationToken cancellationToken = default)
    {
        var ct = cancellationToken == default ? _cancellationToken : cancellationToken;
        var document = await _documentLoader(ct).ConfigureAwait(false);

        var endpoints = new List<EndpointContract>();

        if (document.Paths != null)
        {
            foreach (var (path, pathItem) in document.Paths)
            {
                // Check inclusion/exclusion
                if (_includedEndpoints.Count > 0 && !_includedEndpoints.Contains(path))
                    continue;
                if (_excludedEndpoints.Contains(path))
                    continue;

                if (pathItem.Operations != null)
                {
                    foreach (var (httpMethod, operation) in pathItem.Operations)
                    {
                        var endpoint = BuildEndpointContract(path, httpMethod, operation, pathItem.Parameters);
                        endpoints.Add(endpoint);
                    }
                }
            }
        }

        var name = document.Info?.Title ?? "OpenAPI Contract";
        var metadata = BuildMetadata(document);
        return new ContractDefinition(name, endpoints, _jsonSerializer, null, metadata);
    }

    private ContractMetadata? BuildMetadata(OpenApiDocument document)
    {
        var info = document.Info;
        if (info == null)
            return null;

        ContractContact? contact = null;
        if (info.Contact != null)
        {
            contact = new ContractContact(
                info.Contact.Name,
                info.Contact.Email,
                info.Contact.Url?.ToString());
        }

        ContractLicense? license = null;
        if (info.License != null)
        {
            license = new ContractLicense(
                info.License.Name ?? "Unknown",
                info.License.Url?.ToString());
        }

        return new ContractMetadata(
            version: info.Version,
            description: info.Description,
            contact: contact,
            license: license,
            termsOfService: info.TermsOfService?.ToString());
    }

    private EndpointContract BuildEndpointContract(
        string path,
        HttpMethod method,
        OpenApiOperation operation,
        IList<IOpenApiParameter>? pathParameters)
    {
        // Build request expectation
        RequestExpectation? requestExpectation = null;
        object? requestBodyExample = null;
        if (operation.RequestBody != null)
        {
            requestExpectation = BuildRequestExpectation(operation.RequestBody);
            requestBodyExample = ExtractRequestBodyExample(operation.RequestBody);
        }

        // Build response expectations
        var responseExpectations = new List<ResponseExpectation>();
        if (operation.Responses != null)
        {
            foreach (var (statusCode, response) in operation.Responses)
            {
                if (int.TryParse(statusCode, out var code))
                {
                    var expectation = BuildResponseExpectation(code, response);
                    responseExpectations.Add(expectation);
                }
                else if (statusCode == "default")
                {
                    // Default response - treat as 200 for mock purposes
                    var expectation = BuildResponseExpectation(200, response);
                    responseExpectations.Add(expectation);
                }
            }
        }

        // Build header expectations and extract examples
        var headers = new Dictionary<string, HeaderExpectation>(StringComparer.OrdinalIgnoreCase);
        var headerExamples = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (operation.Parameters != null)
        {
            foreach (var param in operation.Parameters.Where(p => p.In == ParameterLocation.Header))
            {
                headers[param.Name] = new HeaderExpectation(param.Name, param.Required, null, null);
                var example = ExtractParameterExample(param);
                if (example != null)
                {
                    headerExamples[param.Name] = example.ToString() ?? "";
                }
            }
        }

        // Build query parameter expectations and extract examples
        var queryParams = new Dictionary<string, QueryParameterExpectation>(StringComparer.OrdinalIgnoreCase);
        var queryExamples = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (operation.Parameters != null)
        {
            foreach (var param in operation.Parameters.Where(p => p.In == ParameterLocation.Query))
            {
                var type = OpenApiSchemaToQueryParamType(param.Schema);
                queryParams[param.Name] = new QueryParameterExpectation(param.Name, param.Required, type, null);
                var example = ExtractParameterExample(param);
                if (example != null)
                {
                    queryExamples[param.Name] = example;
                }
            }
        }

        // Extract path parameter examples (from operation parameters and path-level parameters)
        var pathParamExamples = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var operationPathParams = operation.Parameters?.Where(p => p.In == ParameterLocation.Path) ?? [];
        var pathLevelParams = pathParameters?.Where(p => p.In == ParameterLocation.Path) ?? [];
        var allPathParams = operationPathParams.Concat(pathLevelParams);

        foreach (var param in allPathParams)
        {
            var example = ExtractParameterExample(param);
            if (example != null && !pathParamExamples.ContainsKey(param.Name))
            {
                pathParamExamples[param.Name] = example;
            }
        }

        // Build ExampleData if we have any examples
        ExampleData? exampleData = null;
        if (pathParamExamples.Count > 0 || queryExamples.Count > 0 || headerExamples.Count > 0 || requestBodyExample != null)
        {
            exampleData = new ExampleData(pathParamExamples, queryExamples, headerExamples, requestBodyExample);
        }

        return new EndpointContract(path, method, requestExpectation, responseExpectations, headers, queryParams, exampleData);
    }

    private object? ExtractRequestBodyExample(IOpenApiRequestBody requestBody)
    {
        // Try application/json first
        if (requestBody.Content != null && requestBody.Content.TryGetValue("application/json", out var mediaType))
        {
            return ExtractMediaTypeExample(mediaType);
        }

        // Fall back to first content type
        if (requestBody.Content?.Count > 0)
        {
            return ExtractMediaTypeExample(requestBody.Content.First().Value);
        }

        return null;
    }

    private object? ExtractMediaTypeExample(IOpenApiMediaType mediaType)
    {
        // Check for direct example
        if (mediaType.Example != null)
        {
            return ConvertJsonNode(mediaType.Example);
        }

        // Check for named examples (use first one)
        if (mediaType.Examples?.Count > 0)
        {
            var firstExample = mediaType.Examples.First().Value;
            if (firstExample?.Value != null)
            {
                return ConvertJsonNode(firstExample.Value);
            }
        }

        // Check schema example
        if (mediaType.Schema?.Example != null)
        {
            return ConvertJsonNode(mediaType.Schema.Example);
        }

        return null;
    }

    private object? ExtractParameterExample(IOpenApiParameter parameter)
    {
        // Check for direct example
        if (parameter.Example != null)
        {
            return ConvertJsonNode(parameter.Example);
        }

        // Check for named examples (use first one)
        if (parameter.Examples?.Count > 0)
        {
            var firstExample = parameter.Examples.First().Value;
            if (firstExample?.Value != null)
            {
                return ConvertJsonNode(firstExample.Value);
            }
        }

        // Check schema example
        if (parameter.Schema?.Example != null)
        {
            return ConvertJsonNode(parameter.Schema.Example);
        }

        return null;
    }

    private object? ConvertJsonNode(JsonNode? node)
    {
        if (node == null)
            return null;

        return node switch
        {
            JsonValue value => ConvertJsonValue(value),
            JsonArray array => array.Select(ConvertJsonNode).ToList(),
            JsonObject obj => obj.ToDictionary(
                kvp => kvp.Key,
                kvp => ConvertJsonNode(kvp.Value)),
            _ => node.ToString()
        };
    }

    private static object? ConvertJsonValue(JsonValue value)
    {
        var kind = value.GetValueKind();

        return kind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => ExtractNumber(value),
            JsonValueKind.String => value.GetValue<string>(),
            JsonValueKind.Null => null,
            _ => value.ToString()
        };
    }

    private static object ExtractNumber(JsonValue value)
    {
        if (value.TryGetValue<int>(out var i)) return i;
        if (value.TryGetValue<long>(out var l)) return l;
        if (value.TryGetValue<double>(out var d)) return d;
        if (value.TryGetValue<decimal>(out var dec)) return dec;
        var str = value.ToString();
        if (int.TryParse(str, out var intVal)) return intVal;
        if (long.TryParse(str, out var longVal)) return longVal;
        if (double.TryParse(str, out var doubleVal)) return doubleVal;
        return str;
    }

    private RequestExpectation BuildRequestExpectation(IOpenApiRequestBody requestBody)
    {
        string? contentType = null;
        ISchemaValidator? validator = null;

        if (requestBody.Content != null && requestBody.Content.TryGetValue("application/json", out var mediaType))
        {
            contentType = "application/json";
            if (mediaType.Schema != null)
            {
                validator = new OpenApiSchemaValidator(mediaType.Schema, _jsonSerializer);
            }
        }
        else if (requestBody.Content?.Count > 0)
        {
            var firstContent = requestBody.Content.First();
            contentType = firstContent.Key;
            if (firstContent.Value.Schema != null)
            {
                validator = new OpenApiSchemaValidator(firstContent.Value.Schema, _jsonSerializer);
            }
        }

        return new RequestExpectation(contentType, validator, requestBody.Required);
    }

    private ResponseExpectation BuildResponseExpectation(int statusCode, IOpenApiResponse response)
    {
        string? contentType = null;
        ISchemaValidator? validator = null;

        if (response.Content != null && response.Content.TryGetValue("application/json", out var mediaType))
        {
            contentType = "application/json";
            if (mediaType.Schema != null)
            {
                validator = new OpenApiSchemaValidator(mediaType.Schema, _jsonSerializer);
            }
        }
        else if (response.Content?.Count > 0)
        {
            var firstContent = response.Content.First();
            contentType = firstContent.Key;
            if (firstContent.Value.Schema != null)
            {
                validator = new OpenApiSchemaValidator(firstContent.Value.Schema, _jsonSerializer);
            }
        }

        var headers = new Dictionary<string, HeaderExpectation>(StringComparer.OrdinalIgnoreCase);
        if (response.Headers != null)
        {
            foreach (var (headerName, headerSchema) in response.Headers)
            {
                headers[headerName] = new HeaderExpectation(headerName, headerSchema.Required, null, null);
            }
        }

        return new ResponseExpectation(statusCode, contentType, validator, headers, null);
    }

    private static QueryParameterType OpenApiSchemaToQueryParamType(IOpenApiSchema? schema)
    {
        if (schema == null)
            return QueryParameterType.String;

        var schemaType = schema.Type;
        if (schemaType == null)
            return QueryParameterType.String;

        if (schemaType.Value.HasFlag(JsonSchemaType.Integer)) return QueryParameterType.Integer;
        if (schemaType.Value.HasFlag(JsonSchemaType.Number)) return QueryParameterType.Number;
        if (schemaType.Value.HasFlag(JsonSchemaType.Boolean)) return QueryParameterType.Boolean;
        if (schemaType.Value.HasFlag(JsonSchemaType.Array)) return QueryParameterType.Array;

        return QueryParameterType.String;
    }
}
