using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Treaty.Contracts;
using Treaty.Validation;

namespace Treaty.Provider;

/// <summary>
/// Verifies that an API provider implementation meets contract expectations.
/// </summary>
/// <typeparam name="TStartup">The startup class of the API being verified.</typeparam>
public sealed class ProviderVerifier<TStartup> : IDisposable where TStartup : class
{
    private readonly Contract _contract;
    private readonly ILogger _logger;
    private readonly IHost _host;
    private readonly HttpClient _client;
    private readonly IStateHandler? _stateHandler;
    private bool _disposed;

    internal ProviderVerifier(Contract contract, ILoggerFactory loggerFactory, IStateHandler? stateHandler = null)
    {
        _contract = contract;
        _logger = loggerFactory.CreateLogger<ProviderVerifier<TStartup>>();
        _stateHandler = stateHandler;

        var builder = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .UseStartup<TStartup>();
            });

        _host = builder.Build();
        _host.Start();
        _client = _host.GetTestClient();
    }

    /// <summary>
    /// Gets the state handler configured for this verifier.
    /// </summary>
    public IStateHandler? StateHandler => _stateHandler;

    /// <summary>
    /// Verifies that the endpoint at the specified path meets contract expectations.
    /// Throws <see cref="ContractViolationException"/> if validation fails.
    /// </summary>
    /// <param name="path">The request path (e.g., "/users/123").</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="body">Optional request body.</param>
    /// <param name="headers">Optional request headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ContractViolationException">Thrown when contract violations are detected.</exception>
    public async Task VerifyAsync(
        string path,
        HttpMethod method,
        object? body = null,
        Dictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var result = await TryVerifyAsync(path, method, body, headers, cancellationToken);
        result.ThrowIfInvalid();
    }

    /// <summary>
    /// Verifies that the endpoint meets contract expectations without throwing.
    /// </summary>
    /// <param name="path">The request path (e.g., "/users/123").</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="body">Optional request body.</param>
    /// <param name="headers">Optional request headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A validation result indicating success or failure with violations.</returns>
    public async Task<ValidationResult> TryVerifyAsync(
        string path,
        HttpMethod method,
        object? body = null,
        Dictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = $"{method.Method} {path}";
        _logger.LogInformation("[Treaty] Validating {Endpoint}", endpoint);

        // Find matching endpoint contract
        var endpointContract = _contract.FindEndpoint(path, method);
        if (endpointContract == null)
        {
            return ValidationResult.Failure(endpoint, new ContractViolation(
                endpoint,
                "$",
                $"No contract definition found for endpoint {endpoint}",
                ViolationType.MissingRequired));
        }

        // Set up provider states
        var statesToTeardown = new List<ProviderState>();
        try
        {
            if (endpointContract.ProviderStates.Count > 0)
            {
                await SetupProviderStatesAsync(endpointContract, endpoint, statesToTeardown, cancellationToken);
            }

            return await ExecuteVerificationAsync(endpointContract, path, method, body, headers, endpoint, cancellationToken);
        }
        finally
        {
            // Tear down provider states in reverse order
            await TeardownProviderStatesAsync(statesToTeardown, endpoint, cancellationToken);
        }
    }

    private async Task SetupProviderStatesAsync(
        EndpointContract endpointContract,
        string endpoint,
        List<ProviderState> statesToTeardown,
        CancellationToken cancellationToken)
    {
        foreach (var state in endpointContract.ProviderStates)
        {
            if (_stateHandler != null)
            {
                _logger.LogDebug("[Treaty] Setting up provider state: {StateName}", state.Name);
                await _stateHandler.SetupAsync(state, cancellationToken);
                statesToTeardown.Add(state);
            }
            else
            {
                _logger.LogWarning(
                    "[Treaty] Provider state '{StateName}' declared for {Endpoint} but no state handler configured",
                    state.Name, endpoint);
            }
        }
    }

    private async Task TeardownProviderStatesAsync(
        List<ProviderState> states,
        string endpoint,
        CancellationToken cancellationToken)
    {
        if (_stateHandler == null || states.Count == 0)
            return;

        // Tear down in reverse order
        for (int i = states.Count - 1; i >= 0; i--)
        {
            try
            {
                _logger.LogDebug("[Treaty] Tearing down provider state: {StateName}", states[i].Name);
                await _stateHandler.TeardownAsync(states[i], cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Treaty] Error during teardown of state '{StateName}' for {Endpoint}",
                    states[i].Name, endpoint);
            }
        }
    }

    private async Task<ValidationResult> ExecuteVerificationAsync(
        EndpointContract endpointContract,
        string path,
        HttpMethod method,
        object? body,
        Dictionary<string, string>? headers,
        string endpoint,
        CancellationToken cancellationToken)
    {
        var violations = new List<ContractViolation>();

        // Build and send request
        var request = new HttpRequestMessage(method, path);

        // Add headers
        if (headers != null)
        {
            foreach (var (name, value) in headers)
            {
                request.Headers.TryAddWithoutValidation(name, value);
            }
        }

        // Add body
        if (body != null)
        {
            var json = _contract.JsonSerializer.Serialize(body, body.GetType());
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            // Validate request body
            if (endpointContract.RequestExpectation?.BodyValidator != null)
            {
                _logger.LogDebug("[Treaty] Validating request body");
                var requestViolations = endpointContract.RequestExpectation.BodyValidator.Validate(json, endpoint);
                violations.AddRange(requestViolations);
            }
        }
        else if (endpointContract.RequestExpectation?.IsRequired == true)
        {
            violations.Add(new ContractViolation(
                endpoint,
                "$",
                "Request body is required but was not provided",
                ViolationType.MissingRequired));
        }

        // Validate request headers
        ValidateRequestHeaders(endpointContract, request, endpoint, violations);

        // Send request
        var response = await _client.SendAsync(request, cancellationToken);

        // Validate response
        await ValidateResponseAsync(endpointContract, response, endpoint, violations, cancellationToken);

        if (violations.Count == 0)
        {
            _logger.LogInformation("[Treaty] ✓ {Endpoint} validation passed", endpoint);
            return ValidationResult.Success(endpoint);
        }

        _logger.LogWarning("[Treaty] ✗ {Endpoint} validation failed with {Count} violation(s)", endpoint, violations.Count);
        return ValidationResult.Failure(endpoint, violations);
    }

    private void ValidateRequestHeaders(
        EndpointContract endpointContract,
        HttpRequestMessage request,
        string endpoint,
        List<ContractViolation> violations)
    {
        // Check contract defaults
        if (_contract.Defaults?.RequestHeaders != null)
        {
            foreach (var (name, expectation) in _contract.Defaults.RequestHeaders)
            {
                ValidateHeader(request.Headers, name, expectation, endpoint, violations);
            }
        }

        // Check endpoint-specific headers
        foreach (var (name, expectation) in endpointContract.ExpectedHeaders)
        {
            ValidateHeader(request.Headers, name, expectation, endpoint, violations);
        }
    }

    private static void ValidateHeader(
        System.Net.Http.Headers.HttpHeaders headers,
        string name,
        HeaderExpectation expectation,
        string endpoint,
        List<ContractViolation> violations)
    {
        if (headers.TryGetValues(name, out var values))
        {
            var value = string.Join(", ", values);
            if (expectation.ExactValue != null &&
                !string.Equals(value, expectation.ExactValue, StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(new ContractViolation(
                    endpoint,
                    $"header:{name}",
                    $"Header '{name}' has incorrect value",
                    ViolationType.InvalidHeaderValue,
                    expectation.ExactValue,
                    value));
            }
        }
        else if (expectation.IsRequired)
        {
            violations.Add(new ContractViolation(
                endpoint,
                $"header:{name}",
                $"Missing required header '{name}'",
                ViolationType.MissingHeader));
        }
    }

    private async Task ValidateResponseAsync(
        EndpointContract endpointContract,
        HttpResponseMessage response,
        string endpoint,
        List<ContractViolation> violations,
        CancellationToken cancellationToken)
    {
        var statusCode = (int)response.StatusCode;
        _logger.LogDebug("[Treaty] Response status code: {StatusCode}", statusCode);

        // Find matching response expectation
        var responseExpectation = endpointContract.ResponseExpectations
            .FirstOrDefault(r => r.StatusCode == statusCode);

        if (responseExpectation == null && endpointContract.ResponseExpectations.Count > 0)
        {
            violations.Add(new ContractViolation(
                endpoint,
                "$",
                $"Unexpected status code {statusCode}",
                ViolationType.UnexpectedStatusCode,
                string.Join(", ", endpointContract.ResponseExpectations.Select(r => r.StatusCode)),
                statusCode.ToString()));
            return;
        }

        if (responseExpectation == null)
            return;

        _logger.LogDebug("[Treaty] ✓ Status code {StatusCode} matches expected", statusCode);

        // Validate content type
        if (responseExpectation.ContentType != null)
        {
            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType != null && !contentType.StartsWith(responseExpectation.ContentType.Split(';')[0], StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(new ContractViolation(
                    endpoint,
                    "$",
                    "Content type mismatch",
                    ViolationType.InvalidContentType,
                    responseExpectation.ContentType,
                    contentType));
            }
            else
            {
                _logger.LogDebug("[Treaty] ✓ Content-Type matches expected");
            }
        }

        // Validate response headers
        foreach (var (name, expectation) in responseExpectation.ExpectedHeaders)
        {
            ValidateHeader(response.Headers, name, expectation, endpoint, violations);
        }

        // Check contract defaults for response headers
        if (_contract.Defaults?.ResponseHeaders != null)
        {
            foreach (var (name, expectation) in _contract.Defaults.ResponseHeaders)
            {
                ValidateHeader(response.Headers, name, expectation, endpoint, violations);
            }
        }

        // Validate response body
        if (responseExpectation.BodyValidator != null)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!string.IsNullOrEmpty(body))
            {
                _logger.LogDebug("[Treaty] Validating response body");
                var bodyViolations = responseExpectation.BodyValidator.Validate(
                    body, endpoint, responseExpectation.PartialValidation);

                if (bodyViolations.Count == 0)
                {
                    _logger.LogDebug("[Treaty] ✓ Response body matches schema");
                }
                else
                {
                    foreach (var v in bodyViolations)
                    {
                        _logger.LogDebug("[Treaty] ✗ {Message}", v.Message);
                    }
                }

                violations.AddRange(bodyViolations);
            }
        }
    }

    /// <summary>
    /// Verifies all endpoints in the contract that have example data.
    /// </summary>
    /// <param name="options">Optional verification options.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A bulk verification result containing all endpoint results.</returns>
    public async Task<BulkVerificationResult> VerifyAllAsync(
        VerificationOptions? options = null,
        IProgress<VerificationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await VerifyAsync(_ => true, options, progress, cancellationToken);
    }

    /// <summary>
    /// Verifies endpoints that match the specified filter.
    /// </summary>
    /// <param name="filter">A predicate to filter which endpoints to verify.</param>
    /// <param name="options">Optional verification options.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A bulk verification result containing all endpoint results.</returns>
    public async Task<BulkVerificationResult> VerifyAsync(
        Func<EndpointContract, bool> filter,
        VerificationOptions? options = null,
        IProgress<VerificationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options ??= VerificationOptions.Default;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Get all endpoints matching the filter
        var allEndpoints = _contract.Endpoints.Where(filter).ToList();
        var endpointsToVerify = new List<EndpointContract>();
        var skippedCount = 0;

        foreach (var endpoint in allEndpoints)
        {
            if (endpoint.HasExampleData)
            {
                endpointsToVerify.Add(endpoint);
            }
            else if (options.SkipEndpointsWithoutExampleData)
            {
                _logger.LogDebug("[Treaty] Skipping {Endpoint} - no example data defined", endpoint);
                skippedCount++;
            }
            else
            {
                throw new InvalidOperationException(
                    $"Endpoint {endpoint} has no example data defined. " +
                    "Use .WithExampleData() to define example values, or set " +
                    "VerificationOptions.SkipEndpointsWithoutExampleData = true.");
            }
        }

        var totalEndpoints = endpointsToVerify.Count;
        _logger.LogInformation("[Treaty] Starting bulk verification of {Count} endpoint(s)", totalEndpoints);

        progress?.Report(VerificationProgress.Starting(totalEndpoints));

        var results = new List<EndpointVerificationResult>();
        var passed = 0;
        var failed = 0;

        if (options.ParallelExecution && totalEndpoints > 1)
        {
            results = await VerifyParallelAsync(endpointsToVerify, options, progress, totalEndpoints, cancellationToken);
            passed = results.Count(r => r.Passed);
            failed = results.Count(r => !r.Passed);
        }
        else
        {
            foreach (var endpoint in endpointsToVerify)
            {
                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report(new VerificationProgress(
                    totalEndpoints,
                    results.Count,
                    passed,
                    failed,
                    skippedCount,
                    endpoint,
                    $"Verifying {endpoint}..."));

                var result = await VerifyEndpointAsync(endpoint, options, cancellationToken);
                results.Add(result);

                if (result.Passed)
                    passed++;
                else
                    failed++;

                if (!result.Passed && options.StopOnFirstFailure)
                {
                    _logger.LogWarning("[Treaty] Stopping verification after first failure");
                    break;
                }
            }
        }

        stopwatch.Stop();
        progress?.Report(VerificationProgress.Completed(totalEndpoints, passed, failed, skippedCount));

        var bulkResult = new BulkVerificationResult(results, skippedCount, stopwatch.Elapsed);

        _logger.LogInformation("[Treaty] Bulk verification complete: {Passed}/{Total} passed in {Duration}ms",
            passed, totalEndpoints, stopwatch.ElapsedMilliseconds);

        return bulkResult;
    }

    private async Task<List<EndpointVerificationResult>> VerifyParallelAsync(
        List<EndpointContract> endpoints,
        VerificationOptions options,
        IProgress<VerificationProgress>? progress,
        int totalEndpoints,
        CancellationToken cancellationToken)
    {
        var results = new System.Collections.Concurrent.ConcurrentBag<EndpointVerificationResult>();
        var completed = 0;
        var passed = 0;
        var failed = 0;
        var lockObj = new object();

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = options.MaxDegreeOfParallelism,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(endpoints, parallelOptions, async (endpoint, ct) =>
        {
            var result = await VerifyEndpointAsync(endpoint, options, ct);
            results.Add(result);

            lock (lockObj)
            {
                completed++;
                if (result.Passed)
                    passed++;
                else
                    failed++;

                progress?.Report(new VerificationProgress(
                    totalEndpoints,
                    completed,
                    passed,
                    failed,
                    0,
                    null,
                    $"Completed {completed}/{totalEndpoints}"));
            }
        });

        return results.ToList();
    }

    private async Task<EndpointVerificationResult> VerifyEndpointAsync(
        EndpointContract endpoint,
        VerificationOptions options,
        CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Apply per-endpoint timeout if configured
            using var timeoutCts = options.PerEndpointTimeout.HasValue
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : null;

            if (timeoutCts != null)
            {
                timeoutCts.CancelAfter(options.PerEndpointTimeout!.Value);
            }

            var effectiveCt = timeoutCts?.Token ?? cancellationToken;

            // Build path with example data
            var path = endpoint.GetExampleUrl();

            // Build headers from example data
            Dictionary<string, string>? headers = null;
            if (endpoint.ExampleData?.Headers.Count > 0)
            {
                headers = new Dictionary<string, string>(endpoint.ExampleData.Headers);
            }

            // Get request body from example data
            var body = endpoint.ExampleData?.RequestBody;

            var validationResult = await TryVerifyAsync(path, endpoint.Method, body, headers, effectiveCt);

            stopwatch.Stop();
            return new EndpointVerificationResult(endpoint, validationResult, stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (options.PerEndpointTimeout.HasValue)
        {
            stopwatch.Stop();
            var timeoutViolation = new ContractViolation(
                endpoint.ToString(),
                "$",
                $"Verification timed out after {options.PerEndpointTimeout.Value.TotalSeconds:F1} seconds",
                ViolationType.Timeout);

            return new EndpointVerificationResult(
                endpoint,
                ValidationResult.Failure(endpoint.ToString(), timeoutViolation),
                stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Disposes resources used by the verifier.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _client.Dispose();
            _host.Dispose();
            _disposed = true;
        }
    }
}
