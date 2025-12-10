using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Treaty.Serialization;

namespace Treaty.OpenApi;

/// <summary>
/// An in-memory mock server generated from an OpenAPI specification.
/// </summary>
public sealed class OpenApiMockServer : IAsyncDisposable
{
    private readonly OpenApiDocument _document;
    private readonly IJsonSerializer _serializer;
    private readonly ILogger<OpenApiMockServer> _logger;
    private readonly bool _useHttps;
    private readonly int? _minLatencyMs;
    private readonly int? _maxLatencyMs;
    private readonly AuthConfig? _authConfig;
    private readonly Dictionary<string, Func<object>> _customGenerators;
    private readonly Dictionary<string, MockEndpointConfig> _endpointConfigs;
    private readonly Random _random = new();

    private WebApplication? _app;

    /// <summary>
    /// Gets the base URL of the mock server once started.
    /// </summary>
    public string? BaseUrl { get; private set; }

    internal OpenApiMockServer(
        OpenApiDocument document,
        IJsonSerializer serializer,
        ILoggerFactory loggerFactory,
        bool useHttps,
        int? minLatencyMs,
        int? maxLatencyMs,
        AuthConfig? authConfig,
        Dictionary<string, Func<object>> customGenerators,
        Dictionary<string, MockEndpointConfig> endpointConfigs)
    {
        _document = document;
        _serializer = serializer;
        _logger = loggerFactory.CreateLogger<OpenApiMockServer>();
        _useHttps = useHttps;
        _minLatencyMs = minLatencyMs;
        _maxLatencyMs = maxLatencyMs;
        _authConfig = authConfig;
        _customGenerators = customGenerators;
        _endpointConfigs = endpointConfigs;
    }

    /// <summary>
    /// Starts the mock server.
    /// </summary>
    /// <param name="port">Optional port number. If not specified, a random available port is used.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StartAsync(int? port = null, CancellationToken cancellationToken = default)
    {
        var builder = WebApplication.CreateBuilder();
        // Use 127.0.0.1 instead of localhost for dynamic port binding (port 0)
        var host = port.HasValue ? "localhost" : "127.0.0.1";
        builder.WebHost.UseUrls($"{(_useHttps ? "https" : "http")}://{host}:{port ?? 0}");
        builder.Logging.ClearProviders();

        _app = builder.Build();

        // Set up the catch-all handler
        _app.MapFallback(HandleRequestAsync);

        await _app.StartAsync(cancellationToken);

        // Get the actual address the server is listening on
        var serverAddresses = _app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()
            .Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>();
        BaseUrl = serverAddresses?.Addresses.FirstOrDefault() ?? $"{(_useHttps ? "https" : "http")}://localhost:{port}";

        _logger.LogInformation("[Treaty] Mock server started at {BaseUrl}", BaseUrl);
    }

    /// <summary>
    /// Stops the mock server.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_app != null)
        {
            await _app.StopAsync(cancellationToken);
            _logger.LogInformation("[Treaty] Mock server stopped");
        }
    }

    private async Task HandleRequestAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";
        var method = context.Request.Method;
        var endpoint = $"{method} {path}";

        _logger.LogDebug("[Treaty] Mock: Received request {Endpoint}", endpoint);

        // Check authentication
        if (_authConfig?.RequiredHeader != null)
        {
            if (!context.Request.Headers.ContainsKey(_authConfig.RequiredHeader))
            {
                _logger.LogDebug("[Treaty] Mock: Authentication header missing, returning {StatusCode}", _authConfig.MissingStatusCode);
                context.Response.StatusCode = _authConfig.MissingStatusCode;
                return;
            }
        }

        // Simulate latency
        if (_minLatencyMs.HasValue && _maxLatencyMs.HasValue)
        {
            var delay = _random.Next(_minLatencyMs.Value, _maxLatencyMs.Value);
            await Task.Delay(delay);
        }

        // Find matching endpoint
        var (pathItem, matchedPath, pathParams) = FindMatchingPath(path);
        if (pathItem == null)
        {
            await WriteNotFoundResponseAsync(context, path);
            return;
        }

        var operationType = method.ToUpperInvariant() switch
        {
            "GET" => OperationType.Get,
            "POST" => OperationType.Post,
            "PUT" => OperationType.Put,
            "DELETE" => OperationType.Delete,
            "PATCH" => OperationType.Patch,
            "HEAD" => OperationType.Head,
            "OPTIONS" => OperationType.Options,
            _ => (OperationType?)null
        };

        if (operationType == null || !pathItem.Operations.TryGetValue(operationType.Value, out var operation))
        {
            await WriteNotFoundResponseAsync(context, path);
            return;
        }

        // Get query parameters
        var queryParams = context.Request.Query.ToDictionary(
            q => q.Key,
            q => q.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);

        // Get headers
        var headers = context.Request.Headers.ToDictionary(
            h => h.Key,
            h => h.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);

        // Check for custom response rules
        if (_endpointConfigs.TryGetValue(matchedPath!, out var endpointConfig))
        {
            var requestContext = new MockRequestContext(pathParams!, queryParams, headers);
            foreach (var rule in endpointConfig.ResponseRules)
            {
                if (rule.Condition(requestContext))
                {
                    _logger.LogDebug("[Treaty] Mock: Using custom response rule, returning {StatusCode}", rule.StatusCode);
                    context.Response.StatusCode = rule.StatusCode;

                    if (rule.Body != null)
                    {
                        context.Response.ContentType = "application/json";
                        var json = _serializer.Serialize(rule.Body, rule.Body.GetType());
                        await context.Response.WriteAsync(json);
                    }
                    else if (operation.Responses.TryGetValue(rule.StatusCode.ToString(), out var customResponse))
                    {
                        await WriteResponseAsync(context, rule.StatusCode, customResponse, operation);
                    }

                    return;
                }
            }
        }

        // Generate default response (first successful response or 200)
        var responseCode = operation.Responses.Keys
            .Select(k => int.TryParse(k, out var code) ? code : 0)
            .Where(c => c >= 200 && c < 300)
            .DefaultIfEmpty(200)
            .First();

        if (operation.Responses.TryGetValue(responseCode.ToString(), out var response) ||
            operation.Responses.TryGetValue("default", out response))
        {
            await WriteResponseAsync(context, responseCode, response, operation);
        }
        else
        {
            context.Response.StatusCode = 200;
        }
    }

    private async Task WriteResponseAsync(HttpContext context, int statusCode, OpenApiResponse response, OpenApiOperation operation)
    {
        context.Response.StatusCode = statusCode;

        // Add response headers
        foreach (var (headerName, headerSchema) in response.Headers)
        {
            context.Response.Headers[headerName] = GenerateHeaderValue(headerName, headerSchema.Schema);
        }

        // Generate response body
        if (response.Content.TryGetValue("application/json", out var mediaType) && mediaType.Schema != null)
        {
            context.Response.ContentType = "application/json";
            var body = GenerateValueFromSchema(mediaType.Schema);
            var json = JsonSerializer.Serialize(body);
            await context.Response.WriteAsync(json);
        }
        else if (response.Content.Count > 0)
        {
            var firstContent = response.Content.First();
            context.Response.ContentType = firstContent.Key;

            if (firstContent.Value.Schema != null)
            {
                var body = GenerateValueFromSchema(firstContent.Value.Schema);
                var json = JsonSerializer.Serialize(body);
                await context.Response.WriteAsync(json);
            }
        }
    }

    private async Task WriteNotFoundResponseAsync(HttpContext context, string requestedPath)
    {
        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        context.Response.ContentType = "application/json";

        var availableEndpoints = _document.Paths
            .SelectMany(p => p.Value.Operations.Select(op => $"{op.Key.ToString().ToUpperInvariant()} {p.Key}"))
            .ToList();

        var errorResponse = new
        {
            treaty_error = $"No endpoint defined for {context.Request.Method} {requestedPath}",
            available_endpoints = availableEndpoints
        };

        var json = JsonSerializer.Serialize(errorResponse);
        await context.Response.WriteAsync(json);
    }

    private (OpenApiPathItem? pathItem, string? matchedPath, Dictionary<string, string>? pathParams) FindMatchingPath(string requestPath)
    {
        foreach (var (pathTemplate, pathItem) in _document.Paths)
        {
            var (matches, pathParams) = MatchPath(pathTemplate, requestPath);
            if (matches)
            {
                return (pathItem, pathTemplate, pathParams);
            }
        }
        return (null, null, null);
    }

    private static (bool matches, Dictionary<string, string> pathParams) MatchPath(string template, string path)
    {
        var pathParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Convert template to regex pattern
        // Note: Regex.Escape only escapes { to \{, but NOT } - so we match \{...} not \{...\}
        var pattern = "^" + Regex.Replace(
            Regex.Escape(template),
            @"\\{([^}]+)}",
            match =>
            {
                return $"(?<{match.Groups[1].Value}>[^/]+)";
            }) + "$";

        var regex = new Regex(pattern, RegexOptions.IgnoreCase);
        var match = regex.Match(path);

        if (!match.Success)
            return (false, pathParams);

        foreach (var groupName in regex.GetGroupNames())
        {
            if (groupName != "0" && match.Groups[groupName].Success)
            {
                pathParams[groupName] = match.Groups[groupName].Value;
            }
        }

        return (true, pathParams);
    }

    private object? GenerateValueFromSchema(OpenApiSchema schema)
    {
        // Priority 1: Use example if provided
        if (schema.Example != null)
        {
            return ConvertOpenApiAny(schema.Example);
        }

        // Priority 2: Use enum values
        if (schema.Enum?.Count > 0)
        {
            return ConvertOpenApiAny(schema.Enum[0]);
        }

        var schemaType = schema.Type?.ToLowerInvariant();

        return schemaType switch
        {
            "object" => GenerateObject(schema),
            "array" => GenerateArray(schema),
            "string" => GenerateString(schema),
            "integer" => GenerateInteger(schema),
            "number" => GenerateNumber(schema),
            "boolean" => true,
            _ => schema.Properties?.Count > 0 ? GenerateObject(schema) : null
        };
    }

    private object GenerateObject(OpenApiSchema schema)
    {
        var result = new Dictionary<string, object?>();
        if (schema.Properties != null)
        {
            foreach (var (propName, propSchema) in schema.Properties)
            {
                // Check for custom generator
                if (_customGenerators.TryGetValue(propName, out var generator))
                {
                    result[propName] = generator();
                }
                else
                {
                    result[propName] = GenerateValueFromSchema(propSchema);
                }
            }
        }
        return result;
    }

    private object GenerateArray(OpenApiSchema schema)
    {
        if (schema.Items != null)
        {
            return new[] { GenerateValueFromSchema(schema.Items) };
        }
        return Array.Empty<object>();
    }

    private string GenerateString(OpenApiSchema schema)
    {
        return schema.Format?.ToLowerInvariant() switch
        {
            "email" => "user@example.com",
            "uri" or "url" => "https://example.com",
            "uuid" => Guid.NewGuid().ToString(),
            "date-time" => DateTime.UtcNow.ToString("O"),
            "date" => DateOnly.FromDateTime(DateTime.UtcNow).ToString("O"),
            "time" => TimeOnly.FromDateTime(DateTime.UtcNow).ToString("O"),
            "ipv4" => "192.168.1.1",
            "ipv6" => "::1",
            "hostname" => "example.com",
            "byte" => Convert.ToBase64String("sample"u8.ToArray()),
            _ => "string"
        };
    }

    private long GenerateInteger(OpenApiSchema schema)
    {
        if (schema.Minimum.HasValue)
            return (long)schema.Minimum.Value;
        if (schema.Maximum.HasValue)
            return (long)schema.Maximum.Value;
        return 1;
    }

    private decimal GenerateNumber(OpenApiSchema schema)
    {
        if (schema.Minimum.HasValue)
            return schema.Minimum.Value;
        if (schema.Maximum.HasValue)
            return schema.Maximum.Value;
        return 1.0m;
    }

    private string GenerateHeaderValue(string headerName, OpenApiSchema? schema)
    {
        if (_customGenerators.TryGetValue(headerName, out var generator))
        {
            return generator()?.ToString() ?? "";
        }

        if (schema != null)
        {
            return GenerateValueFromSchema(schema)?.ToString() ?? "";
        }

        return Guid.NewGuid().ToString();
    }

    private static object? ConvertOpenApiAny(Microsoft.OpenApi.Any.IOpenApiAny any)
    {
        return any switch
        {
            Microsoft.OpenApi.Any.OpenApiString s => s.Value,
            Microsoft.OpenApi.Any.OpenApiInteger i => i.Value,
            Microsoft.OpenApi.Any.OpenApiLong l => l.Value,
            Microsoft.OpenApi.Any.OpenApiFloat f => f.Value,
            Microsoft.OpenApi.Any.OpenApiDouble d => d.Value,
            Microsoft.OpenApi.Any.OpenApiBoolean b => b.Value,
            Microsoft.OpenApi.Any.OpenApiNull => null,
            Microsoft.OpenApi.Any.OpenApiArray arr => arr.Select(ConvertOpenApiAny).ToArray(),
            Microsoft.OpenApi.Any.OpenApiObject obj => obj.ToDictionary(kv => kv.Key, kv => ConvertOpenApiAny(kv.Value)),
            _ => any?.ToString()
        };
    }

    /// <summary>
    /// Disposes the mock server resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_app != null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
