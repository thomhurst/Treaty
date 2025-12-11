using Treaty.Contracts;

namespace Treaty.Validation;

/// <summary>
/// Interface for validating JSON content against a schema.
/// </summary>
public interface ISchemaValidator
{
    /// <summary>
    /// Gets the expected CLR type, if any.
    /// </summary>
    Type? ExpectedType { get; }

    /// <summary>
    /// Gets the root schema type name (e.g., "object", "array", "string", "integer", "number", "boolean").
    /// Used for contract comparison when ExpectedType is not available.
    /// </summary>
    string? SchemaTypeName { get; }

    /// <summary>
    /// Validates the given JSON content against the schema.
    /// </summary>
    /// <param name="json">The JSON content to validate.</param>
    /// <param name="path">The current JSON path (for error reporting).</param>
    /// <param name="partialValidation">Optional partial validation configuration.</param>
    /// <returns>A list of validation violations, empty if valid.</returns>
    IReadOnlyList<ContractViolation> Validate(string json, string path, PartialValidationConfig? partialValidation = null);
}
