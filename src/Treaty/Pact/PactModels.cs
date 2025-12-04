using System.Text.Json.Serialization;

namespace Treaty.Pact;

/// <summary>
/// Represents a Pact contract file.
/// </summary>
public sealed class PactContract
{
    /// <summary>
    /// Gets or sets the consumer information.
    /// </summary>
    [JsonPropertyName("consumer")]
    public PactParticipant Consumer { get; set; } = new();

    /// <summary>
    /// Gets or sets the provider information.
    /// </summary>
    [JsonPropertyName("provider")]
    public PactParticipant Provider { get; set; } = new();

    /// <summary>
    /// Gets or sets the interactions (request/response pairs).
    /// </summary>
    [JsonPropertyName("interactions")]
    public List<PactInteraction> Interactions { get; set; } = [];

    /// <summary>
    /// Gets or sets the Pact metadata.
    /// </summary>
    [JsonPropertyName("metadata")]
    public PactMetadata Metadata { get; set; } = new();
}

/// <summary>
/// Represents a participant (consumer or provider) in a Pact.
/// </summary>
public sealed class PactParticipant
{
    /// <summary>
    /// Gets or sets the participant name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Represents a single interaction (request/response) in a Pact.
/// </summary>
public sealed class PactInteraction
{
    /// <summary>
    /// Gets or sets the description of this interaction.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider states required for this interaction.
    /// </summary>
    [JsonPropertyName("providerStates")]
    public List<PactProviderState>? ProviderStates { get; set; }

    /// <summary>
    /// Alternative property name for provider states in Pact v2.
    /// </summary>
    [JsonPropertyName("provider_states")]
    public List<PactProviderState>? ProviderStatesV2 { get; set; }

    /// <summary>
    /// Alternative property name for single provider state in Pact v1.
    /// </summary>
    [JsonPropertyName("providerState")]
    public string? ProviderStateV1 { get; set; }

    /// <summary>
    /// Gets or sets the expected request.
    /// </summary>
    [JsonPropertyName("request")]
    public PactRequest Request { get; set; } = new();

    /// <summary>
    /// Gets or sets the expected response.
    /// </summary>
    [JsonPropertyName("response")]
    public PactResponse Response { get; set; } = new();
}

/// <summary>
/// Represents a provider state in a Pact.
/// </summary>
public sealed class PactProviderState
{
    /// <summary>
    /// Gets or sets the name of the provider state.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets optional parameters for the provider state.
    /// </summary>
    [JsonPropertyName("params")]
    public Dictionary<string, object>? Params { get; set; }
}

/// <summary>
/// Represents an expected request in a Pact interaction.
/// </summary>
public sealed class PactRequest
{
    /// <summary>
    /// Gets or sets the HTTP method.
    /// </summary>
    [JsonPropertyName("method")]
    public string Method { get; set; } = "GET";

    /// <summary>
    /// Gets or sets the request path.
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = "/";

    /// <summary>
    /// Gets or sets the query string (without leading ?).
    /// </summary>
    [JsonPropertyName("query")]
    public string? Query { get; set; }

    /// <summary>
    /// Gets or sets the request headers.
    /// </summary>
    [JsonPropertyName("headers")]
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// Gets or sets the request body.
    /// </summary>
    [JsonPropertyName("body")]
    public object? Body { get; set; }

    /// <summary>
    /// Gets or sets matching rules for the request.
    /// </summary>
    [JsonPropertyName("matchingRules")]
    public PactMatchingRules? MatchingRules { get; set; }
}

/// <summary>
/// Represents an expected response in a Pact interaction.
/// </summary>
public sealed class PactResponse
{
    /// <summary>
    /// Gets or sets the HTTP status code.
    /// </summary>
    [JsonPropertyName("status")]
    public int Status { get; set; } = 200;

    /// <summary>
    /// Gets or sets the response headers.
    /// </summary>
    [JsonPropertyName("headers")]
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// Gets or sets the response body.
    /// </summary>
    [JsonPropertyName("body")]
    public object? Body { get; set; }

    /// <summary>
    /// Gets or sets matching rules for the response.
    /// </summary>
    [JsonPropertyName("matchingRules")]
    public PactMatchingRules? MatchingRules { get; set; }
}

/// <summary>
/// Represents matching rules in a Pact (v3 format).
/// </summary>
public sealed class PactMatchingRules
{
    /// <summary>
    /// Gets or sets body matching rules.
    /// </summary>
    [JsonPropertyName("body")]
    public Dictionary<string, PactMatcherRule>? Body { get; set; }

    /// <summary>
    /// Gets or sets header matching rules.
    /// </summary>
    [JsonPropertyName("header")]
    public Dictionary<string, PactMatcherRule>? Header { get; set; }

    /// <summary>
    /// Gets or sets path matching rules.
    /// </summary>
    [JsonPropertyName("path")]
    public PactMatcherRule? Path { get; set; }
}

/// <summary>
/// Represents a single matching rule.
/// </summary>
public sealed class PactMatcherRule
{
    /// <summary>
    /// Gets or sets the matchers for this rule.
    /// </summary>
    [JsonPropertyName("matchers")]
    public List<PactMatcher>? Matchers { get; set; }

    /// <summary>
    /// Gets or sets the combine mode (AND/OR).
    /// </summary>
    [JsonPropertyName("combine")]
    public string? Combine { get; set; }
}

/// <summary>
/// Represents a single matcher definition.
/// </summary>
public sealed class PactMatcher
{
    /// <summary>
    /// Gets or sets the match type (e.g., "type", "regex", "date").
    /// </summary>
    [JsonPropertyName("match")]
    public string Match { get; set; } = "type";

    /// <summary>
    /// Gets or sets the regex pattern (for regex matchers).
    /// </summary>
    [JsonPropertyName("regex")]
    public string? Regex { get; set; }

    /// <summary>
    /// Gets or sets the minimum count (for array matchers).
    /// </summary>
    [JsonPropertyName("min")]
    public int? Min { get; set; }

    /// <summary>
    /// Gets or sets the maximum count (for array matchers).
    /// </summary>
    [JsonPropertyName("max")]
    public int? Max { get; set; }

    /// <summary>
    /// Gets or sets the format (for date/time matchers).
    /// </summary>
    [JsonPropertyName("format")]
    public string? Format { get; set; }
}

/// <summary>
/// Represents Pact file metadata.
/// </summary>
public sealed class PactMetadata
{
    /// <summary>
    /// Gets or sets the Pact specification version.
    /// </summary>
    [JsonPropertyName("pactSpecification")]
    public PactSpecification PactSpecification { get; set; } = new();
}

/// <summary>
/// Represents the Pact specification version.
/// </summary>
public sealed class PactSpecification
{
    /// <summary>
    /// Gets or sets the version string.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "3.0.0";
}
