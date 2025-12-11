using Treaty.Contracts;

namespace Treaty.Validation;

/// <summary>
/// Interface for generating sample values that conform to a schema.
/// </summary>
public interface ISchemaGenerator
{
    /// <summary>
    /// Generates a sample value conforming to this schema.
    /// </summary>
    /// <returns>A JSON string representing a valid sample value.</returns>
    string GenerateSample();

    /// <summary>
    /// Generates a sample value conforming to this schema with direction awareness.
    /// </summary>
    /// <param name="direction">The validation direction for readOnly/writeOnly field handling.</param>
    /// <returns>A JSON string representing a valid sample value.</returns>
    string GenerateSample(ValidationDirection direction);
}
