using FluentAssertions;
using Treaty.Contracts;

namespace Treaty.Tests.Unit.Contracts;

public class ProviderStateTests
{
    [Test]
    public void ProviderState_Create_WithName_SetsName()
    {
        // Act
        var state = ProviderState.Create("a user exists");

        // Assert
        state.Name.Should().Be("a user exists");
    }

    [Test]
    public void ProviderState_Create_WithoutParams_HasEmptyParameters()
    {
        // Act
        var state = ProviderState.Create("a user exists");

        // Assert
        state.Parameters.Should().BeEmpty();
    }

    [Test]
    public void ProviderState_Create_WithAnonymousObject_StoresParameters()
    {
        // Act
        var state = ProviderState.Create("a user exists", new { id = 123, name = "John" });

        // Assert
        state.Parameters["id"].Should().Be(123);
        state.Parameters["name"].Should().Be("John");
    }

    [Test]
    public void ProviderState_GetParameter_ReturnsTypedValue()
    {
        // Arrange
        var state = ProviderState.Create("a user exists", new { id = 123 });

        // Act
        var id = state.GetParameter<int>("id");

        // Assert
        id.Should().Be(123);
    }

    [Test]
    public void ProviderState_GetParameter_MissingKey_ThrowsKeyNotFound()
    {
        // Arrange
        var state = ProviderState.Create("a user exists");

        // Act & Assert
        var act = () => state.GetParameter<int>("missing");
        act.Should().Throw<KeyNotFoundException>();
    }

    [Test]
    public void ProviderState_TryGetParameter_ReturnsTrueForExisting()
    {
        // Arrange
        var state = ProviderState.Create("a user exists", new { id = 123 });

        // Act
        var found = state.TryGetParameter<int>("id", out var value);

        // Assert
        found.Should().BeTrue();
        value.Should().Be(123);
    }

    [Test]
    public void ProviderState_TryGetParameter_ReturnsFalseForMissing()
    {
        // Arrange
        var state = ProviderState.Create("a user exists");

        // Act
        var found = state.TryGetParameter<int>("missing", out var value);

        // Assert
        found.Should().BeFalse();
        value.Should().Be(default);
    }

    [Test]
    public void ProviderState_ToString_ReturnsNameOnly_WhenNoParams()
    {
        // Arrange
        var state = ProviderState.Create("a user exists");

        // Act
        var result = state.ToString();

        // Assert
        result.Should().Be("a user exists");
    }

    [Test]
    public void ProviderState_ToString_IncludesParams_WhenPresent()
    {
        // Arrange
        var state = ProviderState.Create("a user exists", new { id = 123 });

        // Act
        var result = state.ToString();

        // Assert
        result.Should().Contain("a user exists");
        result.Should().Contain("id");
        result.Should().Contain("123");
    }
}
