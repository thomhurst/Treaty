using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Treaty.Tests.Integration.Provider;

namespace Treaty.Tests.TestApi;

public class ConfigurableTestStartup
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public ConfigurableTestStartup(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddRouting();
        services.AddSingleton<ITestService, DefaultTestService>();
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            // Returns a configuration value
            endpoints.MapGet("/config", async context =>
            {
                var configValue = _configuration["TestSettings:ConfigValue"] ?? "DefaultConfigValue";
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { value = configValue }));
            });

            // Returns a response from the injected service
            endpoints.MapGet("/service", async context =>
            {
                var service = app.ApplicationServices.GetRequiredService<ITestService>();
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { message = service.GetMessage() }));
            });

            // Returns the current environment name
            endpoints.MapGet("/environment", async context =>
            {
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { environment = _environment.EnvironmentName }));
            });
        });
    }
}
