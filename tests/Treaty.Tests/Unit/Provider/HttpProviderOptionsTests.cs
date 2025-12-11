using Treaty.Provider;

namespace Treaty.Tests.Unit.Provider;

public class HttpProviderOptionsTests
{
    #region HttpProviderOptions Tests

    [Test]
    public async Task Default_HasExpectedValues()
    {
        // Arrange & Act
        var options = HttpProviderOptions.Default;

        // Assert
        await Assert.That(options.RequestTimeout).IsEqualTo(TimeSpan.FromSeconds(30));
        await Assert.That(options.FollowRedirects).IsTrue();
        await Assert.That(options.MaxRedirects).IsEqualTo(5);
        await Assert.That(options.ValidateCertificates).IsTrue();
    }

    [Test]
    public async Task CustomOptions_CanBeCreated()
    {
        // Arrange & Act
        var options = new HttpProviderOptions
        {
            RequestTimeout = TimeSpan.FromSeconds(60),
            FollowRedirects = false,
            MaxRedirects = 10,
            ValidateCertificates = false
        };

        // Assert
        await Assert.That(options.RequestTimeout).IsEqualTo(TimeSpan.FromSeconds(60));
        await Assert.That(options.FollowRedirects).IsFalse();
        await Assert.That(options.MaxRedirects).IsEqualTo(10);
        await Assert.That(options.ValidateCertificates).IsFalse();
    }

    [Test]
    public async Task DefaultInstance_IsSingleton()
    {
        // Arrange & Act
        var options1 = HttpProviderOptions.Default;
        var options2 = HttpProviderOptions.Default;

        // Assert
        await Assert.That(options1).IsSameReferenceAs(options2);
    }

    #endregion

    #region HttpProviderOptionsBuilder Tests

    [Test]
    public async Task Build_WithDefaults_ReturnsDefaultValues()
    {
        // Arrange
        var builder = new HttpProviderOptionsBuilder();

        // Act
        var options = builder.Build();

        // Assert
        await Assert.That(options.RequestTimeout).IsEqualTo(TimeSpan.FromSeconds(30));
        await Assert.That(options.FollowRedirects).IsTrue();
        await Assert.That(options.MaxRedirects).IsEqualTo(5);
        await Assert.That(options.ValidateCertificates).IsTrue();
    }

    [Test]
    public async Task WithTimeout_TimeSpan_SetsTimeout()
    {
        // Arrange
        var builder = new HttpProviderOptionsBuilder();

        // Act
        var options = builder
            .WithTimeout(TimeSpan.FromMinutes(2))
            .Build();

        // Assert
        await Assert.That(options.RequestTimeout).IsEqualTo(TimeSpan.FromMinutes(2));
    }

    [Test]
    public async Task WithTimeout_Seconds_SetsTimeout()
    {
        // Arrange
        var builder = new HttpProviderOptionsBuilder();

        // Act
        var options = builder
            .WithTimeout(90)
            .Build();

        // Assert
        await Assert.That(options.RequestTimeout).IsEqualTo(TimeSpan.FromSeconds(90));
    }

    [Test]
    public async Task FollowRedirects_True_EnablesRedirects()
    {
        // Arrange
        var builder = new HttpProviderOptionsBuilder();

        // Act
        var options = builder
            .FollowRedirects(true)
            .Build();

        // Assert
        await Assert.That(options.FollowRedirects).IsTrue();
    }

    [Test]
    public async Task FollowRedirects_False_DisablesRedirects()
    {
        // Arrange
        var builder = new HttpProviderOptionsBuilder();

        // Act
        var options = builder
            .FollowRedirects(false)
            .Build();

        // Assert
        await Assert.That(options.FollowRedirects).IsFalse();
    }

    [Test]
    public async Task FollowRedirects_WithMaxRedirects_SetsMaxRedirects()
    {
        // Arrange
        var builder = new HttpProviderOptionsBuilder();

        // Act
        var options = builder
            .FollowRedirects(true, maxRedirects: 10)
            .Build();

        // Assert
        await Assert.That(options.FollowRedirects).IsTrue();
        await Assert.That(options.MaxRedirects).IsEqualTo(10);
    }

    [Test]
    public async Task SkipCertificateValidation_DisablesValidation()
    {
        // Arrange
        var builder = new HttpProviderOptionsBuilder();

        // Act
        var options = builder
            .SkipCertificateValidation()
            .Build();

        // Assert
        await Assert.That(options.ValidateCertificates).IsFalse();
    }

    [Test]
    public async Task Builder_MethodChaining_ReturnsSameInstance()
    {
        // Arrange
        var builder = new HttpProviderOptionsBuilder();

        // Act
        var builder1 = builder.WithTimeout(60);
        var builder2 = builder1.FollowRedirects(false);
        var builder3 = builder2.SkipCertificateValidation();

        // Assert
        await Assert.That(builder1).IsSameReferenceAs(builder);
        await Assert.That(builder2).IsSameReferenceAs(builder);
        await Assert.That(builder3).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task Build_WithAllOptions_CreatesCorrectOptions()
    {
        // Arrange
        var builder = new HttpProviderOptionsBuilder();

        // Act
        var options = builder
            .WithTimeout(TimeSpan.FromMinutes(5))
            .FollowRedirects(true, maxRedirects: 20)
            .SkipCertificateValidation()
            .Build();

        // Assert
        await Assert.That(options.RequestTimeout).IsEqualTo(TimeSpan.FromMinutes(5));
        await Assert.That(options.FollowRedirects).IsTrue();
        await Assert.That(options.MaxRedirects).IsEqualTo(20);
        await Assert.That(options.ValidateCertificates).IsFalse();
    }

    #endregion
}
