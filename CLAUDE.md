# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build the solution
dotnet build

# Run all tests (TUnit-based)
dotnet build && ./tests/Treaty.Tests/bin/Debug/net10.0/Treaty.Tests.exe

# Run a specific test class
./tests/Treaty.Tests/bin/Debug/net10.0/Treaty.Tests.exe --treenode-filter "/*/*/ClassName/*"

# Run a specific test method
./tests/Treaty.Tests/bin/Debug/net10.0/Treaty.Tests.exe --treenode-filter "/*/*/ClassName/MethodName"
```

Note: The project uses TUnit as the test framework. Tests must be run via the built executable, not `dotnet test` (which is not supported on .NET 10 with Microsoft.Testing.Platform).

## Architecture

Treaty is an OpenAPI-first contract testing framework for .NET.

### Async-First Design

**IMPORTANT**: Treaty uses an async-first API design. All builder methods return `Task<T>` and must be awaited:

```csharp
// ✅ CORRECT - Always use await with BuildAsync()
var contract = await Contract.FromOpenApi("spec.yaml").BuildAsync();
var provider = await ProviderVerifier.ForWebApplication<Program>()
    .WithContract(contract)
    .BuildAsync();

// ❌ INCORRECT - Never use blocking calls
var contract = Contract.FromOpenApi("spec.yaml").BuildAsync().Result; // DON'T DO THIS
var provider = ProviderVerifier.ForWebApplication<Program>().BuildAsync().GetAwaiter().GetResult(); // DON'T DO THIS
```

**Why async-only?**
- Consistent API surface - no synchronous alternatives that might block
- Future-proof for I/O operations (network calls, file operations)
- Integrates naturally with modern async/await C# patterns
- Avoids deadlocks and thread pool starvation

### Entry Points
The library provides domain-specific static classes as entry points:

**Contract** (`src/Treaty/Contract.cs`)
- `Contract.FromOpenApi(path)` / `Contract.FromOpenApi(stream, format)` - Load contracts from OpenAPI YAML/JSON (returns builder, requires `await .BuildAsync()`)
- `Contract.Compare(old, new)` - Detect breaking changes between contract versions

**MockServer** (`src/Treaty/MockServer.cs`)
- `MockServer.FromOpenApi(path)` / `MockServer.FromOpenApi(stream, format)` - Create mock server from OpenAPI spec (returns builder, requires `await .BuildAsync()`)
- `MockServer.FromContract(contract)` - Create mock server from loaded contract (returns builder, requires `await .BuildAsync()`)

**ProviderVerifier** (`src/Treaty/ProviderVerifier.cs`)
- `ProviderVerifier.ForWebApplication<TEntryPoint>()` - Create WebApplicationFactory-based verifier for API testing (returns builder, requires `await .BuildAsync()`)
- `ProviderVerifier.ForHttpClient()` - Create HTTP-based verifier for live API testing (returns builder, requires `await .BuildAsync()`)

**ConsumerVerifier** (`src/Treaty/ConsumerVerifier.cs`)
- `ConsumerVerifier.Create()` - Create consumer verifier for client testing (returns builder, requires `await .BuildAsync()`)

**Note**: All builders use async `BuildAsync()` for consistency. Always use `await` when calling `BuildAsync()`.

### Key Abstractions

**Contracts** (`src/Treaty/Contracts/`)
- `ContractDefinition` - Immutable contract with endpoints, metadata, and defaults
- `EndpointContract` - Single endpoint definition with request/response expectations
- `ContractComparer` - Detects breaking changes between contract versions

**OpenAPI** (`src/Treaty/OpenApi/`)
- `OpenApiContractBuilder` - Builds contracts from OpenAPI specs
- `OpenApiSchemaValidator` - Validates JSON against OpenAPI schemas
- `MockServer` / `MockServerBuilder` - Mock server from OpenAPI specs

**Provider Verification** (`src/Treaty/Provider/`)
- `ProviderVerifier<TEntryPoint>` - Uses WebApplicationFactory to verify API endpoints
- `HttpProviderVerifier` - HTTP-based verifier for live APIs
- `IStateHandler` - Interface for setting up provider states before verification
- `BulkVerificationResult` - Results from `VerifyAllAsync()` with summary methods
- `VerificationProgress` - Progress reporting for bulk verification
- Supports bulk verification with parallel execution
- Provides both throwing (`VerifyAsync()`) and non-throwing (`TryVerifyAsync()`) validation

**Consumer Verification** (`src/Treaty/Consumer/`)
- `ConsumerValidationClient` - Validates outgoing HTTP requests against contracts
- `ContractValidatingHandler` - DelegatingHandler that validates requests

**Mock Servers** (`src/Treaty/Mocking/`)
- `ContractMockServer` - Mock server from loaded contracts
- Supports custom response rules, latency simulation, auth

**Validation** (`src/Treaty/Validation/`)
- `ISchemaValidator` - Interface for body validation strategies
- `OpenApiSchemaValidator` - Validates JSON against OpenAPI schemas
- `ValidationResult` - Contains violations from validation
- `ContractViolation` - Represents a single contract violation
- `ContractViolationException` - Thrown on validation failures
- Lenient by default (extra fields ignored); strict mode via `additionalProperties: false` in spec
- Supports `readOnly`/`writeOnly` field constraints based on direction (request vs response)
- Validates formats (email, uuid, date-time, ipv4, ipv6, etc.), patterns, enums, and numeric constraints
- Handles OpenAPI 3.1 features: discriminators, oneOf, anyOf, allOf, const values

**Diagnostics** (`src/Treaty/Diagnostics/`)
- `DiagnosticReport` - Comprehensive failure reports with suggestions for fixing violations
- `DiagnosticFormatter` - Formats violations into readable output
- `JsonDiffGenerator` - Generates visual diffs for body mismatches
- `JsonDiff` - Represents differences between expected and actual JSON

### Test Structure
- `tests/Treaty.Tests/Unit/` - Fast, isolated unit tests organized by namespace
  - `Builders/` - Builder pattern tests
  - `Contracts/` - Contract model tests
  - `Diagnostics/` - Diagnostic reporting tests
  - `Provider/` - Provider verification logic tests
  - `Validation/` - Validation engine tests
- `tests/Treaty.Tests/Integration/` - End-to-end integration tests
  - `Provider/` - Full provider verification scenarios
  - `Consumer/` - Consumer validation scenarios
  - `OpenApi/` - OpenAPI parsing and contract building
- `tests/Treaty.Tests/Specs/` - Sample OpenAPI specifications for testing
  - `petstore.yaml`, `users-api.json`, `with-examples.yaml`, etc.
- `tests/Treaty.Tests/TestApi/` - Simple test API for integration tests

### Common Patterns

**Bulk Verification with Progress Reporting**
```csharp
var contract = await Contract.FromOpenApi("api-spec.yaml").BuildAsync();
var provider = await ProviderVerifier.ForWebApplication<Program>()
    .WithContract(contract)
    .BuildAsync();

var progress = new Progress<VerificationProgress>(p =>
    Console.WriteLine($"[{p.CompletedCount}/{p.TotalCount}] {p.CurrentEndpoint}"));

var results = await provider.VerifyAllAsync(progress: progress);

// Check results
if (results.AllPassed)
{
    Console.WriteLine($"✓ All {results.PassedCount} endpoints passed!");
}
else
{
    Console.WriteLine(results.GetSummary()); // Formatted summary
    results.ThrowIfAnyFailed(); // Throws with all violations
}
```

**Non-Throwing Validation**
```csharp
// Use TryVerifyAsync() instead of VerifyAsync() for non-throwing validation
var result = await provider.TryVerifyAsync("/users/123", HttpMethod.Get);

if (!result.IsValid)
{
    foreach (var violation in result.Violations)
    {
        Console.WriteLine($"{violation.Type} at {violation.Path}: {violation.Message}");
    }
}
```

**Provider State Handling**
```csharp
var contract = await Contract.FromOpenApi("api-spec.yaml").BuildAsync();
var provider = await ProviderVerifier.ForWebApplication<Program>()
    .WithContract(contract)
    .WithStateHandler(async (state, ct) => {
        // Setup test data based on state.Name and state.Parameters
        if (state.Name == "user exists")
        {
            var userId = state.Parameters["userId"];
            await SeedUserAsync(userId);
        }
        else if (state.Name == "user does not exist")
        {
            await ClearUsersAsync();
        }
    })
    .BuildAsync();

await provider.VerifyAsync("/users/{id}", HttpMethod.Get);
```

**Conditional Mock Responses**
```csharp
var mockServer = await MockServer.FromOpenApi("api-spec.yaml")
    .ForEndpoint("/users/{id}")
        .When(req => req.PathParam("id") == "0").Return(404)
        .When(req => req.PathParam("id") == "999").Return(500)
        .Otherwise().Return(200)
    .ForEndpoint("/admin/*")
        .When(req => !req.HasHeader("Authorization")).Return(401)
        .Otherwise().Return(200)
    .BuildAsync();

await mockServer.StartAsync();
// Use mockServer.BaseUrl in your tests
```

**Breaking Change Detection**
```csharp
var oldContract = await Contract.FromOpenApi("api-v1.yaml").BuildAsync();
var newContract = await Contract.FromOpenApi("api-v2.yaml").BuildAsync();

var diff = Contract.Compare(oldContract, newContract);

if (diff.HasBreakingChanges)
{
    Console.WriteLine("Breaking changes detected:");
    foreach (var change in diff.BreakingChanges)
    {
        Console.WriteLine($"  [{change.Severity}] {change.Description}");
        Console.WriteLine($"    Endpoint: {change.Endpoint}");
    }

    diff.ThrowIfBreaking(); // Throws if breaking changes exist
}
```

### Troubleshooting

**Getting Detailed Diagnostics**
```csharp
try
{
    await provider.VerifyAsync("/users/123", HttpMethod.Get);
}
catch (ContractViolationException ex)
{
    // Exception message includes formatted diagnostic report
    Console.WriteLine(ex.Message);

    // Or iterate through violations manually
    foreach (var violation in ex.Violations)
    {
        Console.WriteLine($"  {violation.Type} at {violation.Path}");
        Console.WriteLine($"    {violation.Message}");
        if (violation.Expected != null)
        {
            Console.WriteLine($"    Expected: {violation.Expected}");
        }
        if (violation.Actual != null)
        {
            Console.WriteLine($"    Actual: {violation.Actual}");
        }
    }
}
```

**Common Issues**
- **UnexpectedField violations**: By default, extra fields in responses are allowed (lenient mode). UnexpectedField violations only occur when:
  - The OpenAPI spec explicitly sets `additionalProperties: false` on the schema, OR
  - Strict mode is explicitly enabled
  - This design supports forward compatibility - providers can add new optional fields without breaking consumer tests

- **Test framework compatibility**: This project uses TUnit as the test framework on .NET 10. Tests must be run via the built executable (see Build Commands above), not `dotnet test` (which is not supported by Microsoft.Testing.Platform on .NET 10).

- **ReadOnly/WriteOnly violations**:
  - `readOnly` fields should NOT be sent in requests (they're server-generated, like `id` or `createdAt`)
  - `writeOnly` fields should NOT appear in responses (they're input-only, like `password`)
  - Violations occur when these constraints are violated

- **Missing provider states**: If verification fails because provider states aren't set up, use `.WithStateHandler()` to configure state setup logic. Each state in the contract can have a handler that seeds test data.

- **Status code mismatches**: Ensure your API returns one of the status codes defined in the OpenAPI spec for that endpoint. The contract expects specific status codes (e.g., 200, 404) to be documented.
