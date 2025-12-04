using FluentAssertions;
using Treaty.Tests.TestApi;
using Treaty.Validation;
using Xunit;
using TreatyLib = Treaty.Treaty;
using TreatyProvider = Treaty.Provider;

namespace Treaty.Tests;

public class ProviderIntegrationTests : IDisposable
{
    private readonly TreatyProvider.ProviderVerifier<TestStartup> _provider;

    public ProviderIntegrationTests()
    {
        var contract = TreatyLib.DefineContract("TestApi")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithJsonBody<TestUser[]>())
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithJsonBody<TestUser>())
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingRequest(req => req.WithJsonBody<CreateUserRequest>())
                .ExpectingResponse(r => r
                    .WithStatus(201)
                    .WithJsonBody<TestUser>())
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Delete)
                .ExpectingResponse(r => r.WithStatus(204))
            .Build();

        _provider = TreatyLib.ForProvider<TestStartup>()
            .WithContract(contract)
            .Build();
    }

    [Fact]
    public async Task VerifyAsync_GetUsers_PassesValidation()
    {
        // Act & Assert - should not throw
        await _provider.VerifyAsync("/users", HttpMethod.Get);
    }

    [Fact]
    public async Task VerifyAsync_GetUserById_PassesValidation()
    {
        // Act & Assert - should not throw
        await _provider.VerifyAsync("/users/1", HttpMethod.Get);
    }

    [Fact]
    public async Task VerifyAsync_CreateUser_PassesValidation()
    {
        // Act & Assert - should not throw
        await _provider.VerifyAsync("/users", HttpMethod.Post, new CreateUserRequest("Test User", "test@example.com"));
    }

    [Fact]
    public async Task VerifyAsync_DeleteUser_PassesValidation()
    {
        // Act & Assert - should not throw
        await _provider.VerifyAsync("/users/1", HttpMethod.Delete);
    }

    [Fact]
    public async Task TryVerifyAsync_GetUsers_ReturnsSuccessResult()
    {
        // Act
        var result = await _provider.TryVerifyAsync("/users", HttpMethod.Get);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Violations.Should().BeEmpty();
    }

    [Fact]
    public async Task TryVerifyAsync_GetUserById_ReturnsSuccessResult()
    {
        // Act
        var result = await _provider.TryVerifyAsync("/users/123", HttpMethod.Get);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task TryVerifyAsync_UndefinedEndpoint_ReturnsFailure()
    {
        // Act
        var result = await _provider.TryVerifyAsync("/nonexistent", HttpMethod.Get);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violations.Should().ContainSingle()
            .Which.Message.Should().Contain("No contract definition found");
    }

    [Fact]
    public async Task VerifyAsync_WithPathParameter_ExtractsParameterCorrectly()
    {
        // Act & Assert - different path parameter values should work
        await _provider.VerifyAsync("/users/1", HttpMethod.Get);
        await _provider.VerifyAsync("/users/100", HttpMethod.Get);
        await _provider.VerifyAsync("/users/abc", HttpMethod.Get);
    }

    public void Dispose()
    {
        _provider.Dispose();
    }

    private record TestUser(int Id, string Name, string Email);
    private record CreateUserRequest(string Name, string Email);
}
