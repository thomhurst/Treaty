using System.Text;
using Treaty.Contracts;
using Treaty.Validation;

namespace Treaty.Diagnostics;

/// <summary>
/// A comprehensive diagnostic report for a contract verification failure.
/// Provides detailed information to help debug verification issues.
/// </summary>
/// <remarks>
/// Creates a new diagnostic report.
/// </remarks>
public sealed class DiagnosticReport(
    string endpoint,
    IReadOnlyList<ContractViolation> violations,
    IReadOnlyList<ProviderState>? providerStates = null,
    string? requestSent = null,
    string? responseReceived = null,
    IReadOnlyList<JsonDiff>? bodyDiffs = null,
    int? statusCode = null,
    IReadOnlyList<int>? expectedStatusCodes = null)
{
    /// <summary>
    /// Gets the endpoint being verified (e.g., "GET /users/123").
    /// </summary>
    public string Endpoint { get; } = endpoint;

    /// <summary>
    /// Gets the contract violations found during verification.
    /// </summary>
    public IReadOnlyList<ContractViolation> Violations { get; } = violations;

    /// <summary>
    /// Gets the provider states that were set up for this verification.
    /// </summary>
    public IReadOnlyList<ProviderState> ProviderStates { get; } = providerStates ?? [];

    /// <summary>
    /// Gets the request that was sent.
    /// </summary>
    public string? RequestSent { get; } = requestSent;

    /// <summary>
    /// Gets the response that was received.
    /// </summary>
    public string? ResponseReceived { get; } = responseReceived;

    /// <summary>
    /// Gets the body diffs if response body validation failed.
    /// </summary>
    public IReadOnlyList<JsonDiff>? BodyDiffs { get; } = bodyDiffs;

    /// <summary>
    /// Gets the HTTP status code received.
    /// </summary>
    public int? StatusCode { get; } = statusCode;

    /// <summary>
    /// Gets the expected status codes from the contract.
    /// </summary>
    public IReadOnlyList<int>? ExpectedStatusCodes { get; } = expectedStatusCodes;

    /// <summary>
    /// Formats the diagnostic report as a detailed string.
    /// </summary>
    public string FormatDetailed()
    {
        var sb = new StringBuilder();

        sb.AppendLine("╔════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║                   TREATY VERIFICATION FAILED                       ║");
        sb.AppendLine("╚════════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        // Endpoint info
        sb.AppendLine($"Endpoint: {Endpoint}");

        // Provider states
        if (ProviderStates.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Provider States:");
            foreach (var state in ProviderStates)
            {
                sb.AppendLine($"  • {state}");
            }
        }

        // Status code
        if (StatusCode.HasValue)
        {
            sb.AppendLine();
            sb.AppendLine($"Response Status: {StatusCode}");
            if (ExpectedStatusCodes?.Count > 0)
            {
                sb.AppendLine($"Expected Status: {string.Join(", ", ExpectedStatusCodes)}");
            }
        }

        // Violations
        sb.AppendLine();
        sb.AppendLine($"Violations ({Violations.Count}):");
        sb.AppendLine(new string('-', 70));

        int index = 1;
        foreach (var violation in Violations)
        {
            sb.AppendLine();
            sb.AppendLine($"{index}. {violation.Type} at `{violation.Path}`:");
            sb.AppendLine($"   {violation.Message}");

            if (violation.Expected != null)
            {
                sb.AppendLine($"   Expected: {violation.Expected}");
            }
            if (violation.Actual != null)
            {
                sb.AppendLine($"   Actual:   {violation.Actual}");
            }

            index++;
        }

        // Body diff
        if (BodyDiffs?.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Body Diff:");
            sb.AppendLine(new string('-', 70));
            sb.AppendLine(JsonDiffGenerator.FormatDiffs(BodyDiffs));
        }

        // Suggestions
        var suggestions = GenerateSuggestions();
        if (suggestions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Suggestions:");
            foreach (var suggestion in suggestions)
            {
                sb.AppendLine($"  → {suggestion}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats the diagnostic report as a concise summary.
    /// </summary>
    public string FormatSummary()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Treaty Verification Failed: [{Endpoint}]");
        sb.AppendLine();

        foreach (var violation in Violations)
        {
            sb.AppendLine($"  • {violation.Type} at {violation.Path}: {violation.Message}");
        }

        return sb.ToString();
    }

    private List<string> GenerateSuggestions()
    {
        var suggestions = new List<string>();

        foreach (var violation in Violations)
        {
            var suggestion = violation.Type switch
            {
                ViolationType.MissingRequired =>
                    $"Ensure the field '{GetFieldName(violation.Path)}' is included in the response.",
                ViolationType.InvalidType =>
                    $"Check that '{GetFieldName(violation.Path)}' returns the correct type ({violation.Expected}).",
                ViolationType.UnexpectedNull =>
                    $"The field '{GetFieldName(violation.Path)}' should not be null. Check your data.",
                ViolationType.UnexpectedField =>
                    $"The field '{GetFieldName(violation.Path)}' is not in the contract. This violation occurs in strict mode or when additionalProperties is false.",
                ViolationType.UnexpectedStatusCode =>
                    $"The API returned status {violation.Actual}. Expected: {violation.Expected}. Check your API logic.",
                ViolationType.MissingHeader =>
                    $"Ensure the header '{GetFieldName(violation.Path)}' is included in responses.",
                ViolationType.InvalidHeaderValue =>
                    $"The header '{GetFieldName(violation.Path)}' has an incorrect value. Expected: {violation.Expected}.",
                ViolationType.InvalidFormat =>
                    $"The value at '{GetFieldName(violation.Path)}' doesn't match the expected format ({violation.Expected}).",
                _ => null
            };

            if (suggestion != null && !suggestions.Contains(suggestion))
            {
                suggestions.Add(suggestion);
            }
        }

        return suggestions;
    }

    private static string GetFieldName(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "$")
        {
            return "root";
        }

        var parts = path.Split('.');
        return parts.Length > 0 ? parts[^1] : path;
    }
}
