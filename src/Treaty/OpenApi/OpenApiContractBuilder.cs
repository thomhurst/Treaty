using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Treaty.Contracts;
using Treaty.Serialization;
using Treaty.Validation;

namespace Treaty.OpenApi;

/// <summary>
/// Builder for creating contracts from OpenAPI specifications.
/// </summary>
public sealed class OpenApiContractBuilder
{
    private readonly OpenApiDocument _document;
    private readonly ILogger _logger;
    private IJsonSerializer _jsonSerializer = new SystemTextJsonSerializer();
    private readonly HashSet<string> _includedEndpoints = [];
    private readonly HashSet<string> _excludedEndpoints = [];

    internal OpenApiContractBuilder(string specPath)
        : this(File.OpenRead(specPath), Path.GetExtension(specPath).ToLowerInvariant() == ".json" ? OpenApiFormat.Json : OpenApiFormat.Yaml)
    {
    }

    internal OpenApiContractBuilder(Stream specStream, OpenApiFormat format)
    {
        _logger = NullLogger.Instance;

        var reader = new OpenApiStreamReader();
        _document = reader.Read(specStream, out var diagnostic);

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
    public OpenApiContractBuilder WithJsonSerializer(IJsonSerializer serializer)
    {
        _jsonSerializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        return this;
    }

    /// <summary>
    /// Includes only the specified endpoint in the contract.
    /// </summary>
    /// <param name="pathTemplate">The path template to include.</param>
    /// <returns>This builder for chaining.</returns>
    public OpenApiContractBuilder ForEndpoint(string pathTemplate)
    {
        _includedEndpoints.Add(pathTemplate);
        return this;
    }

    /// <summary>
    /// Excludes the specified endpoint from the contract.
    /// </summary>
    /// <param name="pathTemplate">The path template to exclude.</param>
    /// <returns>This builder for chaining.</returns>
    public OpenApiContractBuilder ExcludeEndpoint(string pathTemplate)
    {
        _excludedEndpoints.Add(pathTemplate);
        return this;
    }

    /// <summary>
    /// Builds the contract from the OpenAPI specification.
    /// </summary>
    /// <returns>The built contract.</returns>
    public Contract Build()
    {
        var endpoints = new List<EndpointContract>();

        foreach (var (path, pathItem) in _document.Paths)
        {
            // Check inclusion/exclusion
            if (_includedEndpoints.Count > 0 && !_includedEndpoints.Contains(path))
                continue;
            if (_excludedEndpoints.Contains(path))
                continue;

            foreach (var (operationType, operation) in pathItem.Operations)
            {
                var method = OperationTypeToHttpMethod(operationType);
                var endpoint = BuildEndpointContract(path, method, operation, pathItem.Parameters);
                endpoints.Add(endpoint);
            }
        }

        var name = _document.Info?.Title ?? "OpenAPI Contract";
        return new Contract(name, endpoints, _jsonSerializer, null);
    }

    private EndpointContract BuildEndpointContract(
        string path,
        HttpMethod method,
        OpenApiOperation operation,
        IList<OpenApiParameter>? pathParameters)
    {
        // Build request expectation
        RequestExpectation? requestExpectation = null;
        if (operation.RequestBody != null)
        {
            requestExpectation = BuildRequestExpectation(operation.RequestBody);
        }

        // Build response expectations
        var responseExpectations = new List<ResponseExpectation>();
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

        // Build header expectations
        var headers = new Dictionary<string, HeaderExpectation>(StringComparer.OrdinalIgnoreCase);
        foreach (var param in operation.Parameters.Where(p => p.In == ParameterLocation.Header))
        {
            headers[param.Name] = new HeaderExpectation(param.Name, param.Required, null, null);
        }

        // Build query parameter expectations
        var queryParams = new Dictionary<string, QueryParameterExpectation>(StringComparer.OrdinalIgnoreCase);
        foreach (var param in operation.Parameters.Where(p => p.In == ParameterLocation.Query))
        {
            var type = OpenApiSchemaToQueryParamType(param.Schema);
            queryParams[param.Name] = new QueryParameterExpectation(param.Name, param.Required, type, null);
        }

        return new EndpointContract(path, method, requestExpectation, responseExpectations, headers, queryParams);
    }

    private RequestExpectation BuildRequestExpectation(OpenApiRequestBody requestBody)
    {
        string? contentType = null;
        ISchemaValidator? validator = null;

        if (requestBody.Content.TryGetValue("application/json", out var mediaType))
        {
            contentType = "application/json";
            if (mediaType.Schema != null)
            {
                validator = new OpenApiSchemaValidator(mediaType.Schema, _jsonSerializer);
            }
        }
        else if (requestBody.Content.Count > 0)
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

    private ResponseExpectation BuildResponseExpectation(int statusCode, OpenApiResponse response)
    {
        string? contentType = null;
        ISchemaValidator? validator = null;

        if (response.Content.TryGetValue("application/json", out var mediaType))
        {
            contentType = "application/json";
            if (mediaType.Schema != null)
            {
                validator = new OpenApiSchemaValidator(mediaType.Schema, _jsonSerializer);
            }
        }
        else if (response.Content.Count > 0)
        {
            var firstContent = response.Content.First();
            contentType = firstContent.Key;
            if (firstContent.Value.Schema != null)
            {
                validator = new OpenApiSchemaValidator(firstContent.Value.Schema, _jsonSerializer);
            }
        }

        var headers = new Dictionary<string, HeaderExpectation>(StringComparer.OrdinalIgnoreCase);
        foreach (var (headerName, headerSchema) in response.Headers)
        {
            headers[headerName] = new HeaderExpectation(headerName, headerSchema.Required, null, null);
        }

        return new ResponseExpectation(statusCode, contentType, validator, headers, null);
    }

    private static HttpMethod OperationTypeToHttpMethod(OperationType operationType)
    {
        return operationType switch
        {
            OperationType.Get => HttpMethod.Get,
            OperationType.Post => HttpMethod.Post,
            OperationType.Put => HttpMethod.Put,
            OperationType.Delete => HttpMethod.Delete,
            OperationType.Patch => HttpMethod.Patch,
            OperationType.Head => HttpMethod.Head,
            OperationType.Options => HttpMethod.Options,
            OperationType.Trace => HttpMethod.Trace,
            _ => HttpMethod.Get
        };
    }

    private static QueryParameterType OpenApiSchemaToQueryParamType(OpenApiSchema? schema)
    {
        if (schema == null)
            return QueryParameterType.String;

        return schema.Type?.ToLowerInvariant() switch
        {
            "integer" => QueryParameterType.Integer,
            "number" => QueryParameterType.Number,
            "boolean" => QueryParameterType.Boolean,
            "array" => QueryParameterType.Array,
            _ => QueryParameterType.String
        };
    }
}
