# Treaty Quick Reference

A one-page guide to all Treaty entry points and common patterns.

## Entry Points

All Treaty APIs start from the static `Treaty` class:

```csharp
using Treaty;
```

| Method | Purpose | Returns |
|--------|---------|---------|
| `Treaty.OpenApi(path)` | Load contract from file | `OpenApiContractBuilder` |
| `Treaty.OpenApi(stream, format)` | Load contract from stream | `OpenApiContractBuilder` |
| `Treaty.MockServer(path)` | Create mock server from file | `MockServerBuilder` |
| `Treaty.MockServer(contract)` | Create mock server from contract | `MockServerBuilder` |
| `Treaty.ForProvider<TStartup>()` | Create TestServer-based verifier | `ProviderBuilder<T>` |
| `Treaty.ForHttpProvider()` | Create HTTP-based verifier | `HttpProviderBuilder` |
| `Treaty.ForConsumer()` | Create consumer verifier | `ConsumerBuilder` |
| `Treaty.CompareContracts(old, new)` | Detect breaking changes | `ContractDiff` |

## Loading Contracts

```csharp
// From file
var contract = Treaty.OpenApi("api-spec.yaml").Build();

// From stream
using var stream = File.OpenRead("api-spec.yaml");
var contract = Treaty.OpenApi(stream, OpenApiFormat.Yaml).Build();

// With endpoint filter
var contract = Treaty.OpenApi("api-spec.yaml")
    .ForEndpoint("/users/{id}")
    .Build();
```

## Provider Verification

### TestServer (In-Process)

```csharp
using var provider = Treaty.ForProvider<Startup>()
    .WithContract(contract)
    .WithStateHandler(states => states
        .ForState("a user exists", () => SeedUser()))
    .Build();

// Single endpoint
await provider.VerifyAsync("/users/123", HttpMethod.Get);

// All endpoints
var result = await provider.VerifyAllAsync();
```

### HTTP (Live API)

```csharp
using var provider = Treaty.ForHttpProvider()
    .WithBaseUrl("https://api.staging.example.com")
    .WithContract(contract)
    .WithBearerToken("your-token")
    .WithRetryPolicy()
    .Build();

await provider.VerifyAllAsync();
```

## Authentication Options

```csharp
// Bearer token (static)
.WithBearerToken("token")

// Bearer token (dynamic)
.WithBearerToken(async ct => await GetTokenAsync(ct))

// API key (header)
.WithApiKey("key-value")
.WithApiKey("key-value", "X-Custom-Header")

// API key (query string)
.WithApiKey("key-value", "api_key", ApiKeyLocation.QueryString)

// Basic auth
.WithBasicAuth("username", "password")

// Custom headers
.WithCustomHeaders(new Dictionary<string, string>
{
    ["X-Custom"] = "value"
})
```

## Consumer Verification

```csharp
// Create validating HTTP client
var httpClient = Treaty.ForConsumer()
    .WithContract(contract)
    .CreateHttpClient();

// Use normally - requests are validated against contract
var response = await httpClient.GetAsync("/users/123");
```

## Mock Server

```csharp
// Basic mock server
await using var server = Treaty.MockServer("api-spec.yaml").Build();
await server.StartAsync();
var baseUrl = server.BaseUrl; // e.g., "http://127.0.0.1:5001"

// With customizations
await using var server = Treaty.MockServer(contract)
    .UseHttps()
    .WithLatency(50, 200) // 50-200ms delay
    .RequireHeader("Authorization")
    .ForEndpoint("/users/{id}")
        .When(req => req.PathParam("id") == "0")
        .Return(404)
    .Build();
```

## Contract Comparison

```csharp
var oldContract = Treaty.OpenApi("v1/api-spec.yaml").Build();
var newContract = Treaty.OpenApi("v2/api-spec.yaml").Build();

var diff = Treaty.CompareContracts(oldContract, newContract);

if (diff.HasBreakingChanges)
{
    foreach (var change in diff.BreakingChanges)
    {
        Console.WriteLine($"Breaking: {change}");
    }
}
```

## State Handlers

```csharp
.WithStateHandler(states => states
    // Sync setup
    .ForState("a user exists", () => { /* setup */ })

    // Async setup
    .ForState("a user exists", async () => { /* async setup */ })

    // With parameters
    .ForState("user {id} exists", p => SeedUser(p["id"]))

    // With teardown
    .ForState("a user exists",
        setup: () => { /* setup */ },
        teardown: () => { /* cleanup */ }))
```

## HTTP Options

```csharp
.WithHttpOptions(opts => opts
    .WithTimeout(60)           // 60 seconds
    .FollowRedirects(false)    // Don't follow redirects
    .SkipCertificateValidation()) // For self-signed certs
```

## Retry Policy

```csharp
// Default: 3 retries, exponential backoff
.WithRetryPolicy()

// Custom options
.WithRetryPolicy(new RetryPolicyOptions
{
    MaxRetries = 5,
    InitialDelayMs = 100,
    UseExponentialBackoff = true,
    MaxDelay = TimeSpan.FromSeconds(10)
})
```

## Validation Results

```csharp
// Throws on failure
await provider.VerifyAsync("/users/123", HttpMethod.Get);

// Returns result (no throw)
var result = await provider.TryVerifyAsync("/users/123", HttpMethod.Get);
if (!result.IsValid)
{
    foreach (var violation in result.Violations)
    {
        Console.WriteLine($"{violation.Type}: {violation.Message}");
    }
}

// Bulk verification with progress
var progress = new Progress<VerificationProgress>(p =>
    Console.WriteLine($"{p.Completed}/{p.Total}: {p.CurrentEndpoint}"));

var bulkResult = await provider.VerifyAllAsync(
    options: new VerificationOptions { MaxParallelism = 4 },
    progress: progress);
```

## Common Violation Types

| Type | Meaning |
|------|---------|
| `MissingRequired` | Required field not in response |
| `InvalidType` | Wrong JSON type (string vs number) |
| `InvalidFormat` | Format mismatch (e.g., date format) |
| `UnexpectedStatusCode` | Wrong HTTP status code |
| `MissingHeader` | Required header not present |
| `DiscriminatorMismatch` | Polymorphic type doesn't match |
| `UnexpectedField` | Extra field in strict mode |

## Logging

```csharp
// With Microsoft.Extensions.Logging
var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

Treaty.ForProvider<Startup>()
    .WithContract(contract)
    .WithLogging(loggerFactory)
    .Build();
```
