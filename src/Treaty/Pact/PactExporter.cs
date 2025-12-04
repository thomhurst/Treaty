using System.Text.Json;
using System.Text.Json.Serialization;
using Treaty.Contracts;

namespace Treaty.Pact;

/// <summary>
/// Exports Treaty contracts to Pact JSON format.
/// </summary>
public static class PactExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Exports a Treaty contract to Pact JSON format.
    /// </summary>
    /// <param name="contract">The Treaty contract to export.</param>
    /// <param name="consumerName">Name of the consumer application.</param>
    /// <param name="providerName">Name of the provider application.</param>
    /// <returns>A PactContract object that can be serialized to JSON.</returns>
    public static PactContract ToPact(Contract contract, string consumerName, string providerName)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        var pact = new PactContract
        {
            Consumer = new PactParticipant { Name = consumerName },
            Provider = new PactParticipant { Name = providerName },
            Metadata = new PactMetadata
            {
                PactSpecification = new PactSpecification { Version = "3.0.0" }
            }
        };

        foreach (var endpoint in contract.Endpoints)
        {
            var interaction = ConvertEndpointToInteraction(endpoint);
            pact.Interactions.Add(interaction);
        }

        return pact;
    }

    /// <summary>
    /// Exports a Treaty contract to a Pact JSON string.
    /// </summary>
    /// <param name="contract">The Treaty contract to export.</param>
    /// <param name="consumerName">Name of the consumer application.</param>
    /// <param name="providerName">Name of the provider application.</param>
    /// <returns>A JSON string in Pact format.</returns>
    public static string ToJson(Contract contract, string consumerName, string providerName)
    {
        var pact = ToPact(contract, consumerName, providerName);
        return JsonSerializer.Serialize(pact, JsonOptions);
    }

    /// <summary>
    /// Exports a Treaty contract to a Pact JSON file.
    /// </summary>
    /// <param name="contract">The Treaty contract to export.</param>
    /// <param name="filePath">Path where the Pact file will be written.</param>
    /// <param name="consumerName">Name of the consumer application.</param>
    /// <param name="providerName">Name of the provider application.</param>
    public static void ToFile(Contract contract, string filePath, string consumerName, string providerName)
    {
        var json = ToJson(contract, consumerName, providerName);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Exports a Treaty contract to a stream in Pact JSON format.
    /// </summary>
    /// <param name="contract">The Treaty contract to export.</param>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="consumerName">Name of the consumer application.</param>
    /// <param name="providerName">Name of the provider application.</param>
    public static void ToStream(Contract contract, Stream stream, string consumerName, string providerName)
    {
        var pact = ToPact(contract, consumerName, providerName);
        JsonSerializer.Serialize(stream, pact, JsonOptions);
    }

    private static PactInteraction ConvertEndpointToInteraction(EndpointContract endpoint)
    {
        var interaction = new PactInteraction
        {
            Description = $"{endpoint.Method.Method} {endpoint.PathTemplate}",
            Request = new PactRequest
            {
                Method = endpoint.Method.Method,
                Path = GetExamplePath(endpoint)
            },
            Response = new PactResponse()
        };

        // Add provider states
        if (endpoint.ProviderStates.Count > 0)
        {
            interaction.ProviderStates = endpoint.ProviderStates
                .Select(ConvertProviderState)
                .ToList();
        }

        // Add request headers
        if (endpoint.ExpectedHeaders.Count > 0)
        {
            interaction.Request.Headers = endpoint.ExpectedHeaders
                .ToDictionary(
                    h => h.Key,
                    h => h.Value.ExactValue ?? "*");
        }

        // Add example query parameters
        if (endpoint.ExampleData?.QueryParameters.Count > 0)
        {
            var queryParts = endpoint.ExampleData.QueryParameters
                .Select(q => $"{Uri.EscapeDataString(q.Key)}={Uri.EscapeDataString(q.Value?.ToString() ?? "")}");
            interaction.Request.Query = string.Join("&", queryParts);
        }

        // Add example request body
        if (endpoint.ExampleData?.RequestBody != null)
        {
            interaction.Request.Body = endpoint.ExampleData.RequestBody;
        }

        // Add response expectation
        var successResponse = endpoint.ResponseExpectations
            .Where(r => r.StatusCode >= 200 && r.StatusCode < 300)
            .FirstOrDefault()
            ?? endpoint.ResponseExpectations.FirstOrDefault();

        if (successResponse != null)
        {
            interaction.Response.Status = successResponse.StatusCode;

            // Add response headers
            if (successResponse.ExpectedHeaders.Count > 0 || successResponse.ContentType != null)
            {
                interaction.Response.Headers = new Dictionary<string, string>();

                if (successResponse.ContentType != null)
                {
                    interaction.Response.Headers["Content-Type"] = successResponse.ContentType;
                }

                foreach (var (name, headerExpectation) in successResponse.ExpectedHeaders)
                {
                    interaction.Response.Headers[name] = headerExpectation.ExactValue ?? "*";
                }
            }

            // Generate example response body
            if (successResponse.BodyValidator != null)
            {
                var sampleJson = successResponse.BodyValidator.GenerateSample();
                try
                {
                    interaction.Response.Body = JsonSerializer.Deserialize<object>(sampleJson);
                }
                catch
                {
                    // If deserialization fails, use raw string
                    interaction.Response.Body = sampleJson;
                }

                // Add matching rules if type information is available
                interaction.Response.MatchingRules = GenerateMatchingRules(successResponse);
            }
        }

        return interaction;
    }

    private static string GetExamplePath(EndpointContract endpoint)
    {
        if (endpoint.ExampleData?.PathParameters.Count > 0)
        {
            var path = endpoint.PathTemplate;
            foreach (var (name, value) in endpoint.ExampleData.PathParameters)
            {
                path = path.Replace($"{{{name}}}", value?.ToString() ?? name);
            }
            return path;
        }

        // Use placeholder values for path parameters
        return System.Text.RegularExpressions.Regex.Replace(
            endpoint.PathTemplate,
            @"\{([^}]+)\}",
            "1");
    }

    private static PactProviderState ConvertProviderState(ProviderState state)
    {
        var pactState = new PactProviderState { Name = state.Name };

        if (state.Parameters.Count > 0)
        {
            pactState.Params = state.Parameters
                .ToDictionary(p => p.Key, p => p.Value);
        }

        return pactState;
    }

    private static PactMatchingRules? GenerateMatchingRules(ResponseExpectation response)
    {
        var expectedType = response.BodyValidator?.ExpectedType;
        if (expectedType == null)
            return null;

        // Generate basic type matching rules
        var bodyRules = new Dictionary<string, PactMatcherRule>();

        // Add a root-level type matcher
        bodyRules["$"] = new PactMatcherRule
        {
            Matchers = [new PactMatcher { Match = "type" }]
        };

        if (bodyRules.Count > 0)
        {
            return new PactMatchingRules { Body = bodyRules };
        }

        return null;
    }
}
