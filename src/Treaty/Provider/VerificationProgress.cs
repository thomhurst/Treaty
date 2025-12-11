using Treaty.Contracts;

namespace Treaty.Provider;

/// <summary>
/// Reports progress during bulk verification operations.
/// </summary>
/// <remarks>
/// Creates a new progress report.
/// </remarks>
public sealed class VerificationProgress(
    int totalEndpoints,
    int completedEndpoints,
    int passedEndpoints,
    int failedEndpoints,
    int skippedEndpoints = 0,
    EndpointContract? currentEndpoint = null,
    string? statusMessage = null)
{
    /// <summary>
    /// Gets the total number of endpoints to verify.
    /// </summary>
    public int TotalEndpoints { get; } = totalEndpoints;

    /// <summary>
    /// Gets the number of endpoints that have been verified (passed or failed).
    /// </summary>
    public int CompletedEndpoints { get; } = completedEndpoints;

    /// <summary>
    /// Gets the number of endpoints that passed verification.
    /// </summary>
    public int PassedEndpoints { get; } = passedEndpoints;

    /// <summary>
    /// Gets the number of endpoints that failed verification.
    /// </summary>
    public int FailedEndpoints { get; } = failedEndpoints;

    /// <summary>
    /// Gets the number of endpoints that were skipped (e.g., no example data).
    /// </summary>
    public int SkippedEndpoints { get; } = skippedEndpoints;

    /// <summary>
    /// Gets the endpoint currently being verified, if any.
    /// </summary>
    public EndpointContract? CurrentEndpoint { get; } = currentEndpoint;

    /// <summary>
    /// Gets the current status message.
    /// </summary>
    public string? StatusMessage { get; } = statusMessage;

    /// <summary>
    /// Gets the percentage of verification complete (0-100).
    /// </summary>
    public double PercentComplete => TotalEndpoints > 0
        ? (double)CompletedEndpoints / TotalEndpoints * 100
        : 0;

    /// <summary>
    /// Creates an initial progress report.
    /// </summary>
    public static VerificationProgress Starting(int totalEndpoints)
        => new(totalEndpoints, 0, 0, 0, 0, null, "Starting verification...");

    /// <summary>
    /// Creates a completion progress report.
    /// </summary>
    public static VerificationProgress Completed(int total, int passed, int failed, int skipped)
        => new(total, total, passed, failed, skipped, null, "Verification complete.");

    /// <inheritdoc/>
    public override string ToString()
    {
        var current = CurrentEndpoint != null ? $" - {CurrentEndpoint}" : "";
        return $"[{CompletedEndpoints}/{TotalEndpoints}] Passed: {PassedEndpoints}, Failed: {FailedEndpoints}{current}";
    }
}
