# Getting Started with Treaty

This guide will help you set up Treaty and write your first contract test.

## Prerequisites

- .NET 8.0 or later
- A test framework (xUnit, NUnit, TUnit, or MSTest)
- An OpenAPI specification for your API

## Installation

Add Treaty to your test project:

```bash
dotnet add package Treaty
```

## Core Concepts

### Contract
A contract defines the expected behavior of an API, including endpoints, request/response formats, and status codes. In Treaty, contracts are loaded from OpenAPI specifications.

### Endpoint
An endpoint represents a single API operation (e.g., `GET /users/{id}`).

### Schema Validation
Treaty validates requests and responses against the schemas defined in your OpenAPI spec.

## Your First Contract

Let's load a contract from an OpenAPI specification:

```csharp
using Treaty;
using Treaty.OpenApi;

// Load contract from OpenAPI spec
var contract = await Contract.FromOpenApi("api-spec.yaml").BuildAsync();

// Or load from a stream
using var stream = await File.OpenRead("api-spec.yaml");
var contract = await Contract.FromOpenApi(stream, OpenApiFormat.Yaml).BuildAsync();

// Filter to specific endpoints if needed
var contract = await Contract.FromOpenApi("api-spec.yaml")
    .ForEndpoint("/users/{id}")
    .BuildAsync();
```

## Provider Verification

Verify that your API implementation matches the contract:

```csharp
using Treaty;

public class UserApiTests
{
    [Test]
    public async Task Api_ReturnsValidUserResponse()
    {
        // Arrange - Load contract and create provider verifier
        var contract = await Contract.FromOpenApi("api-spec.yaml").BuildAsync();

        var provider = await ProviderVerifier.ForWebApplication<Startup>()
            .WithContract(contract)
            .BuildAsync();

        // Act & Assert - Verify the endpoint
        await provider.VerifyAsync("/users/1", HttpMethod.Get);
    }
}
```

## Consumer Verification

Test your API client against a mock server:

```csharp
using Treaty;
using Treaty.Mocking;

public class UserClientTests : IAsyncDisposable
{
    private IMockServer _mockServer;

    [Before(Test)]
    public async Task Setup()
    {
        _mockServer = await MockServer.FromOpenApi("api-spec.yaml").BuildAsync();
        await _mockServer.StartAsync();
    }

    [Test]
    public async Task Client_CanFetchUser()
    {
        // Arrange
        var client = await new HttpClient { BaseAddress = new Uri(_mockServer.BaseUrl!) };

        // Act
        var response = await client.GetAsync("/users/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await response.Content.ReadFromJsonAsync<User>();
        user.Should().NotBeNull();
    }

    public async ValueTask DisposeAsync()
    {
        await _mockServer.DisposeAsync();
    }
}
```

## Understanding Results

When verification fails, Treaty provides detailed error messages:

```
Contract violation at GET /users/1:

  1. MissingRequired at `$.email`
     Missing required field 'Email'

  2. InvalidType at `$.age`
     Expected: integer
     Actual:   string

Suggestions:
  - Ensure the field 'Email' is included in the response.
  - Check that 'age' returns the correct type (integer).
```

## Next Steps

- [Provider Verification](provider-verification.md) - Deep dive into testing your API
- [Consumer Verification](consumer-verification.md) - Testing API clients
- [Mock Server](mock-server.md) - Advanced mock server configuration
- [OpenAPI Integration](openapi-integration.md) - Working with OpenAPI specs
- [Validation Modes](validation-modes.md) - Lenient vs strict validation
