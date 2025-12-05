using FluentAssertions;
using Treaty.Contracts;
using Treaty.Matching;
using Treaty.Validation;

namespace Treaty.Tests.Unit.Matching;

public class MatcherSchemaValidatorTests
{
    private const string Endpoint = "GET /test";

    [Test]
    public void Validate_SimpleSchema_ValidJson_ReturnsNoViolations()
    {
        // Arrange
        var schema = MatcherSchema.FromObject(new
        {
            id = Match.Guid(),
            name = Match.NonEmptyString(),
            email = Match.Email()
        });
        var validator = new MatcherSchemaValidator(schema);
        var json = """
            {
                "id": "550e8400-e29b-41d4-a716-446655440000",
                "name": "John Doe",
                "email": "john@example.com"
            }
            """;

        // Act
        var violations = validator.Validate(json, Endpoint);

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_MissingRequiredProperty_ReturnsViolation()
    {
        // Arrange
        var schema = MatcherSchema.FromObject(new
        {
            id = Match.Guid(),
            name = Match.NonEmptyString()
        });
        var validator = new MatcherSchemaValidator(schema);
        var json = """{"id": "550e8400-e29b-41d4-a716-446655440000"}""";

        // Act
        var violations = validator.Validate(json, Endpoint);

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.MissingRequired);
    }

    [Test]
    public void Validate_InvalidGuid_ReturnsViolation()
    {
        // Arrange
        var schema = MatcherSchema.FromObject(new
        {
            id = Match.Guid()
        });
        var validator = new MatcherSchemaValidator(schema);
        var json = """{"id": "not-a-guid"}""";

        // Act
        var violations = validator.Validate(json, Endpoint);

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.InvalidFormat);
    }

    [Test]
    public void Validate_NestedObject_ValidJson_ReturnsNoViolations()
    {
        // Arrange
        var schema = MatcherSchema.FromObject(new
        {
            user = Match.Object(new
            {
                id = Match.Guid(),
                name = Match.NonEmptyString()
            })
        });
        var validator = new MatcherSchemaValidator(schema);
        var json = """
            {
                "user": {
                    "id": "550e8400-e29b-41d4-a716-446655440000",
                    "name": "John"
                }
            }
            """;

        // Act
        var violations = validator.Validate(json, Endpoint);

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_Array_ValidItems_ReturnsNoViolations()
    {
        // Arrange
        var schema = MatcherSchema.FromObject(new
        {
            items = Match.EachLike(new
            {
                id = Match.Integer(),
                name = Match.NonEmptyString()
            })
        });
        var validator = new MatcherSchemaValidator(schema);
        var json = """
            {
                "items": [
                    {"id": 1, "name": "Item 1"},
                    {"id": 2, "name": "Item 2"}
                ]
            }
            """;

        // Act
        var violations = validator.Validate(json, Endpoint);

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_Array_InvalidItem_ReturnsViolation()
    {
        // Arrange
        var schema = MatcherSchema.FromObject(new
        {
            items = Match.EachLike(new
            {
                id = Match.Integer(),
                name = Match.NonEmptyString()
            })
        });
        var validator = new MatcherSchemaValidator(schema);
        var json = """
            {
                "items": [
                    {"id": 1, "name": "Item 1"},
                    {"id": "not-a-number", "name": "Item 2"}
                ]
            }
            """;

        // Act
        var violations = validator.Validate(json, Endpoint);

        // Assert
        violations.Should().ContainSingle()
            .Which.Path.Should().Contain("[1]");
    }

    [Test]
    public void Validate_UnexpectedProperty_WithStrictMode_ReturnsViolation()
    {
        // Arrange
        var schema = MatcherSchema.FromObject(new
        {
            id = Match.Guid()
        });
        var validator = new MatcherSchemaValidator(schema);
        var json = """{"id": "550e8400-e29b-41d4-a716-446655440000", "unexpected": "value"}""";
        var strictConfig = new PartialValidationConfig([], false, null, strictMode: true);

        // Act
        var violations = validator.Validate(json, Endpoint, strictConfig);

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.UnexpectedField);
    }

    [Test]
    public void Validate_UnexpectedProperty_WithLenientMode_ReturnsNoViolation()
    {
        // Arrange - By default (lenient mode), extra fields are ignored for better forward compatibility
        var schema = MatcherSchema.FromObject(new
        {
            id = Match.Guid()
        });
        var validator = new MatcherSchemaValidator(schema);
        var json = """{"id": "550e8400-e29b-41d4-a716-446655440000", "unexpected": "value"}""";

        // Act
        var violations = validator.Validate(json, Endpoint);

        // Assert - No violations because extra fields are ignored by default
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_WithRangeConstraints_ValidValue_ReturnsNoViolations()
    {
        // Arrange
        var schema = MatcherSchema.FromObject(new
        {
            score = Match.Integer(min: 0, max: 100)
        });
        var validator = new MatcherSchemaValidator(schema);
        var json = """{"score": 50}""";

        // Act
        var violations = validator.Validate(json, Endpoint);

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_WithRangeConstraints_OutOfRange_ReturnsViolation()
    {
        // Arrange
        var schema = MatcherSchema.FromObject(new
        {
            score = Match.Integer(min: 0, max: 100)
        });
        var validator = new MatcherSchemaValidator(schema);
        var json = """{"score": 150}""";

        // Act
        var violations = validator.Validate(json, Endpoint);

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.OutOfRange);
    }

    [Test]
    public void Validate_RegexPattern_Valid_ReturnsNoViolations()
    {
        // Arrange
        var schema = MatcherSchema.FromObject(new
        {
            status = Match.Regex(@"^(active|inactive|pending)$")
        });
        var validator = new MatcherSchemaValidator(schema);
        var json = """{"status": "active"}""";

        // Act
        var violations = validator.Validate(json, Endpoint);

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_RegexPattern_Invalid_ReturnsViolation()
    {
        // Arrange
        var schema = MatcherSchema.FromObject(new
        {
            status = Match.Regex(@"^(active|inactive|pending)$")
        });
        var validator = new MatcherSchemaValidator(schema);
        var json = """{"status": "unknown"}""";

        // Act
        var violations = validator.Validate(json, Endpoint);

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.PatternMismatch);
    }

    [Test]
    public void Validate_OneOf_Valid_ReturnsNoViolations()
    {
        // Arrange
        var schema = MatcherSchema.FromObject(new
        {
            status = Match.OneOf("draft", "published", "archived")
        });
        var validator = new MatcherSchemaValidator(schema);
        var json = """{"status": "published"}""";

        // Act
        var violations = validator.Validate(json, Endpoint);

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_InvalidJson_ReturnsViolation()
    {
        // Arrange
        var schema = MatcherSchema.FromObject(new { id = Match.Guid() });
        var validator = new MatcherSchemaValidator(schema);
        var json = "{ invalid json";

        // Act
        var violations = validator.Validate(json, Endpoint);

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.InvalidFormat);
    }

    [Test]
    public void GenerateSample_SimpleSchema_ReturnsValidJson()
    {
        // Arrange
        var schema = MatcherSchema.FromObject(new
        {
            id = Match.Guid(),
            name = Match.NonEmptyString(),
            active = Match.Boolean()
        });
        var validator = new MatcherSchemaValidator(schema);

        // Act
        var sample = validator.GenerateSample();

        // Assert
        sample.Should().NotBeNullOrEmpty();
        sample.Should().Contain("id");
        sample.Should().Contain("name");
        sample.Should().Contain("active");
    }
}
