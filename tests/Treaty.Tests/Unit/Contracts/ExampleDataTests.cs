using FluentAssertions;
using Treaty.Contracts;

namespace Treaty.Tests.Unit.Contracts;

public class ExampleDataTests
{
    [Test]
    public void ExampleData_WithPathParam_StoresValue()
    {
        // Arrange
        var builder = new ExampleDataBuilder();

        // Act
        var result = builder.WithPathParam("id", 123).Build();

        // Assert
        result.PathParameters["id"].Should().Be(123);
    }

    [Test]
    public void ExampleData_WithQueryParam_StoresValue()
    {
        // Arrange
        var builder = new ExampleDataBuilder();

        // Act
        var result = builder.WithQueryParam("page", 1).Build();

        // Assert
        result.QueryParameters["page"].Should().Be(1);
    }

    [Test]
    public void ExampleData_WithHeader_StoresValue()
    {
        // Arrange
        var builder = new ExampleDataBuilder();

        // Act
        var result = builder.WithHeader("X-Api-Key", "secret").Build();

        // Assert
        result.Headers["X-Api-Key"].Should().Be("secret");
    }

    [Test]
    public void ExampleData_WithBody_StoresValue()
    {
        // Arrange
        var builder = new ExampleDataBuilder();
        var body = new { name = "test" };

        // Act
        var result = builder.WithBody(body).Build();

        // Assert
        result.RequestBody.Should().NotBeNull();
    }

    [Test]
    public void ExampleData_Empty_HasValuesIsFalse()
    {
        // Arrange
        var builder = new ExampleDataBuilder();

        // Act
        var result = builder.Build();

        // Assert
        result.HasValues.Should().BeFalse();
    }

    [Test]
    public void ExampleData_WithAnyValue_HasValuesIsTrue()
    {
        // Arrange
        var builder = new ExampleDataBuilder();

        // Act
        var result = builder.WithPathParam("id", 1).Build();

        // Assert
        result.HasValues.Should().BeTrue();
    }

    [Test]
    public void ExampleData_WithPathParams_FromAnonymousObject()
    {
        // Arrange
        var builder = new ExampleDataBuilder();

        // Act
        var result = builder.WithPathParams(new { id = 123, name = "test" }).Build();

        // Assert
        result.PathParameters["id"].Should().Be(123);
        result.PathParameters["name"].Should().Be("test");
    }

    [Test]
    public void ExampleData_WithQueryParams_FromAnonymousObject()
    {
        // Arrange
        var builder = new ExampleDataBuilder();

        // Act
        var result = builder.WithQueryParams(new { page = 1, limit = 10 }).Build();

        // Assert
        result.QueryParameters["page"].Should().Be(1);
        result.QueryParameters["limit"].Should().Be(10);
    }
}
