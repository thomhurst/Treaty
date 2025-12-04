using FluentAssertions;
using Treaty.Pact;

namespace Treaty.Tests.Unit.Pact;

public class PactImporterTests
{
    private const string SimplePactJson = """
        {
            "consumer": { "name": "ConsumerApp" },
            "provider": { "name": "ProviderAPI" },
            "interactions": [
                {
                    "description": "GET /users/123",
                    "request": {
                        "method": "GET",
                        "path": "/users/123"
                    },
                    "response": {
                        "status": 200,
                        "headers": {
                            "Content-Type": "application/json"
                        }
                    }
                }
            ],
            "metadata": {
                "pactSpecification": { "version": "3.0.0" }
            }
        }
        """;

    [Test]
    public void FromJson_ParsesSimplePact()
    {
        // Act
        var contract = PactImporter.FromJson(SimplePactJson);

        // Assert
        contract.Should().NotBeNull();
        contract.Endpoints.Should().HaveCount(1);
    }

    [Test]
    public void FromJson_ExtractsEndpointMethod()
    {
        // Act
        var contract = PactImporter.FromJson(SimplePactJson);

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.Method.Method.Should().Be("GET");
    }

    [Test]
    public void FromJson_NormalizesPathToTemplate()
    {
        // Act
        var contract = PactImporter.FromJson(SimplePactJson);

        // Assert - numeric ID should be converted to {id}
        var endpoint = contract.Endpoints[0];
        endpoint.PathTemplate.Should().Be("/users/{id}");
    }

    [Test]
    public void FromJson_ExtractsResponseStatus()
    {
        // Act
        var contract = PactImporter.FromJson(SimplePactJson);

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.ResponseExpectations.Should().NotBeEmpty();
        endpoint.ResponseExpectations[0].StatusCode.Should().Be(200);
    }

    [Test]
    public void FromJson_ExtractsContentType()
    {
        // Act
        var contract = PactImporter.FromJson(SimplePactJson);

        // Assert
        var response = contract.Endpoints[0].ResponseExpectations[0];
        response.ContentType.Should().Be("application/json");
    }

    [Test]
    public void FromJson_SetsContractName()
    {
        // Act
        var contract = PactImporter.FromJson(SimplePactJson);

        // Assert
        contract.Name.Should().Be("ConsumerApp -> ProviderAPI");
    }

    [Test]
    public void FromJson_WithProviderStates_ImportsStates()
    {
        // Arrange
        var pactWithStates = """
            {
                "consumer": { "name": "Consumer" },
                "provider": { "name": "Provider" },
                "interactions": [
                    {
                        "description": "GET user",
                        "providerStates": [
                            {
                                "name": "user exists",
                                "params": { "userId": 123 }
                            }
                        ],
                        "request": { "method": "GET", "path": "/users/123" },
                        "response": { "status": 200 }
                    }
                ],
                "metadata": { "pactSpecification": { "version": "3.0.0" } }
            }
            """;

        // Act
        var contract = PactImporter.FromJson(pactWithStates);

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.ProviderStates.Should().HaveCount(1);
        endpoint.ProviderStates[0].Name.Should().Be("user exists");
    }

    [Test]
    public void FromJson_WithProviderStateV1_ImportsState()
    {
        // Arrange - Pact v1 format with single string provider state
        var pactV1 = """
            {
                "consumer": { "name": "Consumer" },
                "provider": { "name": "Provider" },
                "interactions": [
                    {
                        "description": "GET user",
                        "providerState": "user exists",
                        "request": { "method": "GET", "path": "/users/1" },
                        "response": { "status": 200 }
                    }
                ],
                "metadata": { "pactSpecification": { "version": "1.0.0" } }
            }
            """;

        // Act
        var contract = PactImporter.FromJson(pactV1);

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.ProviderStates.Should().HaveCount(1);
        endpoint.ProviderStates[0].Name.Should().Be("user exists");
    }

    [Test]
    public void FromJson_WithQueryString_ExtractsQueryParams()
    {
        // Arrange
        var pactWithQuery = """
            {
                "consumer": { "name": "Consumer" },
                "provider": { "name": "Provider" },
                "interactions": [
                    {
                        "description": "GET users",
                        "request": {
                            "method": "GET",
                            "path": "/users",
                            "query": "page=1&limit=10"
                        },
                        "response": { "status": 200 }
                    }
                ],
                "metadata": { "pactSpecification": { "version": "3.0.0" } }
            }
            """;

        // Act
        var contract = PactImporter.FromJson(pactWithQuery);

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.ExampleData.Should().NotBeNull();
        // Query params should be extracted as example data
    }

    [Test]
    public void FromJson_WithUuidPath_NormalizesToTemplate()
    {
        // Arrange
        var pactWithUuid = """
            {
                "consumer": { "name": "Consumer" },
                "provider": { "name": "Provider" },
                "interactions": [
                    {
                        "description": "GET order",
                        "request": {
                            "method": "GET",
                            "path": "/orders/550e8400-e29b-41d4-a716-446655440000"
                        },
                        "response": { "status": 200 }
                    }
                ],
                "metadata": { "pactSpecification": { "version": "3.0.0" } }
            }
            """;

        // Act
        var contract = PactImporter.FromJson(pactWithUuid);

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.PathTemplate.Should().Be("/orders/{id}");
    }

    [Test]
    public void FromJson_WithMultipleInteractions_CreatesMultipleEndpoints()
    {
        // Arrange
        var pactMultiple = """
            {
                "consumer": { "name": "Consumer" },
                "provider": { "name": "Provider" },
                "interactions": [
                    {
                        "description": "GET users",
                        "request": { "method": "GET", "path": "/users" },
                        "response": { "status": 200 }
                    },
                    {
                        "description": "POST user",
                        "request": { "method": "POST", "path": "/users" },
                        "response": { "status": 201 }
                    }
                ],
                "metadata": { "pactSpecification": { "version": "3.0.0" } }
            }
            """;

        // Act
        var contract = PactImporter.FromJson(pactMultiple);

        // Assert
        contract.Endpoints.Should().HaveCount(2);
    }

    [Test]
    public void FromJson_ThrowsOnEmptyJson()
    {
        // Act
        var action = () => PactImporter.FromJson("");

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Test]
    public void FromJson_ThrowsOnInvalidJson()
    {
        // Act
        var action = () => PactImporter.FromJson("not valid json");

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Invalid Pact JSON*");
    }

    [Test]
    public void FromStream_ParsesFromStream()
    {
        // Arrange
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(SimplePactJson));

        // Act
        var contract = PactImporter.FromStream(stream);

        // Assert
        contract.Should().NotBeNull();
        contract.Endpoints.Should().HaveCount(1);
    }

    [Test]
    public void FromStream_ThrowsOnNullStream()
    {
        // Act
        var action = () => PactImporter.FromStream(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void FromJson_WithRequestHeaders_ImportsHeaders()
    {
        // Arrange
        var pactWithHeaders = """
            {
                "consumer": { "name": "Consumer" },
                "provider": { "name": "Provider" },
                "interactions": [
                    {
                        "description": "POST user",
                        "request": {
                            "method": "POST",
                            "path": "/users",
                            "headers": {
                                "Authorization": "Bearer token123",
                                "Content-Type": "application/json"
                            }
                        },
                        "response": { "status": 201 }
                    }
                ],
                "metadata": { "pactSpecification": { "version": "3.0.0" } }
            }
            """;

        // Act
        var contract = PactImporter.FromJson(pactWithHeaders);

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.ExpectedHeaders.Should().HaveCount(2);
        endpoint.ExpectedHeaders.Keys.Should().Contain("Authorization");
        endpoint.ExpectedHeaders.Keys.Should().Contain("Content-Type");
    }

    [Test]
    public void FromJson_WithResponseHeaders_ImportsNonContentTypeHeaders()
    {
        // Arrange
        var pactWithResponseHeaders = """
            {
                "consumer": { "name": "Consumer" },
                "provider": { "name": "Provider" },
                "interactions": [
                    {
                        "description": "GET resource",
                        "request": { "method": "GET", "path": "/resource" },
                        "response": {
                            "status": 200,
                            "headers": {
                                "Content-Type": "application/json",
                                "X-Request-Id": "abc123"
                            }
                        }
                    }
                ],
                "metadata": { "pactSpecification": { "version": "3.0.0" } }
            }
            """;

        // Act
        var contract = PactImporter.FromJson(pactWithResponseHeaders);

        // Assert
        var response = contract.Endpoints[0].ResponseExpectations[0];
        response.ContentType.Should().Be("application/json");
        response.ExpectedHeaders.Keys.Should().Contain("X-Request-Id");
    }
}
