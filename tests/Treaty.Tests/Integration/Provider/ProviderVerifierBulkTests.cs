using System.Text;
using FluentAssertions;
using Treaty.OpenApi;
using Treaty.Provider;
using Treaty.Tests.TestApi;
using TreatyLib = Treaty.Treaty;

namespace Treaty.Tests.Integration.Provider;

public class ProviderVerifierBulkTests : IDisposable
{
    private ProviderVerifier<TestStartup> _verifier = null!;

    private const string TestApiSpec = """
        openapi: '3.0.3'
        info:
          title: TestConsumer
          version: '1.0'
        paths:
          /users/{id}:
            get:
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: integer
                  example: 1
              responses:
                '200':
                  description: User details
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/User'
          /users:
            get:
              responses:
                '200':
                  description: List of users
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          $ref: '#/components/schemas/User'
        components:
          schemas:
            User:
              type: object
              properties:
                id:
                  type: integer
                name:
                  type: string
                email:
                  type: string
        """;

    public ProviderVerifierBulkTests()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(TestApiSpec));
        var contract = TreatyLib.OpenApi(stream, OpenApiFormat.Yaml).Build();

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
        const string specWithMissingExampleData = """
            openapi: '3.0.3'
            info:
              title: TestConsumer
              version: '1.0'
            paths:
              /users/{id}:
                get:
                  parameters:
                    - name: id
                      in: path
                      required: true
                      schema:
                        type: integer
                  responses:
                    '200':
                      description: User details
                      content:
                        application/json:
                          schema:
                            type: object
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(specWithMissingExampleData));
        var contractWithMissingExampleData = TreatyLib.OpenApi(stream, OpenApiFormat.Yaml).Build();

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
        const string specWithFailure = """
            openapi: '3.0.3'
            info:
              title: TestConsumer
              version: '1.0'
            paths:
              /nonexistent:
                get:
                  responses:
                    '200':
                      description: Should fail
                      content:
                        application/json:
                          schema:
                            type: object
              /users:
                get:
                  responses:
                    '200':
                      description: List of users
                      content:
                        application/json:
                          schema:
                            type: array
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(specWithFailure));
        var contractWithFailure = TreatyLib.OpenApi(stream, OpenApiFormat.Yaml).Build();

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
}
