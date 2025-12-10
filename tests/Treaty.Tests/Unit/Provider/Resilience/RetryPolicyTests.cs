using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Treaty.Provider.Resilience;

namespace Treaty.Tests.Unit.Provider.Resilience;

public class RetryPolicyTests
{
    #region Successful Execution Tests

    [Test]
    public async Task ExecuteAsync_SuccessOnFirstAttempt_ReturnsResult()
    {
        // Arrange
        var policy = new RetryPolicy();
        var callCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async ct =>
        {
            callCount++;
            return "success";
        });

        // Assert
        await Assert.That(result).IsEqualTo("success");
        await Assert.That(callCount).IsEqualTo(1);
    }

    [Test]
    public async Task ExecuteAsync_SuccessOnFirstAttempt_NoDelay()
    {
        // Arrange
        var policy = new RetryPolicy();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        await policy.ExecuteAsync(async ct => "success");
        stopwatch.Stop();

        // Assert - Should complete almost immediately (no retries)
        await Assert.That(stopwatch.ElapsedMilliseconds).IsLessThan(100);
    }

    #endregion

    #region Retry on HttpRequestException Tests

    [Test]
    public async Task ExecuteAsync_HttpRequestException_RetriesAndSucceeds()
    {
        // Arrange
        var options = new RetryPolicyOptions
        {
            MaxRetries = 3,
            InitialDelayMs = 10,
            UseExponentialBackoff = false
        };
        var policy = new RetryPolicy(options);
        var callCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async ct =>
        {
            callCount++;
            if (callCount < 3)
                throw new HttpRequestException("Connection failed");
            return "success";
        });

        // Assert
        await Assert.That(result).IsEqualTo("success");
        await Assert.That(callCount).IsEqualTo(3);
    }

    [Test]
    public async Task ExecuteAsync_HttpRequestException_ExceedsMaxRetries_Throws()
    {
        // Arrange
        var options = new RetryPolicyOptions
        {
            MaxRetries = 2,
            InitialDelayMs = 10,
            UseExponentialBackoff = false
        };
        var policy = new RetryPolicy(options);
        var callCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await policy.ExecuteAsync<string>(async ct =>
            {
                callCount++;
                throw new HttpRequestException("Connection failed");
            });
        });

        await Assert.That(callCount).IsEqualTo(3); // Initial + 2 retries
    }

    #endregion

    #region Retry on Timeout Tests

    [Test]
    public async Task ExecuteAsync_TaskCanceledWithTimeoutInner_Retries()
    {
        // Arrange
        var options = new RetryPolicyOptions
        {
            MaxRetries = 2,
            InitialDelayMs = 10,
            UseExponentialBackoff = false
        };
        var policy = new RetryPolicy(options);
        var callCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async ct =>
        {
            callCount++;
            if (callCount < 2)
                throw new TaskCanceledException("Timeout", new TimeoutException("Request timed out"));
            return "success";
        });

        // Assert
        await Assert.That(result).IsEqualTo("success");
        await Assert.That(callCount).IsEqualTo(2);
    }

    [Test]
    public async Task ExecuteAsync_OperationCanceledWithTimeoutInner_Retries()
    {
        // Arrange
        var options = new RetryPolicyOptions
        {
            MaxRetries = 2,
            InitialDelayMs = 10,
            UseExponentialBackoff = false
        };
        var policy = new RetryPolicy(options);
        var callCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async ct =>
        {
            callCount++;
            if (callCount < 2)
                throw new OperationCanceledException("Timeout", new TimeoutException("Request timed out"));
            return "success";
        });

        // Assert
        await Assert.That(result).IsEqualTo("success");
        await Assert.That(callCount).IsEqualTo(2);
    }

    #endregion

    #region Non-Transient Exception Tests

    [Test]
    public async Task ExecuteAsync_InvalidOperationException_DoesNotRetry()
    {
        // Arrange
        var options = new RetryPolicyOptions
        {
            MaxRetries = 3,
            InitialDelayMs = 10
        };
        var policy = new RetryPolicy(options);
        var callCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await policy.ExecuteAsync<string>(async ct =>
            {
                callCount++;
                throw new InvalidOperationException("Not transient");
            });
        });

        await Assert.That(callCount).IsEqualTo(1); // No retry
    }

    [Test]
    public async Task ExecuteAsync_ArgumentException_DoesNotRetry()
    {
        // Arrange
        var policy = new RetryPolicy();
        var callCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await policy.ExecuteAsync<string>(async ct =>
            {
                callCount++;
                throw new ArgumentException("Bad argument");
            });
        });

        await Assert.That(callCount).IsEqualTo(1);
    }

    [Test]
    public async Task ExecuteAsync_TaskCanceledWithoutTimeoutInner_DoesNotRetry()
    {
        // Arrange - TaskCanceledException without TimeoutException inner should not retry
        var policy = new RetryPolicy();
        var callCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
        {
            await policy.ExecuteAsync<string>(async ct =>
            {
                callCount++;
                throw new TaskCanceledException("User canceled");
            });
        });

        await Assert.That(callCount).IsEqualTo(1);
    }

    #endregion

    #region Exponential Backoff Tests

    [Test]
    public async Task ExecuteAsync_ExponentialBackoff_DelaysIncrease()
    {
        // Arrange
        var options = new RetryPolicyOptions
        {
            MaxRetries = 3,
            InitialDelayMs = 50,
            UseExponentialBackoff = true
        };
        var policy = new RetryPolicy(options);
        var callTimes = new List<DateTime>();

        // Act
        try
        {
            await policy.ExecuteAsync<string>(async ct =>
            {
                callTimes.Add(DateTime.UtcNow);
                throw new HttpRequestException("Connection failed");
            });
        }
        catch (HttpRequestException)
        {
            // Expected
        }

        // Assert - Should have 4 calls (initial + 3 retries)
        await Assert.That(callTimes.Count).IsEqualTo(4);

        // Delays should be approximately: 50ms, 100ms, 200ms
        // Allow some tolerance for timing
        var delay1 = (callTimes[1] - callTimes[0]).TotalMilliseconds;
        var delay2 = (callTimes[2] - callTimes[1]).TotalMilliseconds;
        var delay3 = (callTimes[3] - callTimes[2]).TotalMilliseconds;

        await Assert.That(delay1).IsGreaterThanOrEqualTo(40);
        await Assert.That(delay2).IsGreaterThanOrEqualTo(80);
        await Assert.That(delay3).IsGreaterThanOrEqualTo(160);
    }

    [Test]
    public async Task ExecuteAsync_LinearBackoff_ConstantDelay()
    {
        // Arrange
        var options = new RetryPolicyOptions
        {
            MaxRetries = 2,
            InitialDelayMs = 50,
            UseExponentialBackoff = false
        };
        var policy = new RetryPolicy(options);
        var callTimes = new List<DateTime>();

        // Act
        try
        {
            await policy.ExecuteAsync<string>(async ct =>
            {
                callTimes.Add(DateTime.UtcNow);
                throw new HttpRequestException("Connection failed");
            });
        }
        catch (HttpRequestException)
        {
            // Expected
        }

        // Assert - Delays should be constant (~50ms each)
        var delay1 = (callTimes[1] - callTimes[0]).TotalMilliseconds;
        var delay2 = (callTimes[2] - callTimes[1]).TotalMilliseconds;

        await Assert.That(delay1).IsGreaterThanOrEqualTo(40).And.IsLessThan(100);
        await Assert.That(delay2).IsGreaterThanOrEqualTo(40).And.IsLessThan(100);
    }

    #endregion

    #region MaxDelay Cap Tests

    [Test]
    public async Task ExecuteAsync_ExponentialBackoff_CapsAtMaxDelay()
    {
        // Arrange
        var options = new RetryPolicyOptions
        {
            MaxRetries = 5,
            InitialDelayMs = 100,
            UseExponentialBackoff = true,
            MaxDelay = TimeSpan.FromMilliseconds(150) // Cap at 150ms
        };
        var policy = new RetryPolicy(options);
        var callTimes = new List<DateTime>();

        // Act
        try
        {
            await policy.ExecuteAsync<string>(async ct =>
            {
                callTimes.Add(DateTime.UtcNow);
                throw new HttpRequestException("Connection failed");
            });
        }
        catch (HttpRequestException)
        {
            // Expected
        }

        // Assert - Later delays should be capped at MaxDelay
        // Delay progression: 100ms, 200ms (capped to 150), 400ms (capped to 150), etc.
        for (int i = 2; i < callTimes.Count; i++)
        {
            var delay = (callTimes[i] - callTimes[i - 1]).TotalMilliseconds;
            await Assert.That(delay).IsLessThanOrEqualTo(200); // With some tolerance
        }
    }

    #endregion

    #region Cancellation Tests

    [Test]
    public async Task ExecuteAsync_CancellationRequested_ThrowsOperationCanceled()
    {
        // Arrange
        var policy = new RetryPolicy(new RetryPolicyOptions { InitialDelayMs = 1000 });
        var cts = new CancellationTokenSource();
        var callCount = 0;

        // Act & Assert
        var task = policy.ExecuteAsync<string>(async ct =>
        {
            callCount++;
            if (callCount == 1)
            {
                // Cancel after first attempt
                cts.Cancel();
                throw new HttpRequestException("Connection failed");
            }
            return "success";
        }, cts.Token);

        await Assert.ThrowsAsync<TaskCanceledException>(async () => await task);
    }

    [Test]
    public async Task ExecuteAsync_CancellationToken_Propagated()
    {
        // Arrange
        var policy = new RetryPolicy();
        var receivedToken = CancellationToken.None;
        var cts = new CancellationTokenSource();

        // Act
        await policy.ExecuteAsync(async ct =>
        {
            receivedToken = ct;
            return "success";
        }, cts.Token);

        // Assert
        await Assert.That(receivedToken).IsEqualTo(cts.Token);
    }

    #endregion

    #region NoRetry Options Tests

    [Test]
    public async Task ExecuteAsync_NoRetryOptions_FailsImmediately()
    {
        // Arrange
        var policy = new RetryPolicy(RetryPolicyOptions.NoRetry);
        var callCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await policy.ExecuteAsync<string>(async ct =>
            {
                callCount++;
                throw new HttpRequestException("Connection failed");
            });
        });

        await Assert.That(callCount).IsEqualTo(1); // No retries
    }

    #endregion

    #region Logging Tests

    [Test]
    public async Task ExecuteAsync_WithLogger_LogsRetryAttempts()
    {
        // Arrange
        var logMessages = new List<string>();
        var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddProvider(new TestLoggerProvider(logMessages)));
        var logger = loggerFactory.CreateLogger<RetryPolicy>();

        var options = new RetryPolicyOptions
        {
            MaxRetries = 2,
            InitialDelayMs = 10,
            UseExponentialBackoff = false
        };
        var policy = new RetryPolicy(options, logger);

        // Act
        try
        {
            await policy.ExecuteAsync<string>(async ct =>
                throw new HttpRequestException("Connection failed"));
        }
        catch (HttpRequestException)
        {
            // Expected
        }

        // Assert - Should have logged retry attempts
        await Assert.That(logMessages.Count).IsGreaterThanOrEqualTo(2);
    }

    #endregion

    #region RetryPolicyOptions Tests

    [Test]
    public async Task DefaultOptions_HasExpectedValues()
    {
        // Arrange & Act
        var options = RetryPolicyOptions.Default;

        // Assert
        await Assert.That(options.MaxRetries).IsEqualTo(3);
        await Assert.That(options.InitialDelayMs).IsEqualTo(500);
        await Assert.That(options.UseExponentialBackoff).IsTrue();
        await Assert.That(options.MaxDelay).IsEqualTo(TimeSpan.FromSeconds(30));
    }

    [Test]
    public async Task NoRetryOptions_HasZeroRetries()
    {
        // Arrange & Act
        var options = RetryPolicyOptions.NoRetry;

        // Assert
        await Assert.That(options.MaxRetries).IsEqualTo(0);
    }

    [Test]
    public async Task CustomOptions_CanBeCreated()
    {
        // Arrange & Act
        var options = new RetryPolicyOptions
        {
            MaxRetries = 5,
            InitialDelayMs = 100,
            UseExponentialBackoff = false,
            MaxDelay = TimeSpan.FromSeconds(10)
        };

        // Assert
        await Assert.That(options.MaxRetries).IsEqualTo(5);
        await Assert.That(options.InitialDelayMs).IsEqualTo(100);
        await Assert.That(options.UseExponentialBackoff).IsFalse();
        await Assert.That(options.MaxDelay).IsEqualTo(TimeSpan.FromSeconds(10));
    }

    #endregion

    private class TestLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _messages;

        public TestLoggerProvider(List<string> messages)
        {
            _messages = messages;
        }

        public ILogger CreateLogger(string categoryName) => new TestLogger(_messages);

        public void Dispose() { }
    }

    private class TestLogger : ILogger
    {
        private readonly List<string> _messages;

        public TestLogger(List<string> messages)
        {
            _messages = messages;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _messages.Add(formatter(state, exception));
        }
    }
}
