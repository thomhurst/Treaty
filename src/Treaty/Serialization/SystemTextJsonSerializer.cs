using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Treaty.Serialization;

/// <summary>
/// Default JSON serializer implementation using System.Text.Json.
/// </summary>
public sealed class SystemTextJsonSerializer : IJsonSerializer
{
    private readonly JsonSerializerOptions _options;

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

    /// <summary>
    /// Creates a new instance with custom options.
    /// </summary>
    /// <param name="options">The JSON serializer options to use.</param>
    public SystemTextJsonSerializer(JsonSerializerOptions options)
    {
        _options = options;
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
