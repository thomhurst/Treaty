using FluentAssertions;
using Treaty.Pact;

namespace Treaty.Tests.Unit.Pact;

public class PactRoundTripTests
{
    [Test]
    public void RoundTrip_ExportThenImport_PreservesEndpointMethod()
    {
        // Arrange
        var original = Treaty.DefineContract("Test")
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Delete)
                .ExpectingResponse(r => r.WithStatus(204))
            .Build();

        // Act - export to JSON, then import back
        var json = Treaty.ToPactJson(original, "Consumer", "Provider");
        var imported = Treaty.FromPactJson(json);

        // Assert
        imported.Endpoints[0].Method.Method.Should().Be("DELETE");
    }

    [Test]
    public void RoundTrip_ExportThenImport_PreservesStatusCode()
    {
        // Arrange
        var original = Treaty.DefineContract()
            .ForEndpoint("/orders")
                .WithMethod(HttpMethod.Post)
                .ExpectingResponse(r => r.WithStatus(201))
            .Build();

        // Act
        var json = Treaty.ToPactJson(original, "OrderService", "OrderAPI");
        var imported = Treaty.FromPactJson(json);

        // Assert
        imported.Endpoints[0].ResponseExpectations[0].StatusCode.Should().Be(201);
    }

    [Test]
    public void RoundTrip_ExportThenImport_PreservesContentType()
    {
        // Arrange
        var original = Treaty.DefineContract()
            .ForEndpoint("/data")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithContentType("text/plain"))
            .Build();

        // Act
        var json = Treaty.ToPactJson(original, "Consumer", "Provider");
        var imported = Treaty.FromPactJson(json);

        // Assert
        imported.Endpoints[0].ResponseExpectations[0].ContentType.Should().Be("text/plain");
    }

    [Test]
    public void RoundTrip_ExportThenImport_PreservesProviderState()
    {
        // Arrange
        var original = Treaty.DefineContract()
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Get)
                .Given("user exists")
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Act
        var json = Treaty.ToPactJson(original, "Consumer", "Provider");
        var imported = Treaty.FromPactJson(json);

        // Assert
        imported.Endpoints[0].ProviderStates.Should().HaveCount(1);
        imported.Endpoints[0].ProviderStates[0].Name.Should().Be("user exists");
    }

    [Test]
    public void RoundTrip_ExportThenImport_PreservesMultipleEndpoints()
    {
        // Arrange
        var original = Treaty.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingResponse(r => r.WithStatus(201))
            .Build();

        // Act
        var json = Treaty.ToPactJson(original, "Consumer", "Provider");
        var imported = Treaty.FromPactJson(json);

        // Assert
        imported.Endpoints.Should().HaveCount(2);
    }

    [Test]
    public void TreatyConvenienceMethods_ToPactJson_Works()
    {
        // Arrange
        var contract = Treaty.DefineContract()
            .ForEndpoint("/health")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Act
        var json = Treaty.ToPactJson(contract, "HealthChecker", "API");

        // Assert
        json.Should().Contain("\"consumer\"");
        json.Should().Contain("HealthChecker");
        json.Should().Contain("API");
    }

    [Test]
    public void TreatyConvenienceMethods_FromPactJson_Works()
    {
        // Arrange
        var pactJson = """
            {
                "consumer": { "name": "Test" },
                "provider": { "name": "API" },
                "interactions": [
                    {
                        "description": "GET health",
                        "request": { "method": "GET", "path": "/health" },
                        "response": { "status": 200 }
                    }
                ],
                "metadata": { "pactSpecification": { "version": "3.0.0" } }
            }
            """;

        // Act
        var contract = Treaty.FromPactJson(pactJson);

        // Assert
        contract.Should().NotBeNull();
        contract.Endpoints[0].PathTemplate.Should().Be("/health");
    }

    [Test]
    public void TreatyConvenienceMethods_FromPactStream_Works()
    {
        // Arrange
        var pactJson = """
            {
                "consumer": { "name": "Test" },
                "provider": { "name": "API" },
                "interactions": [
                    {
                        "description": "GET test",
                        "request": { "method": "GET", "path": "/test" },
                        "response": { "status": 200 }
                    }
                ],
                "metadata": { "pactSpecification": { "version": "3.0.0" } }
            }
            """;

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(pactJson));

        // Act
        var contract = Treaty.FromPactStream(stream);

        // Assert
        contract.Should().NotBeNull();
        contract.Endpoints[0].PathTemplate.Should().Be("/test");
    }
}
