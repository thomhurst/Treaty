using System.Text;

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
    /// Creates a new contract violation exception.
    /// </summary>
    /// <param name="violations">The violations that were detected.</param>
    public ContractViolationException(IReadOnlyList<ContractViolation> violations)
        : base(FormatMessage(violations))
    {
        Violations = violations;
    }

    /// <summary>
    /// Creates a new contract violation exception with a single violation.
    /// </summary>
    /// <param name="violation">The violation that was detected.</param>
    public ContractViolationException(ContractViolation violation)
        : this([violation])
    {
    }

    private static string FormatMessage(IReadOnlyList<ContractViolation> violations)
    {
        if (violations.Count == 0)
            return "Contract validation failed.";

        var sb = new StringBuilder();
        var groupedByEndpoint = violations.GroupBy(v => v.Endpoint);

        foreach (var group in groupedByEndpoint)
        {
            sb.AppendLine($"Contract violation at endpoint {group.Key}:");
            foreach (var violation in group)
            {
                sb.AppendLine(violation.ToString());
            }
        }

        return sb.ToString().TrimEnd();
    }
}
