namespace Treaty.Contracts;

/// <summary>
/// Metadata describing a contract, typically extracted from OpenAPI info section.
/// </summary>
public sealed class ContractMetadata
{
    /// <summary>
    /// Gets the version of the API (e.g., "1.0.0").
    /// </summary>
    public string? Version { get; }

    /// <summary>
    /// Gets the description of the API.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets the contact information for the API.
    /// </summary>
    public ContractContact? Contact { get; }

    /// <summary>
    /// Gets the license information for the API.
    /// </summary>
    public ContractLicense? License { get; }

    /// <summary>
    /// Gets the URL to the Terms of Service for the API.
    /// </summary>
    public string? TermsOfService { get; }

    /// <summary>
    /// Creates a new contract metadata instance.
    /// </summary>
    public ContractMetadata(
        string? version = null,
        string? description = null,
        ContractContact? contact = null,
        ContractLicense? license = null,
        string? termsOfService = null)
    {
        Version = version;
        Description = description;
        Contact = contact;
        License = license;
        TermsOfService = termsOfService;
    }
}

/// <summary>
/// Contact information for the API.
/// </summary>
public sealed class ContractContact
{
    /// <summary>
    /// Gets the name of the contact person/organization.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Gets the email address of the contact.
    /// </summary>
    public string? Email { get; }

    /// <summary>
    /// Gets the URL pointing to the contact information.
    /// </summary>
    public string? Url { get; }

    /// <summary>
    /// Creates a new contact instance.
    /// </summary>
    public ContractContact(string? name = null, string? email = null, string? url = null)
    {
        Name = name;
        Email = email;
        Url = url;
    }
}

/// <summary>
/// License information for the API.
/// </summary>
public sealed class ContractLicense
{
    /// <summary>
    /// Gets the license name (e.g., "MIT", "Apache 2.0").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the URL to the license.
    /// </summary>
    public string? Url { get; }

    /// <summary>
    /// Creates a new license instance.
    /// </summary>
    public ContractLicense(string name, string? url = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Url = url;
    }
}
