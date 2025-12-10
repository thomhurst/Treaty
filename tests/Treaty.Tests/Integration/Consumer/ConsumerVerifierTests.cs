using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Treaty.Consumer;
using Treaty.OpenApi;
using Treaty.Validation;

namespace Treaty.Tests.Integration.Consumer;

public class ConsumerVerifierTests : IAsyncDisposable
{
    private OpenApiMockServer? _mockServer;
    private ConsumerValidationClient? _consumer;

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

    [Before(Test)]
    public async Task Setup()
    {
        // Start mock server
        var specPath = Path.GetTempFileName() + ".yaml";
        await File.WriteAllTextAsync(specPath, TestOpenApiSpec);

        _mockServer = MockServer.FromOpenApi(specPath).Build();
        await _mockServer.StartAsync();

        // Create consumer verifier with contract from OpenAPI spec
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(TestOpenApiSpec));
        var contract = Contract.FromOpenApi(stream, OpenApiFormat.Yaml).Build();

        _consumer = ConsumerVerifier.Create()
            .WithContract(contract)
            .WithBaseUrl(_mockServer.BaseUrl!)
            .Build();

        File.Delete(specPath);
    }

    [After(Test)]
    public async Task Cleanup()
    {
        if (_mockServer != null)
            await _mockServer.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_mockServer != null)
            await _mockServer.DisposeAsync();
    }

    [Test]
    public async Task Consumer_GetUsers_ValidRequestSucceeds()
    {
        // Arrange
        var client = _consumer!.CreateHttpClient();

        // Act
        var response = await client.GetAsync("/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task Consumer_GetUserById_ValidRequestSucceeds()
    {
        // Arrange
        var client = _consumer!.CreateHttpClient();

        // Act
        var response = await client.GetAsync("/users/123");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task Consumer_PostUser_ValidRequestSucceeds()
    {
        // Arrange
        var client = _consumer!.CreateHttpClient();
        var newUser = new { name = "John Doe", email = "john@example.com" };
        var json = JsonSerializer.Serialize(newUser);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/users", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Test]
    public async Task Consumer_PostUser_InvalidBody_ThrowsContractViolation()
    {
        // Arrange
        var client = _consumer!.CreateHttpClient();
        // Missing required 'email' field
        var invalidBody = new { name = "John Doe" };
        var json = JsonSerializer.Serialize(invalidBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var act = async () => await client.PostAsync("/users", content);

        // Assert
        var exception = await act.Should().ThrowAsync<ContractViolationException>();
        exception.Which.Violations.Should().ContainSingle()
            .Which.Message.Should().Contain("email");
    }

    [Test]
    public async Task Consumer_PostUser_MissingBody_ThrowsContractViolation()
    {
        // Arrange - same consumer, required body from OpenAPI spec
        var client = _consumer!.CreateHttpClient();

        // Act
        var act = async () => await client.PostAsync("/users", null);

        // Assert
        var exception = await act.Should().ThrowAsync<ContractViolationException>();
        exception.Which.Violations.Should().ContainSingle()
            .Which.Message.Should().Contain("required");
    }

    [Test]
    public async Task Consumer_WithRequiredHeader_MissingHeader_ThrowsContractViolation()
    {
        // Arrange - create spec with required Authorization header
        const string specWithAuth = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  parameters:
                    - name: Authorization
                      in: header
                      required: true
                      schema:
                        type: string
                  responses:
                    '200':
                      description: OK
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(specWithAuth));
        var contract = Contract.FromOpenApi(stream, OpenApiFormat.Yaml).Build();

        var consumer = ConsumerVerifier.Create()
            .WithContract(contract)
            .WithBaseUrl(_mockServer!.BaseUrl!)
            .Build();

        var client = consumer.CreateHttpClient();

        // Act
        var act = async () => await client.GetAsync("/users");

        // Assert
        var exception = await act.Should().ThrowAsync<ContractViolationException>();
        exception.Which.Violations.Should().ContainSingle()
            .Which.Message.Should().Contain("Authorization");
    }

    [Test]
    public async Task Consumer_WithRequiredHeader_ValidHeader_Succeeds()
    {
        // Arrange - create spec with required Authorization header
        const string specWithAuth = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  parameters:
                    - name: Authorization
                      in: header
                      required: true
                      schema:
                        type: string
                  responses:
                    '200':
                      description: OK
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(specWithAuth));
        var contract = Contract.FromOpenApi(stream, OpenApiFormat.Yaml).Build();

        var consumer = ConsumerVerifier.Create()
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

    [Test]
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

    [Test]
    public async Task Consumer_CreateHandler_WorksWithCustomInnerHandler()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(TestOpenApiSpec));
        var contract = Contract.FromOpenApi(stream, OpenApiFormat.Yaml).Build();

        var consumer = ConsumerVerifier.Create()
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
}
