using FluentAssertions;
using Treaty.Contracts;
using Treaty.Provider;

namespace Treaty.Tests.Unit.Provider;

public class StateHandlerTests
{
    [Test]
    public async Task DelegateStateHandler_Setup_CallsDelegate()
    {
        // Arrange
        var called = false;
        var handler = new DelegateStateHandler()
            .OnState("test state", (ProviderState _) =>
            {
                called = true;
                return Task.CompletedTask;
            });

        var state = ProviderState.Create("test state");

        // Act
        await handler.SetupAsync(state);

        // Assert
        called.Should().BeTrue();
    }

    [Test]
    public async Task DelegateStateHandler_Teardown_CallsDelegate()
    {
        // Arrange
        var tornDown = false;
        var handler = new DelegateStateHandler()
            .OnState("test state", () => { })
            .WithTeardown("test state", _ =>
            {
                tornDown = true;
                return Task.CompletedTask;
            });

        var state = ProviderState.Create("test state");

        // Act
        await handler.TeardownAsync(state);

        // Assert
        tornDown.Should().BeTrue();
    }

    [Test]
    public void DelegateStateHandler_CanHandle_ReturnsTrueForMatchingState()
    {
        // Arrange
        var handler = new DelegateStateHandler()
            .OnState("test state", () => { });

        // Act & Assert
        handler.CanHandle("test state").Should().BeTrue();
    }

    [Test]
    public void DelegateStateHandler_CanHandle_ReturnsFalseForDifferentState()
    {
        // Arrange
        var handler = new DelegateStateHandler()
            .OnState("test state", () => { });

        // Act & Assert
        handler.CanHandle("other state").Should().BeFalse();
    }

    [Test]
    public async Task StateHandlerBuilder_ForState_CreatesHandler()
    {
        // Arrange
        var setupCalled = false;
        var builder = new StateHandlerBuilder();
        builder.ForState("test state", (ProviderState _) =>
        {
            setupCalled = true;
            return Task.CompletedTask;
        });

        var compositeHandler = builder.Build();
        var state = ProviderState.Create("test state");

        // Act
        await compositeHandler.SetupAsync(state);

        // Assert
        setupCalled.Should().BeTrue();
    }

    [Test]
    public async Task StateHandlerBuilder_MultipleStates_HandlesAll()
    {
        // Arrange
        var state1Called = false;
        var state2Called = false;

        var builder = new StateHandlerBuilder();
        builder.ForState("state 1", () => state1Called = true);
        builder.ForState("state 2", () => state2Called = true);

        var compositeHandler = builder.Build();

        // Act
        await compositeHandler.SetupAsync(ProviderState.Create("state 1"));
        await compositeHandler.SetupAsync(ProviderState.Create("state 2"));

        // Assert
        state1Called.Should().BeTrue();
        state2Called.Should().BeTrue();
    }

    [Test]
    public async Task StateHandlerBuilder_WithTeardown_RegistersTeardown()
    {
        // Arrange
        var tornDown = false;

        var builder = new StateHandlerBuilder();
        builder.ForState("test state", () => { });
        builder.WithTeardown("test state", _ =>
        {
            tornDown = true;
            return Task.CompletedTask;
        });

        var compositeHandler = builder.Build();

        // Act
        await compositeHandler.TeardownAsync(ProviderState.Create("test state"));

        // Assert
        tornDown.Should().BeTrue();
    }

    [Test]
    public void StateHandlerBuilder_Build_EmptyBuilder_ReturnsHandler()
    {
        // Arrange
        var builder = new StateHandlerBuilder();

        // Act
        var handler = builder.Build();

        // Assert
        handler.Should().NotBeNull();
    }

    [Test]
    public void CompositeStateHandler_CanHandle_ReturnsTrueIfAnyChildCanHandle()
    {
        // Arrange
        var builder = new StateHandlerBuilder();
        builder.ForState("state 1", () => { });
        builder.ForState("state 2", () => { });

        var compositeHandler = builder.Build();

        // Act & Assert
        compositeHandler.CanHandle("state 1").Should().BeTrue();
        compositeHandler.CanHandle("state 2").Should().BeTrue();
        compositeHandler.CanHandle("state 3").Should().BeFalse();
    }

    [Test]
    public void DelegateStateHandler_RegisteredStates_ReturnsAllRegisteredNames()
    {
        // Arrange
        var handler = new DelegateStateHandler()
            .OnState("state 1", () => { })
            .OnState("state 2", () => { });

        // Act & Assert
        handler.RegisteredStates.Should().Contain("state 1");
        handler.RegisteredStates.Should().Contain("state 2");
        handler.RegisteredStates.Should().HaveCount(2);
    }
}
