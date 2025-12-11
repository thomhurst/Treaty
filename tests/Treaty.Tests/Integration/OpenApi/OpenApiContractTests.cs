using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Treaty.Mocking;
using Treaty.OpenApi;

namespace Treaty.Tests.Integration.OpenApi;

/// <summary>
/// Tests that use actual OpenAPI specification files (YAML and JSON).
/// </summary>
public class OpenApiSpecFileTests
{
    private static string GetSpecPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Specs", fileName);

    [Test]
    public async Task OpenApi_LoadsYamlFile_Successfully()
    {
        // Arrange
        var specPath = GetSpecPath("petstore.yaml");

        // Act
        var contract = await Contract.FromOpenApi(specPath).BuildAsync();

        // Assert
        contract.Should().NotBeNull();
        contract.Name.Should().Be("Petstore API");
    }

    #region Metadata Extraction Tests

    [Test]
    public async Task OpenApi_ExtractsBasicMetadata_FromYaml()
    {
        // Arrange
        var specPath = GetSpecPath("petstore.yaml");

        // Act
        var contract = await Contract.FromOpenApi(specPath).BuildAsync();

        // Assert
        contract.Metadata.Should().NotBeNull();
        contract.Metadata!.Version.Should().Be("1.0.0");
        contract.Metadata.Description.Should().Be("A sample API for testing Treaty OpenAPI integration");
    }

    [Test]
    public async Task OpenApi_ExtractsFullMetadata_FromYaml()
    {
        // Arrange
        var specPath = GetSpecPath("full-metadata.yaml");

        // Act
        var contract = await Contract.FromOpenApi(specPath).BuildAsync();

        // Assert
        contract.Name.Should().Be("Full Metadata API");
        contract.Metadata.Should().NotBeNull();
        contract.Metadata!.Version.Should().Be("2.1.0");
        contract.Metadata.Description.Should().Be("An API with complete OpenAPI metadata for testing Treaty metadata extraction");

        // Contact
        contract.Metadata.Contact.Should().NotBeNull();
        contract.Metadata.Contact!.Name.Should().Be("API Support Team");
        contract.Metadata.Contact.Email.Should().Be("support@example.com");
        contract.Metadata.Contact.Url.Should().Be("https://example.com/support");

        // License
        contract.Metadata.License.Should().NotBeNull();
        contract.Metadata.License!.Name.Should().Be("Apache 2.0");
        contract.Metadata.License.Url.Should().Be("https://www.apache.org/licenses/LICENSE-2.0.html");

        // Terms of Service
        contract.Metadata.TermsOfService.Should().Be("https://example.com/terms");
    }

    #endregion

    #region Example Extraction Tests

    [Test]
    public async Task OpenApi_ExtractsPathParameterExample()
    {
        // Arrange
        var specPath = GetSpecPath("with-examples.yaml");

        // Act
        var contract = await Contract.FromOpenApi(specPath).BuildAsync();

        // Assert - GET /users/{userId} should have path param example
        var getEndpoint = contract.FindEndpoint("/users/123", HttpMethod.Get);
        getEndpoint.Should().NotBeNull();
        getEndpoint!.ExampleData.Should().NotBeNull();
        getEndpoint.ExampleData!.PathParameters.Should().ContainKey("userId");
        getEndpoint.ExampleData.PathParameters["userId"].Should().Be(123);
    }

    [Test]
    public async Task OpenApi_ExtractsQueryParameterExamples()
    {
        // Arrange
        var specPath = GetSpecPath("with-examples.yaml");

        // Act
        var contract = await Contract.FromOpenApi(specPath).BuildAsync();

        // Assert - GET /users should have query param examples
        var listEndpoint = contract.FindEndpoint("/users", HttpMethod.Get);
        listEndpoint.Should().NotBeNull();
        listEndpoint!.ExampleData.Should().NotBeNull();
        listEndpoint.ExampleData!.QueryParameters.Should().ContainKey("limit");
        listEndpoint.ExampleData.QueryParameters["limit"].Should().Be(10);
        listEndpoint.ExampleData.QueryParameters.Should().ContainKey("status");
        listEndpoint.ExampleData.QueryParameters["status"].Should().Be("active");
    }

    [Test]
    public async Task OpenApi_ExtractsHeaderParameterExample()
    {
        // Arrange
        var specPath = GetSpecPath("with-examples.yaml");

        // Act
        var contract = await Contract.FromOpenApi(specPath).BuildAsync();

        // Assert - GET /users/{userId} should have header example
        var getEndpoint = contract.FindEndpoint("/users/123", HttpMethod.Get);
        getEndpoint.Should().NotBeNull();
        getEndpoint!.ExampleData.Should().NotBeNull();
        getEndpoint.ExampleData!.Headers.Should().ContainKey("X-Request-Id");
        getEndpoint.ExampleData.Headers["X-Request-Id"].Should().Be("req-abc-123");
    }

    [Test]
    public async Task OpenApi_ExtractsRequestBodyExample()
    {
        // Arrange
        var specPath = GetSpecPath("with-examples.yaml");

        // Act
        var contract = await Contract.FromOpenApi(specPath).BuildAsync();

        // Assert - POST /users should have request body example
        var postEndpoint = contract.FindEndpoint("/users", HttpMethod.Post);
        postEndpoint.Should().NotBeNull();
        postEndpoint!.ExampleData.Should().NotBeNull();
        postEndpoint.ExampleData!.RequestBody.Should().NotBeNull();

        var body = postEndpoint.ExampleData.RequestBody as Dictionary<string, object?>;
        body.Should().NotBeNull();
        body!["username"].Should().Be("johndoe");
        body["email"].Should().Be("john@example.com");
        body["role"].Should().Be("admin");
    }

    [Test]
    public async Task OpenApi_ExtractsRequestBodyFromNamedExamples()
    {
        // Arrange
        var specPath = GetSpecPath("with-examples.yaml");

        // Act
        var contract = await Contract.FromOpenApi(specPath).BuildAsync();

        // Assert - PUT /users/{userId} should have request body from named examples (uses first one)
        var putEndpoint = contract.FindEndpoint("/users/123", HttpMethod.Put);
        putEndpoint.Should().NotBeNull();
        putEndpoint!.ExampleData.Should().NotBeNull();
        putEndpoint.ExampleData!.RequestBody.Should().NotBeNull();

        var body = putEndpoint.ExampleData.RequestBody as Dictionary<string, object?>;
        body.Should().NotBeNull();
        body!["username"].Should().Be("janedoe"); // From "full-update" example
    }

    [Test]
    public async Task OpenApi_EndpointWithExamples_CanGenerateExamplePath()
    {
        // Arrange
        var specPath = GetSpecPath("with-examples.yaml");
        var contract = await Contract.FromOpenApi(specPath).BuildAsync();

        // Act
        var getEndpoint = contract.FindEndpoint("/users/123", HttpMethod.Get);

        // Assert - Should be able to generate example path from extracted example
        getEndpoint.Should().NotBeNull();
        var examplePath = getEndpoint!.GetExamplePath();
        examplePath.Should().Be("/users/123");
    }

    [Test]
    public async Task OpenApi_EndpointWithExamples_CanGenerateExampleUrl()
    {
        // Arrange
        var specPath = GetSpecPath("with-examples.yaml");
        var contract = await Contract.FromOpenApi(specPath).BuildAsync();

        // Act
        var listEndpoint = contract.FindEndpoint("/users", HttpMethod.Get);

        // Assert - Should be able to generate example URL with query params
        listEndpoint.Should().NotBeNull();
        var exampleUrl = listEndpoint!.GetExampleUrl();
        exampleUrl.Should().Contain("limit=10");
        exampleUrl.Should().Contain("status=active");
    }

    #endregion

    [Test]
    public async Task OpenApi_LoadsJsonFile_Successfully()
    {
        // Arrange
        var specPath = GetSpecPath("users-api.json");

        // Act
        var contract = await Contract.FromOpenApi(specPath).BuildAsync();

        // Assert
        contract.Should().NotBeNull();
        contract.Name.Should().Be("Users API");
    }

    [Test]
    public async Task OpenApi_ContainsAllEndpoints_FromYamlSpec()
    {
        // Arrange
        var specPath = GetSpecPath("petstore.yaml");

        // Act
        var contract = await Contract.FromOpenApi(specPath).BuildAsync();

        // Assert - should have endpoints for /pets and /pets/{petId}
        contract.FindEndpoint("/pets", HttpMethod.Get).Should().NotBeNull();
        contract.FindEndpoint("/pets", HttpMethod.Post).Should().NotBeNull();
        contract.FindEndpoint("/pets/123", HttpMethod.Get).Should().NotBeNull();
        contract.FindEndpoint("/pets/123", HttpMethod.Put).Should().NotBeNull();
        contract.FindEndpoint("/pets/123", HttpMethod.Delete).Should().NotBeNull();
    }

    [Test]
    public async Task OpenApi_ContainsAllEndpoints_FromJsonSpec()
    {
        // Arrange
        var specPath = GetSpecPath("users-api.json");

        // Act
        var contract = await Contract.FromOpenApi(specPath).BuildAsync();

        // Assert
        contract.FindEndpoint("/users", HttpMethod.Get).Should().NotBeNull();
        contract.FindEndpoint("/users", HttpMethod.Post).Should().NotBeNull();
        contract.FindEndpoint("/users/456", HttpMethod.Get).Should().NotBeNull();
    }

    [Test]
    public async Task OpenApi_WithStream_LoadsYaml()
    {
        // Arrange
        var specPath = GetSpecPath("petstore.yaml");
        using var stream = File.OpenRead(specPath);

        // Act
        var contract = await Contract.FromOpenApi(stream, OpenApiFormat.Yaml).BuildAsync();

        // Assert
        contract.Should().NotBeNull();
        contract.Name.Should().Be("Petstore API");
    }

    [Test]
    public async Task OpenApi_WithStream_LoadsJson()
    {
        // Arrange
        var specPath = GetSpecPath("users-api.json");
        using var stream = File.OpenRead(specPath);

        // Act
        var contract = await Contract.FromOpenApi(stream, OpenApiFormat.Json).BuildAsync();

        // Assert
        contract.Should().NotBeNull();
        contract.Name.Should().Be("Users API");
    }
}

/// <summary>
/// Tests mock server generation from actual OpenAPI spec files.
/// </summary>
public class OpenApiMockServerFromSpecTests : IAsyncDisposable
{
    private IMockServer? _mockServer;
    private HttpClient? _client;

    private static string GetSpecPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Specs", fileName);

    [Before(Test)]
    public async Task Setup()
    {
        var specPath = GetSpecPath("petstore.yaml");
        _mockServer = await MockServer.FromOpenApi(specPath).BuildAsync();
        await _mockServer.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_mockServer.BaseUrl!) };
    }

    [After(Test)]
    public async Task Cleanup()
    {
        _client?.Dispose();
        if (_mockServer != null)
        {
            await _mockServer.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_mockServer != null)
        {
            await _mockServer.DisposeAsync();
        }
    }

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
    public async Task MockServer_FromYamlSpec_DeletePet_Returns204()
    {
        // Act
        var response = await _client!.DeleteAsync("/pets/123");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
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
public class OpenApiMockServerWithConditionsTests : IAsyncDisposable
{
    private IMockServer? _mockServer;
    private HttpClient? _client;

    private static string GetSpecPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Specs", fileName);

    [Before(Test)]
    public async Task Setup()
    {
        var specPath = GetSpecPath("petstore.yaml");
        _mockServer = await MockServer.FromOpenApi(specPath)
            .ForEndpoint("/pets/{petId}")
                .When(req => req.PathParam("petId") == "0").Return(404)
                .When(req => req.PathParam("petId") == "999").Return(404)
                .Otherwise().Return(200)
            .BuildAsync();

        await _mockServer.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_mockServer.BaseUrl!) };
    }

    [After(Test)]
    public async Task Cleanup()
    {
        _client?.Dispose();
        if (_mockServer != null)
        {
            await _mockServer.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_mockServer != null)
        {
            await _mockServer.DisposeAsync();
        }
    }

    [Test]
    public async Task MockServer_WithConditions_Returns404ForInvalidIds()
    {
        // Act
        var response1 = await _client!.GetAsync("/pets/0");
        var response2 = await _client!.GetAsync("/pets/999");

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response2.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
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
public class OpenApiJsonMockServerTests : IAsyncDisposable
{
    private IMockServer? _mockServer;
    private HttpClient? _client;

    private static string GetSpecPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Specs", fileName);

    [Before(Test)]
    public async Task Setup()
    {
        var specPath = GetSpecPath("users-api.json");
        _mockServer = await MockServer.FromOpenApi(specPath).BuildAsync();
        await _mockServer.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_mockServer.BaseUrl!) };
    }

    [After(Test)]
    public async Task Cleanup()
    {
        _client?.Dispose();
        if (_mockServer != null)
        {
            await _mockServer.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_mockServer != null)
        {
            await _mockServer.DisposeAsync();
        }
    }

    [Test]
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

    [Test]
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

    [Test]
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
public class OpenApiProviderValidationTests : IAsyncDisposable
{
    private IMockServer? _mockServer;
    private HttpClient? _client;

    private static string GetSpecPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Specs", fileName);

    [Before(Test)]
    public async Task Setup()
    {
        // Use the mock server as the "provider" to test against
        var specPath = GetSpecPath("petstore.yaml");
        _mockServer = await MockServer.FromOpenApi(specPath).BuildAsync();
        await _mockServer.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_mockServer.BaseUrl!) };
    }

    [After(Test)]
    public async Task Cleanup()
    {
        _client?.Dispose();
        if (_mockServer != null)
        {
            await _mockServer.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_mockServer != null)
        {
            await _mockServer.DisposeAsync();
        }
    }

    [Test]
    public async Task OpenContractDefinition_ValidatesProviderResponse_ForListPets()
    {
        // Arrange - Load contract from same spec
        var specPath = GetSpecPath("petstore.yaml");
        var contract = await Contract.FromOpenApi(specPath).BuildAsync();

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

    [Test]
    public async Task OpenContractDefinition_ValidatesProviderResponse_ForGetPet()
    {
        // Arrange
        var specPath = GetSpecPath("petstore.yaml");
        var contract = await Contract.FromOpenApi(specPath).BuildAsync();

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
