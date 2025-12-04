using System.Net;
using System.Text.Json;
using FluentAssertions;
using Treaty.Matching;

namespace Treaty.Tests.Integration;

public class ContractMockServerTests
{
    [Test]
    public async Task MockFromContract_GetUsers_ReturnsGeneratedResponse()
    {
        // Arrange
        var contract = Treaty.DefineContract("Users API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithJsonBody<User[]>())
            .Build();

        await using var mockServer = Treaty.MockFromContract(contract).Build();
        await mockServer.StartAsync();

        using var httpClient = new HttpClient { BaseAddress = new Uri(mockServer.BaseUrl!) };

        // Act
        var response = await httpClient.GetAsync("/users");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().NotBeNullOrEmpty();
        var users = JsonSerializer.Deserialize<User[]>(content);
        users.Should().NotBeNull();
    }

    [Test]
    public async Task MockFromContract_GetUserById_ReturnsGeneratedUser()
    {
        // Arrange
        var contract = Treaty.DefineContract("Users API")
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithJsonBody<User>())
            .Build();

        await using var mockServer = Treaty.MockFromContract(contract).Build();
        await mockServer.StartAsync();

        using var httpClient = new HttpClient { BaseAddress = new Uri(mockServer.BaseUrl!) };

        // Act
        var response = await httpClient.GetAsync("/users/123");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = JsonSerializer.Deserialize<User>(content);
        user.Should().NotBeNull();
    }

    [Test]
    public async Task MockFromContract_WithMatcherSchema_ReturnsValidSample()
    {
        // Arrange
        var contract = Treaty.DefineContract("Users API")
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithMatcherSchema(new
                    {
                        id = Match.Guid(),
                        name = Match.NonEmptyString(),
                        email = Match.Email(),
                        active = Match.Boolean()
                    }))
            .Build();

        await using var mockServer = Treaty.MockFromContract(contract).Build();
        await mockServer.StartAsync();

        using var httpClient = new HttpClient { BaseAddress = new Uri(mockServer.BaseUrl!) };

        // Act
        var response = await httpClient.GetAsync("/users/abc");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("id");
        content.Should().Contain("name");
        content.Should().Contain("email");
        content.Should().Contain("active");
    }

    [Test]
    public async Task MockFromContract_UndefinedEndpoint_Returns404WithDetails()
    {
        // Arrange
        var contract = Treaty.DefineContract("Users API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        await using var mockServer = Treaty.MockFromContract(contract).Build();
        await mockServer.StartAsync();

        using var httpClient = new HttpClient { BaseAddress = new Uri(mockServer.BaseUrl!) };

        // Act
        var response = await httpClient.GetAsync("/products");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        content.Should().Contain("treaty_error");
        content.Should().Contain("available_endpoints");
    }

    [Test]
    public async Task MockFromContract_WithConditions_Returns404ForInvalidId()
    {
        // Arrange
        var contract = Treaty.DefineContract("Users API")
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithJsonBody<User>())
            .Build();

        await using var mockServer = Treaty.MockFromContract(contract)
            .ForEndpoint("/users/{id}")
                .When(req => req.PathParam("id") == "0").Return(404)
                .Otherwise().Return(200)
            .Build();

        await mockServer.StartAsync();

        using var httpClient = new HttpClient { BaseAddress = new Uri(mockServer.BaseUrl!) };

        // Act
        var notFoundResponse = await httpClient.GetAsync("/users/0");
        var okResponse = await httpClient.GetAsync("/users/123");

        // Assert
        notFoundResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        okResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task MockFromContract_WithConditions_ReturnsCustomBody()
    {
        // Arrange
        var contract = Treaty.DefineContract("Users API")
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithJsonBody<User>())
            .Build();

        var customUser = new User { Id = 999, Name = "Custom User", Email = "custom@test.com" };

        await using var mockServer = Treaty.MockFromContract(contract)
            .ForEndpoint("/users/{id}")
                .When(req => req.PathParam("id") == "special").Return(200, customUser)
            .Build();

        await mockServer.StartAsync();

        using var httpClient = new HttpClient { BaseAddress = new Uri(mockServer.BaseUrl!) };

        // Act
        var response = await httpClient.GetAsync("/users/special");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("Custom User");
        content.Should().Contain("999");
    }

    [Test]
    public async Task MockFromContract_WithAuth_RequireHeader_MissingHeader_Returns401()
    {
        // Arrange
        var contract = Treaty.DefineContract("Users API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        await using var mockServer = Treaty.MockFromContract(contract)
            .WithAuth(auth => auth.RequireHeader("Authorization").WhenMissing().Return(401))
            .Build();

        await mockServer.StartAsync();

        using var httpClient = new HttpClient { BaseAddress = new Uri(mockServer.BaseUrl!) };

        // Act
        var response = await httpClient.GetAsync("/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task MockFromContract_WithAuth_WithHeader_Returns200()
    {
        // Arrange
        var contract = Treaty.DefineContract("Users API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        await using var mockServer = Treaty.MockFromContract(contract)
            .WithAuth(auth => auth.RequireHeader("Authorization").WhenMissing().Return(401))
            .Build();

        await mockServer.StartAsync();

        using var httpClient = new HttpClient { BaseAddress = new Uri(mockServer.BaseUrl!) };
        httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer token");

        // Act
        var response = await httpClient.GetAsync("/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task MockFromContract_WithLatency_DelaysResponse()
    {
        // Arrange
        var contract = Treaty.DefineContract("Users API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        await using var mockServer = Treaty.MockFromContract(contract)
            .WithLatency(100, 200)
            .Build();

        await mockServer.StartAsync();

        using var httpClient = new HttpClient { BaseAddress = new Uri(mockServer.BaseUrl!) };

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await httpClient.GetAsync("/users");
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeGreaterOrEqualTo(100);
    }

    [Test]
    public async Task MockFromContract_WithCustomGenerator_UsesGeneratedValue()
    {
        // Arrange
        var customId = "custom-generated-id";
        var contract = Treaty.DefineContract("Users API")
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithMatcherSchema(new
                    {
                        correlationId = Match.Guid(),
                        name = Match.NonEmptyString()
                    }))
            .Build();

        await using var mockServer = Treaty.MockFromContract(contract)
            .WithCustomGenerator("correlationId", () => customId)
            .Build();

        await mockServer.StartAsync();

        using var httpClient = new HttpClient { BaseAddress = new Uri(mockServer.BaseUrl!) };

        // Act
        var response = await httpClient.GetAsync("/users/123");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain(customId);
    }

    [Test]
    public async Task MockFromContract_PostEndpoint_Returns201()
    {
        // Arrange
        var contract = Treaty.DefineContract("Users API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingResponse(r => r
                    .WithStatus(201)
                    .WithJsonBody<User>())
            .Build();

        await using var mockServer = Treaty.MockFromContract(contract).Build();
        await mockServer.StartAsync();

        using var httpClient = new HttpClient { BaseAddress = new Uri(mockServer.BaseUrl!) };
        var content = new StringContent("{\"name\":\"Test\"}", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.PostAsync("/users", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Test]
    public async Task MockFromContract_BaseUrl_IsSet()
    {
        // Arrange
        var contract = Treaty.DefineContract("Test API")
            .ForEndpoint("/test")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        await using var mockServer = Treaty.MockFromContract(contract).Build();

        // Act
        await mockServer.StartAsync();

        // Assert
        mockServer.BaseUrl.Should().NotBeNull();
        mockServer.BaseUrl.Should().StartWith("http://");
    }

    private class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
