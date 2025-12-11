using System.Text;
using Treaty.Contracts;
using Treaty.Validation;

namespace Treaty.Provider;

/// <summary>
/// Result of bulk verification containing all individual results.
/// </summary>
/// <param name="Results">All individual verification results.</param>
/// <param name="SkippedCount">The number of endpoints that were skipped (e.g., no example data).</param>
/// <param name="Duration">The total duration of the verification run.</param>
public sealed record BulkVerificationResult(
    IReadOnlyList<EndpointVerificationResult> Results,
    int SkippedCount,
    TimeSpan Duration)
{
    /// <summary>
    /// Gets the total number of endpoints that were included in verification.
    /// </summary>
    public int TotalCount { get; } = Results.Count + SkippedCount;

    /// <summary>
    /// Gets the number of endpoints that passed verification.
    /// </summary>
    public int PassedCount { get; } = Results.Count(r => r.Passed);

    /// <summary>
    /// Gets the number of endpoints that failed verification.
    /// </summary>
    public int FailedCount { get; } = Results.Count(r => !r.Passed);

    /// <summary>
    /// Gets a value indicating whether all verified endpoints passed.
    /// </summary>
    public bool AllPassed => FailedCount == 0;

    /// <summary>
    /// Gets only the failed verification results.
    /// </summary>
    public IReadOnlyList<EndpointVerificationResult> Failures { get; } = Results.Where(r => !r.Passed).ToList();

    /// <summary>
    /// Throws <see cref="ContractViolationException"/> if any endpoints failed verification.
    /// </summary>
    /// <exception cref="ContractViolationException">Thrown when one or more endpoints failed.</exception>
    public void ThrowIfAnyFailed()
    {
        if (FailedCount == 0)
        {
            return;
        }

        var allViolations = Failures
            .SelectMany(f => f.ValidationResult.Violations)
            .ToList();

        throw new ContractViolationException(
            $"Bulk verification failed: {FailedCount} of {TotalCount} endpoints failed",
            allViolations);
    }

    /// <summary>
    /// Gets a summary string of the verification results.
    /// </summary>
    public string GetSummary()
    {
        var sb = new StringBuilder();

        sb.AppendLine("╔════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║                   VERIFICATION SUMMARY                             ║");
        sb.AppendLine("╚════════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        sb.AppendLine($"Total:    {TotalCount}");
        sb.AppendLine($"Passed:   {PassedCount}");
        sb.AppendLine($"Failed:   {FailedCount}");
        if (SkippedCount > 0)
        {
            sb.AppendLine($"Skipped:  {SkippedCount}");
        }
        sb.AppendLine($"Duration: {Duration.TotalMilliseconds:F0}ms");
        sb.AppendLine();

        if (AllPassed)
        {
            sb.AppendLine("All endpoints passed verification!");
        }
        else
        {
            sb.AppendLine("Failed endpoints:");
            sb.AppendLine(new string('-', 60));

            foreach (var failure in Failures)
            {
                sb.AppendLine($"  X {failure.Endpoint}");
                foreach (var violation in failure.ValidationResult.Violations.Take(3))
                {
                    sb.AppendLine($"    - {violation.Type} at {violation.Path}");
                }
                if (failure.ValidationResult.Violations.Count > 3)
                {
                    sb.AppendLine($"    ... and {failure.ValidationResult.Violations.Count - 3} more violations");
                }
            }
        }

        return sb.ToString();
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return AllPassed
            ? $"Verification passed: {PassedCount}/{TotalCount} endpoints"
            : $"Verification failed: {FailedCount}/{TotalCount} endpoints failed";
    }
}

/// <summary>
/// Result of verifying a single endpoint.
/// </summary>
/// <param name="Endpoint">The endpoint that was verified.</param>
/// <param name="ValidationResult">The validation result.</param>
/// <param name="Duration">The duration of this endpoint's verification.</param>
public sealed record EndpointVerificationResult(
    EndpointContract Endpoint,
    ValidationResult ValidationResult,
    TimeSpan Duration)
{
    /// <summary>
    /// Gets a value indicating whether the endpoint passed verification.
    /// </summary>
    public bool Passed => ValidationResult.IsValid;

    /// <inheritdoc/>
    public override string ToString()
    {
        return Passed
            ? $"Pass: {Endpoint} ({Duration.TotalMilliseconds:F0}ms)"
            : $"Fail: {Endpoint} - {ValidationResult.Violations.Count} violation(s)";
    }
}
