using FluentAssertions;
using TreatyLib = Treaty.Treaty;

namespace Treaty.Tests.Unit.Builders;

public class ResponseExpectationBuilderTests
{
    [Test]
    public void WithStatus_SetsStatusCode()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.ResponseExpectations.Should().ContainSingle()
            .Which.StatusCode.Should().Be(200);
    }

    [Test]
    public void WithStatus_MultipleResponses_AllCaptured()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200).WithJsonBody<TestUser>())
                .ExpectingResponse(r => r.WithStatus(404))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.ResponseExpectations.Should().HaveCount(2);
        endpoint.ResponseExpectations[0].StatusCode.Should().Be(200);
        endpoint.ResponseExpectations[1].StatusCode.Should().Be(404);
    }

    [Test]
    public void WithContentType_SetsContentType()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200).WithContentType("application/xml"))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.ResponseExpectations[0].ContentType.Should().Be("application/xml");
    }

    [Test]
    public void WithJsonBody_Generic_SetsBodyType()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200).WithJsonBody<TestUser[]>())
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.ResponseExpectations[0].BodyValidator.Should().NotBeNull();
        endpoint.ResponseExpectations[0].BodyValidator!.ExpectedType.Should().Be(typeof(TestUser[]));
    }

    [Test]
    public void WithJsonBody_Type_SetsBodyType()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200).WithJsonBody(typeof(TestUser[])))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.ResponseExpectations[0].BodyValidator.Should().NotBeNull();
        endpoint.ResponseExpectations[0].BodyValidator!.ExpectedType.Should().Be(typeof(TestUser[]));
    }

    [Test]
    public void WithJsonBody_SetsDefaultContentType()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200).WithJsonBody<TestUser[]>())
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.ResponseExpectations[0].ContentType.Should().Be("application/json");
    }

    [Test]
    public void WithHeader_AddsHeaderExpectation()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithHeader("X-Request-Id"))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.ResponseExpectations[0].ExpectedHeaders.Should().ContainKey("X-Request-Id");
        endpoint.ResponseExpectations[0].ExpectedHeaders["X-Request-Id"].IsRequired.Should().BeTrue();
    }

    [Test]
    public void WithHeader_WithValue_AddsHeaderWithExactValue()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithHeader("Content-Type", "application/json"))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.ResponseExpectations[0].ExpectedHeaders.Should().ContainKey("Content-Type");
        endpoint.ResponseExpectations[0].ExpectedHeaders["Content-Type"].ExactValue.Should().Be("application/json");
    }

    [Test]
    public void WithJsonBody_WithPartialValidation_OnlyValidate_ConfiguresCorrectly()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithJsonBody<TestUser>(v => v
                        .OnlyValidate(u => u.Id, u => u.Email)))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.ResponseExpectations[0].PartialValidation.Should().NotBeNull();
        endpoint.ResponseExpectations[0].PartialValidation!.PropertiesToValidate.Should().Contain("Id");
        endpoint.ResponseExpectations[0].PartialValidation.PropertiesToValidate.Should().Contain("Email");
    }

    [Test]
    public void WithJsonBody_WithPartialValidation_IgnoreExtraFields_ConfiguresCorrectly()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithJsonBody<TestUser>(v => v.IgnoreExtraFields()))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.ResponseExpectations[0].PartialValidation.Should().NotBeNull();
        endpoint.ResponseExpectations[0].PartialValidation!.IgnoreExtraFields.Should().BeTrue();
    }

    [Test]
    public void NoBody_BodyValidatorIsNull()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Delete)
                .ExpectingResponse(r => r.WithStatus(204))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.ResponseExpectations[0].BodyValidator.Should().BeNull();
    }

    private record TestUser(int Id, string Name, string Email);
}
