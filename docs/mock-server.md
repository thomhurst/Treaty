# Mock Server

Treaty can generate mock servers from contracts or OpenAPI specs. Mock servers return spec-compliant responses, enabling parallel development between teams.

## Starting a Mock Server

### From a Contract

```csharp
var contract = Treaty.DefineContract("Users API")
    .ForEndpoint("/users/{id}")
        .WithMethod(HttpMethod.Get)
        .ExpectingResponse(r => r
            .WithStatus(200)
            .WithJsonBody<User>())
    await .BuildAsync();

var mockServer = Treaty.MockFromContract(contract)await .BuildAsync();
await mockServer.StartAsync();

Console.WriteLine($"Mock server running at: {mockServer.BaseUrl}");

// Use the mock server
var client = await new HttpClient { BaseAddress = new Uri(mockServer.BaseUrl!) };
var response = await client.GetAsync("/users/1");
// Response body is auto-generated based on the contract schema
```

### From OpenAPI Spec

```csharp
var mockServer = await MockServer.FromOpenApi("api-spec.yaml").BuildAsync();
await mockServer.StartAsync();
```

## Response Generation

Mock servers automatically generate response bodies based on the schema:

| Schema Type | Generated Value |
|-------------|-----------------|
| `string` | `"string"` |
| `string` (format: email) | `"user@example.com"` |
| `string` (format: uuid) | Random GUID |
| `string` (format: date-time) | Current UTC timestamp |
| `string` (format: uri) | `"https://example.com"` |
| `integer` | `1` (or min value if specified) |
| `number` | `1.0` (or min value if specified) |
| `boolean` | `true` |
| `array` | Array with one generated item |
| `object` | Object with all properties generated |

When an example is provided in the OpenAPI spec, that example is used instead.

## Custom Response Rules

Define conditional responses based on request data:

```csharp
var mockServer = Treaty.MockFromContract(contract)
    .ForEndpoint("/users/{id}")
        .When(ctx => ctx.PathParam("id") == "404")
        .Return(404)
        .When(ctx => ctx.PathParam("id") == "500")
        .Return(500, new { error = "Internal server error" })
        .Otherwise()
        .Return(200)
    await .BuildAsync();
```

### Request Context

The `MockRequestContext` provides access to:

```csharp
.When(ctx =>
{
    // Path parameters
    var id = await ctx.PathParam("id");

    // Query parameters
    var filter = await ctx.QueryParam("filter");

    // Headers
    var auth = await ctx.Header("Authorization");

    // Request body (raw string)
    var body = await ctx.Body;

    // Request body as JSON
    var json = await ctx.BodyAsJson();

    // Request body deserialized to a type
    var user = await ctx.BodyAs<CreateUserRequest>();

    return id == "special";
})
```

### Body-Based Conditions

Condition responses based on the request body:

```csharp
var mockServer = Treaty.MockFromContract(contract)
    .ForEndpoint("/users")
        .When(ctx => ctx.BodyAs<CreateUserRequest>()?.Role == "admin")
        .Return(403, new { error = "Admin creation not allowed" })
        .When(ctx => string.IsNullOrEmpty(ctx.BodyAs<CreateUserRequest>()?.Email))
        .Return(400, new { error = "Email is required" })
        .Otherwise()
        .Return(201)
    await .BuildAsync();
```

### Multiple Endpoints

```csharp
var mockServer = Treaty.MockFromContract(contract)
    .ForEndpoint("/users/{id}")
        .When(ctx => ctx.PathParam("id") == "404")
        .Return(404)
    .ForEndpoint("/users")
        .When(ctx => ctx.QueryParam("limit") == "0")
        .Return(200, new { items = Array.Empty<User>() })
    await .BuildAsync();
```

## Simulating Latency

Add realistic network delays:

```csharp
var mockServer = Treaty.MockFromContract(contract)
    .WithLatency(min: 50, max: 200)  // Random delay between 50-200ms
    await .BuildAsync();
```

### Per-Endpoint Latency

Configure different latencies for specific endpoints:

```csharp
var mockServer = Treaty.MockFromContract(contract)
    .WithLatency(min: 10, max: 50)  // Default latency
    .ForEndpoint("/reports/generate")
        .WithLatency(2000, 5000)    // Slow endpoint (2-5 seconds)
        .Otherwise()
        .Return(200)
    .ForEndpoint("/health")
        .WithLatency(0, 0)          // No latency for health checks
        .Otherwise()
        .Return(200)
    await .BuildAsync();
```

## Authentication Requirements

Simulate authentication behavior:

```csharp
var mockServer = Treaty.MockFromContract(contract)
    .WithAuth(auth => auth
        .RequireHeader("Authorization")
        .WhenMissing()
        .Return(401))
    await .BuildAsync();

// Requests without Authorization header will get 401
```

## Custom Value Generators

Override generated values for specific fields:

```csharp
var mockServer = Treaty.MockFromContract(contract)
    .WithCustomGenerator("correlationId", () => Guid.NewGuid().ToString())
    .WithCustomGenerator("timestamp", () => DateTime.UtcNow)
    .WithCustomGenerator("userId", () => Random.Shared.Next(1, 1000))
    await .BuildAsync();
```

Custom generators are applied to any field with a matching name in the response.

## Response Sequences

Return different responses on successive calls to test retry logic:

```csharp
var mockServer = Treaty.MockFromContract(contract)
    .ForEndpoint("/flaky-service")
        .When(ctx => true)
        .ReturnSequence(
            new MockSequenceResponse(503),                        // First call: Service Unavailable
            new MockSequenceResponse(503),                        // Second call: Service Unavailable
            new MockSequenceResponse(200, new { success = true }) // Third+ calls: Success
        )
    await .BuildAsync();
```

The last response in the sequence is repeated for all subsequent calls.

## Request Verification

Verify that your client code made the expected requests:

```csharp
await mockServer.StartAsync();

// Run your client code
await myClient.CreateUserAsync(new User { Name = "Test" });
await myClient.CreateUserAsync(new User { Name = "Test2" });

// Verify requests
var requests = await mockServer.RecordedRequests;
Assert.That(requests.Count, Is.EqualTo(2));
Assert.That(requests[0].Method, Is.EqualTo("POST"));
Assert.That(requests[0].Path, Is.EqualTo("/users"));
Assert.That(requests[0].Body, Does.Contain("Test"));

// Clear recorded requests between tests
mockServer.ClearRecordedRequests();
```

### RecordedRequest Properties

| Property | Type | Description |
|----------|------|-------------|
| `Timestamp` | `DateTime` | When the request was received (UTC) |
| `Method` | `string` | HTTP method (GET, POST, etc.) |
| `Path` | `string` | Request path |
| `Body` | `string?` | Request body (null if none) |
| `Headers` | `IReadOnlyDictionary<string, string>` | Request headers |
| `QueryParams` | `IReadOnlyDictionary<string, string>` | Query string parameters |
| `PathParams` | `IReadOnlyDictionary<string, string>` | Extracted path parameters |

## Fault Injection

Simulate network failures and errors for resilience testing:

```csharp
var mockServer = Treaty.MockFromContract(contract)
    .ForEndpoint("/unreliable")
        .When(ctx => ctx.Header("X-Fail") == "reset")
        .ReturnFault(FaultType.ConnectionReset)
        .When(ctx => ctx.Header("X-Fail") == "timeout")
        .ReturnFault(FaultType.Timeout)
        .When(ctx => ctx.Header("X-Fail") == "malformed")
        .ReturnFault(FaultType.MalformedResponse)
        .Otherwise()
        .Return(200)
    await .BuildAsync();
```

### Fault Types

| Fault Type | Behavior |
|------------|----------|
| `ConnectionReset` | Abruptly closes the connection |
| `Timeout` | Delays response for 30 seconds |
| `MalformedResponse` | Returns invalid JSON |
| `EmptyResponse` | Returns empty body with 200 status |

## HTTPS Support

Enable HTTPS for the mock server:

```csharp
var mockServer = Treaty.MockFromContract(contract)
    .UseHttps()
    await .BuildAsync();
```

Note: HTTPS requires a valid development certificate.

## Port Configuration

### Random Port (Default)

```csharp
await mockServer.StartAsync();
// Port is assigned automatically
Console.WriteLine(mockServer.BaseUrl); // e.g., http://127.0.0.1:52341
```

### Specific Port

```csharp
await mockServer.StartAsync(port: 5001);
// Server listens on http://localhost:5001
```

## Logging

Enable logging to see mock server activity:

```csharp
var loggerFactory = await LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});

var mockServer = Treaty.MockFromContract(contract)
    .WithLogging(loggerFactory)
    await .BuildAsync();
```

Sample output:

```
[Treaty] Contract mock server started at http://127.0.0.1:52341 for contract 'Users API'
[Treaty] Contract Mock: Received request GET /users/1
[Treaty] Contract Mock: Using custom response rule, returning 200
```

## Lifecycle Management

### Manual Lifecycle

```csharp
var mockServer = Treaty.MockFromContract(contract)await .BuildAsync();
await mockServer.StartAsync();

// ... use the mock server ...

await mockServer.StopAsync();
await mockServer.DisposeAsync();
```

### Using IAsyncDisposable

```csharp
await using var mockServer = Treaty.MockFromContract(contract)await .BuildAsync();
await mockServer.StartAsync();

// Mock server is automatically disposed at end of scope
```

### In Tests

```csharp
public class ApiTests : IAsyncDisposable
{
    private ContractMockServer _mockServer;

    [Before(Test)]
    public async Task Setup()
    {
        _mockServer = Treaty.MockFromContract(contract)await .BuildAsync();
        await _mockServer.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _mockServer.DisposeAsync();
    }
}
```

## Error Responses

When a request doesn't match any defined endpoint, the mock server returns 404 with helpful information:

```json
{
  "treaty_error": "No endpoint defined for GET /unknown",
  "contract": "Users API",
  "available_endpoints": [
    "GET /users/{id}",
    "POST /users",
    "DELETE /users/{id}"
  ]
}
```

## Complete Example

```csharp
public class UserClientIntegrationTests : IAsyncDisposable
{
    private ContractMockServer _mockServer;
    private UserApiClient _client;

    [Before(Test)]
    public async Task Setup()
    {
        var contract = Treaty.DefineContract("Users API")
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithJsonBody<User>())
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Delete)
                .ExpectingResponse(r => r.WithStatus(204))
            await .BuildAsync();

        _mockServer = Treaty.MockFromContract(contract)
            .WithLatency(10, 50)
            .ForEndpoint("/users/{id}")
                .When(ctx => ctx.PathParam("id") == "999")
                .Return(404, new { error = "User not found" })
            await .BuildAsync();

        await _mockServer.StartAsync();

        _client = new UserApiClient(_mockServer.BaseUrl!);
    }

    [Test]
    public async Task GetUser_ReturnsUser()
    {
        var user = await _client.GetUserAsync(1);
        user.Should().NotBeNull();
    }

    [Test]
    public async Task GetUser_NotFound_ThrowsException()
    {
        var act = await () => _client.GetUserAsync(999);
        await act.Should().ThrowAsync<UserNotFoundException>();
    }

    public async ValueTask DisposeAsync()
    {
        await _mockServer.DisposeAsync();
    }
}
```

## Next Steps

- [OpenAPI Integration](openapi-integration.md) - Working with OpenAPI specs
- [Consumer Verification](consumer-verification.md) - Testing API clients
- [Provider Verification](provider-verification.md) - Testing API implementations
