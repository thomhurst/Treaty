using FluentAssertions;
using Treaty.Provider;
using Treaty.Tests.TestApi;
using TreatyLib = Treaty.Treaty;

namespace Treaty.Tests.Integration.Provider;

public class ProviderVerifierBulkTests : IDisposable
{
    private ProviderVerifier<TestStartup> _verifier = null!;

    public ProviderVerifierBulkTests()
    {
        var contract = TreatyLib.DefineContract("TestConsumer")
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Get)
                .WithExamplePathParams(new { id = 1 })
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithJsonBody<TestUser>())
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithJsonBody<TestUser[]>())
            .Build();

        _verifier = TreatyLib.ForProvider<TestStartup>()
            .WithContract(contract)
            .Build();
    }

    public void Dispose()
    {
        _verifier?.Dispose();
    }

    [Test]
    public async Task VerifyAllAsync_AllEndpointsPass_ReturnsAllPassed()
    {
        // Act
        var result = await _verifier.VerifyAllAsync();

        // Assert
        result.AllPassed.Should().BeTrue();
        result.PassedCount.Should().BeGreaterOrEqualTo(1);
        result.FailedCount.Should().Be(0);
    }

    [Test]
    public async Task VerifyAllAsync_WithSkippedEndpoints_CountsSkipped()
    {
        // Arrange - Create contract with missing example data
        var contractWithMissingExampleData = TreatyLib.DefineContract("TestConsumer")
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Get)
                // No example data provided
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithJsonBody<TestUser>())
            .Build();

        using var verifier = TreatyLib.ForProvider<TestStartup>()
            .WithContract(contractWithMissingExampleData)
            .Build();

        // Act
        var result = await verifier.VerifyAllAsync();

        // Assert
        result.SkippedCount.Should().BeGreaterOrEqualTo(1);
    }

    [Test]
    public async Task VerifyAsync_WithFilter_OnlyVerifiesMatchingEndpoints()
    {
        // Act
        var result = await _verifier.VerifyAsync(e => e.Method == HttpMethod.Get);

        // Assert
        result.TotalCount.Should().BeGreaterOrEqualTo(1);
        result.Results.All(r => r.Endpoint.Method == HttpMethod.Get).Should().BeTrue();
    }

    [Test]
    public async Task VerifyAllAsync_WithStopOnFirstFailure_StopsAfterFirstFailure()
    {
        // Arrange
        var contractWithFailure = TreatyLib.DefineContract("TestConsumer")
            .ForEndpoint("/nonexistent")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithJsonBody<TestUser>())
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithJsonBody<TestUser[]>())
            .Build();

        using var verifier = TreatyLib.ForProvider<TestStartup>()
            .WithContract(contractWithFailure)
            .Build();

        var options = new VerificationOptions { StopOnFirstFailure = true };

        // Act
        var result = await verifier.VerifyAllAsync(options);

        // Assert - should have at least one failure, and possibly stopped early
        result.FailedCount.Should().BeGreaterOrEqualTo(1);
    }

    [Test]
    public async Task VerifyAllAsync_GetSummary_ReturnsFormattedSummary()
    {
        // Act
        var result = await _verifier.VerifyAllAsync();
        var summary = result.GetSummary();

        // Assert
        summary.Should().Contain("VERIFICATION SUMMARY");
        summary.Should().Contain("Passed:");
    }

    [Test]
    public async Task VerifyAllAsync_WithProgress_ReportsProgress()
    {
        // Arrange
        var progressReports = new List<VerificationProgress>();
        var progress = new Progress<VerificationProgress>(p => progressReports.Add(p));

        // Act
        await _verifier.VerifyAllAsync(null, progress);

        // Give time for progress updates to be processed
        await Task.Delay(100);

        // Assert
        progressReports.Should().NotBeEmpty();
    }

    [Test]
    public async Task BulkVerificationResult_ToString_ReturnsReadableSummary()
    {
        // Act
        var result = await _verifier.VerifyAllAsync();
        var str = result.ToString();

        // Assert
        str.Should().Contain("Verification");
    }

    [Test]
    public async Task EndpointVerificationResult_ToString_IncludesEndpointInfo()
    {
        // Act
        var result = await _verifier.VerifyAllAsync();

        // Assert
        result.Results.Should().NotBeEmpty();
        var endpointResult = result.Results.First();
        endpointResult.ToString().Should().Contain(endpointResult.Endpoint.Method.Method);
    }

    private record TestUser(int Id, string Name, string Email);
}
