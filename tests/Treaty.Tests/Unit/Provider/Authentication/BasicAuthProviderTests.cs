using System.Text;
using Treaty.Provider.Authentication;

namespace Treaty.Tests.Unit.Provider.Authentication;

public class BasicAuthProviderTests
{
    [Test]
    public async Task ApplyAuthentication_SetsBasicAuthHeader()
    {
        // Arrange
        var provider = new BasicAuthProvider("user", "pass");
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/test");

        // Act
        await provider.ApplyAuthenticationAsync(request);

        // Assert
        await Assert.That(request.Headers.Authorization).IsNotNull();
        await Assert.That(request.Headers.Authorization!.Scheme).IsEqualTo("Basic");

        var expectedCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("user:pass"));
        await Assert.That(request.Headers.Authorization!.Parameter).IsEqualTo(expectedCredentials);
    }

    [Test]
    public async Task ApplyAuthentication_WithSpecialCharacters_EncodesCorrectly()
    {
        // Arrange
        var provider = new BasicAuthProvider("user@domain.com", "p@ss:word!");
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/test");

        // Act
        await provider.ApplyAuthenticationAsync(request);

        // Assert
        var expectedCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("user@domain.com:p@ss:word!"));
        await Assert.That(request.Headers.Authorization!.Parameter).IsEqualTo(expectedCredentials);
    }

    [Test]
    public async Task ApplyAuthentication_WithEmptyPassword_EncodesCorrectly()
    {
        // Arrange
        var provider = new BasicAuthProvider("user", "");
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/test");

        // Act
        await provider.ApplyAuthenticationAsync(request);

        // Assert
        var expectedCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("user:"));
        await Assert.That(request.Headers.Authorization!.Parameter).IsEqualTo(expectedCredentials);
    }

    [Test]
    public void Constructor_WithNullUsername_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new BasicAuthProvider(null!, "pass"));
    }

    [Test]
    public void Constructor_WithEmptyUsername_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new BasicAuthProvider("", "pass"));
    }

    [Test]
    public void Constructor_WithNullPassword_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new BasicAuthProvider("user", null!));
    }
}
