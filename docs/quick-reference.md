# Treaty Quick Reference

A one-page guide to all Treaty entry points and common patterns.

## Entry Points

Treaty provides domain-specific static classes as entry points:

```csharp
using Treaty;
```

| Class | Method | Purpose | Returns |
|-------|--------|---------|---------|
| `Contract` | `FromOpenApi(path)` | Load contract from file | `OpenApiContractBuilder` |
| `Contract` | `FromOpenApi(stream, format)` | Load contract from stream | `OpenApiContractBuilder` |
| `Contract` | `Compare(old, new)` | Detect breaking changes | `ContractDiff` |
| `MockServer` | `FromOpenApi(path)` | Create mock server from file | `MockServerBuilder` |
| `MockServer` | `FromOpenApi(stream, format)` | Create mock server from stream | `MockServerBuilder` |
| `MockServer` | `FromContract(contract)` | Create mock server from contract | `ContractMockServerBuilder` |
| `ProviderVerifier` | `ForWebApplication<TEntryPoint>()` | Create WebApplicationFactory-based verifier | `ProviderBuilder<T>` |
| `ProviderVerifier` | `ForHttpClient()` | Create HTTP-based verifier | `HttpProviderBuilder` |
| `ConsumerVerifier` | `Create()` | Create consumer verifier | `ConsumerBuilder` |

## Loading Contracts

```csharp
// From file
var contract = await Contract.FromOpenApi("api-spec.yaml").BuildAsync();

// From stream
using var stream = await File.OpenRead("api-spec.yaml");
var contract = await Contract.FromOpenApi(stream, OpenApiFormat.Yaml).BuildAsync();

// With endpoint filter
var contract = await Contract.FromOpenApi("api-spec.yaml")
    .ForEndpoint("/users/{id}")
    .BuildAsync();
```

## Provider Verification

### WebApplicationFactory (In-Process)

```csharp
using var provider = await ProviderVerifier.ForWebApplication<Startup>()
    .WithContract(contract)
    .WithStateHandler(states => states
        .ForState("a user exists", () => SeedUser()))
    .BuildAsync();

// Single endpoint
await provider.VerifyAsync("/users/123", HttpMethod.Get);

// All endpoints
var result = await provider.VerifyAllAsync();
```

### HTTP (Live API)

```csharp
using var provider = await ProviderVerifier.ForHttpClient()
    .WithBaseUrl("https://api.staging.example.com")
    .WithContract(contract)
    .WithBearerToken("your-token")
    .WithRetryPolicy()
    .BuildAsync();

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
var httpClient = await ConsumerVerifier.Create()
    .WithContract(contract)
    .CreateHttpClient();

// Use normally - requests are validated against contract
var response = await httpClient.GetAsync("/users/123");
```

## Mock Server

```csharp
// Basic mock server from OpenAPI
await using var server = await MockServer.FromOpenApi("api-spec.yaml").BuildAsync();
await server.StartAsync();
var baseUrl = await server.BaseUrl; // e.g., "http://127.0.0.1:5001"

// Mock server with customizations
await using var server = await MockServer.FromOpenApi("api-spec.yaml")
    .UseHttps()
    .WithLatency(50, 200)                    // Global latency
    .WithAuth(auth => auth.RequireHeader("Authorization").WhenMissing().Return(401))
    .WithCustomGenerator("correlationId", () => Guid.NewGuid().ToString())
    .ForEndpoint("/users/{id}")
        .WithLatency(100, 500)               // Per-endpoint latency
        .When(req => req.PathParam("id") == "0").Return(404)
        .When(req => req.BodyAs<User>()?.Role == "admin").Return(403)
        .Otherwise().Return(200)
    .ForEndpoint("/flaky")
        .When(ctx => true).ReturnSequence(   // Response sequences
            new MockSequenceResponse(503),
            new MockSequenceResponse(200))
    .ForEndpoint("/chaos")
        .When(ctx => ctx.Header("X-Fail") == "true")
        .ReturnFault(FaultType.ConnectionReset)  // Fault injection
    .BuildAsync();

// Request verification
var requests = await server.RecordedRequests;
server.ClearRecordedRequests();
```

## Contract Comparison

```csharp
var oldContract = await Contract.FromOpenApi("v1/api-spec.yaml").BuildAsync();
var newContract = await Contract.FromOpenApi("v2/api-spec.yaml").BuildAsync();

var diff = await Contract.Compare(oldContract, newContract);

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
    MaxRetries = await 5,
    InitialDelayMs = await 100,
    UseExponentialBackoff = await true,
    MaxDelay = await TimeSpan.FromSeconds(10)
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
var progress = await new Progress<VerificationProgress>(p =>
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
var loggerFactory = await LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

ProviderVerifier.ForWebApplication<Startup>()
    .WithContract(contract)
    .WithLogging(loggerFactory)
    .BuildAsync();
```
