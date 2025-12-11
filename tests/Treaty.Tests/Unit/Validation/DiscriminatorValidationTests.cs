using System.Text.Json.Nodes;
using Microsoft.OpenApi;
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
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                { "petType", new OpenApiSchema { Type = JsonSchemaType.String } },
                { "meowVolume", new OpenApiSchema { Type = JsonSchemaType.Integer } }
            },
            Required = new HashSet<string> { "petType", "meowVolume" },
            Title = "Cat" // Used for discriminator matching
        };

        var dogSchema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                { "petType", new OpenApiSchema { Type = JsonSchemaType.String } },
                { "barkVolume", new OpenApiSchema { Type = JsonSchemaType.Integer } }
            },
            Required = new HashSet<string> { "petType", "barkVolume" },
            Title = "Dog" // Used for discriminator matching
        };

        // Create schema references for cat and dog
        // Note: Without a document, these references won't have their Id property set,
        // so the discriminator matching will fall back to matching by Title
        var catRef = new OpenApiSchemaReference("Cat");
        var dogRef = new OpenApiSchemaReference("Dog");

        return new OpenApiSchema
        {
            OneOf = new List<IOpenApiSchema> { catSchema, dogSchema },
            Discriminator = new OpenApiDiscriminator
            {
                PropertyName = "petType",
                Mapping = new Dictionary<string, OpenApiSchemaReference>
                {
                    { "cat", catRef },
                    { "dog", dogRef }
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
        var stringSchema = new OpenApiSchema { Type = JsonSchemaType.String };
        var integerSchema = new OpenApiSchema { Type = JsonSchemaType.Integer };

        var schema = new OpenApiSchema
        {
            OneOf = new List<IOpenApiSchema> { stringSchema, integerSchema }
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
        var stringSchema = new OpenApiSchema { Type = JsonSchemaType.String };
        var integerSchema = new OpenApiSchema { Type = JsonSchemaType.Integer };

        var schema = new OpenApiSchema
        {
            OneOf = new List<IOpenApiSchema> { stringSchema, integerSchema }
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
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                { "type", new OpenApiSchema { Type = JsonSchemaType.String } },
                { "name", new OpenApiSchema { Type = JsonSchemaType.String } }
            },
            Title = "CatSchema"
        };

        var catRef = new OpenApiSchemaReference("CatSchema");

        var schema = new OpenApiSchema
        {
            OneOf = new List<IOpenApiSchema> { catSchema },
            Discriminator = new OpenApiDiscriminator
            {
                PropertyName = "type",
                Mapping = new Dictionary<string, OpenApiSchemaReference>
                {
                    { "catschema", catRef }  // lowercase mapping
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
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                { "petType", new OpenApiSchema { Type = JsonSchemaType.String } },
                { "meowVolume", new OpenApiSchema { Type = JsonSchemaType.Integer } }
            },
            Required = new HashSet<string> { "petType", "meowVolume" },
            Title = "Cat"
        };

        var catRef = new OpenApiSchemaReference("Cat");

        var schema = new OpenApiSchema
        {
            AnyOf = new List<IOpenApiSchema> { catSchema },
            Discriminator = new OpenApiDiscriminator
            {
                PropertyName = "petType",
                Mapping = new Dictionary<string, OpenApiSchemaReference>
                {
                    { "cat", catRef }
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
