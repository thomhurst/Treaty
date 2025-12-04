using System.Text.Json;
using FluentAssertions;
using Treaty.Serialization;

namespace Treaty.Tests;

public class SerializerTests
{
    [Test]
    public void SystemTextJsonSerializer_GetSchema_ForSimpleType_ReturnsCorrectSchema()
    {
        // Arrange
        var serializer = new SystemTextJsonSerializer();

        // Act
        var schema = serializer.GetSchema<SimpleUser>();

        // Assert
        schema.SchemaType.Should().Be(JsonSchemaType.Object);
        schema.Properties.Should().HaveCount(2);
        schema.Properties.Should().ContainKey("id");
        schema.Properties.Should().ContainKey("name");
        schema.Properties["id"].TypeSchema.SchemaType.Should().Be(JsonSchemaType.Integer);
        schema.Properties["name"].TypeSchema.SchemaType.Should().Be(JsonSchemaType.String);
    }

    [Test]
    public void SystemTextJsonSerializer_GetSchema_ForArrayType_ReturnsArraySchema()
    {
        // Arrange
        var serializer = new SystemTextJsonSerializer();

        // Act
        var schema = serializer.GetSchema<SimpleUser[]>();

        // Assert
        schema.SchemaType.Should().Be(JsonSchemaType.Array);
        schema.ItemSchema.Should().NotBeNull();
        schema.ItemSchema!.SchemaType.Should().Be(JsonSchemaType.Object);
    }

    [Test]
    public void SystemTextJsonSerializer_GetSchema_ForListType_ReturnsArraySchema()
    {
        // Arrange
        var serializer = new SystemTextJsonSerializer();

        // Act
        var schema = serializer.GetSchema<List<SimpleUser>>();

        // Assert
        schema.SchemaType.Should().Be(JsonSchemaType.Array);
        schema.ItemSchema.Should().NotBeNull();
    }

    [Test]
    public void SystemTextJsonSerializer_GetSchema_ForNullableType_IsNullable()
    {
        // Arrange
        var serializer = new SystemTextJsonSerializer();

        // Act
        var schema = serializer.GetSchema<int?>();

        // Assert
        schema.IsNullable.Should().BeTrue();
        schema.SchemaType.Should().Be(JsonSchemaType.Integer);
    }

    [Test]
    public void SystemTextJsonSerializer_GetSchema_ForGuid_ReturnsStringWithUuidFormat()
    {
        // Arrange
        var serializer = new SystemTextJsonSerializer();

        // Act
        var schema = serializer.GetSchema<Guid>();

        // Assert
        schema.SchemaType.Should().Be(JsonSchemaType.String);
        schema.Format.Should().Be("uuid");
    }

    [Test]
    public void SystemTextJsonSerializer_GetSchema_ForDateTime_ReturnsStringWithDateTimeFormat()
    {
        // Arrange
        var serializer = new SystemTextJsonSerializer();

        // Act
        var schema = serializer.GetSchema<DateTime>();

        // Assert
        schema.SchemaType.Should().Be(JsonSchemaType.String);
        schema.Format.Should().Be("date-time");
    }

    [Test]
    public void SystemTextJsonSerializer_GetSchema_ForUri_ReturnsStringWithUriFormat()
    {
        // Arrange
        var serializer = new SystemTextJsonSerializer();

        // Act
        var schema = serializer.GetSchema<Uri>();

        // Assert
        schema.SchemaType.Should().Be(JsonSchemaType.String);
        schema.Format.Should().Be("uri");
    }

    [Test]
    public void SystemTextJsonSerializer_Serialize_UsesCamelCase()
    {
        // Arrange
        var serializer = new SystemTextJsonSerializer();
        var user = new SimpleUser(1, "John");

        // Act
        var json = serializer.Serialize(user);

        // Assert
        json.Should().Contain("\"id\":");
        json.Should().Contain("\"name\":");
    }

    [Test]
    public void SystemTextJsonSerializer_Deserialize_HandlesSuccessfully()
    {
        // Arrange
        var serializer = new SystemTextJsonSerializer();
        var json = """{"id": 1, "name": "John"}""";

        // Act
        var user = serializer.Deserialize<SimpleUser>(json);

        // Assert
        user.Should().NotBeNull();
        user!.Id.Should().Be(1);
        user.Name.Should().Be("John");
    }

    [Test]
    public void SystemTextJsonSerializer_WithCustomOptions_UsesOptions()
    {
        // Arrange
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
        var serializer = new SystemTextJsonSerializer(options);
        var user = new SimpleUser(1, "John");

        // Act
        var json = serializer.Serialize(user);

        // Assert
        json.Should().Contain("\"id\":");
        json.Should().Contain("\"name\":");
    }

    [Test]
    public void SystemTextJsonSerializer_GetSchema_ForNestedTypes_BuildsCorrectly()
    {
        // Arrange
        var serializer = new SystemTextJsonSerializer();

        // Act
        var schema = serializer.GetSchema<UserWithAddress>();

        // Assert
        schema.SchemaType.Should().Be(JsonSchemaType.Object);
        schema.Properties.Should().ContainKey("address");
        schema.Properties["address"].TypeSchema.SchemaType.Should().Be(JsonSchemaType.Object);
        schema.Properties["address"].TypeSchema.Properties.Should().ContainKey("city");
    }

    private record SimpleUser(int Id, string Name);

    private record UserWithAddress(string Name, Address Address);

    private record Address(string City, string Street);
}
