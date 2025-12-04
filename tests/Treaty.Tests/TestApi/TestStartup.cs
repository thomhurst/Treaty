using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Treaty.Tests.TestApi;

public class TestStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddRouting();
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGet("/users", async context =>
            {
                var users = new[]
                {
                    new { id = 1, name = "John Doe", email = "john@example.com" },
                    new { id = 2, name = "Jane Doe", email = "jane@example.com" }
                };
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(users));
            });

            endpoints.MapGet("/users/{id}", async context =>
            {
                var id = context.Request.RouteValues["id"]?.ToString();
                if (id == "0" || id == "999")
                {
                    context.Response.StatusCode = 404;
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "User not found" }));
                    return;
                }

                // Parse id if numeric, otherwise use a default
                var numericId = int.TryParse(id, out var parsedId) ? parsedId : 1;
                var user = new { id = numericId, name = "John Doe", email = "john@example.com" };
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(user));
            });

            endpoints.MapPost("/users", async context =>
            {
                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync();
                var request = JsonSerializer.Deserialize<CreateUserRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (string.IsNullOrEmpty(request?.Name))
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "Name is required" }));
                    return;
                }

                context.Response.StatusCode = 201;
                context.Response.ContentType = "application/json";
                var user = new { id = 3, name = request.Name, email = request.Email };
                await context.Response.WriteAsync(JsonSerializer.Serialize(user));
            });

            endpoints.MapDelete("/users/{id}", context =>
            {
                context.Response.StatusCode = 204;
                return Task.CompletedTask;
            });

            // Endpoint that returns invalid data (for testing validation failures)
            endpoints.MapGet("/users/{id}/invalid", async context =>
            {
                // Return invalid data - age is negative, email is invalid
                var invalidUser = new { id = 1, name = "John", email = "not-an-email", age = -5 };
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(invalidUser));
            });
        });
    }

    private record CreateUserRequest(string? Name, string? Email);
}
