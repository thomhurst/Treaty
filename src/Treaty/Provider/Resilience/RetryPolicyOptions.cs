namespace Treaty.Provider.Resilience;

/// <summary>
/// Options for configuring retry behavior.
/// </summary>
public sealed class RetryPolicyOptions
{
    /// <summary>
    /// Maximum number of retry attempts. Default is 3.
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Initial delay between retries in milliseconds. Default is 500ms.
    /// </summary>
    public int InitialDelayMs { get; init; } = 500;

    /// <summary>
    /// Whether to use exponential backoff. Default is true.
    /// </summary>
    public bool UseExponentialBackoff { get; init; } = true;

    /// <summary>
    /// Maximum delay between retries. Default is 30 seconds.
    /// </summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Default retry policy options.
    /// </summary>
    public static RetryPolicyOptions Default { get; } = new();

    /// <summary>
    /// No retry policy (fails immediately on first error).
    /// </summary>
    public static RetryPolicyOptions NoRetry { get; } = new() { MaxRetries = 0 };
}
