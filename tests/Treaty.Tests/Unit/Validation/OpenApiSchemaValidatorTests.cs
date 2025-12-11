using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.OpenApi;
using Treaty.OpenApi;
using Treaty.Serialization;
using Treaty.Validation;

namespace Treaty.Tests.Unit.Validation;

public class OpenApiSchemaValidatorTests
{
    private readonly IJsonSerializer _serializer = new SystemTextJsonSerializer();

    #region Enum Validation

    [Test]
    public void Validate_EnumValue_Valid_NoViolations()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Enum = new List<JsonNode?>
            {
                JsonValue.Create("active"),
                JsonValue.Create("inactive"),
                JsonValue.Create("pending")
            }
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "\"active\"";

        // Act
        var violations = validator.Validate(json, "GET /status");

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_EnumValue_Invalid_ReturnsViolation()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Enum = new List<JsonNode?>
            {
                JsonValue.Create("active"),
                JsonValue.Create("inactive")
            }
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "\"unknown\"";

        // Act
        var violations = validator.Validate(json, "GET /status");

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.InvalidEnumValue);
    }

    #endregion

    #region String Length Validation

    [Test]
    public void Validate_MinLength_Valid_NoViolations()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            MinLength = 3
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "\"hello\"";

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_MinLength_TooShort_ReturnsViolation()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            MinLength = 5
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "\"hi\"";

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.OutOfRange);
    }

    [Test]
    public void Validate_MaxLength_Valid_NoViolations()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            MaxLength = 10
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "\"hello\"";

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_MaxLength_TooLong_ReturnsViolation()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            MaxLength = 3
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "\"hello\"";

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.OutOfRange);
    }

    #endregion

    #region Pattern Validation

    [Test]
    public void Validate_Pattern_Matches_NoViolations()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Pattern = @"^[A-Z]{2,3}-\d{4}$"  // e.g., AB-1234 or ABC-5678
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "\"AB-1234\"";

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_Pattern_NoMatch_ReturnsViolation()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Pattern = @"^[A-Z]{2,3}-\d{4}$"
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "\"invalid\"";

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.PatternMismatch);
    }

    #endregion

    #region Numeric Constraints Validation

    [Test]
    public void Validate_MinValue_Valid_NoViolations()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Integer,
            Minimum = "0"
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "5";

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_MinValue_BelowMin_ReturnsViolation()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Integer,
            Minimum = "0"
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "-5";

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.OutOfRange);
    }

    [Test]
    public void Validate_MaxValue_Valid_NoViolations()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Integer,
            Maximum = "100"
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "50";

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_MaxValue_AboveMax_ReturnsViolation()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Integer,
            Maximum = "100"
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "150";

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.OutOfRange);
    }

    [Test]
    public void Validate_ExclusiveMin_AtBoundary_ReturnsViolation()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Number,
            ExclusiveMinimum = "0"
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "0";

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.OutOfRange);
    }

    [Test]
    public void Validate_ExclusiveMax_AtBoundary_ReturnsViolation()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Number,
            ExclusiveMaximum = "100"
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "100";

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.OutOfRange);
    }

    #endregion

    #region Array Constraints Validation

    [Test]
    public void Validate_MinItems_Valid_NoViolations()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            MinItems = 1,
            Items = new OpenApiSchema { Type = JsonSchemaType.String }
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "[\"item1\", \"item2\"]";

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_MinItems_TooFew_ReturnsViolation()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            MinItems = 2,
            Items = new OpenApiSchema { Type = JsonSchemaType.String }
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "[\"item1\"]";

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.OutOfRange);
    }

    [Test]
    public void Validate_MaxItems_Valid_NoViolations()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            MaxItems = 5,
            Items = new OpenApiSchema { Type = JsonSchemaType.String }
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "[\"item1\", \"item2\"]";

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_MaxItems_TooMany_ReturnsViolation()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            MaxItems = 2,
            Items = new OpenApiSchema { Type = JsonSchemaType.String }
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "[\"item1\", \"item2\", \"item3\"]";

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.OutOfRange);
    }

    #endregion

    #region AnyOf/OneOf/AllOf Validation

    [Test]
    public void Validate_AnyOf_MatchesOne_NoViolations()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            AnyOf = new List<IOpenApiSchema>
            {
                new OpenApiSchema { Type = JsonSchemaType.String },
                new OpenApiSchema { Type = JsonSchemaType.Integer }
            }
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "\"hello\"";

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_AnyOf_MatchesNone_ReturnsViolation()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            AnyOf = new List<IOpenApiSchema>
            {
                new OpenApiSchema { Type = JsonSchemaType.String },
                new OpenApiSchema { Type = JsonSchemaType.Integer }
            }
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "true";  // Boolean doesn't match string or integer

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.InvalidType);
    }

    [Test]
    public void Validate_AllOf_AllMatch_NoViolations()
    {
        // Arrange - object must have both 'id' and 'name'
        var schema = new OpenApiSchema
        {
            AllOf = new List<IOpenApiSchema>
            {
                new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        { "id", new OpenApiSchema { Type = JsonSchemaType.Integer } }
                    },
                    Required = new HashSet<string> { "id" }
                },
                new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        { "name", new OpenApiSchema { Type = JsonSchemaType.String } }
                    },
                    Required = new HashSet<string> { "name" }
                }
            }
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = """{"id": 1, "name": "John"}""";

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_AllOf_OneFails_ReturnsViolation()
    {
        // Arrange - object must have both 'id' and 'name'
        var schema = new OpenApiSchema
        {
            AllOf = new List<IOpenApiSchema>
            {
                new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        { "id", new OpenApiSchema { Type = JsonSchemaType.Integer } }
                    },
                    Required = new HashSet<string> { "id" }
                },
                new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        { "name", new OpenApiSchema { Type = JsonSchemaType.String } }
                    },
                    Required = new HashSet<string> { "name" }
                }
            }
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = """{"id": 1}""";  // Missing 'name'

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        violations.Should().NotBeEmpty();
        violations.Should().Contain(v => v.Type == ViolationType.MissingRequired);
    }

    #endregion

    #region Format Validation

    [Test]
    public void Validate_Format_Email_Valid_NoViolations()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Format = "email"
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "\"test@example.com\"";

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_Format_Email_Invalid_ReturnsViolation()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Format = "email"
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "\"not-an-email\"";

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.InvalidFormat);
    }

    [Test]
    public void Validate_Format_Uuid_Valid_NoViolations()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Format = "uuid"
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "\"550e8400-e29b-41d4-a716-446655440000\"";

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_Format_Uuid_Invalid_ReturnsViolation()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Format = "uuid"
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "\"not-a-uuid\"";

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.InvalidFormat);
    }

    #endregion
}
