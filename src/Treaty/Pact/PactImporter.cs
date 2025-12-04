using System.Text.Json;
using System.Text.RegularExpressions;
using Treaty.Contracts;

namespace Treaty.Pact;

/// <summary>
/// Imports Pact JSON files and converts them to Treaty contracts.
/// </summary>
public static class PactImporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Imports a Pact contract from a file path.
    /// </summary>
    /// <param name="filePath">Path to the Pact JSON file.</param>
    /// <returns>A Treaty contract.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file is not found.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the Pact file is invalid.</exception>
    public static Contract FromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Pact file not found: {filePath}", filePath);
        }

        var json = File.ReadAllText(filePath);
        return FromJson(json);
    }

    /// <summary>
    /// Imports a Pact contract from a JSON string.
    /// </summary>
    /// <param name="json">The Pact JSON content.</param>
    /// <returns>A Treaty contract.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the Pact JSON is invalid.</exception>
    public static Contract FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        PactContract pact;
        try
        {
            pact = JsonSerializer.Deserialize<PactContract>(json, JsonOptions)
                ?? throw new InvalidOperationException("Failed to deserialize Pact JSON");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Invalid Pact JSON: {ex.Message}", ex);
        }

        return ConvertToTreatyContract(pact);
    }

    /// <summary>
    /// Imports a Pact contract from a stream.
    /// </summary>
    /// <param name="stream">Stream containing the Pact JSON.</param>
    /// <returns>A Treaty contract.</returns>
    public static Contract FromStream(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        return FromJson(json);
    }

    private static Contract ConvertToTreatyContract(PactContract pact)
    {
        var contractName = $"{pact.Consumer.Name} -> {pact.Provider.Name}";
        var builder = Treaty.DefineContract(contractName);

        foreach (var interaction in pact.Interactions)
        {
            var path = NormalizePathToTemplate(interaction.Request.Path);
            var endpointBuilder = builder.ForEndpoint(path);

            // Set HTTP method
            var method = new HttpMethod(interaction.Request.Method);
            endpointBuilder.WithMethod(method);

            // Add provider states
            AddProviderStates(endpointBuilder, interaction);

            // Add request headers
            if (interaction.Request.Headers != null)
            {
                foreach (var (headerName, _) in interaction.Request.Headers)
                {
                    endpointBuilder.WithHeader(headerName);
                }
            }

            // Add response expectation
            endpointBuilder.ExpectingResponse(r =>
            {
                r.WithStatus(interaction.Response.Status);

                // Add response headers
                if (interaction.Response.Headers != null)
                {
                    foreach (var (headerName, headerValue) in interaction.Response.Headers)
                    {
                        if (headerName.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                        {
                            r.WithContentType(headerValue);
                        }
                        else
                        {
                            r.WithHeader(headerName, headerValue);
                        }
                    }
                }

                // Note: We don't import body validation directly since Pact uses example values
                // Users can add .WithJsonBody<T>() or matchers as needed
            });

            // Extract example data for endpoint verification
            AddExampleData(endpointBuilder, interaction);
        }

        return builder.Build();
    }

    private static void AddProviderStates(EndpointBuilder builder, PactInteraction interaction)
    {
        // Handle various Pact versions for provider states
        if (interaction.ProviderStates?.Count > 0)
        {
            foreach (var state in interaction.ProviderStates)
            {
                if (state.Params?.Count > 0)
                {
                    builder.Given(state.Name, state.Params);
                }
                else
                {
                    builder.Given(state.Name);
                }
            }
        }
        else if (interaction.ProviderStatesV2?.Count > 0)
        {
            foreach (var state in interaction.ProviderStatesV2)
            {
                if (state.Params?.Count > 0)
                {
                    builder.Given(state.Name, state.Params);
                }
                else
                {
                    builder.Given(state.Name);
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(interaction.ProviderStateV1))
        {
            builder.Given(interaction.ProviderStateV1);
        }
    }

    private static void AddExampleData(EndpointBuilder builder, PactInteraction interaction)
    {
        // Extract path parameters from the path
        var pathParams = ExtractPathParams(interaction.Request.Path);

        // Extract query parameters
        var queryParams = ParseQueryString(interaction.Request.Query);

        // Extract headers
        var headers = interaction.Request.Headers;

        if (pathParams.Count > 0 || queryParams.Count > 0 || headers?.Count > 0)
        {
            builder.WithExampleData(ex =>
            {
                foreach (var (name, value) in pathParams)
                {
                    ex.WithPathParam(name, value);
                }

                foreach (var (name, value) in queryParams)
                {
                    ex.WithQueryParam(name, value);
                }

                if (headers != null)
                {
                    foreach (var (name, value) in headers)
                    {
                        ex.WithHeader(name, value);
                    }
                }
            });
        }
    }

    private static string NormalizePathToTemplate(string path)
    {
        // Convert actual values in path to template parameters
        // e.g., /users/123 -> /users/{id}
        // This is a heuristic - in practice, paths in Pact often already have concrete values

        // Try to detect numeric IDs and convert to {id}
        var normalized = Regex.Replace(path, @"/(\d+)(?=/|$)", "/{id}");

        // Try to detect UUIDs and convert to {id}
        normalized = Regex.Replace(
            normalized,
            @"/([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})(?=/|$)",
            "/{id}");

        return normalized;
    }

    private static Dictionary<string, object> ExtractPathParams(string path)
    {
        var result = new Dictionary<string, object>();

        // Extract numeric IDs
        var numericMatch = Regex.Match(path, @"/(\d+)(?=/|$)");
        if (numericMatch.Success)
        {
            if (int.TryParse(numericMatch.Groups[1].Value, out var intValue))
            {
                result["id"] = intValue;
            }
            else
            {
                result["id"] = numericMatch.Groups[1].Value;
            }
        }

        // Extract UUIDs
        var uuidMatch = Regex.Match(
            path,
            @"/([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})(?=/|$)");
        if (uuidMatch.Success)
        {
            result["id"] = uuidMatch.Groups[1].Value;
        }

        return result;
    }

    private static Dictionary<string, string> ParseQueryString(string? query)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(query))
            return result;

        // Remove leading ? if present
        query = query.TrimStart('?');

        var pairs = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2)
            {
                result[Uri.UnescapeDataString(parts[0])] = Uri.UnescapeDataString(parts[1]);
            }
            else if (parts.Length == 1)
            {
                result[Uri.UnescapeDataString(parts[0])] = string.Empty;
            }
        }

        return result;
    }
}
