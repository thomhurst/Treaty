# Consumer Verification

Consumer verification ensures your API client code sends requests that conform to the contract. This catches issues before they reach the server.

## What is Consumer Verification?

When you're the **consumer** (the team calling an API), consumer verification validates that your HTTP requests match what the API expects. This catches issues like:

- Invalid request body structure
- Missing required headers
- Wrong content types
- Invalid path parameters

## Setup

### Basic Consumer Verifier

```csharp
using Treaty;

var contract = Contract.FromOpenApi("api-spec.yaml").Build();

var consumer = ConsumerVerifier.Create()
    .WithContract(contract)
    .WithBaseUrl("https://api.example.com")
    .Build();
```

## Creating a Validating HttpClient

The consumer verifier creates an `HttpClient` that validates all requests before sending:

```csharp
var client = consumer.CreateHttpClient();

// This request is validated against the contract before being sent
var response = await client.PostAsJsonAsync("/users", new CreateUserRequest
{
    Name = "John",
    Email = "john@example.com"
});
```

If the request doesn't match the contract, a `ContractViolationException` is thrown before the request is sent.

## Using as a DelegatingHandler

For more control or to integrate with existing configurations:

```csharp
// Create just the handler
var handler = consumer.CreateHandler();

// Use with your own HttpClient setup
var client = new HttpClient(handler)
{
    BaseAddress = new Uri("https://api.example.com")
};
```

### Integration with HttpClientFactory

```csharp
// In Startup.cs or Program.cs
services.AddHttpClient("UsersApi", client =>
{
    client.BaseAddress = new Uri("https://api.example.com");
})
.AddHttpMessageHandler(() => consumer.CreateHandler());
```

## Request Validation

### Body Validation

```csharp
// Contract loaded from OpenAPI spec expects CreateUserRequest with Name and Email
var contract = Contract.FromOpenApi("api-spec.yaml").Build();

var consumer = ConsumerVerifier.Create()
    .WithContract(contract)
    .WithBaseUrl("https://api.example.com")
    .Build();

var client = consumer.CreateHttpClient();

// This will fail validation - missing Email
await client.PostAsJsonAsync("/users", new { Name = "John" });
// Throws: ContractViolationException - MissingRequired at $.email
```

### Header Validation

Headers defined as required in your OpenAPI spec will be validated:

```csharp
// This will fail if the contract requires X-Api-Key header
await client.GetAsync("/data");
// Throws: ContractViolationException - MissingHeader: X-Api-Key
```

## Testing with Mock Server

The most common pattern combines consumer verification with a mock server:

```csharp
using Treaty;
using Treaty.Mocking;

public class UserClientTests : IAsyncDisposable
{
    private readonly ApiContract _contract;
    private readonly ContractMockServer _mockServer;
    private readonly HttpClient _client;

    public UserClientTests()
    {
        _contract = Contract.FromOpenApi("api-spec.yaml").Build();
        _mockServer = MockServer.FromContract(_contract).Build();
    }

    [Before(Test)]
    public async Task Setup()
    {
        await _mockServer.StartAsync();

        // Create client pointing to mock server
        _client = new HttpClient
        {
            BaseAddress = new Uri(_mockServer.BaseUrl!)
        };
    }

    [Test]
    public async Task GetUser_DeserializesCorrectly()
    {
        // Act
        var response = await _client.GetAsync("/users/1");
        var user = await response.Content.ReadFromJsonAsync<User>();

        // Assert
        user.Should().NotBeNull();
        user.Id.Should().NotBeEmpty();
    }

    [Test]
    public async Task CreateUser_SendsValidRequest()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/users", new CreateUserRequest
        {
            Name = "Test",
            Email = "test@example.com"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _mockServer.DisposeAsync();
    }
}
```

## Logging

Enable logging to see validation details:

```csharp
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});

var consumer = ConsumerVerifier.Create()
    .WithContract(contract)
    .WithBaseUrl("https://api.example.com")
    .WithLogging(loggerFactory)
    .Build();
```

## Error Handling

### Catching Validation Errors

```csharp
try
{
    await client.PostAsJsonAsync("/users", invalidRequest);
}
catch (ContractViolationException ex)
{
    Console.WriteLine($"Request validation failed: {ex.Message}");
    foreach (var violation in ex.Violations)
    {
        Console.WriteLine($"  - {violation.Type} at {violation.Path}");
    }
}
```

### Non-Throwing Validation

If you prefer to check validity without exceptions, use the handler's events (if available) or validate manually before sending.

## Best Practices

1. **Test against mocks** - Use Treaty's mock server to test your client code in isolation.

2. **Validate in development** - Enable consumer validation during development to catch issues early.

3. **Disable in production** - Consumer validation adds overhead; consider disabling it in production builds.

4. **Test error handling** - Use mock server rules to simulate error responses and test your client's error handling.

5. **Share contracts** - Keep contract definitions in a shared library so providers and consumers use the same contract.

## Common Patterns

### Shared Contract Library

```
MyApi.Contracts/
  - Contracts.cs           # Contract definitions
  - Models/
    - User.cs
    - CreateUserRequest.cs

MyApi.Server/              # Provider
  - References MyApi.Contracts

MyApi.Client/              # Consumer
  - References MyApi.Contracts
```

### Integration Test Base Class

```csharp
public abstract class ApiClientTestBase : IAsyncDisposable
{
    protected ContractMockServer MockServer { get; private set; }
    protected HttpClient Client { get; private set; }

    protected abstract ApiContract Contract { get; }

    [Before(Test)]
    public async Task BaseSetup()
    {
        MockServer = MockServer.FromContract(Contract).Build();
        await MockServer.StartAsync();
        Client = new HttpClient { BaseAddress = new Uri(MockServer.BaseUrl!) };
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await MockServer.DisposeAsync();
    }
}
```

## Next Steps

- [Mock Server](mock-server.md) - Advanced mock server configuration
- [Provider Verification](provider-verification.md) - Testing your API implementation
- [Matchers](matchers.md) - Flexible validation with matchers
