using Microsoft.OpenApi.Models;
using Treaty.OpenApi;
using Treaty.Serialization;
using Treaty.Validation;

namespace Treaty.Tests.Unit.Validation;

public class DiscriminatorValidationTests
{
    private readonly IJsonSerializer _serializer = new SystemTextJsonSerializer();

    private OpenApiSchema CreatePetSchema()
    {
        var catSchema = new OpenApiSchema
        {
            Type = "object",
            Properties = new Dictionary<string, OpenApiSchema>
            {
                { "petType", new OpenApiSchema { Type = "string" } },
                { "meowVolume", new OpenApiSchema { Type = "integer" } }
            },
            Required = new HashSet<string> { "petType", "meowVolume" },
            Reference = new OpenApiReference { Id = "Cat" }
        };

        var dogSchema = new OpenApiSchema
        {
            Type = "object",
            Properties = new Dictionary<string, OpenApiSchema>
            {
                { "petType", new OpenApiSchema { Type = "string" } },
                { "barkVolume", new OpenApiSchema { Type = "integer" } }
            },
            Required = new HashSet<string> { "petType", "barkVolume" },
            Reference = new OpenApiReference { Id = "Dog" }
        };

        return new OpenApiSchema
        {
            OneOf = new List<OpenApiSchema> { catSchema, dogSchema },
            Discriminator = new OpenApiDiscriminator
            {
                PropertyName = "petType",
                Mapping = new Dictionary<string, string>
                {
                    { "cat", "#/components/schemas/Cat" },
                    { "dog", "#/components/schemas/Dog" }
                }
            }
        };
    }

    [Test]
    public async Task Validate_Discriminator_ValidCatValue_PassesValidation()
    {
        // Arrange
        var schema = CreatePetSchema();
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = """{"petType": "cat", "meowVolume": 5}""";

        // Act
        var violations = validator.Validate(json, "GET /pets");

        // Assert
        await Assert.That(violations).IsEmpty();
    }

    [Test]
    public async Task Validate_Discriminator_ValidDogValue_PassesValidation()
    {
        // Arrange
        var schema = CreatePetSchema();
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = """{"petType": "dog", "barkVolume": 8}""";

        // Act
        var violations = validator.Validate(json, "GET /pets");

        // Assert
        await Assert.That(violations).IsEmpty();
    }

    [Test]
    public async Task Validate_Discriminator_InvalidValue_ReturnsDiscriminatorMismatch()
    {
        // Arrange
        var schema = CreatePetSchema();
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = """{"petType": "bird", "wingSpan": 30}""";

        // Act
        var violations = validator.Validate(json, "GET /pets");

        // Assert
        await Assert.That(violations.Count).IsEqualTo(1);
        await Assert.That(violations[0].Type).IsEqualTo(ViolationType.DiscriminatorMismatch);
        await Assert.That(violations[0].Actual).IsEqualTo("bird");
        await Assert.That(violations[0].Expected).Contains("cat");
        await Assert.That(violations[0].Expected).Contains("dog");
    }

    [Test]
    public async Task Validate_Discriminator_MissingProperty_ReturnsMissingRequired()
    {
        // Arrange
        var schema = CreatePetSchema();
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = """{"meowVolume": 5}""";

        // Act
        var violations = validator.Validate(json, "GET /pets");

        // Assert
        await Assert.That(violations.Count).IsEqualTo(1);
        await Assert.That(violations[0].Type).IsEqualTo(ViolationType.MissingRequired);
        await Assert.That(violations[0].Message).Contains("petType");
    }

    [Test]
    public async Task Validate_Discriminator_ValidDiscriminatorButInvalidSchema_ReturnsSchemaViolation()
    {
        // Arrange
        var schema = CreatePetSchema();
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        // Cat schema requires meowVolume as integer
        var json = """{"petType": "cat", "meowVolume": "loud"}""";

        // Act
        var violations = validator.Validate(json, "GET /pets");

        // Assert
        await Assert.That(violations.Count).IsEqualTo(1);
        await Assert.That(violations[0].Type).IsEqualTo(ViolationType.InvalidType);
    }

    [Test]
    public async Task Validate_Discriminator_ValidDiscriminatorMissingRequiredField_ReturnsMissingRequired()
    {
        // Arrange
        var schema = CreatePetSchema();
        var validator = new OpenApiSchemaValidator(schema, _serializer);
        // Cat schema requires meowVolume
        var json = """{"petType": "cat"}""";

        // Act
        var violations = validator.Validate(json, "GET /pets");

        // Assert
        await Assert.That(violations.Count).IsEqualTo(1);
        await Assert.That(violations[0].Type).IsEqualTo(ViolationType.MissingRequired);
        await Assert.That(violations[0].Message).Contains("meowVolume");
    }

    [Test]
    public async Task Validate_OneOfWithoutDiscriminator_FallsBackToSequential()
    {
        // Arrange - Schema without discriminator
        var stringSchema = new OpenApiSchema { Type = "string" };
        var integerSchema = new OpenApiSchema { Type = "integer" };

        var schema = new OpenApiSchema
        {
            OneOf = new List<OpenApiSchema> { stringSchema, integerSchema }
            // No discriminator
        };

        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "\"test-string\"";

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        await Assert.That(violations).IsEmpty();
    }

    [Test]
    public async Task Validate_OneOfWithoutDiscriminator_InvalidValue_ReturnsInvalidType()
    {
        // Arrange - Schema without discriminator
        var stringSchema = new OpenApiSchema { Type = "string" };
        var integerSchema = new OpenApiSchema { Type = "integer" };

        var schema = new OpenApiSchema
        {
            OneOf = new List<OpenApiSchema> { stringSchema, integerSchema }
            // No discriminator
        };

        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = "true";

        // Act
        var violations = validator.Validate(json, "GET /test");

        // Assert
        await Assert.That(violations.Count).IsEqualTo(1);
        await Assert.That(violations[0].Type).IsEqualTo(ViolationType.InvalidType);
    }

    [Test]
    public async Task Validate_Discriminator_CaseInsensitiveSchemaMatch()
    {
        // Arrange - Create schema with case-sensitive reference IDs but case-insensitive matching
        var catSchema = new OpenApiSchema
        {
            Type = "object",
            Properties = new Dictionary<string, OpenApiSchema>
            {
                { "type", new OpenApiSchema { Type = "string" } },
                { "name", new OpenApiSchema { Type = "string" } }
            },
            Reference = new OpenApiReference { Id = "CatSchema" }
        };

        var schema = new OpenApiSchema
        {
            OneOf = new List<OpenApiSchema> { catSchema },
            Discriminator = new OpenApiDiscriminator
            {
                PropertyName = "type",
                Mapping = new Dictionary<string, string>
                {
                    { "catschema", "#/components/schemas/CatSchema" }  // lowercase mapping
                }
            }
        };

        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = """{"type": "catschema", "name": "Whiskers"}""";

        // Act
        var violations = validator.Validate(json, "GET /pets");

        // Assert
        await Assert.That(violations).IsEmpty();
    }

    [Test]
    public async Task Validate_AnyOfWithDiscriminator_WorksSameAsOneOf()
    {
        // Arrange - Use anyOf instead of oneOf
        var catSchema = new OpenApiSchema
        {
            Type = "object",
            Properties = new Dictionary<string, OpenApiSchema>
            {
                { "petType", new OpenApiSchema { Type = "string" } },
                { "meowVolume", new OpenApiSchema { Type = "integer" } }
            },
            Required = new HashSet<string> { "petType", "meowVolume" },
            Reference = new OpenApiReference { Id = "Cat" }
        };

        var schema = new OpenApiSchema
        {
            AnyOf = new List<OpenApiSchema> { catSchema },
            Discriminator = new OpenApiDiscriminator
            {
                PropertyName = "petType",
                Mapping = new Dictionary<string, string>
                {
                    { "cat", "#/components/schemas/Cat" }
                }
            }
        };

        var validator = new OpenApiSchemaValidator(schema, _serializer);
        var json = """{"petType": "cat", "meowVolume": 5}""";

        // Act
        var violations = validator.Validate(json, "GET /pets");

        // Assert
        await Assert.That(violations).IsEmpty();
    }
}
