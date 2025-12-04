using System.Text.Json.Nodes;
using Treaty.Validation;

namespace Treaty.Matching;

/// <summary>
/// Interface for flexible value matchers used in contract validation.
/// Matchers allow validating values by pattern (e.g., "any GUID") rather than exact value.
/// </summary>
public interface IMatcher
{
    /// <summary>
    /// Gets the type of this matcher.
    /// </summary>
    MatcherType Type { get; }

    /// <summary>
    /// Gets a human-readable description for error messages.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Validates the given JSON node against this matcher's rules.
    /// </summary>
    /// <param name="node">The JSON node to validate (may be null).</param>
    /// <param name="endpoint">The endpoint being validated (for error reporting).</param>
    /// <param name="path">The JSON path (for error reporting).</param>
    /// <returns>A list of validation violations, empty if valid.</returns>
    IReadOnlyList<ContractViolation> Validate(JsonNode? node, string endpoint, string path);

    /// <summary>
    /// Generates a sample value conforming to this matcher.
    /// Used for documentation and mock response generation.
    /// </summary>
    /// <returns>A sample value that would pass validation.</returns>
    object? GenerateSample();
}
