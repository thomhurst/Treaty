using FluentAssertions;
using Treaty.Contracts;
using TreatyLib = Treaty.Treaty;

namespace Treaty.Tests.Unit.Contracts;

public class ContractBuilderTests
{
    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
    public void DefineContract_WithoutName_UsesDefaultName()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Assert
        contract.Name.Should().Be("Contract");
    }

    private record TestUser(int Id, string Name, string Email);
    private record CreateUserRequest(string Name, string Email);
}
