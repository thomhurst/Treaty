using Treaty.Contracts;
using Treaty.OpenApi;

namespace Treaty;

/// <summary>
/// Entry point for loading and comparing API contracts.
/// </summary>
public static class Contract
{
    /// <summary>
    /// Creates a contract from an OpenAPI specification file.
    /// </summary>
    /// <param name="specPath">Path to the OpenAPI specification file (YAML or JSON).</param>
    /// <returns>An OpenAPI contract builder for customization.</returns>
    /// <example>
    /// <code>
    /// var contract = Contract.FromOpenApi("api-spec.yaml")
    ///     .ForEndpoint("/users/{id}")  // optional filtering
    ///     .Build();
    /// </code>
    /// </example>
    public static OpenContractDefinitionBuilder FromOpenApi(string specPath)
        => new(specPath);

    /// <summary>
    /// Creates a contract from an OpenAPI specification stream.
    /// </summary>
    /// <param name="specStream">Stream containing the OpenAPI specification.</param>
    /// <param name="format">The format of the specification (yaml or json).</param>
    /// <returns>An OpenAPI contract builder for customization.</returns>
    public static OpenContractDefinitionBuilder FromOpenApi(Stream specStream, OpenApiFormat format = OpenApiFormat.Yaml)
        => new(specStream, format);

    /// <summary>
    /// Compares two contracts and returns a diff describing all changes.
    /// Use this to detect breaking changes between contract versions.
    /// </summary>
    /// <param name="oldContract">The baseline (old) contract.</param>
    /// <param name="newContract">The new contract to compare.</param>
    /// <returns>A diff containing all detected changes with severity levels.</returns>
    /// <example>
    /// <code>
    /// var oldContract = Contract.FromOpenApi("api-v1.yaml").Build();
    /// var newContract = Contract.FromOpenApi("api-v2.yaml").Build();
    ///
    /// var diff = Contract.Compare(oldContract, newContract);
    ///
    /// if (diff.HasBreakingChanges)
    /// {
    ///     Console.WriteLine("Breaking changes detected!");
    ///     foreach (var change in diff.BreakingChanges)
    ///     {
    ///         Console.WriteLine($"  - {change.Description}");
    ///     }
    /// }
    ///
    /// // Or throw if breaking changes exist
    /// diff.ThrowIfBreaking();
    /// </code>
    /// </example>
    public static ContractDiff Compare(ContractDefinition oldContract, ContractDefinition newContract)
        => ContractComparer.Compare(oldContract, newContract);
}
