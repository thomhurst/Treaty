namespace Treaty.Provider;

/// <summary>
/// Options for controlling verification behavior.
/// </summary>
public sealed class VerificationOptions
{
    /// <summary>
    /// Gets or sets whether to stop verification after the first failure.
    /// Default is false (verify all endpoints even if some fail).
    /// </summary>
    public bool StopOnFirstFailure { get; init; } = false;

    /// <summary>
    /// Gets or sets whether to run endpoint verifications in parallel.
    /// Default is false (sequential execution).
    /// </summary>
    public bool ParallelExecution { get; init; } = false;

    /// <summary>
    /// Gets or sets the maximum degree of parallelism when <see cref="ParallelExecution"/> is true.
    /// Default is 4.
    /// </summary>
    public int MaxDegreeOfParallelism { get; init; } = 4;

    /// <summary>
    /// Gets or sets a timeout for each individual endpoint verification.
    /// If null, no timeout is applied.
    /// </summary>
    public TimeSpan? PerEndpointTimeout { get; init; }

    /// <summary>
    /// Gets or sets a timeout for the entire verification run.
    /// If null, no timeout is applied.
    /// </summary>
    public TimeSpan? TotalTimeout { get; init; }

    /// <summary>
    /// Gets or sets whether to include detailed diagnostics in the results.
    /// Default is true.
    /// </summary>
    public bool IncludeDiagnostics { get; init; } = true;

    /// <summary>
    /// Gets or sets whether to skip endpoints that don't have example data defined.
    /// If false, an error is thrown for endpoints without example data.
    /// Default is true.
    /// </summary>
    public bool SkipEndpointsWithoutExampleData { get; init; } = true;

    /// <summary>
    /// Creates default verification options.
    /// </summary>
    public static VerificationOptions Default { get; } = new();
}
