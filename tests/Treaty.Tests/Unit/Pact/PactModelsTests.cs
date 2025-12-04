using System.Text.Json;
using FluentAssertions;
using Treaty.Pact;

namespace Treaty.Tests.Unit.Pact;

/// <summary>
/// Tests for Pact model classes serialization and deserialization.
/// </summary>
public class PactModelsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    #region PactContract Tests

    [Test]
    public void PactContract_DefaultValues_AreInitialized()
    {
        // Act
        var contract = new PactContract();

        // Assert
        contract.Consumer.Should().NotBeNull();
        contract.Provider.Should().NotBeNull();
        contract.Interactions.Should().NotBeNull();
        contract.Interactions.Should().BeEmpty();
        contract.Metadata.Should().NotBeNull();
    }

    [Test]
    public void PactContract_Serializes_WithCorrectPropertyNames()
    {
        // Arrange
        var contract = new PactContract
        {
            Consumer = new PactParticipant { Name = "MyConsumer" },
            Provider = new PactParticipant { Name = "MyProvider" }
        };

        // Act
        var json = JsonSerializer.Serialize(contract, JsonOptions);

        // Assert
        json.Should().Contain("\"consumer\"");
        json.Should().Contain("\"provider\"");
        json.Should().Contain("\"interactions\"");
        json.Should().Contain("\"metadata\"");
    }

    [Test]
    public void PactContract_Deserializes_FromValidJson()
    {
        // Arrange
        var json = """
            {
                "consumer": { "name": "TestConsumer" },
                "provider": { "name": "TestProvider" },
                "interactions": [],
                "metadata": { "pactSpecification": { "version": "3.0.0" } }
            }
            """;

        // Act
        var contract = JsonSerializer.Deserialize<PactContract>(json, JsonOptions);

        // Assert
        contract.Should().NotBeNull();
        contract!.Consumer.Name.Should().Be("TestConsumer");
        contract.Provider.Name.Should().Be("TestProvider");
    }

    #endregion

    #region PactParticipant Tests

    [Test]
    public void PactParticipant_DefaultName_IsEmptyString()
    {
        // Act
        var participant = new PactParticipant();

        // Assert
        participant.Name.Should().BeEmpty();
    }

    [Test]
    public void PactParticipant_Serializes_Correctly()
    {
        // Arrange
        var participant = new PactParticipant { Name = "MyService" };

        // Act
        var json = JsonSerializer.Serialize(participant, JsonOptions);

        // Assert
        json.Should().Contain("\"name\"");
        json.Should().Contain("MyService");
    }

    #endregion

    #region PactInteraction Tests

    [Test]
    public void PactInteraction_DefaultValues_AreInitialized()
    {
        // Act
        var interaction = new PactInteraction();

        // Assert
        interaction.Description.Should().BeEmpty();
        interaction.Request.Should().NotBeNull();
        interaction.Response.Should().NotBeNull();
        interaction.ProviderStates.Should().BeNull();
    }

    [Test]
    public void PactInteraction_WithProviderStates_Serializes()
    {
        // Arrange
        var interaction = new PactInteraction
        {
            Description = "Get user",
            ProviderStates = new List<PactProviderState>
            {
                new() { Name = "user exists", Params = new Dictionary<string, object> { ["id"] = 123 } }
            },
            Request = new PactRequest { Method = "GET", Path = "/users/123" },
            Response = new PactResponse { Status = 200 }
        };

        // Act
        var json = JsonSerializer.Serialize(interaction, JsonOptions);

        // Assert
        json.Should().Contain("\"providerStates\"");
        json.Should().Contain("user exists");
    }

    [Test]
    public void PactInteraction_Deserializes_WithAllProperties()
    {
        // Arrange
        var json = """
            {
                "description": "Get user by ID",
                "providerStates": [
                    { "name": "user exists", "params": { "userId": 123 } }
                ],
                "request": {
                    "method": "GET",
                    "path": "/users/123"
                },
                "response": {
                    "status": 200
                }
            }
            """;

        // Act
        var interaction = JsonSerializer.Deserialize<PactInteraction>(json, JsonOptions);

        // Assert
        interaction.Should().NotBeNull();
        interaction!.Description.Should().Be("Get user by ID");
        interaction.ProviderStates.Should().HaveCount(1);
        interaction.ProviderStates![0].Name.Should().Be("user exists");
        interaction.Request.Method.Should().Be("GET");
        interaction.Response.Status.Should().Be(200);
    }

    [Test]
    public void PactInteraction_Deserializes_V1ProviderState()
    {
        // Arrange - Pact v1 format with single string provider state
        var json = """
            {
                "description": "Get user",
                "providerState": "user exists",
                "request": { "method": "GET", "path": "/users/1" },
                "response": { "status": 200 }
            }
            """;

        // Act
        var interaction = JsonSerializer.Deserialize<PactInteraction>(json, JsonOptions);

        // Assert
        interaction.Should().NotBeNull();
        interaction!.ProviderStateV1.Should().Be("user exists");
    }

    [Test]
    public void PactInteraction_Deserializes_V2ProviderStates()
    {
        // Arrange - Pact v2 format with provider_states
        var json = """
            {
                "description": "Get user",
                "provider_states": [
                    { "name": "user exists" }
                ],
                "request": { "method": "GET", "path": "/users/1" },
                "response": { "status": 200 }
            }
            """;

        // Act
        var interaction = JsonSerializer.Deserialize<PactInteraction>(json, JsonOptions);

        // Assert
        interaction.Should().NotBeNull();
        interaction!.ProviderStatesV2.Should().HaveCount(1);
    }

    #endregion

    #region PactRequest Tests

    [Test]
    public void PactRequest_DefaultValues()
    {
        // Act
        var request = new PactRequest();

        // Assert
        request.Method.Should().Be("GET");
        request.Path.Should().Be("/");
        request.Query.Should().BeNull();
        request.Headers.Should().BeNull();
        request.Body.Should().BeNull();
    }

    [Test]
    public void PactRequest_Serializes_AllProperties()
    {
        // Arrange
        var request = new PactRequest
        {
            Method = "POST",
            Path = "/users",
            Query = "page=1&limit=10",
            Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            Body = new { name = "John" }
        };

        // Act
        var json = JsonSerializer.Serialize(request, JsonOptions);

        // Assert
        json.Should().Contain("\"method\":\"POST\"");
        json.Should().Contain("\"path\":\"/users\"");
        json.Should().Contain("\"query\"");
        json.Should().Contain("\"headers\"");
        json.Should().Contain("\"body\"");
    }

    #endregion

    #region PactResponse Tests

    [Test]
    public void PactResponse_DefaultStatus_Is200()
    {
        // Act
        var response = new PactResponse();

        // Assert
        response.Status.Should().Be(200);
    }

    [Test]
    public void PactResponse_Serializes_AllProperties()
    {
        // Arrange
        var response = new PactResponse
        {
            Status = 201,
            Headers = new Dictionary<string, string> { ["Location"] = "/users/123" },
            Body = new { id = 123, name = "John" }
        };

        // Act
        var json = JsonSerializer.Serialize(response, JsonOptions);

        // Assert
        json.Should().Contain("\"status\":201");
        json.Should().Contain("\"headers\"");
        json.Should().Contain("\"body\"");
    }

    #endregion

    #region PactProviderState Tests

    [Test]
    public void PactProviderState_DefaultValues()
    {
        // Act
        var state = new PactProviderState();

        // Assert
        state.Name.Should().BeEmpty();
        state.Params.Should().BeNull();
    }

    [Test]
    public void PactProviderState_WithParams_Serializes()
    {
        // Arrange
        var state = new PactProviderState
        {
            Name = "user exists",
            Params = new Dictionary<string, object>
            {
                ["userId"] = 123,
                ["userName"] = "John"
            }
        };

        // Act
        var json = JsonSerializer.Serialize(state, JsonOptions);

        // Assert
        json.Should().Contain("\"name\":\"user exists\"");
        json.Should().Contain("\"params\"");
        json.Should().Contain("userId");
    }

    #endregion

    #region PactMatchingRules Tests

    [Test]
    public void PactMatchingRules_Serializes_BodyRules()
    {
        // Arrange
        var rules = new PactMatchingRules
        {
            Body = new Dictionary<string, PactMatcherRule>
            {
                ["$"] = new PactMatcherRule
                {
                    Matchers = [new PactMatcher { Match = "type" }]
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(rules, JsonOptions);

        // Assert
        json.Should().Contain("\"body\"");
        json.Should().Contain("\"matchers\"");
        json.Should().Contain("\"type\"");
    }

    [Test]
    public void PactMatchingRules_Deserializes_FromJson()
    {
        // Arrange
        var json = """
            {
                "body": {
                    "$.name": {
                        "matchers": [{ "match": "type" }]
                    },
                    "$.age": {
                        "matchers": [{ "match": "integer" }]
                    }
                }
            }
            """;

        // Act
        var rules = JsonSerializer.Deserialize<PactMatchingRules>(json, JsonOptions);

        // Assert
        rules.Should().NotBeNull();
        rules!.Body.Should().HaveCount(2);
        rules.Body!["$.name"].Matchers.Should().HaveCount(1);
    }

    #endregion

    #region PactMatcher Tests

    [Test]
    public void PactMatcher_DefaultMatch_IsType()
    {
        // Act
        var matcher = new PactMatcher();

        // Assert
        matcher.Match.Should().Be("type");
    }

    [Test]
    public void PactMatcher_Serializes_RegexMatcher()
    {
        // Arrange
        var matcher = new PactMatcher
        {
            Match = "regex",
            Regex = @"^\d{4}-\d{2}-\d{2}$"
        };

        // Act
        var json = JsonSerializer.Serialize(matcher, JsonOptions);

        // Assert
        json.Should().Contain("\"match\":\"regex\"");
        json.Should().Contain("\"regex\"");
    }

    [Test]
    public void PactMatcher_Serializes_ArrayMatcher()
    {
        // Arrange
        var matcher = new PactMatcher
        {
            Match = "type",
            Min = 1,
            Max = 10
        };

        // Act
        var json = JsonSerializer.Serialize(matcher, JsonOptions);

        // Assert
        json.Should().Contain("\"min\":1");
        json.Should().Contain("\"max\":10");
    }

    #endregion

    #region PactMetadata Tests

    [Test]
    public void PactMetadata_DefaultVersion_Is3()
    {
        // Act
        var metadata = new PactMetadata();

        // Assert
        metadata.PactSpecification.Version.Should().Be("3.0.0");
    }

    [Test]
    public void PactSpecification_DefaultVersion_Is3()
    {
        // Act
        var spec = new PactSpecification();

        // Assert
        spec.Version.Should().Be("3.0.0");
    }

    #endregion

    #region Full Round-Trip Tests

    [Test]
    public void PactContract_RoundTrip_PreservesAllData()
    {
        // Arrange
        var original = new PactContract
        {
            Consumer = new PactParticipant { Name = "Consumer" },
            Provider = new PactParticipant { Name = "Provider" },
            Interactions =
            [
                new PactInteraction
                {
                    Description = "Get user",
                    ProviderStates = [new PactProviderState { Name = "user exists" }],
                    Request = new PactRequest
                    {
                        Method = "GET",
                        Path = "/users/123",
                        Headers = new Dictionary<string, string> { ["Accept"] = "application/json" }
                    },
                    Response = new PactResponse
                    {
                        Status = 200,
                        Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
                        Body = new { id = 123, name = "John" },
                        MatchingRules = new PactMatchingRules
                        {
                            Body = new Dictionary<string, PactMatcherRule>
                            {
                                ["$"] = new PactMatcherRule { Matchers = [new PactMatcher { Match = "type" }] }
                            }
                        }
                    }
                }
            ],
            Metadata = new PactMetadata { PactSpecification = new PactSpecification { Version = "3.0.0" } }
        };

        // Act
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<PactContract>(json, JsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Consumer.Name.Should().Be("Consumer");
        deserialized.Provider.Name.Should().Be("Provider");
        deserialized.Interactions.Should().HaveCount(1);
        deserialized.Interactions[0].Request.Method.Should().Be("GET");
        deserialized.Interactions[0].Response.Status.Should().Be(200);
        deserialized.Metadata.PactSpecification.Version.Should().Be("3.0.0");
    }

    #endregion
}
