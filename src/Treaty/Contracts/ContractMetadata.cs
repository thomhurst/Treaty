namespace Treaty.Contracts;

/// <summary>
/// Metadata describing a contract, typically extracted from OpenAPI info section.
/// </summary>
/// <remarks>
/// Creates a new contract metadata instance.
/// </remarks>
public sealed class ContractMetadata(
    string? version = null,
    string? description = null,
    ContractContact? contact = null,
    ContractLicense? license = null,
    string? termsOfService = null)
{
    /// <summary>
    /// Gets the version of the API (e.g., "1.0.0").
    /// </summary>
    public string? Version { get; } = version;

    /// <summary>
    /// Gets the description of the API.
    /// </summary>
    public string? Description { get; } = description;

    /// <summary>
    /// Gets the contact information for the API.
    /// </summary>
    public ContractContact? Contact { get; } = contact;

    /// <summary>
    /// Gets the license information for the API.
    /// </summary>
    public ContractLicense? License { get; } = license;

    /// <summary>
    /// Gets the URL to the Terms of Service for the API.
    /// </summary>
    public string? TermsOfService { get; } = termsOfService;
}

/// <summary>
/// Contact information for the API.
/// </summary>
/// <remarks>
/// Creates a new contact instance.
/// </remarks>
public sealed class ContractContact(string? name = null, string? email = null, string? url = null)
{
    /// <summary>
    /// Gets the name of the contact person/organization.
    /// </summary>
    public string? Name { get; } = name;

    /// <summary>
    /// Gets the email address of the contact.
    /// </summary>
    public string? Email { get; } = email;

    /// <summary>
    /// Gets the URL pointing to the contact information.
    /// </summary>
    public string? Url { get; } = url;
}

/// <summary>
/// License information for the API.
/// </summary>
/// <remarks>
/// Creates a new license instance.
/// </remarks>
public sealed class ContractLicense(string name, string? url = null)
{
    /// <summary>
    /// Gets the license name (e.g., "MIT", "Apache 2.0").
    /// </summary>
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    /// <summary>
    /// Gets the URL to the license.
    /// </summary>
    public string? Url { get; } = url;
}
