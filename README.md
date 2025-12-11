# Treaty

A modern, lightweight contract testing framework for .NET that uses OpenAPI specifications as the source of truth. Treaty helps ensure your APIs stay in sync with their contracts by validating producers and enabling consumers to build against reliable mocks.

## Features

- **OpenAPI First** - Your OpenAPI/Swagger specs are the single source of truth
- **Provider Verification** - Verify your API implementation matches the contract
- **Consumer Mocking** - Generate spec-compliant mock servers for parallel development
- **Request Validation** - Catch client errors before they hit the server
- **No Central Server** - Works entirely in your test suite, no infrastructure required

## Quick Start

### Installation

```bash
dotnet add package Treaty
```

### Load a Contract from OpenAPI

```csharp
// Load contract from OpenAPI spec (YAML or JSON)
var contract = await Contract.FromOpenApi("api-spec.yaml").BuildAsync();

// Or filter to specific endpoints
var contract = await Contract.FromOpenApi("api-spec.yaml")
    .ForEndpoint("/users/{id}")
    .BuildAsync();
```

### Verify Your API (Provider Testing) - Spins up an In-Memory TestServer and intercepts your response to validate it

```csharp
// In your test class
var contract = await Contract.FromOpenApi("api-spec.yaml").BuildAsync();

var provider = await ProviderVerifier.ForWebApplication<Startup>()
    .WithContract(contract)
    .BuildAsync();

// Verify a single endpoint
await provider.VerifyAsync("/users/1", HttpMethod.Get);

// Or verify all endpoints at once
var results = await provider.VerifyAllAsync();
```

### Mock for Consumer Development

```csharp
// Start a mock server directly from OpenAPI
var mockServer = await MockServer.FromOpenApi("api-spec.yaml").BuildAsync();
await mockServer.StartAsync();

// Use the mock server URL in your client tests
var client = new HttpClient { BaseAddress = new Uri(mockServer.BaseUrl!) };
var response = await client.GetAsync("/users/1");
// Response body is auto-generated based on the OpenAPI schema

// Or create a mock from an already-loaded contract
var contract = await Contract.FromOpenApi("api-spec.yaml").BuildAsync();
var mockServer = await MockServer.FromContract(contract).BuildAsync();
```

### Conditional Mock Responses

```csharp
var mockServer = await MockServer.FromOpenApi("api-spec.yaml")
    .ForEndpoint("/users/{id}")
        .When(req => req.PathParam("id") == "0").Return(404)
        .Otherwise().Return(200)
    .BuildAsync();
```

### Consumer Request Validation

```csharp
var contract = await Contract.FromOpenApi("api-spec.yaml").BuildAsync();

var consumer = await ConsumerVerifier.Create()
    .WithContract(contract)
    .WithBaseUrl("https://api.example.com")
    .BuildAsync();

// HttpClient validates all requests against the contract
var client = consumer.CreateHttpClient();
await client.GetAsync("/users/1"); // Validated against contract
```

### Contract Comparison (Breaking Change Detection)

```csharp
var oldContract = await Contract.FromOpenApi("api-v1.yaml").BuildAsync();
var newContract = await Contract.FromOpenApi("api-v2.yaml").BuildAsync();

var diff = Contract.Compare(oldContract, newContract);

if (diff.HasBreakingChanges)
{
    Console.WriteLine("Breaking changes detected!");
    foreach (var change in diff.BreakingChanges)
    {
        Console.WriteLine($"  - {change.Description}");
    }
}

// Or throw if breaking changes exist
diff.ThrowIfBreaking();
```

## Validation Modes

Treaty uses **lenient validation by default** - extra fields in responses are ignored for better forward compatibility. This means:

- If a producer adds a new optional field, consumer tests won't break
- Tests focus on the fields you care about, not implementation details

OpenAPI specs with `additionalProperties: false` are automatically validated strictly.

## Documentation

- [Getting Started](docs/getting-started.md)
- [Provider Verification](docs/provider-verification.md)
- [Consumer Verification](docs/consumer-verification.md)
- [Mock Server](docs/mock-server.md)
- [OpenAPI Integration](docs/openapi-integration.md)
- [Validation Modes](docs/validation-modes.md)

## Why Treaty?

| Feature | Treaty | Pact |
|---------|--------|------|
| Contract Source | OpenAPI specs | Consumer-driven |
| Central Server | Not required | Pact Broker needed |
| Setup Complexity | Single NuGet package | Multiple components |
| .NET Integration | Native, first-class | Via wrapper libraries |
| Mock Generation | Built-in, spec-aware | Separate tooling |

Treaty is ideal when:
- You already have OpenAPI specs
- You want a lightweight solution without infrastructure overhead
- You need producer-driven contracts (public APIs, many consumers)
- You want native .NET integration with your test framework

## License

MIT
