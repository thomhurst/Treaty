# Getting Started with Treaty

This guide will help you set up Treaty and write your first contract test.

## Prerequisites

- .NET 8.0 or later
- A test framework (xUnit, NUnit, TUnit, or MSTest)

## Installation

Add Treaty to your test project:

```bash
dotnet add package Treaty
```

## Core Concepts

### Contract
A contract defines the expected behavior of an API, including endpoints, request/response formats, and status codes.

### Endpoint
An endpoint represents a single API operation (e.g., `GET /users/{id}`).

### Expectation
Expectations define what a valid request or response looks like for an endpoint.

### Matcher
Matchers provide flexible validation rules (e.g., "any valid email", "integer between 1-100").

## Your First Contract

Let's create a simple contract for a Users API:

```csharp
using Treaty;

// Define the response type
public record User(int Id, string Name, string Email);

// Create the contract
var contract = Treaty.DefineContract("Users API")
    .ForEndpoint("/users/{id}")
        .WithMethod(HttpMethod.Get)
        .ExpectingResponse(r => r
            .WithStatus(200)
            .WithJsonBody<User>())
    .Build();
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
        // Arrange - Create provider verifier pointing to your API
        var provider = Treaty.ForProvider<Startup>()
            .WithContract(contract)
            .Build();

        // Act & Assert - Verify the endpoint
        await provider.VerifyAsync("/users/1", HttpMethod.Get);
    }
}
```

## Consumer Verification

Test your API client against a mock server:

```csharp
using Treaty;

public class UserClientTests : IAsyncDisposable
{
    private ContractMockServer _mockServer;

    [Before(Test)]
    public async Task Setup()
    {
        _mockServer = Treaty.MockFromContract(contract).Build();
        await _mockServer.StartAsync();
    }

    [Test]
    public async Task Client_CanFetchUser()
    {
        // Arrange
        var client = new HttpClient { BaseAddress = new Uri(_mockServer.BaseUrl!) };

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
- [Matchers](matchers.md) - Flexible validation with matchers
