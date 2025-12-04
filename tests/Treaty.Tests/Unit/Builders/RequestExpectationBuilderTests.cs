using FluentAssertions;
using Treaty.Serialization;
using TreatyLib = Treaty.Treaty;

namespace Treaty.Tests.Unit.Builders;

public class RequestExpectationBuilderTests
{
    [Test]
    public void WithJsonBody_Generic_SetsBodyType()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingRequest(req => req.WithJsonBody<CreateUserRequest>())
                .ExpectingResponse(r => r.WithStatus(201))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.RequestExpectation.Should().NotBeNull();
        endpoint.RequestExpectation!.BodyValidator.Should().NotBeNull();
        endpoint.RequestExpectation.BodyValidator!.ExpectedType.Should().Be(typeof(CreateUserRequest));
    }

    [Test]
    public void WithJsonBody_Type_SetsBodyType()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingRequest(req => req.WithJsonBody(typeof(CreateUserRequest)))
                .ExpectingResponse(r => r.WithStatus(201))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.RequestExpectation.Should().NotBeNull();
        endpoint.RequestExpectation!.BodyValidator.Should().NotBeNull();
        endpoint.RequestExpectation.BodyValidator!.ExpectedType.Should().Be(typeof(CreateUserRequest));
    }

    [Test]
    public void WithContentType_SetsContentType()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/upload")
                .WithMethod(HttpMethod.Post)
                .ExpectingRequest(req => req.WithContentType("multipart/form-data"))
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.RequestExpectation.Should().NotBeNull();
        endpoint.RequestExpectation!.ContentType.Should().Be("multipart/form-data");
    }

    [Test]
    public void Optional_SetsIsRequiredFalse()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingRequest(req => req.WithJsonBody<CreateUserRequest>().Optional())
                .ExpectingResponse(r => r.WithStatus(201))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.RequestExpectation.Should().NotBeNull();
        endpoint.RequestExpectation!.IsRequired.Should().BeFalse();
    }

    [Test]
    public void Default_IsRequiredTrue()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingRequest(req => req.WithJsonBody<CreateUserRequest>())
                .ExpectingResponse(r => r.WithStatus(201))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.RequestExpectation.Should().NotBeNull();
        endpoint.RequestExpectation!.IsRequired.Should().BeTrue();
    }

    [Test]
    public void WithJsonBody_SetsDefaultContentType()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingRequest(req => req.WithJsonBody<CreateUserRequest>())
                .ExpectingResponse(r => r.WithStatus(201))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.RequestExpectation!.ContentType.Should().Be("application/json");
    }

    [Test]
    public void NoExpectingRequest_RequestExpectationIsNull()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.RequestExpectation.Should().BeNull();
    }

    private record CreateUserRequest(string Name, string Email);
}
