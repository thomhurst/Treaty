# Provider Verification

Provider verification ensures your API implementation matches the contract definition. This is essential for confirming that your backend code returns responses that consumers can rely on.

## What is Provider Verification?

When you're the **provider** (the team building the API), provider verification tests that your actual API implementation returns responses matching the contract schema. This catches issues like:

- Missing required fields
- Wrong data types
- Invalid enum values
- Missing headers
- Unexpected status codes

## Setup

### Using TestServer (Recommended)

Treaty uses `Microsoft.AspNetCore.TestHost` internally, so verification runs in-memory without network overhead:

```csharp
using Treaty;

public class UserApiProviderTests : IDisposable
{
    private readonly ProviderVerifier<Startup> _provider;
    private readonly Contract _contract;

    public UserApiProviderTests()
    {
        _contract = Treaty.DefineContract("Users API")
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithJsonBody<User>())
            .Build();

        _provider = Treaty.ForProvider<Startup>()
            .WithContract(_contract)
            .Build();
    }

    [Test]
    public async Task GetUser_ReturnsValidResponse()
    {
        await _provider.VerifyAsync("/users/1", HttpMethod.Get);
    }

    public void Dispose()
    {
        _provider.Dispose();
    }
}
```

The `TStartup` type parameter should be your ASP.NET Core startup class or `Program` class.

## Verifying Endpoints

### Single Endpoint Verification

```csharp
// Simple GET
await provider.VerifyAsync("/users/1", HttpMethod.Get);

// With request body
await provider.VerifyAsync("/users", HttpMethod.Post, body: new CreateUserRequest
{
    Name = "John",
    Email = "john@example.com"
});

// With headers
await provider.VerifyAsync("/users/1", HttpMethod.Get, headers: new Dictionary<string, string>
{
    ["Authorization"] = "Bearer token123"
});
```

### Non-Throwing Verification

Use `TryVerifyAsync` to get results without throwing exceptions:

```csharp
var result = await provider.TryVerifyAsync("/users/1", HttpMethod.Get);

if (!result.IsValid)
{
    foreach (var violation in result.Violations)
    {
        Console.WriteLine($"{violation.Type} at {violation.Path}: {violation.Message}");
    }
}
```

## Bulk Verification

Verify all endpoints at once using `VerifyAllAsync`:

```csharp
var contract = Treaty.DefineContract("Users API")
    .ForEndpoint("/users/{id}")
        .WithMethod(HttpMethod.Get)
        .WithExampleData(e => e.WithPathParameter("id", "1"))
        .ExpectingResponse(r => r.WithStatus(200).WithJsonBody<User>())
    .ForEndpoint("/users")
        .WithMethod(HttpMethod.Post)
        .WithExampleData(e => e.WithRequestBody(new { name = "Test", email = "test@example.com" }))
        .ExpectingResponse(r => r.WithStatus(201).WithJsonBody<User>())
    .Build();

var results = await provider.VerifyAllAsync();

Console.WriteLine($"Passed: {results.PassedCount}/{results.TotalCount}");
Console.WriteLine($"Duration: {results.Duration.TotalMilliseconds}ms");

// Check if all passed
results.ThrowIfAnyFailed();
```

### Verification Options

```csharp
var results = await provider.VerifyAllAsync(new VerificationOptions
{
    // Stop on first failure (default: false)
    StopOnFirstFailure = true,

    // Skip endpoints without example data (default: true)
    SkipEndpointsWithoutExampleData = true,

    // Run endpoints in parallel (default: false)
    ParallelExecution = true,
    MaxDegreeOfParallelism = 4,

    // Per-endpoint timeout
    PerEndpointTimeout = TimeSpan.FromSeconds(30)
});
```

### Progress Reporting

```csharp
var progress = new Progress<VerificationProgress>(p =>
{
    Console.WriteLine($"[{p.CompletedCount}/{p.TotalCount}] {p.CurrentMessage}");
});

var results = await provider.VerifyAllAsync(progress: progress);
```

### Filtering Endpoints

```csharp
// Only verify GET endpoints
var results = await provider.VerifyAsync(
    endpoint => endpoint.Method == HttpMethod.Get);

// Only verify endpoints matching a pattern
var results = await provider.VerifyAsync(
    endpoint => endpoint.PathTemplate.StartsWith("/users"));
```

## Provider States

Provider states set up test data before verification. This is useful when endpoints require specific database state.

### Defining States in Contract

```csharp
var contract = Treaty.DefineContract("Users API")
    .ForEndpoint("/users/{id}")
        .WithMethod(HttpMethod.Get)
        .GivenProviderState("a user exists", new { id = 1 })
        .ExpectingResponse(r => r.WithStatus(200).WithJsonBody<User>())
    .Build();
```

### Handling States

```csharp
var provider = Treaty.ForProvider<Startup>()
    .WithContract(contract)
    .WithStateHandler(states => states
        .ForState("a user exists", async parameters =>
        {
            var id = (int)parameters["id"];
            await _testDatabase.CreateUser(id, "Test User");
        })
        .ForState("no users exist", async () =>
        {
            await _testDatabase.ClearUsers();
        }))
    .Build();
```

### State Handler Interface

For more control, implement `IStateHandler`:

```csharp
public class TestStateHandler : IStateHandler
{
    private readonly TestDatabase _db;

    public TestStateHandler(TestDatabase db) => _db = db;

    public async Task SetupAsync(ProviderState state, CancellationToken cancellationToken)
    {
        switch (state.Name)
        {
            case "a user exists":
                var id = (int)state.Parameters["id"];
                await _db.CreateUser(id, "Test User");
                break;
            case "no users exist":
                await _db.ClearUsers();
                break;
        }
    }

    public async Task TeardownAsync(ProviderState state, CancellationToken cancellationToken)
    {
        // Clean up after verification
        await _db.ClearUsers();
    }
}

// Usage
var provider = Treaty.ForProvider<Startup>()
    .WithContract(contract)
    .WithStateHandler(new TestStateHandler(testDb))
    .Build();
```

## Logging

Enable logging to see verification details:

```csharp
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});

var provider = Treaty.ForProvider<Startup>()
    .WithContract(contract)
    .WithLogging(loggerFactory)
    .Build();
```

Sample output:

```
[Treaty] Validating GET /users/1
[Treaty] Setting up provider state: a user exists
[Treaty] Response status code: 200
[Treaty] Validating response body
[Treaty] Response body matches schema
[Treaty] GET /users/1 validation passed
```

## CI/CD Integration

### GitHub Actions Example

```yaml
- name: Run Contract Tests
  run: dotnet test --filter "Category=Contract"

- name: Upload Results
  if: failure()
  uses: actions/upload-artifact@v3
  with:
    name: contract-test-results
    path: TestResults/
```

### Test Organization

```csharp
[Category("Contract")]
public class UserApiContractTests
{
    // Tests...
}
```

## Best Practices

1. **Use example data** - Define example values for path parameters, query strings, and request bodies so bulk verification works.

2. **Set up test database state** - Use provider states to ensure the database has the data needed for each test.

3. **Run in CI** - Include contract tests in your CI pipeline to catch breaking changes early.

4. **Test error responses** - Define contracts for error scenarios (404, 400, etc.) not just happy paths.

5. **Use lenient validation** - By default, extra fields are ignored, which prevents false positives when you add new fields.

## Error Messages

When verification fails, Treaty provides detailed diagnostics:

```
Contract violation at GET /users/1:

  1. MissingRequired at `$.email`
     Missing required field 'Email'

  2. InvalidType at `$.age`
     Expected: integer
     Actual:   string

Suggestions:
  -> Ensure the field 'email' is included in the response.
  -> Check that 'age' returns the correct type (integer).
```

## Common Gotchas

### State Handler Lifecycle

State handlers run **before each endpoint** verification, not once per test run. If you have expensive setup:

```csharp
// BAD: This runs for EVERY endpoint
.ForState("database is seeded", async () => await SeedEntireDatabase())

// GOOD: Use test fixtures for expensive setup
public class ContractTests : IAsyncLifetime
{
    public async Task InitializeAsync() => await SeedEntireDatabase();
    public async Task DisposeAsync() => await CleanupDatabase();
}
```

### Teardown Order

State teardown runs in **reverse order** of setup. If setup is A → B → C, teardown is C → B → A.

### Thread Safety

- `ProviderVerifier` is **not thread-safe**. Create one instance per test.
- When using `ParallelExecution = true`, each parallel verification shares the same state handler instance. Ensure your state handlers are thread-safe.
- Progress callbacks (`IProgress<T>`) may arrive out-of-order during parallel execution.

### HttpProviderVerifier Specifics

When using `Treaty.ForHttpProvider()` against live APIs:

1. **Request cloning**: Requests are cloned for retries. This adds slight overhead.
2. **HttpClient ownership**: If you provide your own `HttpClient` via `.WithHttpClient()`, Treaty won't dispose it.
3. **Certificate validation**: Use `.SkipCertificateValidation()` only in development/testing.

### Multiple Authentication Methods

Calling multiple auth methods overwrites the previous one:

```csharp
// Only BasicAuth is used - BearerToken is overwritten
.WithBearerToken("token")
.WithBasicAuth("user", "pass")  // This wins
```

Use `CompositeAuthProvider` if you need multiple auth methods:

```csharp
.WithAuthentication(new CompositeAuthProvider(
    new BearerTokenAuthProvider("token"),
    new CustomHeadersAuthProvider(headers)))
```

### Retry Policy Behavior

Retries only happen for **transient** errors:
- `HttpRequestException` (network failures)
- `TaskCanceledException` with `TimeoutException` inner (request timeouts)

Non-transient errors (4xx, 5xx HTTP responses) are **not retried** by default.

### Bulk Verification Without Example Data

Endpoints without example data are **skipped by default**:

```csharp
// This endpoint has a path parameter but no example
.ForEndpoint("/users/{id}")  // Skipped!

// Fix: Add example data
.ForEndpoint("/users/{id}")
    .WithExampleData(e => e.WithPathParam("id", "123"))
```

Set `SkipEndpointsWithoutExampleData = false` to fail instead of skip.

## Next Steps

- [Consumer Verification](consumer-verification.md) - Testing API clients
- [Mock Server](mock-server.md) - Mock server for development
- [Validation Modes](validation-modes.md) - Strict vs lenient validation
