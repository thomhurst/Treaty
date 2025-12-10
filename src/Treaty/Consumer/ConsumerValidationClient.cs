using Microsoft.Extensions.Logging;
using Treaty.Contracts;

namespace Treaty.Consumer;

/// <summary>
/// Validates that consumer HTTP client code makes requests that conform to contracts.
/// </summary>
public sealed class ConsumerValidationClient
{
    private readonly ApiContract _contract;
    private readonly string _baseUrl;
    private readonly ILoggerFactory _loggerFactory;

    internal ConsumerValidationClient(ApiContract contract, string baseUrl, ILoggerFactory loggerFactory)
    {
        _contract = contract;
        _baseUrl = baseUrl;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Creates an HttpClient that validates all requests against the contract.
    /// </summary>
    /// <returns>An HttpClient with contract validation enabled.</returns>
    public HttpClient CreateHttpClient()
    {
        var handler = new ContractValidatingHandler(_contract, _loggerFactory)
        {
            InnerHandler = new HttpClientHandler()
        };

        return new HttpClient(handler)
        {
            BaseAddress = new Uri(_baseUrl)
        };
    }

    /// <summary>
    /// Creates a DelegatingHandler that validates requests against the contract.
    /// Use this when you need to integrate with existing HttpClient configurations.
    /// </summary>
    /// <param name="innerHandler">The inner handler to delegate to.</param>
    /// <returns>A DelegatingHandler with contract validation.</returns>
    public DelegatingHandler CreateHandler(HttpMessageHandler? innerHandler = null)
    {
        return new ContractValidatingHandler(_contract, _loggerFactory)
        {
            InnerHandler = innerHandler ?? new HttpClientHandler()
        };
    }
}
