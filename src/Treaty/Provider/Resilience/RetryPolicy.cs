using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Treaty.Provider.Resilience;

/// <summary>
/// Default retry policy implementation with configurable options.
/// </summary>
public sealed class RetryPolicy : IRetryPolicy
{
    private readonly RetryPolicyOptions _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance with optional options and logger.
    /// </summary>
    /// <param name="options">The retry policy options. Uses defaults if not specified.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public RetryPolicy(RetryPolicyOptions? options = null, ILogger? logger = null)
    {
        _options = options ?? RetryPolicyOptions.Default;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc />
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        var attempt = 0;
        Exception? lastException = null;

        while (true)
        {
            try
            {
                return await operation(cancellationToken);
            }
            catch (Exception ex) when (ShouldRetry(ex, attempt))
            {
                lastException = ex;
                attempt++;

                var delay = CalculateDelay(attempt);
                _logger.LogWarning(ex,
                    "[Treaty] Request failed (attempt {Attempt}/{MaxRetries}), retrying in {Delay}ms",
                    attempt, _options.MaxRetries, delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private bool ShouldRetry(Exception ex, int attempt)
    {
        if (attempt >= _options.MaxRetries)
        {
            return false;
        }

        // Retry on transient HTTP errors and timeouts
        return ex is HttpRequestException
            || ex is TaskCanceledException { InnerException: TimeoutException } || ex is OperationCanceledException { InnerException: TimeoutException };
    }

    private TimeSpan CalculateDelay(int attempt)
    {
        TimeSpan delay;

        if (_options.UseExponentialBackoff)
        {
            // Exponential backoff: initialDelay * 2^(attempt-1)
            var delayMs = _options.InitialDelayMs * Math.Pow(2, attempt - 1);
            delay = TimeSpan.FromMilliseconds(delayMs);
        }
        else
        {
            delay = TimeSpan.FromMilliseconds(_options.InitialDelayMs);
        }

        // Cap at max delay
        return delay > _options.MaxDelay ? _options.MaxDelay : delay;
    }
}
