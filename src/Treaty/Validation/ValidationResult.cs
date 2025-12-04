namespace Treaty.Validation;

/// <summary>
/// Represents the result of contract validation.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>
    /// Gets whether the validation passed with no violations.
    /// </summary>
    public bool IsValid => Violations.Count == 0;

    /// <summary>
    /// Gets all contract violations that were detected.
    /// </summary>
    public IReadOnlyList<ContractViolation> Violations { get; }

    /// <summary>
    /// Gets the endpoint that was validated.
    /// </summary>
    public string Endpoint { get; }

    private ValidationResult(string endpoint, IReadOnlyList<ContractViolation> violations)
    {
        Endpoint = endpoint;
        Violations = violations;
    }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success(string endpoint) => new(endpoint, []);

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static ValidationResult Failure(string endpoint, IReadOnlyList<ContractViolation> violations)
        => new(endpoint, violations);

    /// <summary>
    /// Creates a failed validation result with a single violation.
    /// </summary>
    public static ValidationResult Failure(string endpoint, ContractViolation violation)
        => new(endpoint, [violation]);

    /// <summary>
    /// Throws a <see cref="ContractViolationException"/> if validation failed.
    /// </summary>
    public void ThrowIfInvalid()
    {
        if (!IsValid)
            throw new ContractViolationException(Violations);
    }
}
