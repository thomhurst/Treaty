using System.Net;
using System.Text.Json;
using FluentAssertions;
using Treaty.Mocking;
using Treaty.OpenApi;

namespace Treaty.Tests.Integration.OpenApi;

public class MockServerTests : IAsyncDisposable
{
    private IMockServer? _mockServer;
    private HttpClient? _client;
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
                '404':
                  description: User not found
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
                  example: 1
                name:
                  type: string
                  example: John Doe
                email:
                  type: string
                  format: email
                  example: john@example.com
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

    [Before(Test)]
    public async Task Setup()
    {
        // Write spec to temp file
        var specPath = Path.GetTempFileName() + ".yaml";
        await File.WriteAllTextAsync(specPath, TestOpenApiSpec);

        _mockServer = await MockServer.FromOpenApi(specPath).BuildAsync();
        await _mockServer.StartAsync();

        _client = new HttpClient { BaseAddress = new Uri(_mockServer.BaseUrl!) };

        // Clean up temp file
        File.Delete(specPath);
    }

    [After(Test)]
    public async Task Cleanup()
    {
        _client?.Dispose();
        if (_mockServer != null)
            await _mockServer.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_mockServer != null)
            await _mockServer.DisposeAsync();
    }

    [Test]
    public async Task MockServer_GetUsers_ReturnsArrayOfUsers()
    {
        // Act
        var response = await _client!.GetAsync("/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var content = await response.Content.ReadAsStringAsync();
        var users = JsonSerializer.Deserialize<JsonElement>(content);
        users.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Test]
    public async Task MockServer_GetUserById_ReturnsUser()
    {
        // Act
        var response = await _client!.GetAsync("/users/123");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var user = JsonSerializer.Deserialize<JsonElement>(content);
        user.ValueKind.Should().Be(JsonValueKind.Object);
        user.TryGetProperty("id", out _).Should().BeTrue();
        user.TryGetProperty("name", out _).Should().BeTrue();
        user.TryGetProperty("email", out _).Should().BeTrue();
    }

    [Test]
    public async Task MockServer_UndefinedEndpoint_ReturnsDetailedError()
    {
        // Act
        var response = await _client!.GetAsync("/nonexistent");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("treaty_error");
        content.Should().Contain("available_endpoints");
    }

    [Test]
    public async Task MockServer_BaseUrl_IsSet()
    {
        // Assert
        _mockServer!.BaseUrl.Should().NotBeNullOrEmpty();
        _mockServer.BaseUrl.Should().StartWith("http://");
        _mockServer.BaseUrl.Should().MatchRegex(@"http://[\w\.\:]+:\d+");
    }
}

public class MockServerWithConditionsTests : IAsyncDisposable
{
    private IMockServer? _mockServer;
    private HttpClient? _client;
    private const string TestOpenApiSpec = """
        openapi: '3.0.3'
        info:
          title: Test API
          version: '1.0'
        paths:
          /users/{id}:
            get:
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: string
              responses:
                '200':
                  description: User found
                  content:
                    application/json:
                      schema:
                        type: object
                        properties:
                          id:
                            type: string
                          name:
                            type: string
                '404':
                  description: User not found
                '400':
                  description: Bad request
        """;

    [Before(Test)]
    public async Task Setup()
    {
        var specPath = Path.GetTempFileName() + ".yaml";
        await File.WriteAllTextAsync(specPath, TestOpenApiSpec);

        _mockServer = await MockServer.FromOpenApi(specPath)
            .ForEndpoint("/users/{id}")
                .When(req => req.PathParam("id") == "0").Return(404)
                .When(req => req.PathParam("id") == "bad").Return(400)
                .Otherwise().Return(200)
            .BuildAsync();

        await _mockServer.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_mockServer.BaseUrl!) };

        File.Delete(specPath);
    }

    [After(Test)]
    public async Task Cleanup()
    {
        _client?.Dispose();
        if (_mockServer != null)
            await _mockServer.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_mockServer != null)
            await _mockServer.DisposeAsync();
    }

    [Test]
    public async Task MockServer_WithCondition_Returns404ForZeroId()
    {
        // Act
        var response = await _client!.GetAsync("/users/0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task MockServer_WithCondition_Returns400ForBadId()
    {
        // Act
        var response = await _client!.GetAsync("/users/bad");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task MockServer_WithCondition_Returns200ForNormalId()
    {
        // Act
        var response = await _client!.GetAsync("/users/123");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
