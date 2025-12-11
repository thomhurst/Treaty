using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Treaty.Serialization;

/// <summary>
/// Default JSON serializer implementation using System.Text.Json.
/// </summary>
/// <remarks>
/// Creates a new instance with custom options.
/// </remarks>
/// <param name="options">The JSON serializer options to use.</param>
public sealed class SystemTextJsonSerializer(JsonSerializerOptions options) : IJsonSerializer
{
    private readonly JsonSerializerOptions _options = options;

    /// <summary>
    /// Creates a new instance with default options.
    /// </summary>
    public SystemTextJsonSerializer()
        : this(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        })
    {
    }

    /// <inheritdoc/>
    public string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, _options);
    }

    /// <inheritdoc/>
    public string Serialize(object? value, Type type)
    {
        return JsonSerializer.Serialize(value, type, _options);
    }

    /// <inheritdoc/>
    public T? Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, _options);
    }

    /// <inheritdoc/>
    public object? Deserialize(string json, Type type)
    {
        return JsonSerializer.Deserialize(json, type, _options);
    }

    /// <inheritdoc/>
    public JsonNode? Parse(string json)
    {
        return JsonNode.Parse(json);
    }
}
