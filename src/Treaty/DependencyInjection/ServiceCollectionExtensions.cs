using Microsoft.Extensions.DependencyInjection;
using Treaty.Contracts;
using Treaty.Mocking;
using Treaty.OpenApi;
using Treaty.Serialization;

namespace Treaty.DependencyInjection;

/// <summary>
/// Extension methods for registering Treaty services with Microsoft.Extensions.DependencyInjection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Treaty services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An action to configure Treaty options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddTreaty(options =>
    /// {
    ///     options.AddContractFromOpenApi("api-spec.yaml");
    ///     options.AddMockServer();
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddTreaty(this IServiceCollection services, Action<TreatyOptions> configure)
    {
        var options = new TreatyOptions(services);
        configure(options);
        return services;
    }

    /// <summary>
    /// Adds a contract definition to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="contract">The contract definition to register.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddContract(this IServiceCollection services, ContractDefinition contract)
    {
        services.AddSingleton(contract);
        return services;
    }

    /// <summary>
    /// Adds a contract from an OpenAPI specification file to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="specPath">Path to the OpenAPI specification file.</param>
    /// <param name="configureBuilder">Optional action to configure the contract builder.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddContractFromOpenApi(
        this IServiceCollection services,
        string specPath,
        Action<OpenApiContractBuilder>? configureBuilder = null)
    {
        var builder = new OpenApiContractBuilder(specPath);
        configureBuilder?.Invoke(builder);
        services.AddSingleton(builder.Build());
        return services;
    }

    /// <summary>
    /// Adds a contract from an OpenAPI specification stream to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="specStream">Stream containing the OpenAPI specification.</param>
    /// <param name="format">The format of the specification.</param>
    /// <param name="configureBuilder">Optional action to configure the contract builder.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddContractFromOpenApi(
        this IServiceCollection services,
        Stream specStream,
        OpenApiFormat format = OpenApiFormat.Yaml,
        Action<OpenApiContractBuilder>? configureBuilder = null)
    {
        var builder = new OpenApiContractBuilder(specStream, format);
        configureBuilder?.Invoke(builder);
        services.AddSingleton(builder.Build());
        return services;
    }

    /// <summary>
    /// Adds a mock server based on the registered contract to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureServer">Optional action to configure the mock server.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMockServer(
        this IServiceCollection services,
        Action<ContractMockServerBuilder>? configureServer = null)
    {
        services.AddSingleton<IMockServer>(sp =>
        {
            var contract = sp.GetRequiredService<ContractDefinition>();
            var builder = MockServer.FromContract(contract);
            configureServer?.Invoke(builder);
            return builder.Build();
        });
        return services;
    }

    /// <summary>
    /// Adds a custom JSON serializer for Treaty to use.
    /// </summary>
    /// <typeparam name="TSerializer">The serializer type implementing <see cref="IJsonSerializer"/>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTreatyJsonSerializer<TSerializer>(this IServiceCollection services)
        where TSerializer : class, IJsonSerializer
    {
        services.AddSingleton<IJsonSerializer, TSerializer>();
        return services;
    }
}

/// <summary>
/// Options for configuring Treaty services.
/// </summary>
public sealed class TreatyOptions
{
    private readonly IServiceCollection _services;

    internal TreatyOptions(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Adds a contract from an OpenAPI specification file.
    /// </summary>
    /// <param name="specPath">Path to the OpenAPI specification file.</param>
    /// <param name="configureBuilder">Optional action to configure the contract builder.</param>
    /// <returns>This options instance for chaining.</returns>
    public TreatyOptions AddContractFromOpenApi(string specPath, Action<OpenApiContractBuilder>? configureBuilder = null)
    {
        _services.AddContractFromOpenApi(specPath, configureBuilder);
        return this;
    }

    /// <summary>
    /// Adds a contract from an OpenAPI specification stream.
    /// </summary>
    /// <param name="specStream">Stream containing the OpenAPI specification.</param>
    /// <param name="format">The format of the specification.</param>
    /// <param name="configureBuilder">Optional action to configure the contract builder.</param>
    /// <returns>This options instance for chaining.</returns>
    public TreatyOptions AddContractFromOpenApi(Stream specStream, OpenApiFormat format = OpenApiFormat.Yaml, Action<OpenApiContractBuilder>? configureBuilder = null)
    {
        _services.AddContractFromOpenApi(specStream, format, configureBuilder);
        return this;
    }

    /// <summary>
    /// Adds a pre-built contract definition.
    /// </summary>
    /// <param name="contract">The contract definition to register.</param>
    /// <returns>This options instance for chaining.</returns>
    public TreatyOptions AddContract(ContractDefinition contract)
    {
        _services.AddContract(contract);
        return this;
    }

    /// <summary>
    /// Adds a mock server based on the registered contract.
    /// </summary>
    /// <param name="configureServer">Optional action to configure the mock server.</param>
    /// <returns>This options instance for chaining.</returns>
    public TreatyOptions AddMockServer(Action<ContractMockServerBuilder>? configureServer = null)
    {
        _services.AddMockServer(configureServer);
        return this;
    }

    /// <summary>
    /// Adds a custom JSON serializer for Treaty to use.
    /// </summary>
    /// <typeparam name="TSerializer">The serializer type implementing <see cref="IJsonSerializer"/>.</typeparam>
    /// <returns>This options instance for chaining.</returns>
    public TreatyOptions UseJsonSerializer<TSerializer>() where TSerializer : class, IJsonSerializer
    {
        _services.AddTreatyJsonSerializer<TSerializer>();
        return this;
    }
}
