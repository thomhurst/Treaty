using FluentAssertions;
using Treaty.Contracts;

namespace Treaty.Tests.Unit.Contracts;

public class ContractComparerTests
{
    [Test]
    public void Compare_IdenticalContracts_ReturnsNoDifferences()
    {
        // Arrange
        var oldContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        var newContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.AllChanges.Should().BeEmpty();
        diff.IsCompatible.Should().BeTrue();
        diff.HasBreakingChanges.Should().BeFalse();
    }

    [Test]
    public void Compare_EndpointRemoved_DetectsBreakingChange()
    {
        // Arrange
        var oldContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .ForEndpoint("/products")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        var newContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.BreakingChanges.Should().HaveCount(1);
        diff.BreakingChanges[0].Type.Should().Be(ContractChangeType.EndpointRemoved);
        diff.BreakingChanges[0].Description.Should().Contain("/products");
        diff.IsCompatible.Should().BeFalse();
    }

    [Test]
    public void Compare_EndpointAdded_DetectsInfoChange()
    {
        // Arrange
        var oldContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        var newContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .ForEndpoint("/products")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.InfoChanges.Should().HaveCount(1);
        diff.InfoChanges[0].Type.Should().Be(ContractChangeType.EndpointAdded);
        diff.InfoChanges[0].Description.Should().Contain("/products");
        diff.IsCompatible.Should().BeTrue();
    }

    [Test]
    public void Compare_SuccessStatusCodeRemoved_DetectsBreakingChange()
    {
        // Arrange
        var oldContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
                .ExpectingResponse(r => r.WithStatus(201))
            .Build();

        var newContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.BreakingChanges.Should().HaveCount(1);
        diff.BreakingChanges[0].Type.Should().Be(ContractChangeType.ResponseStatusCodeRemoved);
        diff.BreakingChanges[0].OldValue.Should().Be("201");
    }

    [Test]
    public void Compare_ErrorStatusCodeRemoved_DetectsWarning()
    {
        // Arrange
        var oldContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
                .ExpectingResponse(r => r.WithStatus(404))
            .Build();

        var newContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.Warnings.Should().HaveCount(1);
        diff.Warnings[0].Type.Should().Be(ContractChangeType.ResponseStatusCodeRemoved);
        diff.Warnings[0].OldValue.Should().Be("404");
        diff.IsCompatible.Should().BeTrue();
    }

    [Test]
    public void Compare_StatusCodeAdded_DetectsInfoChange()
    {
        // Arrange
        var oldContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        var newContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
                .ExpectingResponse(r => r.WithStatus(201))
            .Build();

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.InfoChanges.Should().HaveCount(1);
        diff.InfoChanges[0].Type.Should().Be(ContractChangeType.ResponseStatusCodeAdded);
        diff.InfoChanges[0].NewValue.Should().Be("201");
    }

    [Test]
    public void Compare_RequiredRequestBodyAdded_DetectsBreakingChange()
    {
        // Arrange
        var oldContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingResponse(r => r.WithStatus(201))
            .Build();

        var newContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingRequest(r => r.WithJsonBody<CreateUserRequest>())
                .ExpectingResponse(r => r.WithStatus(201))
            .Build();

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.BreakingChanges.Should().HaveCount(1);
        diff.BreakingChanges[0].Type.Should().Be(ContractChangeType.RequestFieldAdded);
        diff.BreakingChanges[0].Location.Should().Be(ChangeLocation.RequestBody);
    }

    [Test]
    public void Compare_OptionalRequestBodyAdded_DetectsInfoChange()
    {
        // Arrange
        var oldContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingResponse(r => r.WithStatus(201))
            .Build();

        var newContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingRequest(r => r.WithJsonBody<CreateUserRequest>().Optional())
                .ExpectingResponse(r => r.WithStatus(201))
            .Build();

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.InfoChanges.Should().HaveCount(1);
        diff.InfoChanges[0].Type.Should().Be(ContractChangeType.RequestFieldAdded);
    }

    [Test]
    public void Compare_RequestBodyBecameRequired_DetectsBreakingChange()
    {
        // Arrange
        var oldContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingRequest(r => r.WithJsonBody<CreateUserRequest>().Optional())
                .ExpectingResponse(r => r.WithStatus(201))
            .Build();

        var newContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingRequest(r => r.WithJsonBody<CreateUserRequest>())
                .ExpectingResponse(r => r.WithStatus(201))
            .Build();

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.BreakingChanges.Should().HaveCount(1);
        diff.BreakingChanges[0].Type.Should().Be(ContractChangeType.RequestFieldMadeRequired);
    }

    [Test]
    public void Compare_RequestBodyBecameOptional_DetectsInfoChange()
    {
        // Arrange
        var oldContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingRequest(r => r.WithJsonBody<CreateUserRequest>())
                .ExpectingResponse(r => r.WithStatus(201))
            .Build();

        var newContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingRequest(r => r.WithJsonBody<CreateUserRequest>().Optional())
                .ExpectingResponse(r => r.WithStatus(201))
            .Build();

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.InfoChanges.Should().HaveCount(1);
        diff.InfoChanges[0].Type.Should().Be(ContractChangeType.RequestFieldMadeOptional);
    }

    [Test]
    public void Compare_RequiredRequestHeaderAdded_DetectsBreakingChange()
    {
        // Arrange
        var oldContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        var newContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .WithHeader("Authorization")
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.BreakingChanges.Should().HaveCount(1);
        diff.BreakingChanges[0].Type.Should().Be(ContractChangeType.RequestHeaderAdded);
        diff.BreakingChanges[0].FieldName.Should().Be("Authorization");
    }

    [Test]
    public void Compare_RequestHeaderRemoved_DetectsInfoChange()
    {
        // Arrange
        var oldContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .WithHeader("Authorization")
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        var newContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.InfoChanges.Should().HaveCount(1);
        diff.InfoChanges[0].Type.Should().Be(ContractChangeType.RequestHeaderRemoved);
    }

    [Test]
    public void Compare_ResponseBodyTypeChanged_DetectsBreakingChange()
    {
        // Arrange
        var oldContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithJsonBody<User[]>())
            .Build();

        var newContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithJsonBody<UserResponse>())
            .Build();

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.BreakingChanges.Should().HaveCount(1);
        diff.BreakingChanges[0].Type.Should().Be(ContractChangeType.ResponseFieldTypeChanged);
        diff.BreakingChanges[0].OldValue.Should().Be("User[]");
        diff.BreakingChanges[0].NewValue.Should().Be("UserResponse");
    }

    [Test]
    public void Compare_ResponseBodySchemaAdded_DetectsInfoChange()
    {
        // Arrange
        var oldContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        var newContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithJsonBody<User[]>())
            .Build();

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.InfoChanges.Should().HaveCount(1);
        diff.InfoChanges[0].Type.Should().Be(ContractChangeType.ResponseFieldAdded);
    }

    [Test]
    public void Compare_ResponseBodySchemaRemoved_DetectsWarning()
    {
        // Arrange
        var oldContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithJsonBody<User[]>())
            .Build();

        var newContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.Warnings.Should().HaveCount(1);
        diff.Warnings[0].Type.Should().Be(ContractChangeType.ResponseFieldRemoved);
    }

    [Test]
    public void Compare_PathParameterEndpoints_MatchesCorrectly()
    {
        // Arrange - endpoints with different param names but same pattern
        var oldContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users/{userId}")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        var newContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert - should match as same endpoint since path pattern is identical
        diff.AllChanges.Should().BeEmpty();
    }

    [Test]
    public void Compare_NullOldContract_ThrowsArgumentNullException()
    {
        // Arrange
        var newContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Act
        var act = () => ContractComparer.Compare(null!, newContract);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("oldContract");
    }

    [Test]
    public void Compare_NullNewContract_ThrowsArgumentNullException()
    {
        // Arrange
        var oldContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Act
        var act = () => ContractComparer.Compare(oldContract, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("newContract");
    }

    [Test]
    public void GetSummary_WithBreakingChanges_ContainsBreakingSection()
    {
        // Arrange
        var oldContract = Treaty.DefineContract("Old API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        var newContract = Treaty.DefineContract("New API").Build();

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);
        var summary = diff.GetSummary();

        // Assert
        summary.Should().Contain("BREAKING CHANGES:");
        summary.Should().Contain("/users");
    }

    [Test]
    public void ThrowIfBreaking_WithBreakingChanges_ThrowsException()
    {
        // Arrange
        var oldContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        var newContract = Treaty.DefineContract("Test API").Build();

        var diff = ContractComparer.Compare(oldContract, newContract);

        // Act
        var act = () => diff.ThrowIfBreaking();

        // Assert
        act.Should().Throw<ContractBreakingChangeException>()
            .Which.Diff.Should().BeSameAs(diff);
    }

    [Test]
    public void ThrowIfBreaking_WithoutBreakingChanges_DoesNotThrow()
    {
        // Arrange
        var oldContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        var newContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .ForEndpoint("/products")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        var diff = ContractComparer.Compare(oldContract, newContract);

        // Act
        var act = () => diff.ThrowIfBreaking();

        // Assert
        act.Should().NotThrow();
    }

    [Test]
    public void Compare_ResponseHeaderAdded_DetectsInfoChange()
    {
        // Arrange
        var oldContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        var newContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithHeader("X-Request-Id"))
            .Build();

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.InfoChanges.Should().HaveCount(1);
        diff.InfoChanges[0].Type.Should().Be(ContractChangeType.ResponseHeaderAdded);
        diff.InfoChanges[0].FieldName.Should().Be("X-Request-Id");
    }

    [Test]
    public void Compare_ResponseHeaderRemoved_DetectsWarning()
    {
        // Arrange
        var oldContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithHeader("X-Request-Id"))
            .Build();

        var newContract = Treaty.DefineContract("Test API")
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.Warnings.Should().HaveCount(1);
        diff.Warnings[0].Type.Should().Be(ContractChangeType.ResponseHeaderRemoved);
        diff.Warnings[0].FieldName.Should().Be("X-Request-Id");
    }

    private class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class UserResponse
    {
        public User[] Users { get; set; } = [];
        public int Total { get; set; }
    }

    private class CreateUserRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
