using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
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
/// Custom WebApplicationFactory that supports both traditional Startup classes and minimal API Program classes.
/// </summary>
internal sealed class TreatyWebApplicationFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint> where TEntryPoint : class
{
    private readonly string? _environment;
    private readonly IEnumerable<Action<IConfigurationBuilder>>? _configurationActions;
    private readonly IEnumerable<Action<IServiceCollection>>? _serviceConfigurations;
    private readonly IEnumerable<Action<IWebHostBuilder>>? _webHostConfigurations;

    public TreatyWebApplicationFactory(
        string? environment,
        IEnumerable<Action<IConfigurationBuilder>>? configurationActions,
        IEnumerable<Action<IServiceCollection>>? serviceConfigurations,
        IEnumerable<Action<IWebHostBuilder>>? webHostConfigurations)
    {
        _environment = environment;
        _configurationActions = configurationActions;
        _serviceConfigurations = serviceConfigurations;
        _webHostConfigurations = webHostConfigurations;
    }

    protected override IHostBuilder? CreateHostBuilder()
    {
        // For traditional Startup classes, create a host builder that uses UseStartup<T>
        // This is called before CreateHost and allows us to set up the Startup pattern
        return Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<TEntryPoint>();
            });
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set content root to current directory (required for Startup classes in test assemblies)
        builder.UseContentRoot(Directory.GetCurrentDirectory());

        // Apply environment if specified
        if (!string.IsNullOrEmpty(_environment))
        {
            builder.UseEnvironment(_environment);
        }

        // Apply configuration overrides
        if (_configurationActions != null)
        {
            foreach (var configAction in _configurationActions)
            {
                builder.ConfigureAppConfiguration(configAction);
            }
        }

        // Apply service configurations (run after app's ConfigureServices)
        if (_serviceConfigurations != null)
        {
            builder.ConfigureServices(services =>
            {
                foreach (var serviceConfig in _serviceConfigurations)
                {
                    serviceConfig(services);
                }
            });
        }

        // Apply custom web host configurations
        if (_webHostConfigurations != null)
        {
            foreach (var webHostConfig in _webHostConfigurations)
            {
                webHostConfig(builder);
            }
        }
    }
}
