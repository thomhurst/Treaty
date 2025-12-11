# Validation Modes

Treaty supports two validation modes: **lenient** (default) and **strict**. Understanding these modes helps you write robust contract tests that don't break unnecessarily.

## Lenient Mode (Default)

By default, Treaty uses lenient validation. Extra fields in responses are ignored.

### Why Lenient by Default?

Consider this scenario:

1. Provider returns `{ "id": 1, "name": "John" }`
2. Consumer tests validate this response
3. Provider adds a new optional field: `{ "id": 1, "name": "John", "avatar": "..." }`

With **strict validation**, the consumer tests break even though the consumer code still works fine.

With **lenient validation** (Treaty's default), the consumer tests continue to pass because the extra `avatar` field is ignored.

This approach:
- Enables forward compatibility
- Reduces false positives
- Allows providers to add optional fields without breaking consumers

### Lenient Mode Behavior

```csharp
// Contract expects: { id, name }
var contract = await Treaty.DefineContract("API")
    .ForEndpoint("/users/{id}")
        .WithMethod(HttpMethod.Get)
        .ExpectingResponse(r => r
            .WithStatus(200)
            .WithJsonBody<User>())  // User has: Id, Name
    .BuildAsync();

// Response: { "id": 1, "name": "John", "extraField": "ignored" }
// Result: PASS - extra field is ignored
```

## Strict Mode

Enable strict mode when extra fields should cause validation failures.

### Enabling Strict Mode

```csharp
.ExpectingResponse(r => r
    .WithStatus(200)
    .WithJsonBody<User>(v => v.StrictMode()))
```

### Strict Mode Behavior

```csharp
var contract = await Treaty.DefineContract("API")
    .ForEndpoint("/users/{id}")
        .WithMethod(HttpMethod.Get)
        .ExpectingResponse(r => r
            .WithStatus(200)
            .WithJsonBody<User>(v => v.StrictMode()))
    .BuildAsync();

// Response: { "id": 1, "name": "John", "extraField": "value" }
// Result: FAIL - UnexpectedField at $.extraField
```

### When to Use Strict Mode

Use strict mode when:

- You need to ensure the API doesn't return sensitive fields
- You're testing security-critical endpoints
- You want to catch unintended schema changes
- Your API specification explicitly forbids extra fields

## OpenAPI additionalProperties

When using OpenAPI specs, `additionalProperties` controls strictness:

```yaml
# Strict - no extra fields allowed
User:
  type: object
  additionalProperties: false
  properties:
    id:
      type: integer
    name:
      type: string
```

With `additionalProperties: false`, Treaty validates strictly for that schema even in lenient mode.

```yaml
# Lenient - extra fields allowed (default behavior)
User:
  type: object
  additionalProperties: true  # Or omitted entirely
  properties:
    id:
      type: integer
```

## Partial Validation

Validate only specific fields while ignoring others:

```csharp
.ExpectingResponse(r => r
    .WithStatus(200)
    .WithJsonBody<User>(v => v
        .OnlyValidate("id", "name")))
```

This validates only `id` and `name`, ignoring all other fields.

### Combining with Strict Mode

```csharp
.ExpectingResponse(r => r
    .WithStatus(200)
    .WithJsonBody<User>(v => v
        .OnlyValidate("id", "name")
        .StrictMode()))  // Strict only for id and name
```

## Nullable Handling

Treaty correctly handles nullable fields:

```csharp
public record User(int Id, string Name, string? Avatar);

// Valid responses:
// { "id": 1, "name": "John", "avatar": "url" }  - PASS
// { "id": 1, "name": "John", "avatar": null }   - PASS
// { "id": 1, "name": "John" }                   - PASS (Avatar is nullable)
```

### Required vs Optional

```csharp
// Non-nullable = required
public record User(int Id, string Name);  // Both required

// Nullable = optional
public record User(int Id, string? Name);  // Name is optional
```

With matchers:

```csharp
.WithMatcherSchema(new
{
    id = Match.Integer(),        // Required
    name = Match.NonEmptyString(),  // Required, non-empty
    avatar = Match.Any()         // Optional (Any accepts null)
})
```

## Validation Summary

| Scenario | Lenient (Default) | Strict |
|----------|-------------------|--------|
| Missing required field | FAIL | FAIL |
| Wrong type | FAIL | FAIL |
| Invalid format | FAIL | FAIL |
| Extra field | PASS | FAIL |
| Null for non-nullable | FAIL | FAIL |
| Null for nullable | PASS | PASS |

## Examples

### Lenient Mode Example

```csharp
var contract = await Treaty.DefineContract("Users API")
    .ForEndpoint("/users/{id}")
        .WithMethod(HttpMethod.Get)
        .ExpectingResponse(r => r
            .WithStatus(200)
            .WithJsonBody<User>())  // Lenient by default
    .BuildAsync();

// API returns: { "id": 1, "name": "John", "internalField": "..." }
// Test result: PASS
// The extra "internalField" is ignored
```

### Strict Mode Example

```csharp
var contract = await Treaty.DefineContract("Users API")
    .ForEndpoint("/users/{id}")
        .WithMethod(HttpMethod.Get)
        .ExpectingResponse(r => r
            .WithStatus(200)
            .WithJsonBody<User>(v => v.StrictMode()))
    .BuildAsync();

// API returns: { "id": 1, "name": "John", "internalField": "..." }
// Test result: FAIL
// Error: UnexpectedField at $.internalField
```

### OpenAPI Strict Example

```yaml
# api-spec.yaml
components:
  schemas:
    User:
      type: object
      additionalProperties: false  # Enforces strictness
      required:
        - id
        - name
      properties:
        id:
          type: integer
        name:
          type: string
```

```csharp
var contract = await Treaty.FromOpenApiSpec("api-spec.yaml")
    .ForEndpoint("/users/{id}")
    .BuildAsync();

// API returns: { "id": 1, "name": "John", "extra": "..." }
// Test result: FAIL (additionalProperties: false is honored)
```

## Migration from Strict Default

If you're migrating from an older version of Treaty that used strict validation by default, you have options:

### Option 1: Enable Strict Mode Globally

Add `.StrictMode()` to all response expectations that need strict validation.

### Option 2: Review Failing Tests

Tests that were passing under strict mode but fail under lenient mode were likely catching unintended fields. Review these cases:

- If the extra fields are intentional, the test now correctly passes
- If the extra fields indicate a bug, add `.StrictMode()` to catch them

## Best Practices

1. **Use lenient mode** for most consumer tests - it provides better forward compatibility.

2. **Use strict mode** for security-sensitive endpoints where extra fields could leak data.

3. **Use OpenAPI `additionalProperties: false`** when you want to document and enforce strict schemas.

4. **Use partial validation** when you only care about specific fields.

5. **Don't fight the framework** - if you find yourself adding `.StrictMode()` everywhere, consider whether strict validation is truly necessary.

## Next Steps

- [Matchers](matchers.md) - Flexible validation with matchers
- [Provider Verification](provider-verification.md) - Testing your API
- [Getting Started](getting-started.md) - Basic Treaty usage
