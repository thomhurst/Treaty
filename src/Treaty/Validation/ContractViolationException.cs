using System.Text;
using Treaty.Diagnostics;

namespace Treaty.Validation;

/// <summary>
/// Exception thrown when one or more contract violations are detected.
/// </summary>
public sealed class ContractViolationException : Exception
{
    /// <summary>
    /// Gets all contract violations that were detected.
    /// </summary>
    public IReadOnlyList<ContractViolation> Violations { get; }

    /// <summary>
    /// Gets the endpoint where the violation occurred (if all violations are for the same endpoint).
    /// </summary>
    public string? Endpoint { get; }

    /// <summary>
    /// Creates a new contract violation exception.
    /// </summary>
    /// <param name="violations">The violations that were detected.</param>
    public ContractViolationException(IReadOnlyList<ContractViolation> violations)
        : base(FormatMessage(violations))
    {
        Violations = violations;
        Endpoint = violations.Count > 0 && violations.All(v => v.Endpoint == violations[0].Endpoint)
            ? violations[0].Endpoint
            : null;
    }

    /// <summary>
    /// Creates a new contract violation exception with a single violation.
    /// </summary>
    /// <param name="violation">The violation that was detected.</param>
    public ContractViolationException(ContractViolation violation)
        : this([violation])
    {
    }

    /// <summary>
    /// Creates a new contract violation exception with a custom message.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="violations">The violations that were detected.</param>
    public ContractViolationException(string message, IReadOnlyList<ContractViolation> violations)
        : base(message + Environment.NewLine + FormatMessage(violations))
    {
        Violations = violations;
        Endpoint = violations.Count > 0 && violations.All(v => v.Endpoint == violations[0].Endpoint)
            ? violations[0].Endpoint
            : null;
    }

    /// <summary>
    /// Gets a detailed, formatted report of the violations with suggestions.
    /// </summary>
    /// <returns>A detailed report string.</returns>
    public string GetDetailedReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("╔══════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║                 TREATY CONTRACT VIOLATION                            ║");
        sb.AppendLine("╚══════════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        var groupedByEndpoint = Violations.GroupBy(v => v.Endpoint);

        foreach (var group in groupedByEndpoint)
        {
            sb.AppendLine($"Endpoint: {group.Key}");
            sb.AppendLine(new string('─', 70));

            var violations = group.ToList();
            for (int i = 0; i < violations.Count; i++)
            {
                sb.AppendLine();
                sb.Append(DiagnosticFormatter.FormatViolation(violations[i]));
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets a single-line summary of each endpoint's violations.
    /// </summary>
    /// <returns>A concise summary suitable for test output.</returns>
    public string GetSummary()
    {
        var sb = new StringBuilder();
        var groupedByEndpoint = Violations.GroupBy(v => v.Endpoint);

        foreach (var group in groupedByEndpoint)
        {
            sb.AppendLine(DiagnosticFormatter.FormatSummaryLine(group.Key, [.. group]));
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatMessage(IReadOnlyList<ContractViolation> violations)
    {
        if (violations.Count == 0)
        {
            return "Contract validation failed.";
        }

        var sb = new StringBuilder();
        var groupedByEndpoint = violations.GroupBy(v => v.Endpoint);

        foreach (var group in groupedByEndpoint)
        {
            var groupList = group.ToList();
            sb.AppendLine();
            sb.AppendLine($"Contract violation at {group.Key}:");
            sb.AppendLine();

            for (int i = 0; i < groupList.Count; i++)
            {
                var v = groupList[i];
                var icon = GetViolationIcon(v.Type);
                sb.AppendLine($"  {i + 1}. {icon} {v.Type} at `{v.Path}`");
                sb.AppendLine($"     {v.Message}");

                if (v.Expected != null)
                {
                    sb.AppendLine($"     Expected: {v.Expected}");
                }
                if (v.Actual != null)
                {
                    sb.AppendLine($"     Actual:   {v.Actual}");
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine($"Total: {violations.Count} violation(s)");

        return sb.ToString().TrimEnd();
    }

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
            ViolationType.Timeout => "⏱",
            _ => "•"
        };
    }
}
