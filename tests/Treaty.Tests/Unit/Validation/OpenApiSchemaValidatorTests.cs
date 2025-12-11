using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.OpenApi;
using Treaty.Contracts;
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

    #region Sample Generation - Array Constraints

    [Test]
    public void GenerateSample_Array_RespectsMinItems()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            MinItems = 3,
            Items = new OpenApiSchema { Type = JsonSchemaType.String }
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);

        // Act
        var sampleJson = validator.GenerateSample();
        var array = JsonSerializer.Deserialize<JsonElement>(sampleJson);

        // Assert
        array.ValueKind.Should().Be(JsonValueKind.Array);
        array.GetArrayLength().Should().BeGreaterThanOrEqualTo(3);
    }

    [Test]
    public void GenerateSample_Array_RespectsMaxItems()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            MinItems = 5,
            MaxItems = 3, // maxItems < minItems edge case - should cap at maxItems
            Items = new OpenApiSchema { Type = JsonSchemaType.String }
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);

        // Act
        var sampleJson = validator.GenerateSample();
        var array = JsonSerializer.Deserialize<JsonElement>(sampleJson);

        // Assert
        array.ValueKind.Should().Be(JsonValueKind.Array);
        array.GetArrayLength().Should().BeLessThanOrEqualTo(3);
    }

    [Test]
    public void GenerateSample_Array_CapsAtReasonableSize()
    {
        // Arrange - minItems very large, should be capped
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            MinItems = 100,
            Items = new OpenApiSchema { Type = JsonSchemaType.String }
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);

        // Act
        var sampleJson = validator.GenerateSample();
        var array = JsonSerializer.Deserialize<JsonElement>(sampleJson);

        // Assert
        array.ValueKind.Should().Be(JsonValueKind.Array);
        array.GetArrayLength().Should().BeLessThanOrEqualTo(10); // Sanity cap
    }

    #endregion

    #region Sample Generation - Integer Constraints

    [Test]
    public void GenerateSample_Integer_RespectsMinimum()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Integer,
            Minimum = "10"
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);

        // Act
        var sampleJson = validator.GenerateSample();
        var value = JsonSerializer.Deserialize<long>(sampleJson);

        // Assert
        value.Should().BeGreaterThanOrEqualTo(10);
    }

    [Test]
    public void GenerateSample_Integer_RespectsMaximum()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Integer,
            Maximum = "5"
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);

        // Act
        var sampleJson = validator.GenerateSample();
        var value = JsonSerializer.Deserialize<long>(sampleJson);

        // Assert
        value.Should().BeLessThanOrEqualTo(5);
    }

    [Test]
    public void GenerateSample_Integer_RespectsExclusiveMinimum()
    {
        // Arrange - exclusiveMinimum means value must be > 10
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Integer,
            ExclusiveMinimum = "10"
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);

        // Act
        var sampleJson = validator.GenerateSample();
        var value = JsonSerializer.Deserialize<long>(sampleJson);

        // Assert
        value.Should().BeGreaterThan(10);
    }

    [Test]
    public void GenerateSample_Integer_RespectsExclusiveMaximum()
    {
        // Arrange - exclusiveMaximum means value must be < 10
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Integer,
            ExclusiveMaximum = "10"
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);

        // Act
        var sampleJson = validator.GenerateSample();
        var value = JsonSerializer.Deserialize<long>(sampleJson);

        // Assert
        value.Should().BeLessThan(10);
    }

    [Test]
    public void GenerateSample_Integer_HandlesBothBounds()
    {
        // Arrange - value must be >= 5 and < 10
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Integer,
            Minimum = "5",
            ExclusiveMaximum = "10"
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);

        // Act
        var sampleJson = validator.GenerateSample();
        var value = JsonSerializer.Deserialize<long>(sampleJson);

        // Assert
        value.Should().BeGreaterThanOrEqualTo(5);
        value.Should().BeLessThan(10);
    }

    #endregion

    #region Sample Generation - Number Constraints

    [Test]
    public void GenerateSample_Number_RespectsExclusiveBounds()
    {
        // Arrange - value must be > 0 and < 100
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Number,
            ExclusiveMinimum = "0",
            ExclusiveMaximum = "100"
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);

        // Act
        var sampleJson = validator.GenerateSample();
        var value = JsonSerializer.Deserialize<decimal>(sampleJson);

        // Assert
        value.Should().BeGreaterThan(0);
        value.Should().BeLessThan(100);
    }

    #endregion

    #region Sample Generation - String Constraints

    [Test]
    public void GenerateSample_String_RespectsMinLength()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            MinLength = 10
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);

        // Act
        var sampleJson = validator.GenerateSample();
        var value = JsonSerializer.Deserialize<string>(sampleJson);

        // Assert
        value.Should().NotBeNull();
        value!.Length.Should().BeGreaterThanOrEqualTo(10);
    }

    [Test]
    public void GenerateSample_String_RespectsMaxLength()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            MaxLength = 3
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);

        // Act
        var sampleJson = validator.GenerateSample();
        var value = JsonSerializer.Deserialize<string>(sampleJson);

        // Assert
        value.Should().NotBeNull();
        value!.Length.Should().BeLessThanOrEqualTo(3);
    }

    [Test]
    public void GenerateSample_String_RespectsMinAndMaxLength()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            MinLength = 5,
            MaxLength = 8
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);

        // Act
        var sampleJson = validator.GenerateSample();
        var value = JsonSerializer.Deserialize<string>(sampleJson);

        // Assert
        value.Should().NotBeNull();
        value!.Length.Should().BeGreaterThanOrEqualTo(5);
        value.Length.Should().BeLessThanOrEqualTo(8);
    }

    [Test]
    public void GenerateSample_String_FormatWithLengthConstraints()
    {
        // Arrange - email format with maxLength should truncate if needed
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Format = "email",
            MaxLength = 10
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);

        // Act
        var sampleJson = validator.GenerateSample();
        var value = JsonSerializer.Deserialize<string>(sampleJson);

        // Assert
        value.Should().NotBeNull();
        value!.Length.Should().BeLessThanOrEqualTo(10);
    }

    [Test]
    public void GenerateSample_String_SimplePatternGeneration()
    {
        // Arrange - simple uppercase pattern
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Pattern = @"^[A-Z]+$"
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);

        // Act
        var sampleJson = validator.GenerateSample();
        var value = JsonSerializer.Deserialize<string>(sampleJson);

        // Assert
        value.Should().NotBeNull();
        value.Should().MatchRegex(@"^[A-Z]+$");
    }

    #endregion

    #region MultipleOf Validation

    [Test]
    public void Validate_MultipleOf_Valid_NoViolations()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Integer,
            MultipleOf = 5
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "15";

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_MultipleOf_Invalid_ReturnsViolation()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Integer,
            MultipleOf = 5
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "13";

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.OutOfRange);
    }

    [Test]
    public void Validate_MultipleOf_Number_Valid_NoViolations()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Number,
            MultipleOf = 0.5m
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "2.5";

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void GenerateSample_Integer_RespectsMultipleOf()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Integer,
            Minimum = "10",
            MultipleOf = 7
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);

        // Act
        var sampleJson = validator.GenerateSample();
        var value = JsonSerializer.Deserialize<long>(sampleJson);

        // Assert
        value.Should().BeGreaterThanOrEqualTo(10);
        (value % 7).Should().Be(0);
    }

    [Test]
    public void GenerateSample_Number_RespectsMultipleOf()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Number,
            Minimum = "1",
            MultipleOf = 0.5m
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);

        // Act
        var sampleJson = validator.GenerateSample();
        var value = JsonSerializer.Deserialize<decimal>(sampleJson);

        // Assert
        value.Should().BeGreaterThanOrEqualTo(1);
        (value % 0.5m).Should().Be(0);
    }

    #endregion

    #region Default Value Generation

    [Test]
    public void GenerateSample_UsesDefaultValue_WhenNoExampleOrEnum()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Default = JsonValue.Create("pending")
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);

        // Act
        var sampleJson = validator.GenerateSample();
        var value = JsonSerializer.Deserialize<string>(sampleJson);

        // Assert
        value.Should().Be("pending");
    }

    [Test]
    public void GenerateSample_PrefersExample_OverDefault()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Example = JsonValue.Create("active"),
            Default = JsonValue.Create("pending")
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);

        // Act
        var sampleJson = validator.GenerateSample();
        var value = JsonSerializer.Deserialize<string>(sampleJson);

        // Assert
        value.Should().Be("active");
    }

    [Test]
    public void GenerateSample_PrefersEnum_OverDefault()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Enum = new List<JsonNode?> { JsonValue.Create("enumValue") },
            Default = JsonValue.Create("defaultValue")
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);

        // Act
        var sampleJson = validator.GenerateSample();
        var value = JsonSerializer.Deserialize<string>(sampleJson);

        // Assert
        value.Should().Be("enumValue");
    }

    #endregion

    #region UniqueItems Validation

    [Test]
    public void Validate_UniqueItems_Valid_NoViolations()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            UniqueItems = true,
            Items = new OpenApiSchema { Type = JsonSchemaType.Integer }
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "[1, 2, 3]";

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_UniqueItems_HasDuplicates_ReturnsViolation()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            UniqueItems = true,
            Items = new OpenApiSchema { Type = JsonSchemaType.Integer }
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "[1, 2, 1]";

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.OutOfRange);
    }

    [Test]
    public void Validate_UniqueItems_False_AllowsDuplicates()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            UniqueItems = false,
            Items = new OpenApiSchema { Type = JsonSchemaType.Integer }
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "[1, 2, 1]";

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void GenerateSample_UniqueItems_GeneratesDistinctValues()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            MinItems = 3,
            UniqueItems = true,
            Items = new OpenApiSchema { Type = JsonSchemaType.Integer }
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);

        // Act
        var sampleJson = validator.GenerateSample();
        var array = JsonSerializer.Deserialize<int[]>(sampleJson);

        // Assert
        array.Should().NotBeNull();
        array.Should().OnlyHaveUniqueItems();
    }

    #endregion

    #region Const Validation

    [Test]
    public void Validate_Const_Valid_NoViolations()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Const = "fixed-value"
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "\"fixed-value\"";

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_Const_Invalid_ReturnsViolation()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Const = "fixed-value"
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "\"other-value\"";

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.InvalidEnumValue);
    }

    [Test]
    public void GenerateSample_ReturnsConstValue()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Const = "fixed-value",
            Example = JsonValue.Create("ignored-example")
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);

        // Act
        var sampleJson = validator.GenerateSample();
        var value = JsonSerializer.Deserialize<string>(sampleJson);

        // Assert
        value.Should().Be("fixed-value");
    }

    #endregion

    #region ReadOnly/WriteOnly Validation

    [Test]
    public void Validate_ReadOnlyField_InRequest_ReturnsViolation()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                { "id", new OpenApiSchema { Type = JsonSchemaType.Integer, ReadOnly = true } },
                { "name", new OpenApiSchema { Type = JsonSchemaType.String } }
            }
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = """{"id": 123, "name": "test"}""";
        var config = new PartialValidationConfig([], false, ValidationDirection.Request);

        // Act
        var violations = validator.Validate(json, "POST /users", config);

        // Assert
        violations.Should().ContainSingle()
            .Which.Message.Should().Contain("ReadOnly");
    }

    [Test]
    public void Validate_ReadOnlyField_InResponse_NoViolations()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                { "id", new OpenApiSchema { Type = JsonSchemaType.Integer, ReadOnly = true } },
                { "name", new OpenApiSchema { Type = JsonSchemaType.String } }
            }
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = """{"id": 123, "name": "test"}""";
        var config = new PartialValidationConfig([], false, ValidationDirection.Response);

        // Act
        var violations = validator.Validate(json, "GET /users/123", config);

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_WriteOnlyField_InResponse_ReturnsViolation()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                { "password", new OpenApiSchema { Type = JsonSchemaType.String, WriteOnly = true } },
                { "username", new OpenApiSchema { Type = JsonSchemaType.String } }
            }
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = """{"password": "secret", "username": "john"}""";
        var config = new PartialValidationConfig([], false, ValidationDirection.Response);

        // Act
        var violations = validator.Validate(json, "GET /users/1", config);

        // Assert
        violations.Should().ContainSingle()
            .Which.Message.Should().Contain("WriteOnly");
    }

    [Test]
    public void Validate_WriteOnlyField_InRequest_NoViolations()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                { "password", new OpenApiSchema { Type = JsonSchemaType.String, WriteOnly = true } },
                { "username", new OpenApiSchema { Type = JsonSchemaType.String } }
            }
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = """{"password": "secret", "username": "john"}""";
        var config = new PartialValidationConfig([], false, ValidationDirection.Request);

        // Act
        var violations = validator.Validate(json, "POST /users", config);

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_DirectionBoth_IgnoresReadOnlyWriteOnly()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                { "id", new OpenApiSchema { Type = JsonSchemaType.Integer, ReadOnly = true } },
                { "password", new OpenApiSchema { Type = JsonSchemaType.String, WriteOnly = true } }
            }
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = """{"id": 123, "password": "secret"}""";
        var config = new PartialValidationConfig([], false, ValidationDirection.Both);

        // Act
        var violations = validator.Validate(json, "GET /test", config);

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void Validate_RequiredReadOnlyField_InRequest_NotRequired()
    {
        // Arrange - readOnly fields shouldn't be required in requests
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                { "id", new OpenApiSchema { Type = JsonSchemaType.Integer, ReadOnly = true } },
                { "name", new OpenApiSchema { Type = JsonSchemaType.String } }
            },
            Required = new HashSet<string> { "id", "name" }
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = """{"name": "test"}""";  // id is missing but it's readOnly
        var config = new PartialValidationConfig([], false, ValidationDirection.Request);

        // Act
        var violations = validator.Validate(json, "POST /users", config);

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public void GenerateSample_Request_ExcludesReadOnlyFields()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                { "id", new OpenApiSchema { Type = JsonSchemaType.Integer, ReadOnly = true } },
                { "name", new OpenApiSchema { Type = JsonSchemaType.String } }
            }
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);

        // Act
        var sampleJson = validator.GenerateSample(ValidationDirection.Request);
        var obj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(sampleJson);

        // Assert
        obj.Should().NotBeNull();
        obj.Should().NotContainKey("id");
        obj.Should().ContainKey("name");
    }

    [Test]
    public void GenerateSample_Response_ExcludesWriteOnlyFields()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                { "password", new OpenApiSchema { Type = JsonSchemaType.String, WriteOnly = true } },
                { "username", new OpenApiSchema { Type = JsonSchemaType.String } }
            }
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);

        // Act
        var sampleJson = validator.GenerateSample(ValidationDirection.Response);
        var obj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(sampleJson);

        // Assert
        obj.Should().NotBeNull();
        obj.Should().NotContainKey("password");
        obj.Should().ContainKey("username");
    }

    [Test]
    public void GenerateSample_Both_IncludesAllFields()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                { "id", new OpenApiSchema { Type = JsonSchemaType.Integer, ReadOnly = true } },
                { "password", new OpenApiSchema { Type = JsonSchemaType.String, WriteOnly = true } },
                { "name", new OpenApiSchema { Type = JsonSchemaType.String } }
            }
        };
        var validator = new OpenApiSchemaValidator(schema, _serializer);

        // Act
        var sampleJson = validator.GenerateSample(ValidationDirection.Both);
        var obj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(sampleJson);

        // Assert
        obj.Should().NotBeNull();
        obj.Should().ContainKey("id");
        obj.Should().ContainKey("password");
        obj.Should().ContainKey("name");
    }

    #endregion
}
