using Treaty.Provider.Authentication;

namespace Treaty.Tests.Unit.Provider.Authentication;

public class BearerTokenAuthProviderTests
{
    [Test]
    public async Task ApplyAuthentication_WithStaticToken_SetsAuthorizationHeader()
    {
        // Arrange
        var provider = new BearerTokenAuthProvider("my-test-token");
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/test");

        // Act
        await provider.ApplyAuthenticationAsync(request);

        // Assert
        await Assert.That(request.Headers.Authorization).IsNotNull();
        await Assert.That(request.Headers.Authorization!.Scheme).IsEqualTo("Bearer");
        await Assert.That(request.Headers.Authorization!.Parameter).IsEqualTo("my-test-token");
    }

    [Test]
    public async Task ApplyAuthentication_WithSyncTokenFactory_CallsFactoryAndSetsHeader()
    {
        // Arrange
        var callCount = 0;
        var provider = new BearerTokenAuthProvider(() =>
        {
            callCount++;
            return "factory-token";
        });
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/test");

        // Act
        await provider.ApplyAuthenticationAsync(request);

        // Assert
        await Assert.That(callCount).IsEqualTo(1);
        await Assert.That(request.Headers.Authorization!.Parameter).IsEqualTo("factory-token");
    }

    [Test]
    public async Task ApplyAuthentication_WithAsyncTokenFactory_CallsFactoryAndSetsHeader()
    {
        // Arrange
        var provider = new BearerTokenAuthProvider(async ct =>
        {
            await Task.Delay(1, ct);
            return "async-token";
        });
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/test");

        // Act
        await provider.ApplyAuthenticationAsync(request);

        // Assert
        await Assert.That(request.Headers.Authorization!.Parameter).IsEqualTo("async-token");
    }

    [Test]
    public void Constructor_WithNullToken_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new BearerTokenAuthProvider((string)null!));
    }

    [Test]
    public void Constructor_WithEmptyToken_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new BearerTokenAuthProvider(""));
    }

    [Test]
    public void Constructor_WithNullSyncFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new BearerTokenAuthProvider((Func<string>)null!));
    }

    [Test]
    public void Constructor_WithNullAsyncFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new BearerTokenAuthProvider((Func<CancellationToken, Task<string>>)null!));
    }
}
