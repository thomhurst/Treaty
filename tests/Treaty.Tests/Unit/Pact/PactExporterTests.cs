using System.Text.Json;
using FluentAssertions;
using Treaty.Contracts;
using Treaty.Pact;

namespace Treaty.Tests.Unit.Pact;

public class PactExporterTests
{
    [Test]
    public void ToPact_WithSimpleContract_ReturnsValidPactContract()
    {
        // Arrange
        var contract = Treaty.DefineContract("Test Contract")
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithContentType("application/json"))
            .Build();

        // Act
        var pact = PactExporter.ToPact(contract, "Consumer", "Provider");

        // Assert
        pact.Consumer.Name.Should().Be("Consumer");
        pact.Provider.Name.Should().Be("Provider");
        pact.Interactions.Should().HaveCount(1);
        pact.Metadata.PactSpecification.Version.Should().Be("3.0.0");
    }

    [Test]
    public void ToPact_PreservesEndpointDetails()
    {
        // Arrange
        var contract = Treaty.DefineContract()
            .ForEndpoint("/orders/{orderId}")
                .WithMethod(HttpMethod.Post)
                .WithHeader("Authorization")
                .ExpectingResponse(r => r
                    .WithStatus(201)
                    .WithContentType("application/json"))
            .Build();

        // Act
        var pact = PactExporter.ToPact(contract, "OrderService", "OrderAPI");

        // Assert
        var interaction = pact.Interactions[0];
        interaction.Request.Method.Should().Be("POST");
        interaction.Request.Path.Should().Contain("/orders/");
        interaction.Response.Status.Should().Be(201);
    }

    [Test]
    public void ToPact_WithExampleData_IncludesExamplePath()
    {
        // Arrange
        var contract = Treaty.DefineContract()
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Get)
                .WithExampleData(e => e.WithPathParam("id", 123))
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Act
        var pact = PactExporter.ToPact(contract, "Consumer", "Provider");

        // Assert
        var interaction = pact.Interactions[0];
        interaction.Request.Path.Should().Be("/users/123");
    }

    [Test]
    public void ToPact_WithQueryParams_IncludesQueryString()
    {
        // Arrange
        var contract = Treaty.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .WithExampleData(e => e
                    .WithQueryParam("page", 1)
                    .WithQueryParam("limit", 10))
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Act
        var pact = PactExporter.ToPact(contract, "Consumer", "Provider");

        // Assert
        var interaction = pact.Interactions[0];
        interaction.Request.Query.Should().Contain("page=1");
        interaction.Request.Query.Should().Contain("limit=10");
    }

    [Test]
    public void ToPact_WithProviderState_IncludesProviderStates()
    {
        // Arrange
        var contract = Treaty.DefineContract()
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Get)
                .Given("user exists", new Dictionary<string, object> { ["userId"] = 123 })
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Act
        var pact = PactExporter.ToPact(contract, "Consumer", "Provider");

        // Assert
        var interaction = pact.Interactions[0];
        interaction.ProviderStates.Should().NotBeNull();
        interaction.ProviderStates.Should().HaveCount(1);
        interaction.ProviderStates![0].Name.Should().Be("user exists");
        interaction.ProviderStates[0].Params.Should().ContainKey("userId");
    }

    [Test]
    public void ToJson_ProducesValidJson()
    {
        // Arrange
        var contract = Treaty.DefineContract()
            .ForEndpoint("/health")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Act
        var json = PactExporter.ToJson(contract, "Consumer", "Provider");

        // Assert
        var parseAction = () => JsonDocument.Parse(json);
        parseAction.Should().NotThrow();
        json.Should().Contain("\"consumer\"");
        json.Should().Contain("\"provider\"");
        json.Should().Contain("\"interactions\"");
    }

    [Test]
    public void ToJson_UsesCamelCaseNaming()
    {
        // Arrange
        var contract = Treaty.DefineContract()
            .ForEndpoint("/test")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Act
        var json = PactExporter.ToJson(contract, "Consumer", "Provider");

        // Assert
        json.Should().Contain("\"pactSpecification\"");
        json.Should().NotContain("\"PactSpecification\"");
    }

    [Test]
    public void ToPact_WithMultipleEndpoints_CreatesMultipleInteractions()
    {
        // Arrange
        var contract = Treaty.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingResponse(r => r.WithStatus(201))
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Delete)
                .ExpectingResponse(r => r.WithStatus(204))
            .Build();

        // Act
        var pact = PactExporter.ToPact(contract, "Consumer", "Provider");

        // Assert
        pact.Interactions.Should().HaveCount(3);
    }

    [Test]
    public void ToPact_ThrowsOnNullContract()
    {
        // Act
        var action = () => PactExporter.ToPact(null!, "Consumer", "Provider");

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void ToPact_ThrowsOnEmptyConsumerName()
    {
        // Arrange
        var contract = Treaty.DefineContract()
            .ForEndpoint("/test")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Act
        var action = () => PactExporter.ToPact(contract, "", "Provider");

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Test]
    public void ToPact_ThrowsOnEmptyProviderName()
    {
        // Arrange
        var contract = Treaty.DefineContract()
            .ForEndpoint("/test")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Act
        var action = () => PactExporter.ToPact(contract, "Consumer", "");

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Test]
    public void ToStream_WritesToStream()
    {
        // Arrange
        var contract = Treaty.DefineContract()
            .ForEndpoint("/test")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        using var stream = new MemoryStream();

        // Act
        PactExporter.ToStream(contract, stream, "Consumer", "Provider");

        // Assert
        stream.Length.Should().BeGreaterThan(0);

        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        json.Should().Contain("\"consumer\"");
    }

    [Test]
    public void ToPact_WithResponseHeaders_IncludesHeaders()
    {
        // Arrange
        var contract = Treaty.DefineContract()
            .ForEndpoint("/test")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithContentType("application/json")
                    .WithHeader("X-Custom-Header", "custom-value"))
            .Build();

        // Act
        var pact = PactExporter.ToPact(contract, "Consumer", "Provider");

        // Assert
        var response = pact.Interactions[0].Response;
        response.Headers.Should().NotBeNull();
        response.Headers.Should().ContainKey("Content-Type");
        response.Headers!["Content-Type"].Should().Be("application/json");
        response.Headers.Should().ContainKey("X-Custom-Header");
    }

    [Test]
    public void ToPact_SelectsSuccessStatusCode()
    {
        // Arrange - contract with multiple response expectations
        var contract = Treaty.DefineContract()
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
                .ExpectingResponse(r => r.WithStatus(404))
            .Build();

        // Act
        var pact = PactExporter.ToPact(contract, "Consumer", "Provider");

        // Assert - should pick the success status
        pact.Interactions[0].Response.Status.Should().Be(200);
    }
}
