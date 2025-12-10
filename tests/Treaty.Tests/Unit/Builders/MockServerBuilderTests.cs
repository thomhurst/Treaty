using System.Net;
using FluentAssertions;
using TreatyLib = Treaty.Treaty;
using TreatyOpenApi = Treaty.OpenApi;

namespace Treaty.Tests.Unit.Builders;

public class MockServerBuilderTests : IAsyncDisposable
{
    private TreatyOpenApi.MockServer? _mockServer;
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
                          correlationId:
                            type: string
                '401':
                  description: Unauthorized
                '404':
                  description: Not found
        """;

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
    public async Task ForEndpoint_When_Return_ConfiguresConditionalResponse_404()
    {
        // Arrange
        var specPath = await WriteSpecToTempFile();

        _mockServer = TreatyLib.MockServer(specPath)
            .ForEndpoint("/users/{id}")
                .When(req => req.PathParam("id") == "0").Return(404)
                .Otherwise().Return(200)
            .Build();

        await _mockServer.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_mockServer.BaseUrl!) };

        File.Delete(specPath);

        // Act
        var response = await _client.GetAsync("/users/0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task ForEndpoint_Otherwise_Return_ConfiguresDefaultResponse()
    {
        // Arrange
        var specPath = await WriteSpecToTempFile();

        _mockServer = TreatyLib.MockServer(specPath)
            .ForEndpoint("/users/{id}")
                .When(req => req.PathParam("id") == "0").Return(404)
                .Otherwise().Return(200)
            .Build();

        await _mockServer.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_mockServer.BaseUrl!) };

        File.Delete(specPath);

        // Act
        var response = await _client.GetAsync("/users/123");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task WithAuth_RequireHeader_MissingHeader_Returns401()
    {
        // Arrange
        var specPath = await WriteSpecToTempFile();

        _mockServer = TreatyLib.MockServer(specPath)
            .WithAuth(auth => auth
                .RequireHeader("Authorization")
                .WhenMissing().Return(401))
            .Build();

        await _mockServer.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_mockServer.BaseUrl!) };

        File.Delete(specPath);

        // Act
        var response = await _client.GetAsync("/users/123");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task WithAuth_RequireHeader_WithHeader_Returns200()
    {
        // Arrange
        var specPath = await WriteSpecToTempFile();

        _mockServer = TreatyLib.MockServer(specPath)
            .WithAuth(auth => auth
                .RequireHeader("Authorization")
                .WhenMissing().Return(401))
            .Build();

        await _mockServer.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_mockServer.BaseUrl!) };
        _client.DefaultRequestHeaders.Add("Authorization", "Bearer token123");

        File.Delete(specPath);

        // Act
        var response = await _client.GetAsync("/users/123");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task WithCustomGenerator_GeneratesCustomValue()
    {
        // Arrange
        var specPath = await WriteSpecToTempFile();
        var customCorrelationId = "custom-correlation-12345";

        _mockServer = TreatyLib.MockServer(specPath)
            .WithCustomGenerator("correlationId", () => customCorrelationId)
            .Build();

        await _mockServer.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_mockServer.BaseUrl!) };

        File.Delete(specPath);

        // Act
        var response = await _client.GetAsync("/users/123");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain(customCorrelationId);
    }

    [Test]
    public async Task MockServer_MultipleConditions_EvaluatesInOrder()
    {
        // Arrange
        var specPath = await WriteSpecToTempFile();

        _mockServer = TreatyLib.MockServer(specPath)
            .ForEndpoint("/users/{id}")
                .When(req => req.PathParam("id") == "0").Return(404)
                .When(req => req.PathParam("id") == "401").Return(401)
                .When(req => req.PathParam("id")!.StartsWith("x")).Return(404)
                .Otherwise().Return(200)
            .Build();

        await _mockServer.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_mockServer.BaseUrl!) };

        File.Delete(specPath);

        // Act & Assert
        var response1 = await _client.GetAsync("/users/0");
        response1.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var response2 = await _client.GetAsync("/users/401");
        response2.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var response3 = await _client.GetAsync("/users/xyz");
        response3.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var response4 = await _client.GetAsync("/users/123");
        response4.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task MockRequestContext_QueryParam_ReturnsValue()
    {
        // Arrange
        var specWithQuery = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /search:
                get:
                  parameters:
                    - name: q
                      in: query
                      schema:
                        type: string
                  responses:
                    '200':
                      description: Results
                    '400':
                      description: Bad request
            """;

        var specPath = Path.GetTempFileName() + ".yaml";
        await File.WriteAllTextAsync(specPath, specWithQuery);

        _mockServer = TreatyLib.MockServer(specPath)
            .ForEndpoint("/search")
                .When(req => string.IsNullOrEmpty(req.QueryParam("q"))).Return(400)
                .Otherwise().Return(200)
            .Build();

        await _mockServer.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_mockServer.BaseUrl!) };

        File.Delete(specPath);

        // Act & Assert
        var response1 = await _client.GetAsync("/search");
        response1.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var response2 = await _client.GetAsync("/search?q=test");
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task MockRequestContext_Header_ReturnsValue()
    {
        // Arrange
        var specPath = await WriteSpecToTempFile();

        _mockServer = TreatyLib.MockServer(specPath)
            .ForEndpoint("/users/{id}")
                .When(req => req.Header("X-Admin") == "true").Return(200)
                .Otherwise().Return(404)
            .Build();

        await _mockServer.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_mockServer.BaseUrl!) };

        File.Delete(specPath);

        // Act & Assert
        var response1 = await _client.GetAsync("/users/123");
        response1.StatusCode.Should().Be(HttpStatusCode.NotFound);

        _client.DefaultRequestHeaders.Add("X-Admin", "true");
        var response2 = await _client.GetAsync("/users/123");
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<string> WriteSpecToTempFile()
    {
        var specPath = Path.GetTempFileName() + ".yaml";
        await File.WriteAllTextAsync(specPath, TestOpenApiSpec);
        return specPath;
    }
}
