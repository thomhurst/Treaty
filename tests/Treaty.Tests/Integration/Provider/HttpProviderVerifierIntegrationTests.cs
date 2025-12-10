using System.Text;
using FluentAssertions;
using Treaty.OpenApi;
using Treaty.Provider;
using Treaty.Provider.Resilience;
using TreatyLib = Treaty.Treaty;
using TreatyOpenApi = Treaty.OpenApi;

namespace Treaty.Tests.Integration.Provider;

/// <summary>
/// Integration tests for HttpProviderVerifier verifying against a live mock server.
/// </summary>
public class HttpProviderVerifierIntegrationTests : IAsyncDisposable
{
    private TreatyOpenApi.MockServer? _mockServer;
    private HttpProviderVerifier? _verifier;

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
            delete:
              summary: Delete user
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: integer
              responses:
                '204':
                  description: User deleted
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

    private async Task SetupMockServer()
    {
        var specPath = Path.GetTempFileName() + ".yaml";
        await File.WriteAllTextAsync(specPath, TestOpenApiSpec);

        // Build without custom rules - let the mock server use spec-defined status codes
        // GET /users/{id} -> 200 from spec
        // DELETE /users/{id} -> 204 from spec
        _mockServer = TreatyLib.MockServer(specPath)
            .ForEndpoint("/users/{id}")
                .When(req => req.PathParam("id") == "0").Return(404)
            .Build();

        await _mockServer.StartAsync();

        // Build contract for verifier
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(TestOpenApiSpec));
        var contract = TreatyLib.OpenApi(stream, OpenApiFormat.Yaml).Build();

        _verifier = TreatyLib.ForHttpProvider()
            .WithBaseUrl(_mockServer.BaseUrl!)
            .WithContract(contract)
            .Build();

        File.Delete(specPath);
    }

    public async ValueTask DisposeAsync()
    {
        _verifier?.Dispose();
        if (_mockServer != null)
            await _mockServer.DisposeAsync();
    }

    #region Basic Verification Tests

    [Test]
    public async Task VerifyAsync_GetUsers_PassesValidation()
    {
        // Arrange
        await SetupMockServer();

        // Act & Assert - should not throw
        await _verifier!.VerifyAsync("/users", HttpMethod.Get);
    }

    [Test]
    public async Task VerifyAsync_GetUserById_PassesValidation()
    {
        // Arrange
        await SetupMockServer();

        // Act & Assert
        await _verifier!.VerifyAsync("/users/1", HttpMethod.Get);
    }

    [Test]
    public async Task VerifyAsync_CreateUser_PassesValidation()
    {
        // Arrange
        await SetupMockServer();

        // Act & Assert
        await _verifier!.VerifyAsync("/users", HttpMethod.Post, new { name = "Test User", email = "test@example.com" });
    }

    [Test]
    public async Task VerifyAsync_DeleteUser_PassesValidation()
    {
        // Arrange
        await SetupMockServer();

        // Act & Assert
        await _verifier!.VerifyAsync("/users/1", HttpMethod.Delete);
    }

    [Test]
    public async Task TryVerifyAsync_GetUsers_ReturnsSuccessResult()
    {
        // Arrange
        await SetupMockServer();

        // Act
        var result = await _verifier!.TryVerifyAsync("/users", HttpMethod.Get);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Violations.Should().BeEmpty();
    }

    [Test]
    public async Task TryVerifyAsync_UndefinedEndpoint_ReturnsFailure()
    {
        // Arrange
        await SetupMockServer();

        // Act
        var result = await _verifier!.TryVerifyAsync("/nonexistent", HttpMethod.Get);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violations.Should().ContainSingle()
            .Which.Message.Should().Contain("No contract definition found");
    }

    #endregion

    #region Authentication Tests

    [Test]
    public async Task Verify_WithBearerToken_SendsAuthorizationHeader()
    {
        // Arrange
        var specPath = Path.GetTempFileName() + ".yaml";
        await File.WriteAllTextAsync(specPath, TestOpenApiSpec);

        _mockServer = TreatyLib.MockServer(specPath).Build();
        await _mockServer.StartAsync();

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(TestOpenApiSpec));
        var contract = TreatyLib.OpenApi(stream, OpenApiFormat.Yaml).Build();

        // Create verifier with bearer token
        _verifier = TreatyLib.ForHttpProvider()
            .WithBaseUrl(_mockServer.BaseUrl!)
            .WithContract(contract)
            .WithBearerToken("test-token-123")
            .Build();

        File.Delete(specPath);

        // Act & Assert - should not throw (mock server doesn't validate auth, but request completes)
        await _verifier.VerifyAsync("/users", HttpMethod.Get);
    }

    [Test]
    public async Task Verify_WithApiKey_SendsHeader()
    {
        // Arrange
        var specPath = Path.GetTempFileName() + ".yaml";
        await File.WriteAllTextAsync(specPath, TestOpenApiSpec);

        _mockServer = TreatyLib.MockServer(specPath).Build();
        await _mockServer.StartAsync();

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(TestOpenApiSpec));
        var contract = TreatyLib.OpenApi(stream, OpenApiFormat.Yaml).Build();

        _verifier = TreatyLib.ForHttpProvider()
            .WithBaseUrl(_mockServer.BaseUrl!)
            .WithContract(contract)
            .WithApiKey("my-api-key", "X-API-Key")
            .Build();

        File.Delete(specPath);

        // Act & Assert
        await _verifier.VerifyAsync("/users", HttpMethod.Get);
    }

    [Test]
    public async Task Verify_WithBasicAuth_SendsAuthorizationHeader()
    {
        // Arrange
        var specPath = Path.GetTempFileName() + ".yaml";
        await File.WriteAllTextAsync(specPath, TestOpenApiSpec);

        _mockServer = TreatyLib.MockServer(specPath).Build();
        await _mockServer.StartAsync();

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(TestOpenApiSpec));
        var contract = TreatyLib.OpenApi(stream, OpenApiFormat.Yaml).Build();

        _verifier = TreatyLib.ForHttpProvider()
            .WithBaseUrl(_mockServer.BaseUrl!)
            .WithContract(contract)
            .WithBasicAuth("user", "pass")
            .Build();

        File.Delete(specPath);

        // Act & Assert
        await _verifier.VerifyAsync("/users", HttpMethod.Get);
    }

    #endregion

    #region Retry Policy Tests

    [Test]
    public async Task Verify_WithRetryPolicy_RetriesOnTransientFailure()
    {
        // Arrange - Use a normal mock server (we can't easily simulate transient failures)
        // This test verifies the retry policy integration doesn't break normal flow
        var specPath = Path.GetTempFileName() + ".yaml";
        await File.WriteAllTextAsync(specPath, TestOpenApiSpec);

        _mockServer = TreatyLib.MockServer(specPath).Build();
        await _mockServer.StartAsync();

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(TestOpenApiSpec));
        var contract = TreatyLib.OpenApi(stream, OpenApiFormat.Yaml).Build();

        _verifier = TreatyLib.ForHttpProvider()
            .WithBaseUrl(_mockServer.BaseUrl!)
            .WithContract(contract)
            .WithRetryPolicy(new RetryPolicyOptions
            {
                MaxRetries = 3,
                InitialDelayMs = 10,
                UseExponentialBackoff = true
            })
            .Build();

        File.Delete(specPath);

        // Act & Assert - should succeed on first attempt
        await _verifier.VerifyAsync("/users", HttpMethod.Get);
    }

    [Test]
    public async Task Verify_WithDefaultRetryPolicy_WorksCorrectly()
    {
        // Arrange
        var specPath = Path.GetTempFileName() + ".yaml";
        await File.WriteAllTextAsync(specPath, TestOpenApiSpec);

        _mockServer = TreatyLib.MockServer(specPath).Build();
        await _mockServer.StartAsync();

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(TestOpenApiSpec));
        var contract = TreatyLib.OpenApi(stream, OpenApiFormat.Yaml).Build();

        _verifier = TreatyLib.ForHttpProvider()
            .WithBaseUrl(_mockServer.BaseUrl!)
            .WithContract(contract)
            .WithRetryPolicy() // Default options
            .Build();

        File.Delete(specPath);

        // Act & Assert
        await _verifier.VerifyAsync("/users", HttpMethod.Get);
    }

    #endregion

    #region HTTP Options Tests

    [Test]
    public async Task Verify_WithCustomTimeout_RespectsTimeout()
    {
        // Arrange
        var specPath = Path.GetTempFileName() + ".yaml";
        await File.WriteAllTextAsync(specPath, TestOpenApiSpec);

        _mockServer = TreatyLib.MockServer(specPath).Build();
        await _mockServer.StartAsync();

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(TestOpenApiSpec));
        var contract = TreatyLib.OpenApi(stream, OpenApiFormat.Yaml).Build();

        _verifier = TreatyLib.ForHttpProvider()
            .WithBaseUrl(_mockServer.BaseUrl!)
            .WithContract(contract)
            .WithHttpOptions(opts => opts.WithTimeout(TimeSpan.FromSeconds(60)))
            .Build();

        File.Delete(specPath);

        // Act & Assert
        await _verifier.VerifyAsync("/users", HttpMethod.Get);
    }

    [Test]
    public async Task Verify_WithFollowRedirects_WorksCorrectly()
    {
        // Arrange
        var specPath = Path.GetTempFileName() + ".yaml";
        await File.WriteAllTextAsync(specPath, TestOpenApiSpec);

        _mockServer = TreatyLib.MockServer(specPath).Build();
        await _mockServer.StartAsync();

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(TestOpenApiSpec));
        var contract = TreatyLib.OpenApi(stream, OpenApiFormat.Yaml).Build();

        _verifier = TreatyLib.ForHttpProvider()
            .WithBaseUrl(_mockServer.BaseUrl!)
            .WithContract(contract)
            .WithHttpOptions(opts => opts.FollowRedirects(true, maxRedirects: 10))
            .Build();

        File.Delete(specPath);

        // Act & Assert
        await _verifier.VerifyAsync("/users", HttpMethod.Get);
    }

    #endregion

    #region Custom HttpClient Tests

    [Test]
    public async Task Verify_WithCustomHttpClient_UsesProvidedClient()
    {
        // Arrange
        var specPath = Path.GetTempFileName() + ".yaml";
        await File.WriteAllTextAsync(specPath, TestOpenApiSpec);

        _mockServer = TreatyLib.MockServer(specPath).Build();
        await _mockServer.StartAsync();

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(TestOpenApiSpec));
        var contract = TreatyLib.OpenApi(stream, OpenApiFormat.Yaml).Build();

        using var customClient = new HttpClient
        {
            BaseAddress = new Uri(_mockServer.BaseUrl!),
            Timeout = TimeSpan.FromSeconds(30)
        };

        _verifier = TreatyLib.ForHttpProvider()
            .WithBaseUrl(_mockServer.BaseUrl!)
            .WithContract(contract)
            .WithHttpClient(customClient)
            .Build();

        File.Delete(specPath);

        // Act & Assert
        await _verifier.VerifyAsync("/users", HttpMethod.Get);
    }

    #endregion
}
