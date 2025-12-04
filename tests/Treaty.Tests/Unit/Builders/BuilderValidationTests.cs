using FluentAssertions;
using Treaty.Contracts;
using Treaty.Serialization;
using TreatyLib = Treaty.Treaty;

namespace Treaty.Tests.Unit.Builders;

/// <summary>
/// Tests for builder validation - ensuring proper exceptions are thrown for invalid configurations.
/// </summary>
public class BuilderValidationTests
{
    #region ProviderBuilder Validation

    [Test]
    public void ProviderBuilder_Build_WithoutContract_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = TreatyLib.ForProvider<TestStartup>();

        // Act
        var act = () => builder.Build();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*contract*");
    }

    [Test]
    public void ProviderBuilder_WithContract_Null_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = TreatyLib.ForProvider<TestStartup>();

        // Act
        var act = () => builder.WithContract(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void ProviderBuilder_WithLogging_Null_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = TreatyLib.ForProvider<TestStartup>();

        // Act
        var act = () => builder.WithLogging(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region ConsumerBuilder Validation

    [Test]
    public void ConsumerBuilder_Build_WithoutContract_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = TreatyLib.ForConsumer();

        // Act
        var act = () => builder.Build();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*contract*");
    }

    [Test]
    public void ConsumerBuilder_WithContract_Null_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = TreatyLib.ForConsumer();

        // Act
        var act = () => builder.WithContract(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void ConsumerBuilder_WithBaseUrl_Null_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = TreatyLib.ForConsumer();

        // Act
        var act = () => builder.WithBaseUrl(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void ConsumerBuilder_WithLogging_Null_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = TreatyLib.ForConsumer();

        // Act
        var act = () => builder.WithLogging(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region ContractBuilder Validation

    [Test]
    public void ContractBuilder_WithJsonSerializer_Null_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = TreatyLib.DefineContract();

        // Act
        var act = () => builder.WithJsonSerializer(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void ContractBuilder_Build_WithNoEndpoints_ReturnsEmptyContract()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract("EmptyContract").Build();

        // Assert
        contract.Should().NotBeNull();
        contract.Endpoints.Should().BeEmpty();
        contract.Name.Should().Be("EmptyContract");
    }

    #endregion

    #region MockServerBuilder Validation

    [Test]
    public void MockServerBuilder_WithJsonSerializer_Null_ThrowsArgumentNullException()
    {
        // Arrange
        var specPath = CreateTempSpecFile();

        try
        {
            var builder = TreatyLib.MockFromOpenApi(specPath);

            // Act
            var act = () => builder.WithJsonSerializer(null!);

            // Assert
            act.Should().Throw<ArgumentNullException>();
        }
        finally
        {
            File.Delete(specPath);
        }
    }

    [Test]
    public void MockServerBuilder_WithLogging_Null_ThrowsArgumentNullException()
    {
        // Arrange
        var specPath = CreateTempSpecFile();

        try
        {
            var builder = TreatyLib.MockFromOpenApi(specPath);

            // Act
            var act = () => builder.WithLogging(null!);

            // Assert
            act.Should().Throw<ArgumentNullException>();
        }
        finally
        {
            File.Delete(specPath);
        }
    }

    #endregion

    #region Helper Methods

    private static string CreateTempSpecFile()
    {
        var spec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /test:
                get:
                  responses:
                    '200':
                      description: OK
            """;

        var specPath = Path.GetTempFileName() + ".yaml";
        File.WriteAllText(specPath, spec);
        return specPath;
    }

    #endregion

    // Test startup class for provider builder tests
    private class TestStartup
    {
        public void ConfigureServices(Microsoft.Extensions.DependencyInjection.IServiceCollection services) { }
        public void Configure(Microsoft.AspNetCore.Builder.IApplicationBuilder app) { }
    }
}
