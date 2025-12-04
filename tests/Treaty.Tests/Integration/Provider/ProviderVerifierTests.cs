using FluentAssertions;
using Treaty.Tests.TestApi;
using Treaty.Validation;
using TreatyLib = Treaty.Treaty;
using TreatyProvider = Treaty.Provider;

namespace Treaty.Tests.Integration.Provider;

public class ProviderVerifierTests : IDisposable
{
    private readonly TreatyProvider.ProviderVerifier<TestStartup> _provider;

    public ProviderVerifierTests()
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

    [Test]
    public async Task VerifyAsync_GetUsers_PassesValidation()
    {
        // Act & Assert - should not throw
        await _provider.VerifyAsync("/users", HttpMethod.Get);
    }

    [Test]
    public async Task VerifyAsync_GetUserById_PassesValidation()
    {
        // Act & Assert - should not throw
        await _provider.VerifyAsync("/users/1", HttpMethod.Get);
    }

    [Test]
    public async Task VerifyAsync_CreateUser_PassesValidation()
    {
        // Act & Assert - should not throw
        await _provider.VerifyAsync("/users", HttpMethod.Post, new CreateUserRequest("Test User", "test@example.com"));
    }

    [Test]
    public async Task VerifyAsync_DeleteUser_PassesValidation()
    {
        // Act & Assert - should not throw
        await _provider.VerifyAsync("/users/1", HttpMethod.Delete);
    }

    [Test]
    public async Task TryVerifyAsync_GetUsers_ReturnsSuccessResult()
    {
        // Act
        var result = await _provider.TryVerifyAsync("/users", HttpMethod.Get);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Violations.Should().BeEmpty();
    }

    [Test]
    public async Task TryVerifyAsync_GetUserById_ReturnsSuccessResult()
    {
        // Act
        var result = await _provider.TryVerifyAsync("/users/123", HttpMethod.Get);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Test]
    public async Task TryVerifyAsync_UndefinedEndpoint_ReturnsFailure()
    {
        // Act
        var result = await _provider.TryVerifyAsync("/nonexistent", HttpMethod.Get);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violations.Should().ContainSingle()
            .Which.Message.Should().Contain("No contract definition found");
    }

    [Test]
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
