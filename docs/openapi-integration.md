# OpenAPI Integration

Treaty can use OpenAPI/Swagger specifications as the source of truth for contract testing. This is ideal when you already have OpenAPI specs defining your API.

## Loading OpenAPI Specs

### From File

```csharp
// YAML format
var contract = Treaty.FromOpenApiSpec("api-spec.yaml")
    .ForEndpoint("/users/{id}")
    .Build();

// JSON format
var contract = Treaty.FromOpenApiSpec("api-spec.json")
    .ForEndpoint("/users/{id}")
    .Build();
```

### From Stream

```csharp
using var stream = File.OpenRead("api-spec.yaml");
var contract = Treaty.FromOpenApiSpec(stream, OpenApiFormat.Yaml)
    .ForEndpoint("/users/{id}")
    .Build();
```

## Selecting Endpoints

### Single Endpoint

```csharp
var contract = Treaty.FromOpenApiSpec("api-spec.yaml")
    .ForEndpoint("/users/{id}")
    .Build();
```

### Multiple Endpoints

```csharp
var contract = Treaty.FromOpenApiSpec("api-spec.yaml")
    .ForEndpoint("/users/{id}")
    .ForEndpoint("/users")
    .ForEndpoint("/orders/{orderId}")
    .Build();
```

### All Endpoints

```csharp
var contract = Treaty.FromOpenApiSpec("api-spec.yaml")
    .ForAllEndpoints()
    .Build();
```

## Validation from OpenAPI

When using OpenAPI specs, Treaty automatically extracts:

- **Path parameters** - Types and constraints
- **Query parameters** - Required/optional, types
- **Request body schema** - JSON schema validation
- **Response schemas** - Per status code
- **Headers** - Required headers and values
- **Content types** - Expected media types

### additionalProperties Handling

OpenAPI specs can explicitly disallow extra properties:

```yaml
components:
  schemas:
    User:
      type: object
      additionalProperties: false  # Strict - no extra fields allowed
      properties:
        id:
          type: integer
        name:
          type: string
```

When `additionalProperties: false`, Treaty validates strictly for that schema regardless of the default lenient mode.

## Mock Server from OpenAPI

Create mock servers directly from OpenAPI specs:

```csharp
var mockServer = Treaty.MockFromOpenApi("api-spec.yaml").Build();
await mockServer.StartAsync();
```

### Response Generation

The mock server generates responses using:

1. **Examples** - If the spec includes examples, they're used
2. **Enum values** - First enum value is used
3. **Format-aware defaults** - `email` -> `user@example.com`, `uuid` -> random GUID, etc.
4. **Type defaults** - `string` -> `"string"`, `integer` -> `1`, etc.

### Example in OpenAPI Spec

```yaml
paths:
  /users/{id}:
    get:
      responses:
        '200':
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/User'
              example:
                id: 123
                name: "John Doe"
                email: "john@example.com"
```

With this spec, the mock server returns the exact example.

## Provider Verification with OpenAPI

```csharp
var contract = Treaty.FromOpenApiSpec("api-spec.yaml")
    .ForEndpoint("/users/{id}")
    .ForEndpoint("/users")
    .Build();

var provider = Treaty.ForProvider<Startup>()
    .WithContract(contract)
    .Build();

// Verify against OpenAPI schema
await provider.VerifyAsync("/users/1", HttpMethod.Get);
```

## Consumer Verification with OpenAPI

```csharp
var contract = Treaty.FromOpenApiSpec("api-spec.yaml")
    .ForEndpoint("/users")
    .Build();

var consumer = Treaty.ForConsumer()
    .WithContract(contract)
    .WithBaseUrl("https://api.example.com")
    .Build();

var client = consumer.CreateHttpClient();
// Requests are validated against OpenAPI request schemas
```

## Custom Configuration

### Adding Example Data

```csharp
var contract = Treaty.FromOpenApiSpec("api-spec.yaml")
    .ForEndpoint("/users/{id}")
        .WithExampleData(e => e
            .WithPathParameter("id", "1")
            .WithHeader("X-Request-Id", "test-123"))
    .Build();
```

### Adding Provider States

```csharp
var contract = Treaty.FromOpenApiSpec("api-spec.yaml")
    .ForEndpoint("/users/{id}")
        .GivenProviderState("user exists", new { id = 1 })
    .Build();
```

## OpenAPI 3.0 Features Supported

| Feature | Supported |
|---------|-----------|
| Path parameters | Yes |
| Query parameters | Yes |
| Header parameters | Yes |
| Request body validation | Yes |
| Response body validation | Yes |
| JSON Schema | Yes |
| `$ref` references | Yes |
| `allOf` | Yes |
| `oneOf` | Yes |
| `anyOf` | Yes |
| `enum` | Yes |
| `format` | Yes |
| `pattern` (regex) | Yes |
| `minimum`/`maximum` | Yes |
| `minLength`/`maxLength` | Yes |
| `required` | Yes |
| `additionalProperties` | Yes |
| `nullable` | Yes |

## Best Practices

### Keep Specs Up to Date

Ensure your OpenAPI spec stays synchronized with your implementation. Use tools like:

- Swashbuckle (generates spec from code)
- NSwag (generates spec from code)
- Manual spec-first development

### Use Examples

Include examples in your OpenAPI spec for realistic mock responses:

```yaml
components:
  schemas:
    User:
      type: object
      properties:
        id:
          type: integer
          example: 123
        name:
          type: string
          example: "John Doe"
```

### Specify additionalProperties

Be explicit about whether extra properties are allowed:

```yaml
# Strict - fail if extra fields present
User:
  type: object
  additionalProperties: false
  properties:
    id:
      type: integer

# Lenient - allow extra fields (default in Treaty)
User:
  type: object
  additionalProperties: true
  properties:
    id:
      type: integer
```

### Organize with $ref

Use references for reusable schemas:

```yaml
paths:
  /users/{id}:
    get:
      responses:
        '200':
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/User'
        '404':
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/Error'

components:
  schemas:
    User:
      type: object
      properties:
        id:
          type: integer
        name:
          type: string

    Error:
      type: object
      properties:
        code:
          type: string
        message:
          type: string
```

## Complete Example

```yaml
# api-spec.yaml
openapi: 3.0.3
info:
  title: Users API
  version: 1.0.0

paths:
  /users/{id}:
    get:
      summary: Get user by ID
      parameters:
        - name: id
          in: path
          required: true
          schema:
            type: integer
      responses:
        '200':
          description: User found
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/User'
              example:
                id: 1
                name: "John Doe"
                email: "john@example.com"
        '404':
          description: User not found

components:
  schemas:
    User:
      type: object
      required:
        - id
        - name
        - email
      properties:
        id:
          type: integer
        name:
          type: string
        email:
          type: string
          format: email
```

```csharp
// Test code
var contract = Treaty.FromOpenApiSpec("api-spec.yaml")
    .ForEndpoint("/users/{id}")
    .Build();

// Provider verification
var provider = Treaty.ForProvider<Startup>()
    .WithContract(contract)
    .Build();

await provider.VerifyAsync("/users/1", HttpMethod.Get);

// Mock server
var mockServer = Treaty.MockFromOpenApi("api-spec.yaml").Build();
await mockServer.StartAsync();

var client = new HttpClient { BaseAddress = new Uri(mockServer.BaseUrl!) };
var response = await client.GetAsync("/users/1");
// Returns: {"id": 1, "name": "John Doe", "email": "john@example.com"}
```

## Next Steps

- [Getting Started](getting-started.md) - Basic Treaty usage
- [Provider Verification](provider-verification.md) - Testing your API
- [Mock Server](mock-server.md) - Mock server configuration
