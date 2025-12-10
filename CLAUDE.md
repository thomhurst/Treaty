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

### Entry Points
The library provides domain-specific static classes as entry points:

**Contract** (`src/Treaty/Contract.cs`)
- `Contract.FromOpenApi(path)` / `Contract.FromOpenApi(stream, format)` - Load contracts from OpenAPI YAML/JSON
- `Contract.Compare(old, new)` - Detect breaking changes between contract versions

**MockServer** (`src/Treaty/MockServer.cs`)
- `MockServer.FromOpenApi(path)` / `MockServer.FromOpenApi(stream, format)` - Create mock server from OpenAPI spec
- `MockServer.FromContract(contract)` - Create mock server from loaded contract

**ProviderVerifier** (`src/Treaty/ProviderVerifier.cs`)
- `ProviderVerifier.ForTestServer<TStartup>()` - Create TestServer-based verifier for API testing
- `ProviderVerifier.ForHttpClient()` - Create HTTP-based verifier for live API testing

**ConsumerVerifier** (`src/Treaty/ConsumerVerifier.cs`)
- `ConsumerVerifier.Create()` - Create consumer verifier for client testing

### Key Abstractions

**Contracts** (`src/Treaty/Contracts/`)
- `ApiContract` - Immutable contract with endpoints, metadata, and defaults
- `EndpointContract` - Single endpoint definition with request/response expectations
- `ContractComparer` - Detects breaking changes between contract versions

**OpenAPI** (`src/Treaty/OpenApi/`)
- `OpenApiContractBuilder` - Builds contracts from OpenAPI specs
- `OpenApiSchemaValidator` - Validates JSON against OpenAPI schemas
- `MockServer` / `MockServerBuilder` - Mock server from OpenAPI specs

**Provider Verification** (`src/Treaty/Provider/`)
- `ProviderVerifier<TStartup>` - Uses TestServer to verify API endpoints
- `IStateHandler` - Interface for setting up provider states before verification
- Supports bulk verification with parallel execution

**Consumer Verification** (`src/Treaty/Consumer/`)
- `ConsumerValidationClient` - Validates outgoing HTTP requests against contracts
- `ContractValidatingHandler` - DelegatingHandler that validates requests

**Mock Servers** (`src/Treaty/Mocking/`)
- `ContractMockServer` - Mock server from loaded contracts
- Supports custom response rules, latency simulation, auth

**Validation** (`src/Treaty/Validation/`)
- `ISchemaValidator` - Interface for body validation strategies
- `OpenApiSchemaValidator` - Validates against OpenAPI schemas
- Lenient by default (extra fields ignored); strict mode opt-in via `additionalProperties: false`

### Test Structure
- `tests/Treaty.Tests/Unit/` - Unit tests organized by namespace
- `tests/Treaty.Tests/Integration/` - Integration tests for Provider, Consumer, OpenApi
- `tests/Treaty.Tests/Specs/` - OpenAPI spec files for testing
