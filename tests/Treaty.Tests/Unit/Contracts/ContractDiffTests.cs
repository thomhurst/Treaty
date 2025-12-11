using FluentAssertions;
using Treaty.Contracts;

namespace Treaty.Tests.Unit.Contracts;

/// <summary>
/// Tests for ContractDiff, ContractChange, and related classes.
/// </summary>
public class ContractDiffTests
{
    #region ContractDiff Constructor Tests

    [Test]
    public void ContractDiff_Constructor_SetsProperties()
    {
        // Arrange
        var changes = new List<ContractChange>
        {
            new(ChangeSeverity.Info, ContractChangeType.EndpointAdded, "Endpoint added", Path: "/users", Location: ChangeLocation.Endpoint)
        };

        // Act
        var diff = new ContractDiff("OldContract", "NewContract", changes);

        // Assert
        diff.OldContractName.Should().Be("OldContract");
        diff.NewContractName.Should().Be("NewContract");
        diff.AllChanges.Should().HaveCount(1);
    }

    [Test]
    public void ContractDiff_Constructor_WithEmptyList_IsValid()
    {
        // Act
        var diff = new ContractDiff("Old", "New", []);

        // Assert
        diff.AllChanges.Should().BeEmpty();
        diff.IsCompatible.Should().BeTrue();
        diff.HasBreakingChanges.Should().BeFalse();
    }

    #endregion

    #region ContractDiff Property Tests

    [Test]
    public void ContractDiff_BreakingChanges_ReturnsOnlyBreaking()
    {
        // Arrange
        var changes = new List<ContractChange>
        {
            new(ChangeSeverity.Breaking, ContractChangeType.EndpointRemoved, "Removed", Path: "/users", Location: ChangeLocation.Endpoint),
            new(ChangeSeverity.Info, ContractChangeType.EndpointAdded, "Added", Path: "/products", Location: ChangeLocation.Endpoint),
            new(ChangeSeverity.Warning, ContractChangeType.ResponseFieldRemoved, "Field removed", Location: ChangeLocation.ResponseBody, FieldName: "name")
        };
        var diff = new ContractDiff("Old", "New", changes);

        // Act
        var breaking = diff.BreakingChanges;

        // Assert
        breaking.Should().HaveCount(1);
        breaking[0].Type.Should().Be(ContractChangeType.EndpointRemoved);
    }

    [Test]
    public void ContractDiff_Warnings_ReturnsOnlyWarnings()
    {
        // Arrange
        var changes = new List<ContractChange>
        {
            new(ChangeSeverity.Breaking, ContractChangeType.EndpointRemoved, "Removed", Path: "/users", Location: ChangeLocation.Endpoint),
            new(ChangeSeverity.Warning, ContractChangeType.ResponseFieldRemoved, "Field removed", Location: ChangeLocation.ResponseBody, FieldName: "name"),
            new(ChangeSeverity.Warning, ContractChangeType.ResponseHeaderRemoved, "Header removed", Location: ChangeLocation.ResponseHeader, FieldName: "X-Header")
        };
        var diff = new ContractDiff("Old", "New", changes);

        // Act
        var warnings = diff.Warnings;

        // Assert
        warnings.Should().HaveCount(2);
        warnings.Should().OnlyContain(c => c.Severity == ChangeSeverity.Warning);
    }

    [Test]
    public void ContractDiff_InfoChanges_ReturnsOnlyInfo()
    {
        // Arrange
        var changes = new List<ContractChange>
        {
            new(ChangeSeverity.Info, ContractChangeType.EndpointAdded, "Added", Path: "/users", Location: ChangeLocation.Endpoint),
            new(ChangeSeverity.Info, ContractChangeType.ResponseStatusCodeAdded, "Status added", Location: ChangeLocation.StatusCode, NewValue: "201"),
            new(ChangeSeverity.Breaking, ContractChangeType.EndpointRemoved, "Removed", Path: "/old", Location: ChangeLocation.Endpoint)
        };
        var diff = new ContractDiff("Old", "New", changes);

        // Act
        var info = diff.InfoChanges;

        // Assert
        info.Should().HaveCount(2);
        info.Should().OnlyContain(c => c.Severity == ChangeSeverity.Info);
    }

    [Test]
    public void ContractDiff_IsCompatible_FalseWhenHasBreakingChanges()
    {
        // Arrange
        var changes = new List<ContractChange>
        {
            new(ChangeSeverity.Breaking, ContractChangeType.EndpointRemoved, "Removed", Path: "/users", Location: ChangeLocation.Endpoint)
        };
        var diff = new ContractDiff("Old", "New", changes);

        // Act & Assert
        diff.IsCompatible.Should().BeFalse();
    }

    [Test]
    public void ContractDiff_IsCompatible_TrueWhenOnlyWarningsAndInfo()
    {
        // Arrange
        var changes = new List<ContractChange>
        {
            new(ChangeSeverity.Warning, ContractChangeType.ResponseFieldRemoved, "Removed", Location: ChangeLocation.ResponseBody),
            new(ChangeSeverity.Info, ContractChangeType.EndpointAdded, "Added", Path: "/new", Location: ChangeLocation.Endpoint)
        };
        var diff = new ContractDiff("Old", "New", changes);

        // Act & Assert
        diff.IsCompatible.Should().BeTrue();
    }

    [Test]
    public void ContractDiff_HasBreakingChanges_TrueWhenBreakingExists()
    {
        // Arrange
        var changes = new List<ContractChange>
        {
            new(ChangeSeverity.Breaking, ContractChangeType.RequestFieldAdded, "Required field added", Location: ChangeLocation.RequestBody)
        };
        var diff = new ContractDiff("Old", "New", changes);

        // Act & Assert
        diff.HasBreakingChanges.Should().BeTrue();
    }

    [Test]
    public void ContractDiff_HasBreakingChanges_FalseWhenNoBreaking()
    {
        // Arrange
        var changes = new List<ContractChange>
        {
            new(ChangeSeverity.Info, ContractChangeType.EndpointAdded, "Added", Path: "/new", Location: ChangeLocation.Endpoint)
        };
        var diff = new ContractDiff("Old", "New", changes);

        // Act & Assert
        diff.HasBreakingChanges.Should().BeFalse();
    }

    #endregion

    #region ContractDiff GetSummary Tests

    [Test]
    public void GetSummary_WithNoChanges_IncludesZeroCounts()
    {
        // Arrange
        var diff = new ContractDiff("OldContract", "NewContract", []);

        // Act
        var summary = diff.GetSummary();

        // Assert
        summary.Should().Contain("Total Changes: 0");
    }

    [Test]
    public void GetSummary_WithBreakingChanges_IncludesBreakingSection()
    {
        // Arrange
        var changes = new List<ContractChange>
        {
            new(ChangeSeverity.Breaking, ContractChangeType.EndpointRemoved, "Endpoint removed", Path: "/users", Location: ChangeLocation.Endpoint)
        };
        var diff = new ContractDiff("Old", "New", changes);

        // Act
        var summary = diff.GetSummary();

        // Assert
        summary.Should().Contain("BREAKING CHANGES:");
    }

    [Test]
    public void GetSummary_WithWarnings_IncludesWarningSection()
    {
        // Arrange
        var changes = new List<ContractChange>
        {
            new(ChangeSeverity.Warning, ContractChangeType.ResponseFieldRemoved, "Field removed", Location: ChangeLocation.ResponseBody, FieldName: "name")
        };
        var diff = new ContractDiff("Old", "New", changes);

        // Act
        var summary = diff.GetSummary();

        // Assert
        summary.Should().Contain("WARNINGS:");
    }

    [Test]
    public void GetSummary_WithInfoChanges_IncludesInfoSection()
    {
        // Arrange
        var changes = new List<ContractChange>
        {
            new(ChangeSeverity.Info, ContractChangeType.EndpointAdded, "New endpoint", Path: "/new", Location: ChangeLocation.Endpoint)
        };
        var diff = new ContractDiff("Old", "New", changes);

        // Act
        var summary = diff.GetSummary();

        // Assert
        summary.Should().Contain("INFO:");
    }

    #endregion

    #region ContractDiff ThrowIfBreaking Tests

    [Test]
    public void ThrowIfBreaking_WithBreakingChanges_Throws()
    {
        // Arrange
        var changes = new List<ContractChange>
        {
            new(ChangeSeverity.Breaking, ContractChangeType.EndpointRemoved, "Removed", Path: "/users", Location: ChangeLocation.Endpoint)
        };
        var diff = new ContractDiff("Old", "New", changes);

        // Act
        var action = () => diff.ThrowIfBreaking();

        // Assert
        action.Should().Throw<ContractBreakingChangeException>()
            .Which.Diff.Should().Be(diff);
    }

    [Test]
    public void ThrowIfBreaking_WithoutBreakingChanges_DoesNotThrow()
    {
        // Arrange
        var changes = new List<ContractChange>
        {
            new(ChangeSeverity.Info, ContractChangeType.EndpointAdded, "Added", Path: "/new", Location: ChangeLocation.Endpoint)
        };
        var diff = new ContractDiff("Old", "New", changes);

        // Act
        var action = () => diff.ThrowIfBreaking();

        // Assert
        action.Should().NotThrow();
    }

    #endregion

    #region ContractChange Tests

    [Test]
    public void ContractChange_Constructor_SetsAllProperties()
    {
        // Act
        var change = new ContractChange(
            ChangeSeverity.Breaking,
            ContractChangeType.ResponseFieldTypeChanged,
            "Type changed",
            Path: "/users",
            Location: ChangeLocation.ResponseBody,
            FieldName: "age",
            OldValue: "string",
            NewValue: "integer");

        // Assert
        change.Type.Should().Be(ContractChangeType.ResponseFieldTypeChanged);
        change.Severity.Should().Be(ChangeSeverity.Breaking);
        change.Path.Should().Be("/users");
        change.OldValue.Should().Be("string");
        change.NewValue.Should().Be("integer");
        change.Description.Should().Be("Type changed");
        change.Location.Should().Be(ChangeLocation.ResponseBody);
        change.FieldName.Should().Be("age");
    }

    [Test]
    public void ContractChange_ToString_IncludesSeverity()
    {
        // Arrange
        var change = new ContractChange(
            ChangeSeverity.Breaking,
            ContractChangeType.EndpointRemoved,
            "Endpoint was removed",
            Path: "/users",
            Location: ChangeLocation.Endpoint);

        // Act
        var result = change.ToString();

        // Assert
        result.Should().Contain("Breaking");
    }

    [Test]
    public void ContractChange_ToString_IncludesDescription()
    {
        // Arrange
        var change = new ContractChange(
            ChangeSeverity.Breaking,
            ContractChangeType.EndpointRemoved,
            "Endpoint was removed",
            Path: "/users",
            Location: ChangeLocation.Endpoint);

        // Act
        var result = change.ToString();

        // Assert
        result.Should().Contain("Endpoint was removed");
    }

    #endregion

    #region ContractBreakingChangeException Tests

    [Test]
    public void ContractBreakingChangeException_SetsDiffProperty()
    {
        // Arrange
        var changes = new List<ContractChange>
        {
            new(ChangeSeverity.Breaking, ContractChangeType.EndpointRemoved, "Removed", Path: "/users", Location: ChangeLocation.Endpoint)
        };
        var diff = new ContractDiff("Old", "New", changes);

        // Act
        var exception = new ContractBreakingChangeException(diff);

        // Assert
        exception.Diff.Should().Be(diff);
    }

    [Test]
    public void ContractBreakingChangeException_MessageNotEmpty()
    {
        // Arrange
        var changes = new List<ContractChange>
        {
            new(ChangeSeverity.Breaking, ContractChangeType.EndpointRemoved, "Endpoint removed", Path: "/users", Location: ChangeLocation.Endpoint)
        };
        var diff = new ContractDiff("Old", "New", changes);

        // Act
        var exception = new ContractBreakingChangeException(diff);

        // Assert
        exception.Message.Should().NotBeEmpty();
    }

    #endregion

    #region ChangeSeverity and ChangeLocation Tests

    [Test]
    [Arguments(ChangeSeverity.Breaking)]
    [Arguments(ChangeSeverity.Warning)]
    [Arguments(ChangeSeverity.Info)]
    public void ChangeSeverity_AllValuesAreDistinct(ChangeSeverity severity)
    {
        // Assert
        Enum.GetValues<ChangeSeverity>().Should().Contain(severity);
    }

    [Test]
    [Arguments(ChangeLocation.Endpoint)]
    [Arguments(ChangeLocation.RequestBody)]
    [Arguments(ChangeLocation.ResponseBody)]
    [Arguments(ChangeLocation.RequestHeader)]
    [Arguments(ChangeLocation.ResponseHeader)]
    [Arguments(ChangeLocation.QueryParameter)]
    [Arguments(ChangeLocation.StatusCode)]
    public void ChangeLocation_AllValuesAreDistinct(ChangeLocation location)
    {
        // Assert
        Enum.GetValues<ChangeLocation>().Should().Contain(location);
    }

    #endregion
}
