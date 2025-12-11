using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Treaty.Contracts;
using Treaty.OpenApi;

namespace Treaty.Mocking;

/// <summary>
/// An in-memory mock server generated from a Treaty contract.
/// </summary>
public sealed class ContractMockServer : IMockServer
{
    private readonly ContractDefinition _contract;
    private readonly ILogger _logger;
    private readonly bool _useHttps;
    private readonly int? _minLatencyMs;
    private readonly int? _maxLatencyMs;
    private readonly AuthConfig? _authConfig;
    private readonly Dictionary<string, Func<object>> _customGenerators;
    private readonly Dictionary<string, ContractMockEndpointConfig> _endpointConfigs;
    private readonly ConcurrentDictionary<ContractMockResponseRule, int> _sequenceCallCounts = new();
    private readonly ConcurrentBag<RecordedRequest> _recordedRequests = [];
    private readonly object _lock = new();

    private WebApplication? _app;

    /// <summary>
    /// Gets the base URL of the mock server once started.
    /// </summary>
    public string? BaseUrl { get; private set; }

    /// <summary>
    /// Gets all recorded requests received by the mock server.
    /// </summary>
    public IReadOnlyList<RecordedRequest> RecordedRequests => _recordedRequests.ToArray();

    /// <summary>
    /// Clears all recorded requests.
    /// </summary>
    public void ClearRecordedRequests() => _recordedRequests.Clear();

    internal ContractMockServer(
        ContractDefinition contract,
        ILoggerFactory loggerFactory,
        bool useHttps,
        int? minLatencyMs,
        int? maxLatencyMs,
        AuthConfig? authConfig,
        Dictionary<string, Func<object>> customGenerators,
        Dictionary<string, ContractMockEndpointConfig> endpointConfigs)
    {
        _contract = contract;
        _logger = loggerFactory.CreateLogger<ContractMockServer>();
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
    /// <remarks>
    /// This method is not thread-safe. Do not call StartAsync concurrently with StopAsync or DisposeAsync.
    /// </remarks>
    public async Task StartAsync(int? port = null, CancellationToken cancellationToken = default)
    {
        WebApplication app;
        lock (_lock)
        {
            if (_app != null)
                throw new InvalidOperationException("The mock server is already started.");

            var builder = WebApplication.CreateBuilder();
            var host = port.HasValue ? "localhost" : "127.0.0.1";
            builder.WebHost.UseUrls($"{(_useHttps ? "https" : "http")}://{host}:{port ?? 0}");
            builder.Logging.ClearProviders();

            app = builder.Build();
            app.MapFallback(HandleRequestAsync);
            _app = app;
        }

        await app.StartAsync(cancellationToken);

        var serverAddresses = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()
            .Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>();
        BaseUrl = serverAddresses?.Addresses.FirstOrDefault() ?? $"{(_useHttps ? "https" : "http")}://localhost:{port}";

        _logger.LogInformation("[Treaty] Contract mock server started at {BaseUrl} for contract '{ContractName}'", BaseUrl, _contract.Name);
    }

    /// <summary>
    /// Stops the mock server.
    /// </summary>
    /// <remarks>
    /// This method is not thread-safe. Do not call StopAsync concurrently with StartAsync or DisposeAsync.
    /// </remarks>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        WebApplication? app;
        lock (_lock)
        {
            app = _app;
        }

        if (app != null)
        {
            await app.StopAsync(cancellationToken);
            _logger.LogInformation("[Treaty] Contract mock server stopped");
        }
    }

    private async Task HandleRequestAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";
        var methodString = context.Request.Method;
        var method = new HttpMethod(methodString);
        var endpoint = $"{methodString} {path}";

        _logger.LogDebug("[Treaty] Contract Mock: Received request {Endpoint}", endpoint);

        // Check authentication
        if (_authConfig?.RequiredHeader != null)
        {
            if (!context.Request.Headers.ContainsKey(_authConfig.RequiredHeader))
            {
                _logger.LogDebug("[Treaty] Contract Mock: Authentication header missing, returning {StatusCode}", _authConfig.MissingStatusCode);
                context.Response.StatusCode = _authConfig.MissingStatusCode;
                return;
            }
        }

        // Find matching endpoint in contract
        var endpointContract = _contract.FindEndpoint(path, method);
        if (endpointContract == null)
        {
            await WriteNotFoundResponseAsync(context, path, methodString);
            return;
        }

        // Get endpoint config if configured
        _endpointConfigs.TryGetValue(endpointContract.PathTemplate, out var endpointConfig);

        // Simulate latency (endpoint-specific or global, using Random.Shared for thread safety)
        var minLatency = endpointConfig?.MinLatencyMs ?? _minLatencyMs;
        var maxLatency = endpointConfig?.MaxLatencyMs ?? _maxLatencyMs;
        if (minLatency.HasValue && maxLatency.HasValue)
        {
            var delay = Random.Shared.Next(minLatency.Value, maxLatency.Value);
            await Task.Delay(delay);
        }

        // Extract path parameters
        var pathParams = endpointContract.ExtractPathParameters(path)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

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

        // Read request body
        string? requestBody = null;
        if (context.Request.ContentLength > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            requestBody = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        }

        // Record the request for verification
        _recordedRequests.Add(new RecordedRequest
        {
            Timestamp = DateTime.UtcNow,
            Method = methodString,
            Path = path,
            Body = requestBody,
            Headers = headers,
            QueryParams = queryParams,
            PathParams = pathParams
        });

        // Check for custom response rules
        if (endpointConfig != null)
        {
            var requestContext = new MockRequestContext(pathParams, queryParams, headers, requestBody);
            foreach (var rule in endpointConfig.ResponseRules)
            {
                if (rule.Condition(requestContext))
                {
                    // Handle fault injection
                    if (rule.Fault.HasValue)
                    {
                        _logger.LogDebug("[Treaty] Contract Mock: Injecting fault {FaultType}", rule.Fault.Value);
                        await InjectFaultAsync(context, rule.Fault.Value);
                        return;
                    }

                    _logger.LogDebug("[Treaty] Contract Mock: Using custom response rule, returning {StatusCode}", rule.StatusCode);
                    await WriteCustomResponseAsync(context, rule, endpointContract);
                    return;
                }
            }
        }

        // Generate default response from contract
        await WriteContractResponseAsync(context, endpointContract);
    }

    private async Task WriteCustomResponseAsync(HttpContext context, ContractMockResponseRule rule, EndpointContract endpointContract)
    {
        int statusCode;
        object? body;

        // Handle sequence responses
        if (rule.Sequence != null && rule.Sequence.Count > 0)
        {
            var callCount = _sequenceCallCounts.AddOrUpdate(rule, 1, (_, count) => count + 1);
            var index = Math.Min(callCount - 1, rule.Sequence.Count - 1);
            var sequenceResponse = rule.Sequence[index];
            statusCode = sequenceResponse.StatusCode;
            body = sequenceResponse.Body;
            _logger.LogDebug("[Treaty] Contract Mock: Sequence response {Index}/{Total}, returning {StatusCode}",
                index + 1, rule.Sequence.Count, statusCode);
        }
        else
        {
            statusCode = rule.StatusCode;
            body = rule.Body;
        }

        context.Response.StatusCode = statusCode;

        if (body != null)
        {
            context.Response.ContentType = "application/json";
            var json = _contract.JsonSerializer.Serialize(body, body.GetType());
            await context.Response.WriteAsync(json);
        }
        else
        {
            // Find matching response expectation for this status code
            var responseExpectation = endpointContract.ResponseExpectations
                .FirstOrDefault(r => r.StatusCode == statusCode);

            if (responseExpectation?.BodyGenerator != null)
            {
                context.Response.ContentType = responseExpectation.ContentType ?? "application/json";
                var sampleJson = responseExpectation.BodyGenerator.GenerateSample();
                await context.Response.WriteAsync(sampleJson);
            }
        }
    }

    private async Task WriteContractResponseAsync(HttpContext context, EndpointContract endpointContract)
    {
        // Find the first successful (2xx) response expectation
        var responseExpectation = endpointContract.ResponseExpectations
            .Where(r => r.StatusCode >= 200 && r.StatusCode < 300)
            .FirstOrDefault()
            ?? endpointContract.ResponseExpectations.FirstOrDefault();

        if (responseExpectation == null)
        {
            context.Response.StatusCode = 200;
            return;
        }

        context.Response.StatusCode = responseExpectation.StatusCode;

        // Add expected headers
        foreach (var (headerName, headerExpectation) in responseExpectation.ExpectedHeaders)
        {
            var headerValue = headerExpectation.ExactValue ?? GenerateHeaderValue(headerName);
            context.Response.Headers[headerName] = headerValue;
        }

        // Generate response body
        if (responseExpectation.BodyGenerator != null)
        {
            context.Response.ContentType = responseExpectation.ContentType ?? "application/json";
            // Use Response direction to exclude writeOnly fields from mock responses
            var sampleJson = responseExpectation.BodyGenerator.GenerateSample(ValidationDirection.Response);

            // Apply custom generators if needed
            if (_customGenerators.Count > 0)
            {
                sampleJson = ApplyCustomGenerators(sampleJson);
            }

            await context.Response.WriteAsync(sampleJson);
        }
    }

    private string ApplyCustomGenerators(string json)
    {
        try
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(json);
            if (node is System.Text.Json.Nodes.JsonObject obj)
            {
                ApplyCustomGeneratorsToObject(obj);
                return obj.ToJsonString();
            }
        }
        catch (Exception ex)
        {
            // Log the exception but continue with original JSON
            _logger.LogWarning(ex, "[Treaty] Contract Mock: Failed to apply custom generators to response JSON");
        }
        return json;
    }

    private void ApplyCustomGeneratorsToObject(System.Text.Json.Nodes.JsonObject obj)
    {
        var keys = obj.Select(kvp => kvp.Key).ToList();
        foreach (var key in keys)
        {
            if (_customGenerators.TryGetValue(key, out var generator))
            {
                var newValue = generator();
                obj[key] = System.Text.Json.Nodes.JsonValue.Create(newValue);
            }
            else if (obj[key] is System.Text.Json.Nodes.JsonObject nestedObj)
            {
                ApplyCustomGeneratorsToObject(nestedObj);
            }
            else if (obj[key] is System.Text.Json.Nodes.JsonArray arr)
            {
                foreach (var item in arr)
                {
                    if (item is System.Text.Json.Nodes.JsonObject itemObj)
                    {
                        ApplyCustomGeneratorsToObject(itemObj);
                    }
                }
            }
        }
    }

    private string GenerateHeaderValue(string headerName)
    {
        if (_customGenerators.TryGetValue(headerName, out var generator))
        {
            return generator().ToString() ?? "";
        }
        return Guid.NewGuid().ToString();
    }

    private async Task WriteNotFoundResponseAsync(HttpContext context, string requestedPath, string method)
    {
        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        context.Response.ContentType = "application/json";

        var availableEndpoints = _contract.Endpoints
            .Select(e => e.ToString())
            .ToList();

        var errorResponse = new
        {
            treaty_error = $"No endpoint defined for {method} {requestedPath}",
            contract = _contract.Name,
            available_endpoints = availableEndpoints
        };

        var json = JsonSerializer.Serialize(errorResponse);
        await context.Response.WriteAsync(json);
    }

    private async Task InjectFaultAsync(HttpContext context, FaultType fault)
    {
        switch (fault)
        {
            case FaultType.ConnectionReset:
                // Abort the connection to simulate a reset
                context.Abort();
                break;

            case FaultType.Timeout:
                // Delay for 30 seconds to simulate a timeout
                await Task.Delay(TimeSpan.FromSeconds(30));
                context.Response.StatusCode = 504;
                break;

            case FaultType.MalformedResponse:
                // Return invalid JSON
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{ invalid json: }}}");
                break;

            case FaultType.EmptyResponse:
                // Return empty body
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                break;
        }
    }

    /// <summary>
    /// Disposes the mock server resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        WebApplication? app;
        lock (_lock)
        {
            app = _app;
            _app = null;
        }

        if (app != null)
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
