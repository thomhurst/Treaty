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
    public ContractDefinition Build()
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
        var metadata = BuildMetadata();
        return new ContractDefinition(name, endpoints, _jsonSerializer, null, metadata);
    }

    private ContractMetadata? BuildMetadata()
    {
        var info = _document.Info;
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
                info.License.Name,
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
        IList<OpenApiParameter>? pathParameters)
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

        // Build header expectations and extract examples
        var headers = new Dictionary<string, HeaderExpectation>(StringComparer.OrdinalIgnoreCase);
        var headerExamples = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var param in operation.Parameters.Where(p => p.In == ParameterLocation.Header))
        {
            headers[param.Name] = new HeaderExpectation(param.Name, param.Required, null, null);
            var example = ExtractParameterExample(param);
            if (example != null)
            {
                headerExamples[param.Name] = example.ToString() ?? "";
            }
        }

        // Build query parameter expectations and extract examples
        var queryParams = new Dictionary<string, QueryParameterExpectation>(StringComparer.OrdinalIgnoreCase);
        var queryExamples = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
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

        // Extract path parameter examples (from operation parameters and path-level parameters)
        var pathParamExamples = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var allPathParams = operation.Parameters
            .Where(p => p.In == ParameterLocation.Path)
            .Concat(pathParameters?.Where(p => p.In == ParameterLocation.Path) ?? []);

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

    private object? ExtractRequestBodyExample(OpenApiRequestBody requestBody)
    {
        // Try application/json first
        if (requestBody.Content.TryGetValue("application/json", out var mediaType))
        {
            return ExtractMediaTypeExample(mediaType);
        }

        // Fall back to first content type
        if (requestBody.Content.Count > 0)
        {
            return ExtractMediaTypeExample(requestBody.Content.First().Value);
        }

        return null;
    }

    private object? ExtractMediaTypeExample(OpenApiMediaType mediaType)
    {
        // Check for direct example
        if (mediaType.Example != null)
        {
            return ConvertOpenApiAny(mediaType.Example);
        }

        // Check for named examples (use first one)
        if (mediaType.Examples?.Count > 0)
        {
            var firstExample = mediaType.Examples.First().Value;
            if (firstExample?.Value != null)
            {
                return ConvertOpenApiAny(firstExample.Value);
            }
        }

        // Check schema example
        if (mediaType.Schema?.Example != null)
        {
            return ConvertOpenApiAny(mediaType.Schema.Example);
        }

        return null;
    }

    private object? ExtractParameterExample(OpenApiParameter parameter)
    {
        // Check for direct example
        if (parameter.Example != null)
        {
            return ConvertOpenApiAny(parameter.Example);
        }

        // Check for named examples (use first one)
        if (parameter.Examples?.Count > 0)
        {
            var firstExample = parameter.Examples.First().Value;
            if (firstExample?.Value != null)
            {
                return ConvertOpenApiAny(firstExample.Value);
            }
        }

        // Check schema example
        if (parameter.Schema?.Example != null)
        {
            return ConvertOpenApiAny(parameter.Schema.Example);
        }

        return null;
    }

    private object? ConvertOpenApiAny(Microsoft.OpenApi.Any.IOpenApiAny? openApiAny)
    {
        if (openApiAny == null)
            return null;

        return openApiAny switch
        {
            Microsoft.OpenApi.Any.OpenApiString s => s.Value,
            Microsoft.OpenApi.Any.OpenApiInteger i => i.Value,
            Microsoft.OpenApi.Any.OpenApiLong l => l.Value,
            Microsoft.OpenApi.Any.OpenApiFloat f => f.Value,
            Microsoft.OpenApi.Any.OpenApiDouble d => d.Value,
            Microsoft.OpenApi.Any.OpenApiBoolean b => b.Value,
            Microsoft.OpenApi.Any.OpenApiNull => null,
            Microsoft.OpenApi.Any.OpenApiArray arr => arr.Select(ConvertOpenApiAny).ToList(),
            Microsoft.OpenApi.Any.OpenApiObject obj => obj.ToDictionary(
                kvp => kvp.Key,
                kvp => ConvertOpenApiAny(kvp.Value)),
            _ => openApiAny.ToString()
        };
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
