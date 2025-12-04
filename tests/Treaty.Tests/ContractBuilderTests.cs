using FluentAssertions;
using Treaty.Contracts;
using Xunit;
using TreatyLib = Treaty.Treaty;

namespace Treaty.Tests;

public class ContractBuilderTests
{
    [Fact]
    public void DefineContract_WithSimpleEndpoint_CreatesContract()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract("TestContract")
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithJsonBody<TestUser>())
            .Build();

        // Assert
        contract.Name.Should().Be("TestContract");
        contract.Endpoints.Should().HaveCount(1);

        var endpoint = contract.Endpoints[0];
        endpoint.PathTemplate.Should().Be("/users/{id}");
        endpoint.Method.Should().Be(HttpMethod.Get);
        endpoint.ResponseExpectations.Should().HaveCount(1);
        endpoint.ResponseExpectations[0].StatusCode.Should().Be(200);
    }

    [Fact]
    public void DefineContract_WithMultipleEndpoints_CreatesContract()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200).WithJsonBody<TestUser[]>())
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingRequest(req => req.WithJsonBody<CreateUserRequest>())
                .ExpectingResponse(r => r.WithStatus(201).WithJsonBody<TestUser>())
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Delete)
                .ExpectingResponse(r => r.WithStatus(204))
            .Build();

        // Assert
        contract.Endpoints.Should().HaveCount(3);
    }

    [Fact]
    public void DefineContract_WithDefaults_AppliesDefaults()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .WithDefaults(d => d
                .AllResponsesHaveHeader("X-Request-Id")
                .AllRequestsHaveHeader("Authorization"))
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Assert
        contract.Defaults.Should().NotBeNull();
        contract.Defaults!.ResponseHeaders.Should().ContainKey("X-Request-Id");
        contract.Defaults.RequestHeaders.Should().ContainKey("Authorization");
    }

    [Fact]
    public void EndpointContract_Matches_WithExactPath()
    {
        // Arrange
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Act & Assert
        var endpoint = contract.FindEndpoint("/users", HttpMethod.Get);
        endpoint.Should().NotBeNull();
        endpoint!.Matches("/users", HttpMethod.Get).Should().BeTrue();
        endpoint.Matches("/users", HttpMethod.Post).Should().BeFalse();
        endpoint.Matches("/other", HttpMethod.Get).Should().BeFalse();
    }

    [Fact]
    public void EndpointContract_Matches_WithPathParameter()
    {
        // Arrange
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Act & Assert
        var endpoint = contract.FindEndpoint("/users/123", HttpMethod.Get);
        endpoint.Should().NotBeNull();
        endpoint!.Matches("/users/123", HttpMethod.Get).Should().BeTrue();
        endpoint.Matches("/users/abc", HttpMethod.Get).Should().BeTrue();
        endpoint.Matches("/users/", HttpMethod.Get).Should().BeFalse();
    }

    [Fact]
    public void EndpointContract_ExtractPathParameters_ReturnsValues()
    {
        // Arrange
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users/{userId}/posts/{postId}")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        var endpoint = contract.FindEndpoint("/users/123/posts/456", HttpMethod.Get);

        // Act
        var pathParams = endpoint!.ExtractPathParameters("/users/123/posts/456");

        // Assert
        pathParams.Should().ContainKey("userId").WhoseValue.Should().Be("123");
        pathParams.Should().ContainKey("postId").WhoseValue.Should().Be("456");
    }

    [Fact]
    public void DefineContract_WithHeaders_SetsHeaders()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .WithHeader("Accept", "application/json")
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithHeader("Content-Type", "application/json"))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.ExpectedHeaders.Should().ContainKey("Accept");
        endpoint.ResponseExpectations[0].ExpectedHeaders.Should().ContainKey("Content-Type");
    }

    [Fact]
    public void DefineContract_WithQueryParams_SetsQueryParams()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .WithQueryParam("page", QueryParameterType.Integer)
                .WithOptionalQueryParam("limit", QueryParameterType.Integer)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.ExpectedQueryParameters.Should().ContainKey("page");
        endpoint.ExpectedQueryParameters["page"].IsRequired.Should().BeTrue();
        endpoint.ExpectedQueryParameters.Should().ContainKey("limit");
        endpoint.ExpectedQueryParameters["limit"].IsRequired.Should().BeFalse();
    }

    private record TestUser(int Id, string Name, string Email);
    private record CreateUserRequest(string Name, string Email);
}
