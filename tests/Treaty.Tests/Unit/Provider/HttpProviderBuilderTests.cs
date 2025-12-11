using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Treaty.Contracts;
using Treaty.OpenApi;
using Treaty.Provider;
using Treaty.Provider.Authentication;
using Treaty.Provider.Resilience;

namespace Treaty.Tests.Unit.Provider;

public class HttpProviderBuilderTests
{
    private static async Task<ContractDefinition> CreateTestContractAsync()
    {
        const string spec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  responses:
                    '200':
                      description: OK
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(spec));
        return await Contract.FromOpenApi(stream, OpenApiFormat.Yaml).BuildAsync();
    }

    #region WithBaseUrl Tests

    [Test]
    public async Task WithBaseUrl_String_SetsBaseUrl()
    {
        // Arrange
        var builder = new HttpProviderBuilder();
        var contract = await CreateTestContractAsync();

        // Act
        var verifier = builder
            .WithBaseUrl("https://api.example.com")
            .WithContract(contract)
            .Build();

        // Assert
        await Assert.That(verifier).IsNotNull();
        verifier.Dispose();
    }

    [Test]
    public async Task WithBaseUrl_String_AddsTrailingSlash()
    {
        // Arrange
        var builder = new HttpProviderBuilder();
        var contract = await CreateTestContractAsync();

        // Act - URL without trailing slash
        var verifier = builder
            .WithBaseUrl("https://api.example.com")
            .WithContract(contract)
            .Build();

        // Assert - verifier should be created successfully
        await Assert.That(verifier).IsNotNull();
        verifier.Dispose();
    }

    [Test]
    public async Task WithBaseUrl_String_PreservesExistingTrailingSlash()
    {
        // Arrange
        var builder = new HttpProviderBuilder();
        var contract = await CreateTestContractAsync();

        // Act - URL with trailing slash
        var verifier = builder
            .WithBaseUrl("https://api.example.com/")
            .WithContract(contract)
            .Build();

        // Assert
        await Assert.That(verifier).IsNotNull();
        verifier.Dispose();
    }

    [Test]
    public void WithBaseUrl_String_NullThrowsArgumentException()
    {
        // Arrange
        var builder = new HttpProviderBuilder();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => builder.WithBaseUrl((string)null!));
    }

    [Test]
    public void WithBaseUrl_String_EmptyThrowsArgumentException()
    {
        // Arrange
        var builder = new HttpProviderBuilder();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => builder.WithBaseUrl(""));
    }

    [Test]
    public void WithBaseUrl_String_WhitespaceThrowsArgumentException()
    {
        // Arrange
        var builder = new HttpProviderBuilder();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => builder.WithBaseUrl("   "));
    }

    [Test]
    public async Task WithBaseUrl_Uri_SetsBaseUrl()
    {
        // Arrange
        var builder = new HttpProviderBuilder();
        var contract = await CreateTestContractAsync();

        // Act
        var verifier = builder
            .WithBaseUrl(new Uri("https://api.example.com/"))
            .WithContract(contract)
            .Build();

        // Assert
        await Assert.That(verifier).IsNotNull();
        verifier.Dispose();
    }

    [Test]
    public void WithBaseUrl_Uri_NullThrowsArgumentNullException()
    {
        // Arrange
        var builder = new HttpProviderBuilder();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.WithBaseUrl((Uri)null!));
    }

    #endregion

    #region WithContract Tests

    [Test]
    public async Task WithContract_SetsContract()
    {
        // Arrange
        var builder = new HttpProviderBuilder();
        var contract = await CreateTestContractAsync();

        // Act
        var verifier = builder
            .WithBaseUrl("https://api.example.com")
            .WithContract(contract)
            .Build();

        // Assert
        await Assert.That(verifier).IsNotNull();
        verifier.Dispose();
    }

    [Test]
    public void WithContract_NullThrowsArgumentNullException()
    {
        // Arrange
        var builder = new HttpProviderBuilder();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.WithContract(null!));
    }

    #endregion

    #region WithLogging Tests

    [Test]
    public async Task WithLogging_SetsLoggerFactory()
    {
        // Arrange
        var builder = new HttpProviderBuilder();
        var contract = await CreateTestContractAsync();
        var loggerFactory = NullLoggerFactory.Instance;

        // Act
        var verifier = builder
            .WithBaseUrl("https://api.example.com")
            .WithContract(contract)
            .WithLogging(loggerFactory)
            .Build();

        // Assert
        await Assert.That(verifier).IsNotNull();
        verifier.Dispose();
    }

    [Test]
    public void WithLogging_NullThrowsArgumentNullException()
    {
        // Arrange
        var builder = new HttpProviderBuilder();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.WithLogging(null!));
    }

    #endregion

    #region WithStateHandler Tests

    [Test]
    public async Task WithStateHandler_IStateHandler_SetsHandler()
    {
        // Arrange
        var builder = new HttpProviderBuilder();
        var contract = await CreateTestContractAsync();
        var stateHandler = new StateHandlerBuilder()
            .ForState("test state", () => { })
            .Build();

        // Act
        var verifier = builder
            .WithBaseUrl("https://api.example.com")
            .WithContract(contract)
            .WithStateHandler(stateHandler)
            .Build();

        // Assert
        await Assert.That(verifier).IsNotNull();
        await Assert.That(verifier.StateHandler).IsNotNull();
        verifier.Dispose();
    }

    [Test]
    public void WithStateHandler_IStateHandler_NullThrowsArgumentNullException()
    {
        // Arrange
        var builder = new HttpProviderBuilder();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.WithStateHandler((IStateHandler)null!));
    }

    [Test]
    public async Task WithStateHandler_Action_ConfiguresBuilder()
    {
        // Arrange
        var builder = new HttpProviderBuilder();
        var contract = await CreateTestContractAsync();

        // Act
        var verifier = builder
            .WithBaseUrl("https://api.example.com")
            .WithContract(contract)
            .WithStateHandler(states => states.ForState("test state", () => { }))
            .Build();

        // Assert
        await Assert.That(verifier).IsNotNull();
        await Assert.That(verifier.StateHandler).IsNotNull();
        verifier.Dispose();
    }

    [Test]
    public void WithStateHandler_Action_NullThrowsArgumentNullException()
    {
        // Arrange
        var builder = new HttpProviderBuilder();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.WithStateHandler((Action<StateHandlerBuilder>)null!));
    }

    #endregion

    #region Authentication Tests

    [Test]
    public async Task WithBearerToken_String_SetsAuthProvider()
    {
        // Arrange
        var builder = new HttpProviderBuilder();
        var contract = await CreateTestContractAsync();

        // Act
        var verifier = builder
            .WithBaseUrl("https://api.example.com")
            .WithContract(contract)
            .WithBearerToken("test-token")
            .Build();

        // Assert
        await Assert.That(verifier).IsNotNull();
        verifier.Dispose();
    }

    [Test]
    public async Task WithBearerToken_AsyncFactory_SetsAuthProvider()
    {
        // Arrange
        var builder = new HttpProviderBuilder();
        var contract = await CreateTestContractAsync();

        // Act
        var verifier = builder
            .WithBaseUrl("https://api.example.com")
            .WithContract(contract)
            .WithBearerToken(async ct =>
            {
                await Task.Delay(1, ct);
                return "async-token";
            })
            .Build();

        // Assert
        await Assert.That(verifier).IsNotNull();
        verifier.Dispose();
    }

    [Test]
    public async Task WithApiKey_DefaultParameters_SetsAuthProvider()
    {
        // Arrange
        var builder = new HttpProviderBuilder();
        var contract = await CreateTestContractAsync();

        // Act
        var verifier = builder
            .WithBaseUrl("https://api.example.com")
            .WithContract(contract)
            .WithApiKey("my-api-key")
            .Build();

        // Assert
        await Assert.That(verifier).IsNotNull();
        verifier.Dispose();
    }

    [Test]
    public async Task WithApiKey_CustomHeader_SetsAuthProvider()
    {
        // Arrange
        var builder = new HttpProviderBuilder();
        var contract = await CreateTestContractAsync();

        // Act
        var verifier = builder
            .WithBaseUrl("https://api.example.com")
            .WithContract(contract)
            .WithApiKey("my-api-key", "Authorization-Key")
            .Build();

        // Assert
        await Assert.That(verifier).IsNotNull();
        verifier.Dispose();
    }

    [Test]
    public async Task WithApiKey_QueryString_SetsAuthProvider()
    {
        // Arrange
        var builder = new HttpProviderBuilder();
        var contract = await CreateTestContractAsync();

        // Act
        var verifier = builder
            .WithBaseUrl("https://api.example.com")
            .WithContract(contract)
            .WithApiKey("my-api-key", "api_key", ApiKeyLocation.QueryString)
            .Build();

        // Assert
        await Assert.That(verifier).IsNotNull();
        verifier.Dispose();
    }

    [Test]
    public async Task WithBasicAuth_SetsAuthProvider()
    {
        // Arrange
        var builder = new HttpProviderBuilder();
        var contract = await CreateTestContractAsync();

        // Act
        var verifier = builder
            .WithBaseUrl("https://api.example.com")
            .WithContract(contract)
            .WithBasicAuth("username", "password")
            .Build();

        // Assert
        await Assert.That(verifier).IsNotNull();
        verifier.Dispose();
    }

    [Test]
    public async Task WithCustomHeaders_SetsAuthProvider()
    {
        // Arrange
        var builder = new HttpProviderBuilder();
        var contract = await CreateTestContractAsync();
        var headers = new Dictionary<string, string>
        {
            { "X-Custom-Header", "value" }
        };

        // Act
        var verifier = builder
            .WithBaseUrl("https://api.example.com")
            .WithContract(contract)
            .WithCustomHeaders(headers)
            .Build();

        // Assert
        await Assert.That(verifier).IsNotNull();
        verifier.Dispose();
    }

    [Test]
    public async Task WithAuthentication_CustomProvider_SetsAuthProvider()
    {
        // Arrange
        var builder = new HttpProviderBuilder();
        var contract = await CreateTestContractAsync();
        var customProvider = new BearerTokenAuthProvider("custom-token");

        // Act
        var verifier = builder
            .WithBaseUrl("https://api.example.com")
            .WithContract(contract)
            .WithAuthentication(customProvider)
            .Build();

        // Assert
        await Assert.That(verifier).IsNotNull();
        verifier.Dispose();
    }

    [Test]
    public void WithAuthentication_NullThrowsArgumentNullException()
    {
        // Arrange
        var builder = new HttpProviderBuilder();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.WithAuthentication(null!));
    }

    #endregion

    #region WithRetryPolicy Tests

    [Test]
    public async Task WithRetryPolicy_Default_SetsRetryPolicy()
    {
        // Arrange
        var builder = new HttpProviderBuilder();
        var contract = await CreateTestContractAsync();

        // Act
        var verifier = builder
            .WithBaseUrl("https://api.example.com")
            .WithContract(contract)
            .WithRetryPolicy()
            .Build();

        // Assert
        await Assert.That(verifier).IsNotNull();
        verifier.Dispose();
    }

    [Test]
    public async Task WithRetryPolicy_Options_SetsRetryPolicy()
    {
        // Arrange
        var builder = new HttpProviderBuilder();
        var contract = await CreateTestContractAsync();
        var options = new RetryPolicyOptions { MaxRetries = 5 };

        // Act
        var verifier = builder
            .WithBaseUrl("https://api.example.com")
            .WithContract(contract)
            .WithRetryPolicy(options)
            .Build();

        // Assert
        await Assert.That(verifier).IsNotNull();
        verifier.Dispose();
    }

    [Test]
    public async Task WithRetryPolicy_Custom_SetsRetryPolicy()
    {
        // Arrange
        var builder = new HttpProviderBuilder();
        var contract = await CreateTestContractAsync();
        var customPolicy = new RetryPolicy();

        // Act
        var verifier = builder
            .WithBaseUrl("https://api.example.com")
            .WithContract(contract)
            .WithRetryPolicy(customPolicy)
            .Build();

        // Assert
        await Assert.That(verifier).IsNotNull();
        verifier.Dispose();
    }

    [Test]
    public void WithRetryPolicy_Custom_NullThrowsArgumentNullException()
    {
        // Arrange
        var builder = new HttpProviderBuilder();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.WithRetryPolicy((IRetryPolicy)null!));
    }

    #endregion

    #region WithHttpOptions Tests

    [Test]
    public async Task WithHttpOptions_Object_SetsOptions()
    {
        // Arrange
        var builder = new HttpProviderBuilder();
        var contract = await CreateTestContractAsync();
        var options = new HttpProviderOptions
        {
            RequestTimeout = TimeSpan.FromSeconds(60),
            FollowRedirects = false
        };

        // Act
        var verifier = builder
            .WithBaseUrl("https://api.example.com")
            .WithContract(contract)
            .WithHttpOptions(options)
            .Build();

        // Assert
        await Assert.That(verifier).IsNotNull();
        verifier.Dispose();
    }

    [Test]
    public void WithHttpOptions_Object_NullThrowsArgumentNullException()
    {
        // Arrange
        var builder = new HttpProviderBuilder();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.WithHttpOptions((HttpProviderOptions)null!));
    }

    [Test]
    public async Task WithHttpOptions_Action_ConfiguresBuilder()
    {
        // Arrange
        var builder = new HttpProviderBuilder();
        var contract = await CreateTestContractAsync();

        // Act
        var verifier = builder
            .WithBaseUrl("https://api.example.com")
            .WithContract(contract)
            .WithHttpOptions(opts => opts
                .WithTimeout(60)
                .FollowRedirects(false)
                .SkipCertificateValidation())
            .Build();

        // Assert
        await Assert.That(verifier).IsNotNull();
        verifier.Dispose();
    }

    [Test]
    public void WithHttpOptions_Action_NullThrowsArgumentNullException()
    {
        // Arrange
        var builder = new HttpProviderBuilder();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.WithHttpOptions((Action<HttpProviderOptionsBuilder>)null!));
    }

    #endregion

    #region WithHttpClient Tests

    [Test]
    public async Task WithHttpClient_SetsClient()
    {
        // Arrange
        var builder = new HttpProviderBuilder();
        var contract = await CreateTestContractAsync();
        var httpClient = new HttpClient { BaseAddress = new Uri("https://api.example.com/") };

        // Act
        var verifier = builder
            .WithBaseUrl("https://api.example.com")
            .WithContract(contract)
            .WithHttpClient(httpClient)
            .Build();

        // Assert
        await Assert.That(verifier).IsNotNull();
        verifier.Dispose();

        // External client should not be disposed
        httpClient.Dispose();
    }

    [Test]
    public void WithHttpClient_NullThrowsArgumentNullException()
    {
        // Arrange
        var builder = new HttpProviderBuilder();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.WithHttpClient(null!));
    }

    #endregion

    #region Build Tests

    [Test]
    public async Task Build_WithoutBaseUrl_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new HttpProviderBuilder();
        var contract = await CreateTestContractAsync();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.WithContract(contract).Build());

        await Assert.That(ex!.Message).Contains("base URL");
    }

    [Test]
    public async Task Build_WithoutContract_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new HttpProviderBuilder();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.WithBaseUrl("https://api.example.com").Build());

        await Assert.That(ex!.Message).Contains("contract");
    }

    [Test]
    public async Task Build_WithMinimalConfiguration_CreatesVerifier()
    {
        // Arrange
        var builder = new HttpProviderBuilder();
        var contract = await CreateTestContractAsync();

        // Act
        var verifier = builder
            .WithBaseUrl("https://api.example.com")
            .WithContract(contract)
            .Build();

        // Assert
        await Assert.That(verifier).IsNotNull();
        verifier.Dispose();
    }

    [Test]
    public async Task Build_WithAllOptions_CreatesVerifier()
    {
        // Arrange
        var builder = new HttpProviderBuilder();
        var contract = await CreateTestContractAsync();

        // Act
        var verifier = builder
            .WithBaseUrl("https://api.example.com")
            .WithContract(contract)
            .WithLogging(NullLoggerFactory.Instance)
            .WithStateHandler(states => states.ForState("test", () => { }))
            .WithBearerToken("token")
            .WithRetryPolicy()
            .WithHttpOptions(opts => opts.WithTimeout(60))
            .Build();

        // Assert
        await Assert.That(verifier).IsNotNull();
        verifier.Dispose();
    }

    #endregion

    #region Method Chaining Tests

    [Test]
    public async Task Builder_MethodsReturnSameInstance()
    {
        // Arrange
        var builder = new HttpProviderBuilder();
        var contract = await CreateTestContractAsync();

        // Act
        var builder1 = builder.WithBaseUrl("https://api.example.com");
        var builder2 = builder1.WithContract(contract);
        var builder3 = builder2.WithLogging(NullLoggerFactory.Instance);

        // Assert
        await Assert.That(builder1).IsSameReferenceAs(builder);
        await Assert.That(builder2).IsSameReferenceAs(builder);
        await Assert.That(builder3).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task Builder_LastAuthProviderWins()
    {
        // Arrange - Setting multiple auth providers, last one should be used
        var builder = new HttpProviderBuilder();
        var contract = await CreateTestContractAsync();

        // Act
        var verifier = builder
            .WithBaseUrl("https://api.example.com")
            .WithContract(contract)
            .WithBearerToken("first-token")
            .WithApiKey("api-key")
            .WithBasicAuth("user", "pass")
            .Build();

        // Assert - Should create successfully with last auth provider
        await Assert.That(verifier).IsNotNull();
        verifier.Dispose();
    }

    #endregion
}
