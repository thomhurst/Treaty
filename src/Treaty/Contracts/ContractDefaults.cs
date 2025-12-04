namespace Treaty.Contracts;

/// <summary>
/// Represents default expectations that apply to all endpoints in a contract.
/// </summary>
public sealed class ContractDefaults
{
    /// <summary>
    /// Gets the default headers expected in all responses.
    /// </summary>
    public IReadOnlyDictionary<string, HeaderExpectation> ResponseHeaders { get; }

    /// <summary>
    /// Gets the default headers expected in all requests.
    /// </summary>
    public IReadOnlyDictionary<string, HeaderExpectation> RequestHeaders { get; }

    internal ContractDefaults(
        IReadOnlyDictionary<string, HeaderExpectation> responseHeaders,
        IReadOnlyDictionary<string, HeaderExpectation> requestHeaders)
    {
        ResponseHeaders = responseHeaders;
        RequestHeaders = requestHeaders;
    }
}
