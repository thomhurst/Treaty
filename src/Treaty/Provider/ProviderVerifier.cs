using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Treaty.Contracts;

namespace Treaty.Provider;

/// <summary>
/// Verifies that an API provider implementation meets contract expectations using WebApplicationFactory.
/// </summary>
/// <typeparam name="TEntryPoint">The entry point class of the API being verified (typically Program or Startup).</typeparam>
public sealed class ProviderVerifier<TEntryPoint> : ProviderVerifierBase where TEntryPoint : class
{
    private readonly WebApplicationFactory<TEntryPoint> _factory;
    private readonly HttpClient _client;

    internal ProviderVerifier(
        ContractDefinition contract,
        ILoggerFactory loggerFactory,
        IStateHandler? stateHandler = null,
        IEnumerable<Action<IServiceCollection>>? serviceConfigurations = null,
        IEnumerable<Action<IConfigurationBuilder>>? configurationActions = null,
        IEnumerable<Action<IWebHostBuilder>>? webHostConfigurations = null,
        string? environment = null)
        : base(contract, loggerFactory, stateHandler)
    {
        _factory = new TreatyWebApplicationFactory<TEntryPoint>(
            environment,
            configurationActions,
            serviceConfigurations,
            webHostConfigurations);

        _client = _factory.CreateClient();
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendRequestAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        return _client.SendAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        lock (_disposeLock)
        {
            if (!_disposed)
            {
                _client.Dispose();
                _factory.Dispose();
                _disposed = true;
            }
        }
    }
}

/// <summary>
/// Custom WebApplicationFactory for Treaty provider verification.
/// Supports both traditional Startup classes and minimal API Program classes.
/// </summary>
internal sealed class TreatyWebApplicationFactory<TEntryPoint>(
    string? environment,
    IEnumerable<Action<IConfigurationBuilder>>? configurationActions,
    IEnumerable<Action<IServiceCollection>>? serviceConfigurations,
    IEnumerable<Action<IWebHostBuilder>>? webHostConfigurations)
    : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    private readonly string? _environment = environment;
    private readonly IEnumerable<Action<IConfigurationBuilder>>? _configurationActions = configurationActions;
    private readonly IEnumerable<Action<IServiceCollection>>? _serviceConfigurations = serviceConfigurations;
    private readonly IEnumerable<Action<IWebHostBuilder>>? _webHostConfigurations = webHostConfigurations;
    private readonly bool _isStartupClass = IsStartupClass();

    /// <summary>
    /// Detects if TEntryPoint is a traditional Startup class (has ConfigureServices/Configure methods)
    /// rather than a minimal API Program class.
    /// </summary>
    private static bool IsStartupClass()
    {
        var type = typeof(TEntryPoint);

        // Check for Configure method (required for Startup classes) - can be instance or static
        var hasConfigureMethod = type.GetMethod("Configure", BindingFlags.Public | BindingFlags.Instance) != null ||
                                   type.GetMethod("Configure", BindingFlags.Public | BindingFlags.Static) != null;

        // Check for ConfigureServices method (common in Startup classes) - can be instance or static
        var hasConfigureServicesMethod = type.GetMethod("ConfigureServices", BindingFlags.Public | BindingFlags.Instance) != null ||
                                           type.GetMethod("ConfigureServices", BindingFlags.Public | BindingFlags.Static) != null;

        return hasConfigureMethod || hasConfigureServicesMethod;
    }

    protected override IHostBuilder? CreateHostBuilder()
    {
        // For traditional Startup classes, create a host builder that uses UseStartup<T>
        if (_isStartupClass)
        {
            return Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<TEntryPoint>();
                });
        }

        // For minimal API apps, let the base class handle it
        return null;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // For Startup classes in test assemblies, set content root to current directory
        // to avoid path resolution issues
        if (_isStartupClass)
        {
            builder.UseContentRoot(Directory.GetCurrentDirectory());
        }

        if (!string.IsNullOrEmpty(_environment))
        {
            builder.UseEnvironment(_environment);
        }

        if (_configurationActions != null)
        {
            foreach (var configAction in _configurationActions)
            {
                builder.ConfigureAppConfiguration(configAction);
            }
        }

        // Use ConfigureTestServices to ensure test overrides run after the app's ConfigureServices
        if (_serviceConfigurations != null)
        {
            builder.ConfigureTestServices(services =>
            {
                foreach (var serviceConfig in _serviceConfigurations)
                {
                    serviceConfig(services);
                }
            });
        }

        if (_webHostConfigurations != null)
        {
            foreach (var webHostConfig in _webHostConfigurations)
            {
                webHostConfig(builder);
            }
        }
    }
}
