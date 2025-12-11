namespace Treaty.Validation;

/// <summary>
/// Represents the result of contract validation.
/// </summary>
/// <param name="Endpoint">The endpoint that was validated.</param>
/// <param name="Violations">All contract violations that were detected.</param>
public sealed record ValidationResult(
    string Endpoint,
    IReadOnlyList<ContractViolation> Violations)
{
    /// <summary>
    /// Gets whether the validation passed with no violations.
    /// </summary>
    public bool IsValid => Violations.Count == 0;

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
        {
            throw new ContractViolationException(Violations);
        }
    }
}
