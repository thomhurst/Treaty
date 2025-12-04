using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using Treaty.Validation;
using TreatyLib = Treaty.Treaty;
using TreatyConsumer = Treaty.Consumer;
using TreatyOpenApi = Treaty.OpenApi;

namespace Treaty.Tests;

public class ConsumerIntegrationTests : IAsyncLifetime
{
    private TreatyOpenApi.MockServer? _mockServer;
    private TreatyConsumer.ConsumerVerifier? _consumer;

    private const string TestOpenApiSpec = """
        openapi: '3.0.3'
        info:
          title: Test API
          version: '1.0'
        paths:
          /users:
            get:
              summary: Get all users
              responses:
                '200':
                  description: List of users
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          $ref: '#/components/schemas/User'
            post:
              summary: Create user
              requestBody:
                required: true
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/CreateUser'
              responses:
                '201':
                  description: Created user
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/User'
          /users/{id}:
            get:
              summary: Get user by ID
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: integer
              responses:
                '200':
                  description: User details
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/User'
        components:
          schemas:
            User:
              type: object
              required:
                - id
                - name
                - email
              properties:
                id:
                  type: integer
                name:
                  type: string
                email:
                  type: string
                  format: email
            CreateUser:
              type: object
              required:
                - name
                - email
              properties:
                name:
                  type: string
                email:
                  type: string
                  format: email
        """;

    public async Task InitializeAsync()
    {
        // Start mock server
        var specPath = Path.GetTempFileName() + ".yaml";
        await File.WriteAllTextAsync(specPath, TestOpenApiSpec);

        _mockServer = TreatyLib.MockFromOpenApi(specPath).Build();
        await _mockServer.StartAsync();

        // Create consumer verifier with contract matching the OpenAPI spec
        var contract = TreatyLib.DefineContract("TestApi")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithJsonBody<UserDto[]>())
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingRequest(req => req.WithJsonBody<CreateUserDto>())
                .ExpectingResponse(r => r
                    .WithStatus(201)
                    .WithJsonBody<UserDto>())
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithJsonBody<UserDto>())
            .Build();

        _consumer = TreatyLib.ForConsumer()
            .WithContract(contract)
            .WithBaseUrl(_mockServer.BaseUrl!)
            .Build();

        File.Delete(specPath);
    }

    public async Task DisposeAsync()
    {
        if (_mockServer != null)
            await _mockServer.DisposeAsync();
    }

    [Fact]
    public async Task Consumer_GetUsers_ValidRequestSucceeds()
    {
        // Arrange
        var client = _consumer!.CreateHttpClient();

        // Act
        var response = await client.GetAsync("/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Consumer_GetUserById_ValidRequestSucceeds()
    {
        // Arrange
        var client = _consumer!.CreateHttpClient();

        // Act
        var response = await client.GetAsync("/users/123");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Consumer_PostUser_ValidRequestSucceeds()
    {
        // Arrange
        var client = _consumer!.CreateHttpClient();
        var newUser = new CreateUserDto("John Doe", "john@example.com");
        var json = JsonSerializer.Serialize(newUser);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/users", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Consumer_PostUser_InvalidBody_ThrowsContractViolation()
    {
        // Arrange
        var client = _consumer!.CreateHttpClient();
        // Missing required 'email' field
        var invalidBody = new { name = "John Doe" };
        var json = JsonSerializer.Serialize(invalidBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ContractViolationException>(
            () => client.PostAsync("/users", content));

        exception.Violations.Should().ContainSingle()
            .Which.Message.Should().Contain("Email");
    }

    [Fact]
    public async Task Consumer_PostUser_MissingBody_ThrowsContractViolation()
    {
        // Arrange - rebuild consumer with required body expectation (required by default)
        var contract = TreatyLib.DefineContract("TestApi")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingRequest(req => req.WithJsonBody<CreateUserDto>())
                .ExpectingResponse(r => r.WithStatus(201))
            .Build();

        var consumer = TreatyLib.ForConsumer()
            .WithContract(contract)
            .WithBaseUrl(_mockServer!.BaseUrl!)
            .Build();

        var client = consumer.CreateHttpClient();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ContractViolationException>(
            () => client.PostAsync("/users", null));

        exception.Violations.Should().ContainSingle()
            .Which.Message.Should().Contain("required");
    }

    [Fact]
    public async Task Consumer_WithRequiredHeader_MissingHeader_ThrowsContractViolation()
    {
        // Arrange
        var contract = TreatyLib.DefineContract("TestApi")
            .WithDefaults(d => d.AllRequestsHaveHeader("Authorization"))
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        var consumer = TreatyLib.ForConsumer()
            .WithContract(contract)
            .WithBaseUrl(_mockServer!.BaseUrl!)
            .Build();

        var client = consumer.CreateHttpClient();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ContractViolationException>(
            () => client.GetAsync("/users"));

        exception.Violations.Should().ContainSingle()
            .Which.Message.Should().Contain("Authorization");
    }

    [Fact]
    public async Task Consumer_WithRequiredHeader_ValidHeader_Succeeds()
    {
        // Arrange
        var contract = TreatyLib.DefineContract("TestApi")
            .WithDefaults(d => d.AllRequestsHaveHeader("Authorization"))
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        var consumer = TreatyLib.ForConsumer()
            .WithContract(contract)
            .WithBaseUrl(_mockServer!.BaseUrl!)
            .Build();

        var client = consumer.CreateHttpClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer token123");

        // Act
        var response = await client.GetAsync("/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Consumer_UndefinedEndpoint_PassesThrough()
    {
        // Consumer should pass through requests for endpoints not defined in the contract
        // (different from provider which validates all endpoints)

        // Arrange
        var client = _consumer!.CreateHttpClient();

        // Act - request to an endpoint not in the contract
        var response = await client.GetAsync("/unknown");

        // Assert - should get 404 from mock server, not a contract violation
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Consumer_CreateHandler_WorksWithCustomInnerHandler()
    {
        // Arrange
        var contract = TreatyLib.DefineContract("TestApi")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        var consumer = TreatyLib.ForConsumer()
            .WithContract(contract)
            .WithBaseUrl(_mockServer!.BaseUrl!)
            .Build();

        var handler = consumer.CreateHandler(new HttpClientHandler());
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(_mockServer.BaseUrl!)
        };

        // Act
        var response = await client.GetAsync("/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // DTOs for type-safe request/response validation
    private record UserDto(
        [property: System.Text.Json.Serialization.JsonPropertyName("id")] int Id,
        [property: System.Text.Json.Serialization.JsonPropertyName("name")] string Name,
        [property: System.Text.Json.Serialization.JsonPropertyName("email")] string Email);

    private record CreateUserDto(
        [property: System.Text.Json.Serialization.JsonPropertyName("name")] string Name,
        [property: System.Text.Json.Serialization.JsonPropertyName("email")] string Email);
}
