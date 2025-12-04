using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using Treaty.OpenApi;
using TreatyLib = Treaty.Treaty;

namespace Treaty.Tests;

/// <summary>
/// Tests that use actual OpenAPI specification files (YAML and JSON).
/// </summary>
public class OpenApiSpecFileTests
{
    private static string GetSpecPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Specs", fileName);

    [Fact]
    public void FromOpenApiSpec_LoadsYamlFile_Successfully()
    {
        // Arrange
        var specPath = GetSpecPath("petstore.yaml");

        // Act
        var contract = TreatyLib.FromOpenApiSpec(specPath).Build();

        // Assert
        contract.Should().NotBeNull();
        contract.Name.Should().Be("Petstore API");
    }

    [Fact]
    public void FromOpenApiSpec_LoadsJsonFile_Successfully()
    {
        // Arrange
        var specPath = GetSpecPath("users-api.json");

        // Act
        var contract = TreatyLib.FromOpenApiSpec(specPath).Build();

        // Assert
        contract.Should().NotBeNull();
        contract.Name.Should().Be("Users API");
    }

    [Fact]
    public void FromOpenApiSpec_ContainsAllEndpoints_FromYamlSpec()
    {
        // Arrange
        var specPath = GetSpecPath("petstore.yaml");

        // Act
        var contract = TreatyLib.FromOpenApiSpec(specPath).Build();

        // Assert - should have endpoints for /pets and /pets/{petId}
        contract.FindEndpoint("/pets", HttpMethod.Get).Should().NotBeNull();
        contract.FindEndpoint("/pets", HttpMethod.Post).Should().NotBeNull();
        contract.FindEndpoint("/pets/123", HttpMethod.Get).Should().NotBeNull();
        contract.FindEndpoint("/pets/123", HttpMethod.Put).Should().NotBeNull();
        contract.FindEndpoint("/pets/123", HttpMethod.Delete).Should().NotBeNull();
    }

    [Fact]
    public void FromOpenApiSpec_ContainsAllEndpoints_FromJsonSpec()
    {
        // Arrange
        var specPath = GetSpecPath("users-api.json");

        // Act
        var contract = TreatyLib.FromOpenApiSpec(specPath).Build();

        // Assert
        contract.FindEndpoint("/users", HttpMethod.Get).Should().NotBeNull();
        contract.FindEndpoint("/users", HttpMethod.Post).Should().NotBeNull();
        contract.FindEndpoint("/users/456", HttpMethod.Get).Should().NotBeNull();
    }

    [Fact]
    public void FromOpenApiSpec_WithStream_LoadsYaml()
    {
        // Arrange
        var specPath = GetSpecPath("petstore.yaml");
        using var stream = File.OpenRead(specPath);

        // Act
        var contract = TreatyLib.FromOpenApiSpec(stream, OpenApiFormat.Yaml).Build();

        // Assert
        contract.Should().NotBeNull();
        contract.Name.Should().Be("Petstore API");
    }

    [Fact]
    public void FromOpenApiSpec_WithStream_LoadsJson()
    {
        // Arrange
        var specPath = GetSpecPath("users-api.json");
        using var stream = File.OpenRead(specPath);

        // Act
        var contract = TreatyLib.FromOpenApiSpec(stream, OpenApiFormat.Json).Build();

        // Assert
        contract.Should().NotBeNull();
        contract.Name.Should().Be("Users API");
    }
}

/// <summary>
/// Tests mock server generation from actual OpenAPI spec files.
/// </summary>
public class OpenApiMockServerTests : IAsyncLifetime
{
    private MockServer? _mockServer;
    private HttpClient? _client;

    private static string GetSpecPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Specs", fileName);

    public async Task InitializeAsync()
    {
        var specPath = GetSpecPath("petstore.yaml");
        _mockServer = TreatyLib.MockFromOpenApi(specPath).Build();
        await _mockServer.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_mockServer.BaseUrl!) };
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_mockServer != null)
            await _mockServer.DisposeAsync();
    }

    [Fact]
    public async Task MockServer_FromYamlSpec_ListPets_ReturnsArray()
    {
        // Act
        var response = await _client!.GetAsync("/pets");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var content = await response.Content.ReadAsStringAsync();
        var pets = JsonSerializer.Deserialize<JsonElement>(content);
        pets.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task MockServer_FromYamlSpec_GetPetById_ReturnsPetObject()
    {
        // Act
        var response = await _client!.GetAsync("/pets/123");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var pet = JsonSerializer.Deserialize<JsonElement>(content);
        pet.ValueKind.Should().Be(JsonValueKind.Object);
        pet.TryGetProperty("id", out _).Should().BeTrue();
        pet.TryGetProperty("name", out _).Should().BeTrue();
        pet.TryGetProperty("status", out _).Should().BeTrue();
    }

    [Fact]
    public async Task MockServer_FromYamlSpec_CreatePet_Returns201()
    {
        // Arrange
        var newPet = new { name = "Buddy", tag = "dog" };
        var json = JsonSerializer.Serialize(newPet);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client!.PostAsync("/pets", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task MockServer_FromYamlSpec_UpdatePet_Returns200()
    {
        // Arrange
        var updatePet = new { name = "Buddy Updated", status = "sold" };
        var json = JsonSerializer.Serialize(updatePet);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client!.PutAsync("/pets/123", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MockServer_FromYamlSpec_DeletePet_Returns204()
    {
        // Act
        var response = await _client!.DeleteAsync("/pets/123");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task MockServer_FromYamlSpec_UndefinedEndpoint_Returns404WithDetails()
    {
        // Act
        var response = await _client!.GetAsync("/unknown");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("treaty_error");
        content.Should().Contain("available_endpoints");
        content.Should().Contain("/pets");
    }
}

/// <summary>
/// Tests mock server with conditional responses using actual OpenAPI specs.
/// </summary>
public class OpenApiMockServerWithConditionsTests : IAsyncLifetime
{
    private MockServer? _mockServer;
    private HttpClient? _client;

    private static string GetSpecPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Specs", fileName);

    public async Task InitializeAsync()
    {
        var specPath = GetSpecPath("petstore.yaml");
        _mockServer = TreatyLib.MockFromOpenApi(specPath)
            .ForEndpoint("/pets/{petId}")
                .When(req => req.PathParam("petId") == "0").Return(404)
                .When(req => req.PathParam("petId") == "999").Return(404)
                .Otherwise().Return(200)
            .Build();

        await _mockServer.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_mockServer.BaseUrl!) };
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_mockServer != null)
            await _mockServer.DisposeAsync();
    }

    [Fact]
    public async Task MockServer_WithConditions_Returns404ForInvalidIds()
    {
        // Act
        var response1 = await _client!.GetAsync("/pets/0");
        var response2 = await _client!.GetAsync("/pets/999");

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response2.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MockServer_WithConditions_Returns200ForValidIds()
    {
        // Act
        var response = await _client!.GetAsync("/pets/123");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

/// <summary>
/// Tests mock server from JSON OpenAPI spec.
/// </summary>
public class OpenApiJsonMockServerTests : IAsyncLifetime
{
    private MockServer? _mockServer;
    private HttpClient? _client;

    private static string GetSpecPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Specs", fileName);

    public async Task InitializeAsync()
    {
        var specPath = GetSpecPath("users-api.json");
        _mockServer = TreatyLib.MockFromOpenApi(specPath).Build();
        await _mockServer.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_mockServer.BaseUrl!) };
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_mockServer != null)
            await _mockServer.DisposeAsync();
    }

    [Fact]
    public async Task MockServer_FromJsonSpec_ListUsers_ReturnsArray()
    {
        // Act
        var response = await _client!.GetAsync("/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var users = JsonSerializer.Deserialize<JsonElement>(content);
        users.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task MockServer_FromJsonSpec_GetUserById_ReturnsUserObject()
    {
        // Act
        var response = await _client!.GetAsync("/users/42");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var user = JsonSerializer.Deserialize<JsonElement>(content);
        user.ValueKind.Should().Be(JsonValueKind.Object);
        user.TryGetProperty("id", out _).Should().BeTrue();
        user.TryGetProperty("username", out _).Should().BeTrue();
        user.TryGetProperty("email", out _).Should().BeTrue();
    }

    [Fact]
    public async Task MockServer_FromJsonSpec_CreateUser_Returns201()
    {
        // Arrange
        var newUser = new { username = "newuser", email = "new@example.com" };
        var json = JsonSerializer.Serialize(newUser);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client!.PostAsync("/users", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}

/// <summary>
/// Tests OpenAPI contract validation against provider implementation.
/// </summary>
public class OpenApiProviderValidationTests : IAsyncLifetime
{
    private MockServer? _mockServer;
    private HttpClient? _client;

    private static string GetSpecPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Specs", fileName);

    public async Task InitializeAsync()
    {
        // Use the mock server as the "provider" to test against
        var specPath = GetSpecPath("petstore.yaml");
        _mockServer = TreatyLib.MockFromOpenApi(specPath).Build();
        await _mockServer.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_mockServer.BaseUrl!) };
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_mockServer != null)
            await _mockServer.DisposeAsync();
    }

    [Fact]
    public async Task OpenApiContract_ValidatesProviderResponse_ForListPets()
    {
        // Arrange - Load contract from same spec
        var specPath = GetSpecPath("petstore.yaml");
        var contract = TreatyLib.FromOpenApiSpec(specPath).Build();

        // Act - Get response from provider (mock server)
        var response = await _client!.GetAsync("/pets");
        var body = await response.Content.ReadAsStringAsync();

        // Assert - Verify response matches contract expectations
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var endpoint = contract.FindEndpoint("/pets", HttpMethod.Get);
        endpoint.Should().NotBeNull();

        // The response should be an array (as defined in spec)
        var json = JsonSerializer.Deserialize<JsonElement>(body);
        json.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task OpenApiContract_ValidatesProviderResponse_ForGetPet()
    {
        // Arrange
        var specPath = GetSpecPath("petstore.yaml");
        var contract = TreatyLib.FromOpenApiSpec(specPath).Build();

        // Act
        var response = await _client!.GetAsync("/pets/1");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var endpoint = contract.FindEndpoint("/pets/1", HttpMethod.Get);
        endpoint.Should().NotBeNull();

        // The response should have required fields
        var pet = JsonSerializer.Deserialize<JsonElement>(body);
        pet.TryGetProperty("id", out _).Should().BeTrue();
        pet.TryGetProperty("name", out _).Should().BeTrue();
        pet.TryGetProperty("status", out _).Should().BeTrue();
    }
}
