using FluentAssertions;
using Treaty.Serialization;
using Treaty.Validation;

namespace Treaty.Tests;

public class ValidationTests
{
    private readonly IJsonSerializer _serializer = new SystemTextJsonSerializer();

    [Test]
    public void TypeSchemaValidator_ValidJson_ReturnsNoViolations()
    {
        // Arrange
        var schema = _serializer.GetSchema<TestUser>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var json = """{"id": 1, "name": "John", "email": "john@example.com"}""";

        // Act
        var violations = validator.Validate(json, "GET /users/1");

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void TypeSchemaValidator_MissingRequiredField_ReturnsViolation()
    {
        // Arrange
        var schema = _serializer.GetSchema<TestUser>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var json = """{"id": 1, "name": "John"}""";

        // Act
        var violations = validator.Validate(json, "GET /users/1");

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.MissingRequired);
    }

    [Test]
    public void TypeSchemaValidator_InvalidType_ReturnsViolation()
    {
        // Arrange
        var schema = _serializer.GetSchema<TestUser>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var json = """{"id": "not-a-number", "name": "John", "email": "john@example.com"}""";

        // Act
        var violations = validator.Validate(json, "GET /users/1");

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.InvalidType);
    }

    [Test]
    public void TypeSchemaValidator_InvalidGuidFormat_ReturnsViolation()
    {
        // Arrange - Guid types get uuid format validation
        var schema = _serializer.GetSchema<UserWithGuid>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var json = """{"id": "not-a-guid"}""";

        // Act
        var violations = validator.Validate(json, "GET /users/1");

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.InvalidFormat);
    }

    [Test]
    public void TypeSchemaValidator_NullValue_WhenNotNullable_ReturnsViolation()
    {
        // Arrange
        var schema = _serializer.GetSchema<TestUser>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var json = """{"id": 1, "name": null, "email": "john@example.com"}""";

        // Act
        var violations = validator.Validate(json, "GET /users/1");

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.UnexpectedNull);
    }

    [Test]
    public void TypeSchemaValidator_NestedObject_ValidatesCorrectly()
    {
        // Arrange
        var schema = _serializer.GetSchema<UserWithAddress>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var json = """
            {
                "name": "John",
                "address": {
                    "street": "123 Main St",
                    "city": "Boston"
                }
            }
            """;

        // Act
        var violations = validator.Validate(json, "GET /users/1");

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void TypeSchemaValidator_Array_ValidatesItems()
    {
        // Arrange
        var schema = _serializer.GetSchema<TestUser[]>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var json = """
            [
                {"id": 1, "name": "John", "email": "john@example.com"},
                {"id": 2, "name": "Jane", "email": "jane@example.com"}
            ]
            """;

        // Act
        var violations = validator.Validate(json, "GET /users");

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void TypeSchemaValidator_Array_InvalidItem_ReturnsViolation()
    {
        // Arrange
        var schema = _serializer.GetSchema<TestUser[]>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var json = """
            [
                {"id": 1, "name": "John", "email": "john@example.com"},
                {"id": "not-a-number", "name": "Jane", "email": "jane@example.com"}
            ]
            """;

        // Act
        var violations = validator.Validate(json, "GET /users");

        // Assert
        violations.Should().ContainSingle()
            .Which.Path.Should().Contain("[1]");
    }

    [Test]
    public void ContractViolationException_FormatsMessage_WithAllViolations()
    {
        // Arrange
        var violations = new[]
        {
            new ContractViolation("GET /users/1", "$.email", "Field 'email' expected format 'email'", ViolationType.InvalidFormat, "email", "not-an-email"),
            new ContractViolation("GET /users/1", "$.age", "Field 'age' expected minimum 0", ViolationType.OutOfRange, "0", "-5")
        };

        // Act
        var exception = new ContractViolationException(violations);

        // Assert
        exception.Message.Should().Contain("GET /users/1");
        exception.Message.Should().Contain("email");
        exception.Message.Should().Contain("age");
        exception.Violations.Should().HaveCount(2);
    }

    [Test]
    public void ValidationResult_Success_IsValid()
    {
        // Arrange & Act
        var result = ValidationResult.Success("GET /users/1");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Violations.Should().BeEmpty();
    }

    [Test]
    public void ValidationResult_Failure_IsNotValid()
    {
        // Arrange
        var violation = new ContractViolation("GET /users/1", "$", "Test violation", ViolationType.InvalidType);

        // Act
        var result = ValidationResult.Failure("GET /users/1", violation);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violations.Should().ContainSingle();
    }

    [Test]
    public void ValidationResult_ThrowIfInvalid_ThrowsWhenInvalid()
    {
        // Arrange
        var violation = new ContractViolation("GET /users/1", "$", "Test violation", ViolationType.InvalidType);
        var result = ValidationResult.Failure("GET /users/1", violation);

        // Act & Assert
        var action = () => result.ThrowIfInvalid();
        action.Should().Throw<ContractViolationException>();
    }

    private record TestUser(int Id, string Name, string Email);

    private record UserWithGuid(Guid Id);

    private record UserWithAddress(string Name, Address Address);

    private record Address(string Street, string City);
}
