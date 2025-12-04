using FluentAssertions;
using TreatyLib = Treaty.Treaty;

namespace Treaty.Tests.Unit.Contracts;

public class EndpointContractTests
{
    [Test]
    public void Matches_WithExactPath_ReturnsTrue()
    {
        // Arrange
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Act & Assert
        var endpoint = contract.FindEndpoint("/users", HttpMethod.Get);
        endpoint.Should().NotBeNull();
        endpoint!.Matches("/users", HttpMethod.Get).Should().BeTrue();
    }

    [Test]
    public void Matches_WithWrongMethod_ReturnsFalse()
    {
        // Arrange
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Act & Assert
        var endpoint = contract.FindEndpoint("/users", HttpMethod.Get);
        endpoint.Should().NotBeNull();
        endpoint!.Matches("/users", HttpMethod.Post).Should().BeFalse();
    }

    [Test]
    public void Matches_WithWrongPath_ReturnsFalse()
    {
        // Arrange
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Act & Assert
        var endpoint = contract.FindEndpoint("/users", HttpMethod.Get);
        endpoint.Should().NotBeNull();
        endpoint!.Matches("/other", HttpMethod.Get).Should().BeFalse();
    }

    [Test]
    public void Matches_WithPathParameter_ReturnsTrue()
    {
        // Arrange
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Act & Assert
        var endpoint = contract.FindEndpoint("/users/123", HttpMethod.Get);
        endpoint.Should().NotBeNull();
        endpoint!.Matches("/users/123", HttpMethod.Get).Should().BeTrue();
        endpoint.Matches("/users/abc", HttpMethod.Get).Should().BeTrue();
    }

    [Test]
    public void Matches_WithEmptyPathParameter_ReturnsFalse()
    {
        // Arrange
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Act & Assert
        var endpoint = contract.FindEndpoint("/users/123", HttpMethod.Get);
        endpoint.Should().NotBeNull();
        endpoint!.Matches("/users/", HttpMethod.Get).Should().BeFalse();
    }

    [Test]
    public void Matches_WithMultiplePathParameters_ReturnsTrue()
    {
        // Arrange
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users/{userId}/posts/{postId}")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Act & Assert
        var endpoint = contract.FindEndpoint("/users/123/posts/456", HttpMethod.Get);
        endpoint.Should().NotBeNull();
        endpoint!.Matches("/users/123/posts/456", HttpMethod.Get).Should().BeTrue();
    }

    [Test]
    public void Matches_WithQueryString_IgnoresQueryString()
    {
        // Arrange
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Act & Assert
        var endpoint = contract.FindEndpoint("/users", HttpMethod.Get);
        endpoint.Should().NotBeNull();
        endpoint!.Matches("/users?page=1&limit=10", HttpMethod.Get).Should().BeTrue();
    }

    [Test]
    public void ExtractPathParameters_SingleParam_ReturnsValue()
    {
        // Arrange
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        var endpoint = contract.FindEndpoint("/users/123", HttpMethod.Get);

        // Act
        var pathParams = endpoint!.ExtractPathParameters("/users/123");

        // Assert
        pathParams.Should().ContainKey("id").WhoseValue.Should().Be("123");
    }

    [Test]
    public void ExtractPathParameters_MultipleParams_ReturnsAllValues()
    {
        // Arrange
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users/{userId}/posts/{postId}")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        var endpoint = contract.FindEndpoint("/users/123/posts/456", HttpMethod.Get);

        // Act
        var pathParams = endpoint!.ExtractPathParameters("/users/123/posts/456");

        // Assert
        pathParams.Should().ContainKey("userId").WhoseValue.Should().Be("123");
        pathParams.Should().ContainKey("postId").WhoseValue.Should().Be("456");
    }

    [Test]
    public void ExtractPathParameters_NoParams_ReturnsEmpty()
    {
        // Arrange
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        var endpoint = contract.FindEndpoint("/users", HttpMethod.Get);

        // Act
        var pathParams = endpoint!.ExtractPathParameters("/users");

        // Assert
        pathParams.Should().BeEmpty();
    }

    [Test]
    public void ExtractPathParameters_WithQueryString_IgnoresQueryString()
    {
        // Arrange
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        var endpoint = contract.FindEndpoint("/users/123", HttpMethod.Get);

        // Act
        var pathParams = endpoint!.ExtractPathParameters("/users/123?expand=true");

        // Assert
        pathParams.Should().ContainKey("id").WhoseValue.Should().Be("123");
    }

    [Test]
    public void ToString_ReturnsMethodAndPath()
    {
        // Arrange
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        var endpoint = contract.Endpoints[0];

        // Act
        var result = endpoint.ToString();

        // Assert
        result.Should().Be("GET /users/{id}");
    }

    [Test]
    public void FindEndpoint_NonExistent_ReturnsNull()
    {
        // Arrange
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Act
        var endpoint = contract.FindEndpoint("/nonexistent", HttpMethod.Get);

        // Assert
        endpoint.Should().BeNull();
    }
}
