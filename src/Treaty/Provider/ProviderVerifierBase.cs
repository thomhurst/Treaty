using System.Text;
using Microsoft.Extensions.Logging;
using Treaty.Contracts;
using Treaty.Validation;

namespace Treaty.Provider;

/// <summary>
/// Base class containing shared verification logic for provider verifiers.
/// </summary>
public abstract class ProviderVerifierBase : IProviderVerifier
{
    /// <summary>
    /// The contract to verify against.
    /// </summary>
    protected readonly ContractDefinition _contract;

    /// <summary>
    /// Logger for diagnostic output.
    /// </summary>
    protected readonly ILogger _logger;

    /// <summary>
    /// Optional state handler for provider states.
    /// </summary>
    protected readonly IStateHandler? _stateHandler;

    /// <summary>
    /// Whether this instance has been disposed.
    /// </summary>
    protected bool _disposed;

    /// <summary>
    /// Lock object for thread-safe disposal.
    /// </summary>
    protected readonly object _disposeLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderVerifierBase"/> class.
    /// </summary>
    /// <param name="contract">The contract to verify against.</param>
    /// <param name="loggerFactory">Logger factory for diagnostic output.</param>
    /// <param name="stateHandler">Optional state handler for provider states.</param>
    protected ProviderVerifierBase(
        ContractDefinition contract,
        ILoggerFactory loggerFactory,
        IStateHandler? stateHandler)
    {
        _contract = contract;
        _logger = loggerFactory.CreateLogger(GetType());
        _stateHandler = stateHandler;
    }

    /// <inheritdoc />
    public IStateHandler? StateHandler => _stateHandler;

    /// <summary>
    /// Sends an HTTP request and returns the response.
    /// </summary>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The HTTP response.</returns>
    protected abstract Task<HttpResponseMessage> SendRequestAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken);

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <summary>
    /// Sets up provider states before verification.
    /// </summary>
    protected async Task SetupProviderStatesAsync(
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

    /// <summary>
    /// Tears down provider states after verification.
    /// </summary>
    protected async Task TeardownProviderStatesAsync(
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

    /// <summary>
    /// Executes the core verification logic.
    /// </summary>
    protected async Task<ValidationResult> ExecuteVerificationAsync(
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
        var response = await SendRequestAsync(request, cancellationToken);

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

    /// <summary>
    /// Validates request headers against the contract.
    /// </summary>
    protected void ValidateRequestHeaders(
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

    /// <summary>
    /// Validates a single header against its expectation.
    /// </summary>
    protected static void ValidateHeader(
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

    /// <summary>
    /// Validates the HTTP response against the contract.
    /// </summary>
    protected async Task ValidateResponseAsync(
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

    /// <inheritdoc />
    public async Task<BulkVerificationResult> VerifyAllAsync(
        VerificationOptions? options = null,
        IProgress<VerificationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await VerifyAsync(_ => true, options, progress, cancellationToken);
    }

    /// <inheritdoc />
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

    /// <summary>
    /// Verifies endpoints in parallel.
    /// </summary>
    protected async Task<List<EndpointVerificationResult>> VerifyParallelAsync(
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

    /// <summary>
    /// Verifies a single endpoint with timeout handling.
    /// </summary>
    protected async Task<EndpointVerificationResult> VerifyEndpointAsync(
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

    /// <inheritdoc />
    public abstract void Dispose();
}
