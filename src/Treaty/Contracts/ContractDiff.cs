namespace Treaty.Contracts;

/// <summary>
/// Represents the result of comparing two contracts.
/// </summary>
public sealed class ContractDiff
{
    /// <summary>
    /// Gets the old (baseline) contract name.
    /// </summary>
    public string OldContractName { get; }

    /// <summary>
    /// Gets the new contract name.
    /// </summary>
    public string NewContractName { get; }

    /// <summary>
    /// Gets all detected changes.
    /// </summary>
    public IReadOnlyList<ContractChange> AllChanges { get; }

    /// <summary>
    /// Gets only the breaking changes.
    /// </summary>
    public IReadOnlyList<ContractChange> BreakingChanges =>
        AllChanges.Where(c => c.Severity == ChangeSeverity.Breaking).ToList();

    /// <summary>
    /// Gets only the warning-level changes.
    /// </summary>
    public IReadOnlyList<ContractChange> Warnings =>
        AllChanges.Where(c => c.Severity == ChangeSeverity.Warning).ToList();

    /// <summary>
    /// Gets only the informational changes.
    /// </summary>
    public IReadOnlyList<ContractChange> InfoChanges =>
        AllChanges.Where(c => c.Severity == ChangeSeverity.Info).ToList();

    /// <summary>
    /// Gets a value indicating whether there are any breaking changes.
    /// </summary>
    public bool HasBreakingChanges => BreakingChanges.Count > 0;

    /// <summary>
    /// Gets a value indicating whether the contracts are compatible (no breaking changes).
    /// </summary>
    public bool IsCompatible => !HasBreakingChanges;

    internal ContractDiff(string oldContractName, string newContractName, IReadOnlyList<ContractChange> changes)
    {
        OldContractName = oldContractName;
        NewContractName = newContractName;
        AllChanges = changes;
    }

    /// <summary>
    /// Gets a formatted summary of all changes.
    /// </summary>
    public string GetSummary()
    {
        var lines = new List<string>
        {
            $"Contract Comparison: '{OldContractName}' -> '{NewContractName}'",
            $"Total Changes: {AllChanges.Count} (Breaking: {BreakingChanges.Count}, Warnings: {Warnings.Count}, Info: {InfoChanges.Count})",
            ""
        };

        if (HasBreakingChanges)
        {
            lines.Add("BREAKING CHANGES:");
            foreach (var change in BreakingChanges)
            {
                lines.Add($"  - {change.Description}");
            }
            lines.Add("");
        }

        if (Warnings.Count > 0)
        {
            lines.Add("WARNINGS:");
            foreach (var change in Warnings)
            {
                lines.Add($"  - {change.Description}");
            }
            lines.Add("");
        }

        if (InfoChanges.Count > 0)
        {
            lines.Add("INFO:");
            foreach (var change in InfoChanges)
            {
                lines.Add($"  - {change.Description}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Throws if there are breaking changes.
    /// </summary>
    /// <exception cref="ContractBreakingChangeException">Thrown when breaking changes are detected.</exception>
    public void ThrowIfBreaking()
    {
        if (HasBreakingChanges)
        {
            throw new ContractBreakingChangeException(this);
        }
    }
}

/// <summary>
/// Represents a single change between two contract versions.
/// </summary>
public sealed class ContractChange
{
    /// <summary>
    /// Gets the severity of the change.
    /// </summary>
    public ChangeSeverity Severity { get; }

    /// <summary>
    /// Gets the type of change.
    /// </summary>
    public ContractChangeType Type { get; }

    /// <summary>
    /// Gets a human-readable description of the change.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the endpoint path affected by this change (if applicable).
    /// </summary>
    public string? Path { get; }

    /// <summary>
    /// Gets the HTTP method affected by this change (if applicable).
    /// </summary>
    public HttpMethod? Method { get; }

    /// <summary>
    /// Gets the location of the change within the endpoint.
    /// </summary>
    public ChangeLocation Location { get; }

    /// <summary>
    /// Gets the field name or path affected by this change (if applicable).
    /// </summary>
    public string? FieldName { get; }

    /// <summary>
    /// Gets the old value or type (if applicable).
    /// </summary>
    public string? OldValue { get; }

    /// <summary>
    /// Gets the new value or type (if applicable).
    /// </summary>
    public string? NewValue { get; }

    internal ContractChange(
        ChangeSeverity severity,
        ContractChangeType type,
        string description,
        string? path = null,
        HttpMethod? method = null,
        ChangeLocation location = ChangeLocation.Endpoint,
        string? fieldName = null,
        string? oldValue = null,
        string? newValue = null)
    {
        Severity = severity;
        Type = type;
        Description = description;
        Path = path;
        Method = method;
        Location = location;
        FieldName = fieldName;
        OldValue = oldValue;
        NewValue = newValue;
    }

    /// <inheritdoc/>
    public override string ToString() => $"[{Severity}] {Description}";
}

/// <summary>
/// Severity levels for contract changes.
/// </summary>
public enum ChangeSeverity
{
    /// <summary>
    /// Informational change that doesn't affect compatibility.
    /// </summary>
    Info,

    /// <summary>
    /// Change that might cause issues but isn't strictly breaking.
    /// </summary>
    Warning,

    /// <summary>
    /// Breaking change that will cause client failures.
    /// </summary>
    Breaking
}

/// <summary>
/// Types of contract changes that can be detected.
/// </summary>
public enum ContractChangeType
{
    // Endpoint-level changes

    /// <summary>A new endpoint was added to the contract.</summary>
    EndpointAdded,

    /// <summary>An endpoint was removed from the contract (breaking change).</summary>
    EndpointRemoved,

    /// <summary>The HTTP method of an endpoint changed.</summary>
    EndpointMethodChanged,

    // Response field changes

    /// <summary>A new field was added to a response body.</summary>
    ResponseFieldAdded,

    /// <summary>A field was removed from a response body (breaking change for consumers).</summary>
    ResponseFieldRemoved,

    /// <summary>The type of a response field changed.</summary>
    ResponseFieldTypeChanged,

    /// <summary>A response field was changed to allow null values.</summary>
    ResponseFieldMadeNullable,

    /// <summary>A response field no longer allows null values (breaking change).</summary>
    ResponseFieldMadeNonNullable,

    // Request field changes

    /// <summary>A new field was added to a request body.</summary>
    RequestFieldAdded,

    /// <summary>A field was removed from a request body.</summary>
    RequestFieldRemoved,

    /// <summary>The type of a request field changed.</summary>
    RequestFieldTypeChanged,

    /// <summary>A request field was changed from optional to required (breaking change).</summary>
    RequestFieldMadeRequired,

    /// <summary>A request field was changed from required to optional.</summary>
    RequestFieldMadeOptional,

    // Status code changes

    /// <summary>A new response status code was added.</summary>
    ResponseStatusCodeAdded,

    /// <summary>A response status code was removed (breaking change).</summary>
    ResponseStatusCodeRemoved,

    /// <summary>A response status code was changed.</summary>
    ResponseStatusCodeChanged,

    // Header changes

    /// <summary>A new header was added to a response.</summary>
    ResponseHeaderAdded,

    /// <summary>A header was removed from a response.</summary>
    ResponseHeaderRemoved,

    /// <summary>A new header was added to a request.</summary>
    RequestHeaderAdded,

    /// <summary>A header was removed from a request.</summary>
    RequestHeaderRemoved
}

/// <summary>
/// Location within an endpoint where a change occurred.
/// </summary>
public enum ChangeLocation
{
    /// <summary>
    /// Change at the endpoint level (e.g., endpoint added/removed).
    /// </summary>
    Endpoint,

    /// <summary>
    /// Change in the request body.
    /// </summary>
    RequestBody,

    /// <summary>
    /// Change in the response body.
    /// </summary>
    ResponseBody,

    /// <summary>
    /// Change in request headers.
    /// </summary>
    RequestHeader,

    /// <summary>
    /// Change in response headers.
    /// </summary>
    ResponseHeader,

    /// <summary>
    /// Change in query parameters.
    /// </summary>
    QueryParameter,

    /// <summary>
    /// Change in path parameters.
    /// </summary>
    PathParameter,

    /// <summary>
    /// Change in status codes.
    /// </summary>
    StatusCode
}

/// <summary>
/// Exception thrown when breaking changes are detected between contracts.
/// </summary>
public sealed class ContractBreakingChangeException : Exception
{
    /// <summary>
    /// Gets the contract diff containing the breaking changes.
    /// </summary>
    public ContractDiff Diff { get; }

    internal ContractBreakingChangeException(ContractDiff diff)
        : base($"Contract has {diff.BreakingChanges.Count} breaking change(s):\n{string.Join("\n", diff.BreakingChanges.Select(c => $"  - {c.Description}"))}")
    {
        Diff = diff;
    }
}
