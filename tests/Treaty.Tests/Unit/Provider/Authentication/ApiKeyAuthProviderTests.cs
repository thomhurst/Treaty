using Treaty.Provider.Authentication;

namespace Treaty.Tests.Unit.Provider.Authentication;

public class ApiKeyAuthProviderTests
{
    [Test]
    public async Task ApplyAuthentication_WithHeaderLocation_SetsHeader()
    {
        // Arrange
        var provider = new ApiKeyAuthProvider("my-api-key");
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/test");

        // Act
        await provider.ApplyAuthenticationAsync(request);

        // Assert
        await Assert.That(request.Headers.GetValues("X-API-Key").First()).IsEqualTo("my-api-key");
    }

    [Test]
    public async Task ApplyAuthentication_WithCustomHeaderName_SetsCustomHeader()
    {
        // Arrange
        var provider = new ApiKeyAuthProvider("my-api-key", "Authorization-Key");
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/test");

        // Act
        await provider.ApplyAuthenticationAsync(request);

        // Assert
        await Assert.That(request.Headers.GetValues("Authorization-Key").First()).IsEqualTo("my-api-key");
    }

    [Test]
    public async Task ApplyAuthentication_WithQueryStringLocation_AddsToQueryString()
    {
        // Arrange
        var provider = new ApiKeyAuthProvider("my-api-key", "apiKey", ApiKeyLocation.QueryString);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/test");

        // Act
        await provider.ApplyAuthenticationAsync(request);

        // Assert
        await Assert.That(request.RequestUri!.ToString()).Contains("apiKey=my-api-key");
    }

    [Test]
    public async Task ApplyAuthentication_WithExistingQueryString_AppendsApiKey()
    {
        // Arrange
        var provider = new ApiKeyAuthProvider("my-api-key", "apiKey", ApiKeyLocation.QueryString);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/test?existing=value");

        // Act
        await provider.ApplyAuthenticationAsync(request);

        // Assert
        var uri = request.RequestUri!.ToString();
        await Assert.That(uri).Contains("existing=value");
        await Assert.That(uri).Contains("&apiKey=my-api-key");
    }

    [Test]
    public void Constructor_WithNullApiKey_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new ApiKeyAuthProvider(null!));
    }

    [Test]
    public void Constructor_WithEmptyApiKey_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new ApiKeyAuthProvider(""));
    }

    [Test]
    public void Constructor_WithNullParameterName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new ApiKeyAuthProvider("key", null!));
    }
}
