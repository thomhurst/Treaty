using System.Text;
using FluentAssertions;
using Treaty.OpenApi;
using Treaty.Provider;
using Treaty.Tests.TestApi;

namespace Treaty.Tests.Integration.Provider;

public class ProviderVerifierTests : IDisposable
{
    private ProviderVerifier<TestStartup> _provider = null!;

    private const string TestApiSpec = """
        openapi: '3.0.3'
        info:
          title: TestApi
          version: '1.0'
        paths:
          /users:
            get:
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
              requestBody:
                required: true
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/CreateUserRequest'
              responses:
                '201':
                  description: User created
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/User'
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
                  description: User details
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/User'
            delete:
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: string
              responses:
                '204':
                  description: User deleted
        components:
          schemas:
            User:
              type: object
              properties:
                id:
                  type: integer
                name:
                  type: string
                email:
                  type: string
            CreateUserRequest:
              type: object
              properties:
                name:
                  type: string
                email:
                  type: string
        """;

    [Before(Test)]
    public async Task Setup()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(TestApiSpec));
        var contract = await Contract.FromOpenApi(stream, OpenApiFormat.Yaml).BuildAsync();

        _provider = ProviderVerifier.ForWebApplication<TestStartup>()
            .WithContract(contract)
            .BuildAsync();
    }

    [Test]
    public async Task VerifyAsync_GetUsers_PassesValidation()
    {
        // Act & Assert - should not throw
        await _provider.VerifyAsync("/users", HttpMethod.Get);
    }

    [Test]
    public async Task VerifyAsync_GetUserById_PassesValidation()
    {
        // Act & Assert - should not throw
        await _provider.VerifyAsync("/users/1", HttpMethod.Get);
    }

    [Test]
    public async Task VerifyAsync_CreateUser_PassesValidation()
    {
        // Act & Assert - should not throw
        await _provider.VerifyAsync("/users", HttpMethod.Post, new { name = "Test User", email = "test@example.com" });
    }

    [Test]
    public async Task VerifyAsync_DeleteUser_PassesValidation()
    {
        // Act & Assert - should not throw
        await _provider.VerifyAsync("/users/1", HttpMethod.Delete);
    }

    [Test]
    public async Task TryVerifyAsync_GetUsers_ReturnsSuccessResult()
    {
        // Act
        var result = await _provider.TryVerifyAsync("/users", HttpMethod.Get);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Violations.Should().BeEmpty();
    }

    [Test]
    public async Task TryVerifyAsync_GetUserById_ReturnsSuccessResult()
    {
        // Act
        var result = await _provider.TryVerifyAsync("/users/123", HttpMethod.Get);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Test]
    public async Task TryVerifyAsync_UndefinedEndpoint_ReturnsFailure()
    {
        // Act
        var result = await _provider.TryVerifyAsync("/nonexistent", HttpMethod.Get);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violations.Should().ContainSingle()
            .Which.Message.Should().Contain("No contract definition found");
    }

    [Test]
    public async Task VerifyAsync_WithPathParameter_ExtractsParameterCorrectly()
    {
        // Act & Assert - different path parameter values should work
        await _provider.VerifyAsync("/users/1", HttpMethod.Get);
        await _provider.VerifyAsync("/users/100", HttpMethod.Get);
        await _provider.VerifyAsync("/users/abc", HttpMethod.Get);
    }

    [Test]
    public async Task VerifyAsync_WithHttpRequestMessage_GetUsers_PassesValidation()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/users");

        // Act & Assert - should not throw
        await _provider.VerifyAsync(request);
    }

    [Test]
    public async Task VerifyAsync_WithHttpRequestMessage_CreateUser_PassesValidation()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/users")
        {
            Content = new StringContent(
                """{"name": "Test User", "email": "test@example.com"}""",
                Encoding.UTF8,
                "application/json")
        };

        // Act & Assert - should not throw
        await _provider.VerifyAsync(request);
    }

    [Test]
    public async Task TryVerifyAsync_WithHttpRequestMessage_ReturnsSuccessResult()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/users/1");

        // Act
        var result = await _provider.TryVerifyAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Violations.Should().BeEmpty();
    }

    [Test]
    public async Task TryVerifyAsync_WithHttpRequestMessage_UndefinedEndpoint_ReturnsFailure()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/nonexistent");

        // Act
        var result = await _provider.TryVerifyAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violations.Should().ContainSingle()
            .Which.Message.Should().Contain("No contract definition found");
    }

    public void Dispose()
    {
        _provider.Dispose();
    }
}
