using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Treaty.Contracts;

namespace Treaty.Provider;

/// <summary>
/// Verifies that an API provider implementation meets contract expectations using TestServer.
/// </summary>
/// <typeparam name="TStartup">The startup class of the API being verified.</typeparam>
public sealed class ProviderVerifier<TStartup> : ProviderVerifierBase where TStartup : class
{
    private readonly IHost _host;
    private readonly HttpClient _client;

    internal ProviderVerifier(
        ApiContract contract,
        ILoggerFactory loggerFactory,
        IStateHandler? stateHandler = null,
        IEnumerable<Action<IServiceCollection>>? serviceConfigurations = null,
        IEnumerable<Action<IConfigurationBuilder>>? configurationActions = null,
        IEnumerable<Action<IWebHostBuilder>>? webHostConfigurations = null,
        string? environment = null)
        : base(contract, loggerFactory, stateHandler)
    {
        var builder = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .UseStartup<TStartup>();

                // Apply environment if specified
                if (!string.IsNullOrEmpty(environment))
                {
                    webBuilder.UseEnvironment(environment);
                }

                // Apply configuration overrides
                if (configurationActions != null)
                {
                    foreach (var configAction in configurationActions)
                    {
                        webBuilder.ConfigureAppConfiguration(configAction);
                    }
                }

                // Apply service configurations (run after Startup.ConfigureServices)
                if (serviceConfigurations != null)
                {
                    webBuilder.ConfigureServices(services =>
                    {
                        foreach (var serviceConfig in serviceConfigurations)
                        {
                            serviceConfig(services);
                        }
                    });
                }

                // Apply custom web host configurations
                if (webHostConfigurations != null)
                {
                    foreach (var webHostConfig in webHostConfigurations)
                    {
                        webHostConfig(webBuilder);
                    }
                }
            });

        _host = builder.Build();
        _host.Start();
        _client = _host.GetTestClient();
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
                _host.Dispose();
                _disposed = true;
            }
        }
    }
}
