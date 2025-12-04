using FluentAssertions;
using Treaty.Serialization;
using Treaty.Validation;

namespace Treaty.Tests.Unit.Validation;

public class TypeSchemaValidatorTests
{
    private readonly IJsonSerializer _serializer = new SystemTextJsonSerializer();

    [Test]
    public void Validate_ValidJson_ReturnsNoViolations()
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
    public void Validate_MissingRequiredField_ReturnsViolation()
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
    public void Validate_InvalidType_ReturnsViolation()
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
    public void Validate_InvalidGuidFormat_ReturnsViolation()
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
    public void Validate_NullValue_WhenNotNullable_ReturnsViolation()
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
    public void Validate_NestedObject_ValidatesCorrectly()
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
    public void Validate_Array_ValidatesItems()
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
    public void Validate_Array_InvalidItem_ReturnsViolation()
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
    public void Validate_InvalidJson_ReturnsViolation()
    {
        // Arrange
        var schema = _serializer.GetSchema<TestUser>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var json = """{ invalid json """;

        // Act
        var violations = validator.Validate(json, "GET /users/1");

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.InvalidType);
    }

    [Test]
    public void Validate_WrongRootType_Object_ExpectedArray_ReturnsViolation()
    {
        // Arrange
        var schema = _serializer.GetSchema<TestUser[]>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var json = """{"id": 1, "name": "John", "email": "john@example.com"}""";

        // Act
        var violations = validator.Validate(json, "GET /users");

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.InvalidType);
    }

    [Test]
    public void Validate_WrongRootType_Array_ExpectedObject_ReturnsViolation()
    {
        // Arrange
        var schema = _serializer.GetSchema<TestUser>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var json = """[{"id": 1, "name": "John", "email": "john@example.com"}]""";

        // Act
        var violations = validator.Validate(json, "GET /users/1");

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.InvalidType);
    }

    [Test]
    public void Validate_BooleanType_Valid_ReturnsNoViolations()
    {
        // Arrange
        var schema = _serializer.GetSchema<UserWithActive>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var json = """{"name": "John", "isActive": true}""";

        // Act
        var violations = validator.Validate(json, "GET /users/1");

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_BooleanType_InvalidType_ReturnsViolation()
    {
        // Arrange
        var schema = _serializer.GetSchema<UserWithActive>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var json = """{"name": "John", "isActive": "yes"}""";

        // Act
        var violations = validator.Validate(json, "GET /users/1");

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.InvalidType);
    }

    [Test]
    public void Validate_NumberType_Valid_ReturnsNoViolations()
    {
        // Arrange
        var schema = _serializer.GetSchema<UserWithBalance>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var json = """{"name": "John", "balance": 123.45}""";

        // Act
        var violations = validator.Validate(json, "GET /users/1");

        // Assert
        violations.Should().BeEmpty();
    }

    #region Format Validation Tests

    [Test]
    public void Validate_DateTimeFormat_Valid_NoViolations()
    {
        // Arrange
        var schema = _serializer.GetSchema<EventWithDateTime>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var json = """{"name": "Meeting", "timestamp": "2024-01-15T10:30:00Z"}""";

        // Act
        var violations = validator.Validate(json, "GET /events/1");

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_DateTimeFormat_Invalid_ReturnsViolation()
    {
        // Arrange
        var schema = _serializer.GetSchema<EventWithDateTime>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var json = """{"name": "Meeting", "timestamp": "not-a-datetime"}""";

        // Act
        var violations = validator.Validate(json, "GET /events/1");

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.InvalidFormat);
    }

    [Test]
    public void Validate_DateOnlyFormat_Valid_NoViolations()
    {
        // Arrange
        var schema = _serializer.GetSchema<EventWithDate>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var json = """{"name": "Birthday", "date": "2024-01-15"}""";

        // Act
        var violations = validator.Validate(json, "GET /events/1");

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_DateOnlyFormat_Invalid_ReturnsViolation()
    {
        // Arrange
        var schema = _serializer.GetSchema<EventWithDate>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var json = """{"name": "Birthday", "date": "not-a-date"}""";

        // Act
        var violations = validator.Validate(json, "GET /events/1");

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.InvalidFormat);
    }

    [Test]
    public void Validate_TimeOnlyFormat_Valid_NoViolations()
    {
        // Arrange
        var schema = _serializer.GetSchema<ScheduleWithTime>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var json = """{"name": "Standup", "startTime": "09:30:00"}""";

        // Act
        var violations = validator.Validate(json, "GET /schedules/1");

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_TimeOnlyFormat_Invalid_ReturnsViolation()
    {
        // Arrange
        var schema = _serializer.GetSchema<ScheduleWithTime>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var json = """{"name": "Standup", "startTime": "not-a-time"}""";

        // Act
        var violations = validator.Validate(json, "GET /schedules/1");

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.InvalidFormat);
    }

    [Test]
    public void Validate_UriFormat_Valid_NoViolations()
    {
        // Arrange
        var schema = _serializer.GetSchema<ResourceWithUri>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var json = """{"name": "Docs", "url": "https://example.com/docs"}""";

        // Act
        var violations = validator.Validate(json, "GET /resources/1");

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_UriFormat_Invalid_ReturnsViolation()
    {
        // Arrange
        var schema = _serializer.GetSchema<ResourceWithUri>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var json = """{"name": "Docs", "url": "not-a-valid-uri"}""";

        // Act
        var violations = validator.Validate(json, "GET /resources/1");

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.InvalidFormat);
    }

    [Test]
    public void Validate_GuidFormat_Valid_NoViolations()
    {
        // Arrange
        var schema = _serializer.GetSchema<UserWithGuid>();
        var validator = new TypeSchemaValidator(schema, _serializer);
        var json = """{"id": "550e8400-e29b-41d4-a716-446655440000"}""";

        // Act
        var violations = validator.Validate(json, "GET /users/1");

        // Assert
        violations.Should().BeEmpty();
    }

    #endregion

    private record TestUser(int Id, string Name, string Email);
    private record UserWithGuid(Guid Id);
    private record UserWithAddress(string Name, Address Address);
    private record Address(string Street, string City);
    private record UserWithActive(string Name, bool IsActive);
    private record UserWithBalance(string Name, decimal Balance);
    private record EventWithDateTime(string Name, DateTime Timestamp);
    private record EventWithDate(string Name, DateOnly Date);
    private record ScheduleWithTime(string Name, TimeOnly StartTime);
    private record ResourceWithUri(string Name, Uri Url);
}
