using System.Text;
using Treaty.Contracts;
using Treaty.Diagnostics;
using Treaty.Validation;

namespace Treaty.Provider;

/// <summary>
/// Result of bulk verification containing all individual results.
/// </summary>
public sealed class BulkVerificationResult
{
    /// <summary>
    /// Gets a value indicating whether all verified endpoints passed.
    /// </summary>
    public bool AllPassed => FailedCount == 0;

    /// <summary>
    /// Gets the total number of endpoints that were included in verification.
    /// </summary>
    public int TotalCount { get; }

    /// <summary>
    /// Gets the number of endpoints that passed verification.
    /// </summary>
    public int PassedCount { get; }

    /// <summary>
    /// Gets the number of endpoints that failed verification.
    /// </summary>
    public int FailedCount { get; }

    /// <summary>
    /// Gets the number of endpoints that were skipped (e.g., no example data).
    /// </summary>
    public int SkippedCount { get; }

    /// <summary>
    /// Gets all individual verification results.
    /// </summary>
    public IReadOnlyList<EndpointVerificationResult> Results { get; }

    /// <summary>
    /// Gets only the failed verification results.
    /// </summary>
    public IReadOnlyList<EndpointVerificationResult> Failures => Results.Where(r => !r.Passed).ToList();

    /// <summary>
    /// Gets the total duration of the verification run.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Creates a new bulk verification result.
    /// </summary>
    public BulkVerificationResult(
        IReadOnlyList<EndpointVerificationResult> results,
        int skippedCount,
        TimeSpan duration)
    {
        Results = results;
        SkippedCount = skippedCount;
        Duration = duration;
        TotalCount = results.Count + skippedCount;
        PassedCount = results.Count(r => r.Passed);
        FailedCount = results.Count(r => !r.Passed);
    }

    /// <summary>
    /// Throws <see cref="ContractViolationException"/> if any endpoints failed verification.
    /// </summary>
    /// <exception cref="ContractViolationException">Thrown when one or more endpoints failed.</exception>
    public void ThrowIfAnyFailed()
    {
        if (FailedCount == 0)
            return;

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
            sb.AppendLine($"Skipped:  {SkippedCount}");
        sb.AppendLine($"Duration: {Duration.TotalMilliseconds:F0}ms");
        sb.AppendLine();

        if (AllPassed)
        {
            sb.AppendLine("✓ All endpoints passed verification!");
        }
        else
        {
            sb.AppendLine("Failed endpoints:");
            sb.AppendLine(new string('-', 60));

            foreach (var failure in Failures)
            {
                sb.AppendLine($"  ✗ {failure.Endpoint}");
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
            ? $"✓ Verification passed: {PassedCount}/{TotalCount} endpoints"
            : $"✗ Verification failed: {FailedCount}/{TotalCount} endpoints failed";
    }
}

/// <summary>
/// Result of verifying a single endpoint.
/// </summary>
public sealed class EndpointVerificationResult
{
    /// <summary>
    /// Gets the endpoint that was verified.
    /// </summary>
    public EndpointContract Endpoint { get; }

    /// <summary>
    /// Gets the validation result.
    /// </summary>
    public ValidationResult ValidationResult { get; }

    /// <summary>
    /// Gets a value indicating whether the endpoint passed verification.
    /// </summary>
    public bool Passed => ValidationResult.IsValid;

    /// <summary>
    /// Gets the duration of this endpoint's verification.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Creates a new endpoint verification result.
    /// </summary>
    public EndpointVerificationResult(
        EndpointContract endpoint,
        ValidationResult validationResult,
        TimeSpan duration)
    {
        Endpoint = endpoint;
        ValidationResult = validationResult;
        Duration = duration;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return Passed
            ? $"✓ {Endpoint} ({Duration.TotalMilliseconds:F0}ms)"
            : $"✗ {Endpoint} - {ValidationResult.Violations.Count} violation(s)";
    }
}
