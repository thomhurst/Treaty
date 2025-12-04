using FluentAssertions;
using Treaty.Diagnostics;

namespace Treaty.Tests.Unit.Diagnostics;

public class JsonDiffTests
{
    [Test]
    public void JsonDiffGenerator_Compare_IdenticalJson_ReturnsEmptyList()
    {
        // Arrange
        var json1 = """{"name": "John", "age": 30}""";
        var json2 = """{"name": "John", "age": 30}""";

        // Act
        var diffs = JsonDiffGenerator.Compare(json1, json2);

        // Assert
        diffs.Should().BeEmpty();
    }

    [Test]
    public void JsonDiffGenerator_Compare_DifferentValues_ReturnsChangedDiff()
    {
        // Arrange
        var json1 = """{"name": "John"}""";
        var json2 = """{"name": "Jane"}""";

        // Act
        var diffs = JsonDiffGenerator.Compare(json1, json2);

        // Assert
        diffs.Should().HaveCountGreaterOrEqualTo(1);
        diffs.Should().Contain(d => d.Type == DiffType.Changed);
    }

    [Test]
    public void JsonDiffGenerator_Compare_AddedField_ReturnsAddedDiff()
    {
        // Arrange
        var json1 = """{"name": "John"}""";
        var json2 = """{"name": "John", "age": 30}""";

        // Act
        var diffs = JsonDiffGenerator.Compare(json1, json2);

        // Assert
        diffs.Should().HaveCountGreaterOrEqualTo(1);
        diffs.Should().Contain(d => d.Type == DiffType.Added);
    }

    [Test]
    public void JsonDiffGenerator_Compare_RemovedField_ReturnsRemovedDiff()
    {
        // Arrange
        var json1 = """{"name": "John", "age": 30}""";
        var json2 = """{"name": "John"}""";

        // Act
        var diffs = JsonDiffGenerator.Compare(json1, json2);

        // Assert
        diffs.Should().HaveCountGreaterOrEqualTo(1);
        diffs.Should().Contain(d => d.Type == DiffType.Removed);
    }

    [Test]
    public void JsonDiffGenerator_Compare_TypeMismatch_ReturnsTypeMismatchDiff()
    {
        // Arrange
        var json1 = """{"value": 123}""";
        var json2 = """{"value": "123"}""";

        // Act
        var diffs = JsonDiffGenerator.Compare(json1, json2);

        // Assert
        diffs.Should().HaveCountGreaterOrEqualTo(1);
        diffs.Should().Contain(d => d.Type == DiffType.TypeMismatch);
    }

    [Test]
    public void JsonDiffGenerator_Compare_NestedObjects_DetectsDifferences()
    {
        // Arrange
        var json1 = """{"user": {"name": "John"}}""";
        var json2 = """{"user": {"name": "Jane"}}""";

        // Act
        var diffs = JsonDiffGenerator.Compare(json1, json2);

        // Assert
        diffs.Should().HaveCountGreaterOrEqualTo(1);
        diffs.Should().Contain(d => d.Path.Contains("user") && d.Path.Contains("name"));
    }

    [Test]
    public void JsonDiffGenerator_Compare_NullExpected_ReturnsEmptyForNull()
    {
        // Arrange
        var json = """{"name": "John"}""";

        // Act
        var diffs = JsonDiffGenerator.Compare(null, json);

        // Assert
        diffs.Should().NotBeNull();
    }

    [Test]
    public void JsonDiffGenerator_FormatDiffs_ReturnsFormattedString()
    {
        // Arrange
        var diffs = new List<JsonDiff>
        {
            JsonDiff.Changed("$.name", "John", "Jane")
        };

        // Act
        var result = JsonDiffGenerator.FormatDiffs(diffs);

        // Assert
        result.Should().Contain("name");
    }

    [Test]
    public void JsonDiff_Added_CreatesDiffWithAddedType()
    {
        // Act
        var diff = JsonDiff.Added("$.newField", "value");

        // Assert
        diff.Type.Should().Be(DiffType.Added);
        diff.Path.Should().Be("$.newField");
        diff.Actual.Should().Be("value");
    }

    [Test]
    public void JsonDiff_Removed_CreatesDiffWithRemovedType()
    {
        // Act
        var diff = JsonDiff.Removed("$.oldField", "value");

        // Assert
        diff.Type.Should().Be(DiffType.Removed);
        diff.Path.Should().Be("$.oldField");
        diff.Expected.Should().Be("value");
    }

    [Test]
    public void JsonDiff_Changed_CreatesDiffWithChangedType()
    {
        // Act
        var diff = JsonDiff.Changed("$.field", "old", "new");

        // Assert
        diff.Type.Should().Be(DiffType.Changed);
        diff.Expected.Should().Be("old");
        diff.Actual.Should().Be("new");
    }
}
