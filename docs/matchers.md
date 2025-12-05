# Matchers

Matchers provide flexible validation rules for contract testing. Instead of checking exact values, matchers validate that values conform to expected patterns or types.

## Why Use Matchers?

Exact value matching is brittle:

```csharp
// Brittle - fails if ID changes
.WithJsonBody(new { id = 123, name = "John" })
```

Matchers are flexible:

```csharp
// Flexible - any integer ID is valid
.WithJsonBody(new { id = Match.Integer(), name = Match.NonEmptyString() })
```

## Using Matchers

### In Response Expectations

```csharp
var contract = Treaty.DefineContract("Users API")
    .ForEndpoint("/users/{id}")
        .WithMethod(HttpMethod.Get)
        .ExpectingResponse(r => r
            .WithStatus(200)
            .WithMatcherSchema(new
            {
                id = Match.Guid(),
                name = Match.NonEmptyString(),
                email = Match.Email(),
                age = Match.Integer(min: 0, max: 150),
                status = Match.OneOf("active", "inactive"),
                createdAt = Match.DateTime()
            }))
    .Build();
```

### In Request Expectations

```csharp
.ExpectingRequest(r => r
    .WithMatcherSchema(new
    {
        name = Match.NonEmptyString(),
        email = Match.Email()
    }))
```

## Available Matchers

### String Matchers

| Matcher | Description | Sample Generated |
|---------|-------------|------------------|
| `Match.String()` | Any string (including empty) | `"string"` |
| `Match.NonEmptyString()` | Non-null, non-empty string | `"string"` |
| `Match.Email()` | Valid email format | `"user@example.com"` |
| `Match.Uri()` | Valid absolute URI | `"https://example.com"` |
| `Match.Regex(pattern)` | Matches regex pattern | Pattern-based |

```csharp
// Examples
email = Match.Email()           // "user@example.com"
website = Match.Uri()           // "https://example.com"
phone = Match.Regex(@"^\d{3}-\d{4}$")  // Validates format
```

### Numeric Matchers

| Matcher | Description | Sample Generated |
|---------|-------------|------------------|
| `Match.Integer()` | Any integer | `1` |
| `Match.Integer(min, max)` | Integer in range | `min` value |
| `Match.Decimal()` | Any decimal/float | `1.0` |
| `Match.Decimal(min, max)` | Decimal in range | `min` value |

```csharp
// Examples
count = Match.Integer()              // Any integer
age = Match.Integer(min: 0, max: 150)  // 0-150
price = Match.Decimal(min: 0)        // >= 0
score = Match.Decimal(min: 0, max: 100)  // 0-100
```

### Type Matchers

| Matcher | Description | Sample Generated |
|---------|-------------|------------------|
| `Match.Guid()` | Valid GUID/UUID | Random GUID |
| `Match.Boolean()` | `true` or `false` | `true` |
| `Match.DateTime()` | ISO 8601 date-time | Current UTC |
| `Match.DateOnly()` | ISO 8601 date | Current date |
| `Match.TimeOnly()` | ISO 8601 time | Current time |
| `Match.Null()` | Only null | `null` |
| `Match.Any()` | Any value | `null` |

```csharp
// Examples
id = Match.Guid()               // "550e8400-e29b-41d4-a716-446655440000"
active = Match.Boolean()        // true
updatedAt = Match.DateTime()    // "2024-01-15T10:30:00Z"
birthDate = Match.DateOnly()    // "2024-01-15"
startTime = Match.TimeOnly()    // "10:30:00"
```

### Enum Matcher

```csharp
status = Match.OneOf("pending", "approved", "rejected")
```

Validates that the value is one of the specified options.

### Collection Matchers

| Matcher | Description |
|---------|-------------|
| `Match.EachLike(example)` | Array where each item matches example |
| `Match.EachLike(example, minCount)` | Array with minimum items |

```csharp
// Array of objects
items = Match.EachLike(new
{
    id = Match.Integer(),
    name = Match.NonEmptyString()
})

// Array with minimum 2 items
tags = Match.EachLike(Match.NonEmptyString(), minCount: 2)
```

### Object Matcher

```csharp
// Nested object
address = Match.Object(new
{
    street = Match.NonEmptyString(),
    city = Match.NonEmptyString(),
    zipCode = Match.Regex(@"^\d{5}$")
})
```

### Type Matcher

```csharp
// Match any value of a specific CLR type
value = Match.Type<int>()
data = Match.Type<DateTime>()

// Match based on example's type
sample = Match.Type(42)  // Matches any int
```

## Complete Example

```csharp
var contract = Treaty.DefineContract("E-Commerce API")
    .ForEndpoint("/orders/{id}")
        .WithMethod(HttpMethod.Get)
        .ExpectingResponse(r => r
            .WithStatus(200)
            .WithMatcherSchema(new
            {
                id = Match.Guid(),
                orderNumber = Match.Regex(@"^ORD-\d{8}$"),
                status = Match.OneOf("pending", "processing", "shipped", "delivered"),
                customer = Match.Object(new
                {
                    id = Match.Integer(),
                    name = Match.NonEmptyString(),
                    email = Match.Email()
                }),
                items = Match.EachLike(new
                {
                    productId = Match.Guid(),
                    name = Match.NonEmptyString(),
                    quantity = Match.Integer(min: 1),
                    unitPrice = Match.Decimal(min: 0)
                }),
                total = Match.Decimal(min: 0),
                createdAt = Match.DateTime(),
                shippedAt = Match.Any()  // Can be null or DateTime
            }))
    .Build();
```

## Matcher Schema Validation

When validation fails, Treaty provides detailed error messages:

```
Contract violation at GET /orders/123:

  1. InvalidFormat at `$.id`
     Expected: valid GUID
     Actual:   "not-a-guid"

  2. PatternMismatch at `$.orderNumber`
     Expected: pattern ^ORD-\d{8}$
     Actual:   "12345"

  3. InvalidEnumValue at `$.status`
     Expected: one of [pending, processing, shipped, delivered]
     Actual:   "unknown"

  4. OutOfRange at `$.items[0].quantity`
     Expected: >= 1
     Actual:   0
```

## Mock Server Response Generation

When using matchers with mock servers, Treaty generates sample values:

```csharp
var contract = Treaty.DefineContract("API")
    .ForEndpoint("/data")
        .ExpectingResponse(r => r
            .WithMatcherSchema(new
            {
                id = Match.Guid(),
                email = Match.Email(),
                count = Match.Integer()
            }))
    .Build();

var mockServer = Treaty.MockFromContract(contract).Build();
await mockServer.StartAsync();

// GET /data returns:
// {
//   "id": "550e8400-e29b-41d4-a716-446655440000",
//   "email": "user@example.com",
//   "count": 1
// }
```

## Combining with Type Schemas

You can mix matchers with strongly-typed schemas:

```csharp
// Define expected type
public record User(int Id, string Name, string Email);

// Use type for structure, matchers for flexibility
.ExpectingResponse(r => r
    .WithStatus(200)
    .WithJsonBody<User>())  // Validates structure from type
```

Or use matchers for specific fields while keeping type safety:

```csharp
.WithMatcherSchema(new
{
    id = Match.Integer(),
    name = Match.NonEmptyString(),
    email = Match.Email(),
    // Additional fields use matchers
    metadata = Match.Any()
})
```

## Best Practices

1. **Use appropriate matchers** - Don't use `Match.Any()` when a more specific matcher exists.

2. **Set constraints** - Use `min`/`max` parameters when values have known bounds.

3. **Validate formats** - Use `Match.Email()`, `Match.Guid()`, etc. for formatted strings.

4. **Use OneOf for enums** - Explicitly list valid enum values.

5. **Document patterns** - When using `Match.Regex()`, the pattern serves as documentation.

## Matcher Reference

| Matcher | Validates | Generated Sample |
|---------|-----------|------------------|
| `Match.Any()` | Any value | `null` |
| `Match.Boolean()` | Boolean | `true` |
| `Match.DateOnly()` | ISO date | Current date |
| `Match.DateTime()` | ISO date-time | Current UTC |
| `Match.Decimal(min?, max?)` | Decimal in range | `min` or `1.0` |
| `Match.EachLike(example, min?)` | Array of items | `[example]` |
| `Match.Email()` | Email format | `"user@example.com"` |
| `Match.Guid()` | GUID format | Random GUID |
| `Match.Integer(min?, max?)` | Integer in range | `min` or `1` |
| `Match.NonEmptyString()` | Non-empty string | `"string"` |
| `Match.Null()` | Null only | `null` |
| `Match.Object(schema)` | Nested object | Schema sample |
| `Match.OneOf(values)` | One of values | First value |
| `Match.Regex(pattern)` | Regex match | Pattern-based |
| `Match.String()` | Any string | `"string"` |
| `Match.TimeOnly()` | ISO time | Current time |
| `Match.Type<T>()` | Type match | Type default |
| `Match.Uri()` | URI format | `"https://example.com"` |

## Next Steps

- [Validation Modes](validation-modes.md) - Strict vs lenient validation
- [Getting Started](getting-started.md) - Basic Treaty usage
- [Provider Verification](provider-verification.md) - Testing your API
