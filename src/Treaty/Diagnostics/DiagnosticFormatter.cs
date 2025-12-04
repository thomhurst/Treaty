using System.Text;
using Treaty.Validation;

namespace Treaty.Diagnostics;

/// <summary>
/// Utility class for formatting diagnostic information in a human-readable way.
/// </summary>
public static class DiagnosticFormatter
{
    /// <summary>
    /// Formats a single contract violation with full context.
    /// </summary>
    /// <param name="violation">The violation to format.</param>
    /// <param name="includeContext">Whether to include surrounding context.</param>
    /// <returns>A formatted string representation.</returns>
    public static string FormatViolation(ContractViolation violation, bool includeContext = true)
    {
        var sb = new StringBuilder();

        // Icon based on violation type
        var icon = GetViolationIcon(violation.Type);
        sb.AppendLine($"{icon} {violation.Type}");

        // Path
        sb.AppendLine($"   Path: {FormatPath(violation.Path)}");

        // Message
        sb.AppendLine($"   Issue: {violation.Message}");

        // Expected/Actual
        if (violation.Expected != null || violation.Actual != null)
        {
            if (violation.Expected != null)
                sb.AppendLine($"   Expected: {violation.Expected}");
            if (violation.Actual != null)
                sb.AppendLine($"   Actual:   {violation.Actual}");
        }

        // Suggestion
        if (includeContext)
        {
            var suggestion = GenerateSuggestion(violation);
            if (!string.IsNullOrEmpty(suggestion))
            {
                sb.AppendLine();
                sb.AppendLine($"   Fix: {suggestion}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats a JSON path for better readability.
    /// </summary>
    /// <param name="path">The JSON path (e.g., "$.user.address.street").</param>
    /// <returns>A formatted path string.</returns>
    public static string FormatPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "(root)";

        // Add backticks for readability
        return $"`{path}`";
    }

    /// <summary>
    /// Generates a contextual suggestion for fixing a violation.
    /// </summary>
    /// <param name="violation">The violation to generate a suggestion for.</param>
    /// <returns>A suggestion string, or null if no suggestion is available.</returns>
    public static string? GenerateSuggestion(ContractViolation violation)
    {
        return violation.Type switch
        {
            ViolationType.MissingRequired =>
                $"Add the missing field to your response or mark it as optional in the contract.",

            ViolationType.InvalidType =>
                $"Ensure the value is serialized as {violation.Expected} instead of {violation.Actual}.",

            ViolationType.InvalidFormat =>
                $"The value should match the format '{violation.Expected}'. Check your serialization settings.",

            ViolationType.OutOfRange =>
                $"The value exceeds the allowed range. Ensure it's within the defined limits.",

            ViolationType.InvalidEnumValue =>
                $"Use one of the allowed enum values: {violation.Expected}.",

            ViolationType.PatternMismatch =>
                $"The value doesn't match the required pattern: {violation.Expected}.",

            ViolationType.UnexpectedStatusCode =>
                $"Your API returned {violation.Actual} but the contract expects {violation.Expected}.",

            ViolationType.MissingHeader =>
                $"Add the header to your response middleware or controller.",

            ViolationType.InvalidHeaderValue =>
                $"The header value should be '{violation.Expected}'.",

            ViolationType.UnexpectedNull =>
                $"Return a non-null value, or update the contract to allow null.",

            ViolationType.UnexpectedField =>
                $"Remove this field from the response, or use .IgnoreExtraFields() in your contract.",

            ViolationType.InvalidContentType =>
                $"Set Content-Type to '{violation.Expected}'.",

            ViolationType.MissingQueryParameter =>
                $"Include the query parameter or mark it as optional with WithOptionalQueryParam().",

            ViolationType.InvalidQueryParameterValue =>
                $"Ensure the query parameter value matches the expected type.",

            _ => null
        };
    }

    /// <summary>
    /// Gets an icon/emoji for a violation type.
    /// </summary>
    private static string GetViolationIcon(ViolationType type)
    {
        return type switch
        {
            ViolationType.MissingRequired => "✗",
            ViolationType.InvalidType => "⚠",
            ViolationType.InvalidFormat => "⚠",
            ViolationType.OutOfRange => "⚠",
            ViolationType.InvalidEnumValue => "⚠",
            ViolationType.PatternMismatch => "⚠",
            ViolationType.UnexpectedStatusCode => "✗",
            ViolationType.MissingHeader => "✗",
            ViolationType.InvalidHeaderValue => "⚠",
            ViolationType.UnexpectedNull => "✗",
            ViolationType.UnexpectedField => "?",
            ViolationType.InvalidContentType => "⚠",
            ViolationType.MissingQueryParameter => "✗",
            ViolationType.InvalidQueryParameterValue => "⚠",
            _ => "•"
        };
    }

    /// <summary>
    /// Formats multiple violations into a consolidated report.
    /// </summary>
    /// <param name="endpoint">The endpoint being verified.</param>
    /// <param name="violations">The violations found.</param>
    /// <returns>A formatted report string.</returns>
    public static string FormatViolations(string endpoint, IReadOnlyList<ContractViolation> violations)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Contract verification failed for {endpoint}");
        sb.AppendLine();
        sb.AppendLine($"Found {violations.Count} violation(s):");
        sb.AppendLine(new string('─', 60));

        for (int i = 0; i < violations.Count; i++)
        {
            if (i > 0) sb.AppendLine();
            sb.AppendLine($"{i + 1}. {FormatViolation(violations[i])}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Creates a summary line for test output.
    /// </summary>
    /// <param name="endpoint">The endpoint.</param>
    /// <param name="violations">The violations.</param>
    /// <returns>A single-line summary.</returns>
    public static string FormatSummaryLine(string endpoint, IReadOnlyList<ContractViolation> violations)
    {
        if (violations.Count == 0)
            return $"✓ {endpoint} - PASSED";

        var firstViolation = violations[0];
        var extra = violations.Count > 1 ? $" (+{violations.Count - 1} more)" : "";

        return $"✗ {endpoint} - {firstViolation.Type} at {firstViolation.Path}{extra}";
    }
}
