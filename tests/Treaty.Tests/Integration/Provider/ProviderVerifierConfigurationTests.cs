using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Treaty.OpenApi;
using Treaty.Provider;
using Treaty.Tests.TestApi;

namespace Treaty.Tests.Integration.Provider;

public class ProviderVerifierConfigurationTests : IDisposable
{
    private ProviderVerifier<ConfigurableTestStartup>? _provider;

    private const string TestApiSpec = """
        openapi: '3.0.3'
        info:
          title: TestApi
          version: '1.0'
        paths:
          /config:
            get:
              responses:
                '200':
                  description: Returns config value
                  content:
                    application/json:
                      schema:
                        type: object
                        properties:
                          value:
                            type: string
          /service:
            get:
              responses:
                '200':
                  description: Returns service response
                  content:
                    application/json:
                      schema:
                        type: object
                        properties:
                          message:
                            type: string
          /environment:
            get:
              responses:
                '200':
                  description: Returns environment name
                  content:
                    application/json:
                      schema:
                        type: object
                        properties:
                          environment:
                            type: string
        """;

    [Test]
    public async Task ConfigureServices_ReplacesService_UsesReplacementService()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(TestApiSpec));
        var contract = Contract.FromOpenApi(stream, OpenApiFormat.Yaml).Build();

        _provider = ProviderVerifier.ForWebApplication<ConfigurableTestStartup>()
            .WithContract(contract)
            .ConfigureServices(services =>
            {
                // Remove default service and add mock
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ITestService));
                if (descriptor != null)
                    services.Remove(descriptor);

                services.AddSingleton<ITestService>(new MockTestService("Mocked response"));
            })
            .Build();

        // Act
        var result = await _provider.TryVerifyAsync("/service", HttpMethod.Get);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Test]
    public async Task ConfigureAppConfiguration_OverridesConfigValue_UsesOverriddenValue()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(TestApiSpec));
        var contract = Contract.FromOpenApi(stream, OpenApiFormat.Yaml).Build();

        _provider = ProviderVerifier.ForWebApplication<ConfigurableTestStartup>()
            .WithContract(contract)
            .ConfigureAppConfiguration(config =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TestSettings:ConfigValue"] = "OverriddenValue"
                });
            })
            .Build();

        // Act
        var result = await _provider.TryVerifyAsync("/config", HttpMethod.Get);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Test]
    public async Task UseEnvironment_SetsEnvironment_UsesSpecifiedEnvironment()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(TestApiSpec));
        var contract = Contract.FromOpenApi(stream, OpenApiFormat.Yaml).Build();

        _provider = ProviderVerifier.ForWebApplication<ConfigurableTestStartup>()
            .WithContract(contract)
            .UseEnvironment("Testing")
            .Build();

        // Act
        var result = await _provider.TryVerifyAsync("/environment", HttpMethod.Get);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Test]
    public async Task ConfigureWebHost_CustomConfiguration_AppliesConfiguration()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(TestApiSpec));
        var contract = Contract.FromOpenApi(stream, OpenApiFormat.Yaml).Build();

        _provider = ProviderVerifier.ForWebApplication<ConfigurableTestStartup>()
            .WithContract(contract)
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseSetting("TestSettings:ConfigValue", "WebHostConfiguredValue");
            })
            .Build();

        // Act
        var result = await _provider.TryVerifyAsync("/config", HttpMethod.Get);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Test]
    public async Task MultipleConfigureServices_AllAreApplied()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(TestApiSpec));
        var contract = Contract.FromOpenApi(stream, OpenApiFormat.Yaml).Build();

        var servicesCalled = new List<string>();

        _provider = ProviderVerifier.ForWebApplication<ConfigurableTestStartup>()
            .WithContract(contract)
            .ConfigureServices(services =>
            {
                servicesCalled.Add("First");
            })
            .ConfigureServices(services =>
            {
                servicesCalled.Add("Second");
            })
            .Build();

        // Act
        var result = await _provider.TryVerifyAsync("/config", HttpMethod.Get);

        // Assert
        servicesCalled.Should().ContainInOrder("First", "Second");
    }

    public void Dispose()
    {
        _provider?.Dispose();
    }
}

// Test interfaces and implementations for service replacement tests
public interface ITestService
{
    string GetMessage();
}

public class DefaultTestService : ITestService
{
    public string GetMessage() => "Default response";
}

public class MockTestService : ITestService
{
    private readonly string _message;
    public MockTestService(string message) => _message = message;
    public string GetMessage() => _message;
}
