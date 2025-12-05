# Treaty

A modern, lightweight, code-first contract testing framework for .NET. Treaty helps ensure your APIs stay in sync with their contracts by validating producers and enabling consumers to build against reliable mocks.

## Features

- **OpenAPI Integration** - Use your existing OpenAPI/Swagger specs as the source of truth
- **Code-First Contracts** - Define contracts in C# with a fluent, type-safe API
- **Provider Verification** - Verify your API implementation matches the contract
- **Consumer Mocking** - Generate spec-compliant mock servers for parallel development
- **Request Validation** - Catch client errors before they hit the server
- **No Central Server** - Works entirely in your test suite, no infrastructure required
- **Lenient by Default** - Extra fields are ignored for better forward compatibility

## Quick Start

### Installation

```bash
dotnet add package Treaty
```

### Define a Contract

```csharp
var contract = Treaty.DefineContract("Users API")
    .ForEndpoint("/users/{id}")
        .WithMethod(HttpMethod.Get)
        .ExpectingResponse(r => r
            .WithStatus(200)
            .WithJsonBody<User>())
    .ForEndpoint("/users")
        .WithMethod(HttpMethod.Post)
        .ExpectingRequest(r => r
            .WithJsonBody<CreateUserRequest>())
        .ExpectingResponse(r => r
            .WithStatus(201)
            .WithJsonBody<User>())
    .Build();
```

### Verify Your API (Provider Testing)

```csharp
// In your test class
var provider = Treaty.ForProvider<Startup>()
    .WithContract(contract)
    .Build();

// Verify a single endpoint
await provider.VerifyAsync("/users/1", HttpMethod.Get);

// Or verify all endpoints at once
var results = await provider.VerifyAllAsync();
```

### Mock for Consumer Development

```csharp
// Start a mock server from your contract
var mockServer = Treaty.MockFromContract(contract).Build();
await mockServer.StartAsync();

// Use the mock server URL in your client tests
var client = new HttpClient { BaseAddress = new Uri(mockServer.BaseUrl!) };
var response = await client.GetAsync("/users/1");
// Response body is auto-generated based on the contract schema
```

### Using OpenAPI Specs

```csharp
// Load contract from OpenAPI spec
var contract = Treaty.FromOpenApiSpec("api-spec.yaml")
    .ForEndpoint("/users/{id}")
    .Build();

// Or start a mock server directly from OpenAPI
var mockServer = Treaty.MockFromOpenApi("api-spec.yaml").Build();
await mockServer.StartAsync();
```

## Validation Modes

Treaty uses **lenient validation by default** - extra fields in responses are ignored for better forward compatibility. This means:

- If a producer adds a new optional field, consumer tests won't break
- Tests focus on the fields you care about, not implementation details

### Strict Mode (Opt-in)

When you need strict validation:

```csharp
.ExpectingResponse(r => r
    .WithStatus(200)
    .WithJsonBody<User>(v => v.StrictMode())) // Extra fields will cause violations
```

OpenAPI specs with `additionalProperties: false` are automatically validated strictly.

## Matchers

Use matchers for flexible validation:

```csharp
.ExpectingResponse(r => r
    .WithStatus(200)
    .WithMatcherSchema(new {
        id = Match.Guid(),
        name = Match.NonEmptyString(),
        email = Match.Email(),
        age = Match.Integer(min: 0, max: 150),
        status = Match.OneOf("active", "inactive"),
        createdAt = Match.DateTime()
    }))
```

Available matchers: `Guid()`, `Email()`, `Integer()`, `Decimal()`, `DateTime()`, `DateOnly()`, `TimeOnly()`, `Uri()`, `Regex()`, `OneOf()`, `NonEmptyString()`, `Boolean()`, `Any()`, `Null()`, and more.

## Documentation

- [Getting Started](docs/getting-started.md)
- [Provider Verification](docs/provider-verification.md)
- [Consumer Verification](docs/consumer-verification.md)
- [Mock Server](docs/mock-server.md)
- [OpenAPI Integration](docs/openapi-integration.md)
- [Matchers](docs/matchers.md)
- [Validation Modes](docs/validation-modes.md)

## Why Treaty?

| Feature | Treaty | Pact |
|---------|--------|------|
| Contract Source | OpenAPI/Code-first | Consumer-driven |
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
