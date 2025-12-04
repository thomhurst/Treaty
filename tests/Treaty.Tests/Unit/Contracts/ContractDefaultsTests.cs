using FluentAssertions;
using TreatyLib = Treaty.Treaty;

namespace Treaty.Tests.Unit.Contracts;

public class ContractDefaultsTests
{
    [Test]
    public void AllResponsesHaveHeader_AddsHeaderExpectation()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .WithDefaults(d => d.AllResponsesHaveHeader("X-Request-Id"))
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Assert
        contract.Defaults.Should().NotBeNull();
        contract.Defaults!.ResponseHeaders.Should().ContainKey("X-Request-Id");
        contract.Defaults.ResponseHeaders["X-Request-Id"].IsRequired.Should().BeTrue();
    }

    [Test]
    public void AllResponsesHaveHeader_WithValue_AddsHeaderWithExactValue()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .WithDefaults(d => d.AllResponsesHaveHeader("Content-Type", "application/json"))
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Assert
        contract.Defaults.Should().NotBeNull();
        contract.Defaults!.ResponseHeaders.Should().ContainKey("Content-Type");
        contract.Defaults.ResponseHeaders["Content-Type"].ExactValue.Should().Be("application/json");
    }

    [Test]
    public void AllRequestsHaveHeader_AddsRequestHeaderExpectation()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .WithDefaults(d => d.AllRequestsHaveHeader("Authorization"))
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Assert
        contract.Defaults.Should().NotBeNull();
        contract.Defaults!.RequestHeaders.Should().ContainKey("Authorization");
        contract.Defaults.RequestHeaders["Authorization"].IsRequired.Should().BeTrue();
    }

    [Test]
    public void MultipleDefaults_AllApplied()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .WithDefaults(d => d
                .AllResponsesHaveHeader("X-Request-Id")
                .AllResponsesHaveHeader("X-Correlation-Id")
                .AllRequestsHaveHeader("Authorization")
                .AllRequestsHaveHeader("X-Api-Key"))
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Assert
        contract.Defaults.Should().NotBeNull();
        contract.Defaults!.ResponseHeaders.Should().HaveCount(2);
        contract.Defaults.ResponseHeaders.Should().ContainKey("X-Request-Id");
        contract.Defaults.ResponseHeaders.Should().ContainKey("X-Correlation-Id");
        contract.Defaults.RequestHeaders.Should().HaveCount(2);
        contract.Defaults.RequestHeaders.Should().ContainKey("Authorization");
        contract.Defaults.RequestHeaders.Should().ContainKey("X-Api-Key");
    }

    [Test]
    public void NoDefaults_DefaultsIsNull()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Assert
        contract.Defaults.Should().BeNull();
    }

    [Test]
    public void HeaderNames_AreCaseInsensitive()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .WithDefaults(d => d
                .AllResponsesHaveHeader("x-request-id")
                .AllRequestsHaveHeader("AUTHORIZATION"))
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Assert
        contract.Defaults!.ResponseHeaders.Should().ContainKey("X-Request-Id");
        contract.Defaults.RequestHeaders.Should().ContainKey("authorization");
    }
}
