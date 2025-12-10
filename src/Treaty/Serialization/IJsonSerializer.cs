using System.Text.Json.Nodes;

namespace Treaty.Serialization;

/// <summary>
/// Interface for JSON serialization operations.
/// Implement this interface to use a custom JSON serializer (e.g., Newtonsoft.Json).
/// </summary>
public interface IJsonSerializer
{
    /// <summary>
    /// Serializes an object to a JSON string.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="value">The object to serialize.</param>
    /// <returns>The JSON string representation.</returns>
    string Serialize<T>(T value);

    /// <summary>
    /// Serializes an object to a JSON string.
    /// </summary>
    /// <param name="value">The object to serialize.</param>
    /// <param name="type">The type of the object.</param>
    /// <returns>The JSON string representation.</returns>
    string Serialize(object? value, Type type);

    /// <summary>
    /// Deserializes a JSON string to an object.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized object, or default if the JSON is null.</returns>
    T? Deserialize<T>(string json);

    /// <summary>
    /// Deserializes a JSON string to an object.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="type">The type to deserialize to.</param>
    /// <returns>The deserialized object, or null if the JSON is null.</returns>
    object? Deserialize(string json, Type type);

    /// <summary>
    /// Parses a JSON string into a JsonNode for dynamic access.
    /// </summary>
    /// <param name="json">The JSON string to parse.</param>
    /// <returns>The parsed JsonNode.</returns>
    JsonNode? Parse(string json);
}
