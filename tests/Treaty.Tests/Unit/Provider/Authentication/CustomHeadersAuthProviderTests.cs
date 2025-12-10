using Treaty.Provider.Authentication;

namespace Treaty.Tests.Unit.Provider.Authentication;

public class CustomHeadersAuthProviderTests
{
    [Test]
    public async Task ApplyAuthentication_AddsSingleHeader()
    {
        // Arrange
        var headers = new Dictionary<string, string>
        {
            { "X-Custom-Auth", "secret-value" }
        };
        var provider = new CustomHeadersAuthProvider(headers);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/test");

        // Act
        await provider.ApplyAuthenticationAsync(request);

        // Assert
        await Assert.That(request.Headers.GetValues("X-Custom-Auth").First()).IsEqualTo("secret-value");
    }

    [Test]
    public async Task ApplyAuthentication_AddsMultipleHeaders()
    {
        // Arrange
        var headers = new Dictionary<string, string>
        {
            { "X-API-Key", "key123" },
            { "X-Client-ID", "client456" },
            { "X-Tenant", "tenant789" }
        };
        var provider = new CustomHeadersAuthProvider(headers);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/test");

        // Act
        await provider.ApplyAuthenticationAsync(request);

        // Assert
        await Assert.That(request.Headers.GetValues("X-API-Key").First()).IsEqualTo("key123");
        await Assert.That(request.Headers.GetValues("X-Client-ID").First()).IsEqualTo("client456");
        await Assert.That(request.Headers.GetValues("X-Tenant").First()).IsEqualTo("tenant789");
    }

    [Test]
    public async Task ApplyAuthentication_WithEmptyHeaders_DoesNotThrow()
    {
        // Arrange
        var headers = new Dictionary<string, string>();
        var provider = new CustomHeadersAuthProvider(headers);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/test");

        // Act & Assert - should not throw
        await provider.ApplyAuthenticationAsync(request);
    }

    [Test]
    public void Constructor_WithNullHeaders_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CustomHeadersAuthProvider(null!));
    }

    [Test]
    public async Task ApplyAuthentication_DoesNotModifyOriginalDictionary()
    {
        // Arrange
        var headers = new Dictionary<string, string>
        {
            { "X-Custom", "value" }
        };
        var provider = new CustomHeadersAuthProvider(headers);

        // Modify original after creating provider
        headers["X-New"] = "new-value";

        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/test");

        // Act
        await provider.ApplyAuthenticationAsync(request);

        // Assert - should only have the original header
        await Assert.That(request.Headers.Contains("X-Custom")).IsTrue();
        await Assert.That(request.Headers.Contains("X-New")).IsFalse();
    }
}
