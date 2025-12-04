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
    private bool _disposed;

    internal ProviderVerifier(Contract contract, ILoggerFactory loggerFactory)
    {
        _contract = contract;
        _logger = loggerFactory.CreateLogger<ProviderVerifier<TStartup>>();

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
