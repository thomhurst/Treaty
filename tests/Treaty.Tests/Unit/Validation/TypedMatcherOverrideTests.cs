using FluentAssertions;
using Treaty.Contracts;
using Treaty.Matching;
using Treaty.Serialization;
using Treaty.Validation;

namespace Treaty.Tests.Unit.Validation;

public class TypedMatcherOverrideTests
{
    private readonly IJsonSerializer _serializer = new SystemTextJsonSerializer();

    [Test]
    public void Validate_WithMatcherOverride_UsesMatcherInsteadOfTypeValidation()
    {
        // Arrange - User has string Id but we want GUID validation
        var schema = _serializer.GetSchema<UserWithStringId>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var partialValidation = new PartialValidationConfig(
            [],
            false,
            new MatcherValidationConfig(new Dictionary<string, IMatcher>
            {
                ["Id"] = Match.Guid()
            }));

        var json = """{"id": "550e8400-e29b-41d4-a716-446655440000", "name": "John"}""";

        // Act
        var violations = validator.Validate(json, "GET /users/1", partialValidation);

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_WithMatcherOverride_InvalidGuid_ReturnsViolation()
    {
        // Arrange
        var schema = _serializer.GetSchema<UserWithStringId>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var partialValidation = new PartialValidationConfig(
            [],
            false,
            new MatcherValidationConfig(new Dictionary<string, IMatcher>
            {
                ["Id"] = Match.Guid()
            }));

        var json = """{"id": "not-a-guid", "name": "John"}""";

        // Act
        var violations = validator.Validate(json, "GET /users/1", partialValidation);

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.InvalidFormat);
    }

    [Test]
    public void Validate_WithMultipleMatcherOverrides_ValidatesAllWithMatchers()
    {
        // Arrange
        var schema = _serializer.GetSchema<UserWithTimestamps>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var partialValidation = new PartialValidationConfig(
            [],
            false,
            new MatcherValidationConfig(new Dictionary<string, IMatcher>
            {
                ["Id"] = Match.Guid(),
                ["CreatedAt"] = Match.DateTime(),
                ["Email"] = Match.Email()
            }));

        var json = """
            {
                "id": "550e8400-e29b-41d4-a716-446655440000",
                "name": "John",
                "email": "john@example.com",
                "createdAt": "2024-01-15T10:30:00Z"
            }
            """;

        // Act
        var violations = validator.Validate(json, "GET /users/1", partialValidation);

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_WithMatcherOverride_InvalidEmail_ReturnsViolation()
    {
        // Arrange
        var schema = _serializer.GetSchema<UserWithTimestamps>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var partialValidation = new PartialValidationConfig(
            [],
            false,
            new MatcherValidationConfig(new Dictionary<string, IMatcher>
            {
                ["Email"] = Match.Email()
            }));

        var json = """{"id": "123", "name": "John", "email": "not-an-email", "createdAt": "2024-01-15"}""";

        // Act
        var violations = validator.Validate(json, "GET /users/1", partialValidation);

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.InvalidFormat);
    }

    [Test]
    public void Validate_WithMatcherOverride_OneOfValues_Valid()
    {
        // Arrange
        var schema = _serializer.GetSchema<UserWithStatus>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var partialValidation = new PartialValidationConfig(
            [],
            false,
            new MatcherValidationConfig(new Dictionary<string, IMatcher>
            {
                ["Status"] = Match.OneOf("active", "inactive", "pending")
            }));

        var json = """{"name": "John", "status": "active"}""";

        // Act
        var violations = validator.Validate(json, "GET /users/1", partialValidation);

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_WithMatcherOverride_OneOfValues_Invalid()
    {
        // Arrange
        var schema = _serializer.GetSchema<UserWithStatus>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var partialValidation = new PartialValidationConfig(
            [],
            false,
            new MatcherValidationConfig(new Dictionary<string, IMatcher>
            {
                ["Status"] = Match.OneOf("active", "inactive", "pending")
            }));

        var json = """{"name": "John", "status": "unknown"}""";

        // Act
        var violations = validator.Validate(json, "GET /users/1", partialValidation);

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.InvalidEnumValue);
    }

    [Test]
    public void Validate_WithMatcherOverride_IntegerRange_Valid()
    {
        // Arrange
        var schema = _serializer.GetSchema<UserWithScore>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var partialValidation = new PartialValidationConfig(
            [],
            false,
            new MatcherValidationConfig(new Dictionary<string, IMatcher>
            {
                ["Score"] = Match.Integer(min: 0, max: 100)
            }));

        var json = """{"name": "John", "score": 75}""";

        // Act
        var violations = validator.Validate(json, "GET /users/1", partialValidation);

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_WithMatcherOverride_IntegerRange_OutOfRange()
    {
        // Arrange
        var schema = _serializer.GetSchema<UserWithScore>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var partialValidation = new PartialValidationConfig(
            [],
            false,
            new MatcherValidationConfig(new Dictionary<string, IMatcher>
            {
                ["Score"] = Match.Integer(min: 0, max: 100)
            }));

        var json = """{"name": "John", "score": 150}""";

        // Act
        var violations = validator.Validate(json, "GET /users/1", partialValidation);

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.OutOfRange);
    }

    [Test]
    public void Validate_WithMatcherOverride_RegexPattern_Valid()
    {
        // Arrange
        var schema = _serializer.GetSchema<ProductWithSku>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var partialValidation = new PartialValidationConfig(
            [],
            false,
            new MatcherValidationConfig(new Dictionary<string, IMatcher>
            {
                ["Sku"] = Match.Regex(@"^[A-Z]{3}-\d{5}$")
            }));

        var json = """{"name": "Widget", "sku": "ABC-12345"}""";

        // Act
        var violations = validator.Validate(json, "GET /products/1", partialValidation);

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_WithMatcherOverride_RegexPattern_Invalid()
    {
        // Arrange
        var schema = _serializer.GetSchema<ProductWithSku>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var partialValidation = new PartialValidationConfig(
            [],
            false,
            new MatcherValidationConfig(new Dictionary<string, IMatcher>
            {
                ["Sku"] = Match.Regex(@"^[A-Z]{3}-\d{5}$")
            }));

        var json = """{"name": "Widget", "sku": "invalid-sku"}""";

        // Act
        var violations = validator.Validate(json, "GET /products/1", partialValidation);

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.PatternMismatch);
    }

    [Test]
    public void Validate_MixedValidation_MatcherOverrideAndTypeValidation()
    {
        // Arrange - Override Id with matcher, but keep Name as type validation
        var schema = _serializer.GetSchema<UserWithStringId>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var partialValidation = new PartialValidationConfig(
            [],
            false,
            new MatcherValidationConfig(new Dictionary<string, IMatcher>
            {
                ["Id"] = Match.Guid()
            }));

        // Id is valid GUID, Name is valid string
        var json = """{"id": "550e8400-e29b-41d4-a716-446655440000", "name": "John"}""";

        // Act
        var violations = validator.Validate(json, "GET /users/1", partialValidation);

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_MixedValidation_TypeValidationStillApplies()
    {
        // Arrange - Override Id with matcher, but Name still uses type validation
        var schema = _serializer.GetSchema<UserWithStringId>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var partialValidation = new PartialValidationConfig(
            [],
            false,
            new MatcherValidationConfig(new Dictionary<string, IMatcher>
            {
                ["Id"] = Match.Guid()
            }));

        // Id is valid GUID, but Name is wrong type (number instead of string)
        var json = """{"id": "550e8400-e29b-41d4-a716-446655440000", "name": 123}""";

        // Act
        var violations = validator.Validate(json, "GET /users/1", partialValidation);

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.InvalidType);
    }

    [Test]
    public void Validate_NoMatcherConfig_UsesTypeValidation()
    {
        // Arrange - No matcher overrides, should use normal type validation
        var schema = _serializer.GetSchema<UserWithStringId>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var partialValidation = new PartialValidationConfig([], false, null);

        var json = """{"id": "any-string-value", "name": "John"}""";

        // Act
        var violations = validator.Validate(json, "GET /users/1", partialValidation);

        // Assert
        violations.Should().BeEmpty(); // String type validation passes
    }

    // Test models
    private record UserWithStringId(string Id, string Name);
    private record UserWithTimestamps(string Id, string Name, string Email, string CreatedAt);
    private record UserWithStatus(string Name, string Status);
    private record UserWithScore(string Name, int Score);
    private record ProductWithSku(string Name, string Sku);
}
