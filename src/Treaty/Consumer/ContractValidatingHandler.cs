using Microsoft.Extensions.Logging;
using Treaty.Contracts;
using Treaty.Validation;

namespace Treaty.Consumer;

/// <summary>
/// A DelegatingHandler that validates outgoing HTTP requests against a contract.
/// </summary>
internal sealed class ContractValidatingHandler : DelegatingHandler
{
    private readonly Contract _contract;
    private readonly ILogger _logger;

    public ContractValidatingHandler(Contract contract, ILoggerFactory loggerFactory)
    {
        _contract = contract;
        _logger = loggerFactory.CreateLogger<ContractValidatingHandler>();
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.PathAndQuery ?? "/";
        var method = request.Method;
        var endpoint = $"{method.Method} {path}";

        _logger.LogDebug("[Treaty] Consumer: Validating request to {Endpoint}", endpoint);

        // Find matching endpoint contract
        var endpointContract = _contract.FindEndpoint(path, method);
        if (endpointContract == null)
        {
            _logger.LogWarning("[Treaty] Consumer: No contract found for {Endpoint}", endpoint);
            // No contract defined - pass through
            return await base.SendAsync(request, cancellationToken);
        }

        var violations = new List<ContractViolation>();

        // Validate request headers
        ValidateRequestHeaders(endpointContract, request, endpoint, violations);

        // Validate request body
        if (request.Content != null)
        {
            var body = await request.Content.ReadAsStringAsync(cancellationToken);
            if (!string.IsNullOrEmpty(body) && endpointContract.RequestExpectation?.BodyValidator != null)
            {
                _logger.LogDebug("[Treaty] Consumer: Validating request body");
                var bodyViolations = endpointContract.RequestExpectation.BodyValidator.Validate(body, endpoint);
                violations.AddRange(bodyViolations);
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

        // Validate query parameters
        ValidateQueryParameters(endpointContract, request, endpoint, violations);

        // If we have violations, throw before sending
        if (violations.Count > 0)
        {
            _logger.LogWarning("[Treaty] Consumer: Request to {Endpoint} has {Count} contract violation(s)", endpoint, violations.Count);
            throw new ContractViolationException(violations);
        }

        _logger.LogDebug("[Treaty] Consumer: Request to {Endpoint} passed validation", endpoint);

        // Send the request
        var response = await base.SendAsync(request, cancellationToken);

        // Validate response
        await ValidateResponseAsync(endpointContract, response, endpoint, cancellationToken);

        return response;
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

    private void ValidateQueryParameters(
        EndpointContract endpointContract,
        HttpRequestMessage request,
        string endpoint,
        List<ContractViolation> violations)
    {
        var queryString = request.RequestUri?.Query ?? "";
        var queryParams = System.Web.HttpUtility.ParseQueryString(queryString);

        foreach (var (name, expectation) in endpointContract.ExpectedQueryParameters)
        {
            var value = queryParams[name];
            if (string.IsNullOrEmpty(value))
            {
                if (expectation.IsRequired)
                {
                    violations.Add(new ContractViolation(
                        endpoint,
                        $"query:{name}",
                        $"Missing required query parameter '{name}'",
                        ViolationType.MissingQueryParameter));
                }
            }
            else
            {
                // Validate type
                var isValid = expectation.Type switch
                {
                    QueryParameterType.Integer => int.TryParse(value, out _),
                    QueryParameterType.Number => decimal.TryParse(value, out _),
                    QueryParameterType.Boolean => bool.TryParse(value, out _),
                    _ => true
                };

                if (!isValid)
                {
                    violations.Add(new ContractViolation(
                        endpoint,
                        $"query:{name}",
                        $"Query parameter '{name}' has invalid type",
                        ViolationType.InvalidQueryParameterValue,
                        expectation.Type.ToString(),
                        value));
                }
            }
        }
    }

    private async Task ValidateResponseAsync(
        EndpointContract endpointContract,
        HttpResponseMessage response,
        string endpoint,
        CancellationToken cancellationToken)
    {
        var statusCode = (int)response.StatusCode;
        var violations = new List<ContractViolation>();

        // Find matching response expectation
        var responseExpectation = endpointContract.ResponseExpectations
            .FirstOrDefault(r => r.StatusCode == statusCode);

        if (responseExpectation == null && endpointContract.ResponseExpectations.Count > 0)
        {
            _logger.LogWarning("[Treaty] Consumer: Unexpected status code {StatusCode} from {Endpoint}", statusCode, endpoint);
            // Don't fail on unexpected status codes from consumer side - the server might be misbehaving
            return;
        }

        if (responseExpectation == null)
            return;

        // Validate response body
        if (responseExpectation.BodyValidator != null)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!string.IsNullOrEmpty(body))
            {
                _logger.LogDebug("[Treaty] Consumer: Validating response body");
                var bodyViolations = responseExpectation.BodyValidator.Validate(
                    body, endpoint, responseExpectation.PartialValidation);
                violations.AddRange(bodyViolations);
            }
        }

        if (violations.Count > 0)
        {
            _logger.LogWarning("[Treaty] Consumer: Response from {Endpoint} has {Count} contract violation(s)", endpoint, violations.Count);
            throw new ContractViolationException(violations);
        }
    }
}
