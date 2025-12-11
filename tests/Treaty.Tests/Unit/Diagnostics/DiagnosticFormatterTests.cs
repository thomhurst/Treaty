using FluentAssertions;
using Treaty.Diagnostics;
using Treaty.Validation;

namespace Treaty.Tests.Unit.Diagnostics;

/// <summary>
/// Tests for DiagnosticFormatter static class.
/// </summary>
public class DiagnosticFormatterTests
{
    #region FormatViolation Tests

    [Test]
    public void FormatViolation_WithMissingRequired_IncludesIcon()
    {
        // Arrange
        var violation = new ContractViolation(
            "GET /users",
            "$.name",
            "Required field 'name' is missing",
            ViolationType.MissingRequired);

        // Act
        var result = DiagnosticFormatter.FormatViolation(violation);

        // Assert
        result.Should().Contain("MissingRequired");
    }

    [Test]
    public void FormatViolation_WithInvalidType_ReportsProperly()
    {
        // Arrange
        var violation = new ContractViolation(
            "GET /users",
            "$.age",
            "Type mismatch",
            ViolationType.InvalidType,
            Expected: "number",
            Actual: "string");

        // Act
        var result = DiagnosticFormatter.FormatViolation(violation);

        // Assert
        result.Should().Contain("InvalidType");
    }

    [Test]
    public void FormatViolation_IncludesPath()
    {
        // Arrange
        var violation = new ContractViolation(
            "GET /users",
            "$.user.address.street",
            "Required field missing",
            ViolationType.MissingRequired);

        // Act
        var result = DiagnosticFormatter.FormatViolation(violation);

        // Assert
        result.Should().Contain("$.user.address.street");
    }

    [Test]
    public void FormatViolation_WithExpectedAndActual_IncludesBoth()
    {
        // Arrange
        var violation = new ContractViolation(
            "GET /users",
            "$.count",
            "Type mismatch",
            ViolationType.InvalidType,
            Expected: "integer",
            Actual: "string");

        // Act
        var result = DiagnosticFormatter.FormatViolation(violation);

        // Assert
        result.Should().Contain("Expected: integer");
        result.Should().Contain("Actual:   string");
    }

    [Test]
    public void FormatViolation_WithContextDisabled_OmitsSuggestion()
    {
        // Arrange
        var violation = new ContractViolation(
            "GET /users",
            "$.name",
            "Required field missing",
            ViolationType.MissingRequired);

        // Act
        var result = DiagnosticFormatter.FormatViolation(violation, includeContext: false);

        // Assert
        result.Should().NotContain("Fix:");
    }

    [Test]
    public void FormatViolation_WithContextEnabled_IncludesSuggestion()
    {
        // Arrange
        var violation = new ContractViolation(
            "GET /users",
            "$.name",
            "Required field missing",
            ViolationType.MissingRequired);

        // Act
        var result = DiagnosticFormatter.FormatViolation(violation, includeContext: true);

        // Assert
        result.Should().Contain("Fix:");
    }

    #endregion

    #region FormatPath Tests

    [Test]
    public void FormatPath_WithNullOrEmpty_ReturnsRoot()
    {
        // Act & Assert
        DiagnosticFormatter.FormatPath(null!).Should().Be("(root)");
        DiagnosticFormatter.FormatPath("").Should().Be("(root)");
    }

    [Test]
    public void FormatPath_WithValidPath_AddsBackticks()
    {
        // Act
        var result = DiagnosticFormatter.FormatPath("$.user.name");

        // Assert
        result.Should().Be("`$.user.name`");
    }

    #endregion

    #region GenerateSuggestion Tests

    [Test]
    [Arguments(ViolationType.MissingRequired, "optional")]
    [Arguments(ViolationType.InvalidType, "serialized")]
    [Arguments(ViolationType.InvalidFormat, "format")]
    [Arguments(ViolationType.OutOfRange, "range")]
    [Arguments(ViolationType.InvalidEnumValue, "enum")]
    [Arguments(ViolationType.PatternMismatch, "pattern")]
    [Arguments(ViolationType.UnexpectedStatusCode, "API returned")]
    [Arguments(ViolationType.MissingHeader, "header")]
    [Arguments(ViolationType.InvalidHeaderValue, "header value")]
    [Arguments(ViolationType.UnexpectedNull, "null")]
    [Arguments(ViolationType.UnexpectedField, "strict mode")]
    [Arguments(ViolationType.InvalidContentType, "Content-Type")]
    [Arguments(ViolationType.MissingQueryParameter, "query parameter")]
    [Arguments(ViolationType.InvalidQueryParameterValue, "query parameter")]
    public void GenerateSuggestion_ForViolationType_ReturnsRelevantSuggestion(
        ViolationType type, string expectedFragment)
    {
        // Arrange
        var violation = new ContractViolation(
            "GET /test",
            "$.field",
            "Test message",
            type,
            Expected: "expected",
            Actual: "actual");

        // Act
        var suggestion = DiagnosticFormatter.GenerateSuggestion(violation);

        // Assert
        suggestion.Should().NotBeNull();
        suggestion.Should().Contain(expectedFragment);
    }

    [Test]
    public void GenerateSuggestion_ForTimeout_ReturnsNull()
    {
        // Arrange
        var violation = new ContractViolation(
            "GET /test",
            "$.field",
            "Request timed out",
            ViolationType.Timeout);

        // Act
        var suggestion = DiagnosticFormatter.GenerateSuggestion(violation);

        // Assert
        suggestion.Should().BeNull();
    }

    #endregion

    #region FormatViolations Tests

    [Test]
    public void FormatViolations_WithMultipleViolations_IncludesAll()
    {
        // Arrange
        var violations = new List<ContractViolation>
        {
            new("GET /users", "$.name", "Missing name", ViolationType.MissingRequired),
            new("GET /users", "$.age", "Invalid age type", ViolationType.InvalidType),
            new("GET /users", "$.email", "Unexpected null", ViolationType.UnexpectedNull)
        };

        // Act
        var result = DiagnosticFormatter.FormatViolations("GET /users", violations);

        // Assert
        result.Should().Contain("Found 3 violation(s)");
        result.Should().Contain("$.name");
        result.Should().Contain("$.age");
        result.Should().Contain("$.email");
    }

    [Test]
    public void FormatViolations_IncludesEndpoint()
    {
        // Arrange
        var violations = new List<ContractViolation>
        {
            new("GET /users/123", "$.id", "Missing id", ViolationType.MissingRequired)
        };

        // Act
        var result = DiagnosticFormatter.FormatViolations("GET /users/123", violations);

        // Assert
        result.Should().Contain("GET /users/123");
    }

    #endregion

    #region FormatSummaryLine Tests

    [Test]
    public void FormatSummaryLine_WithNoViolations_ReturnsPassedMessage()
    {
        // Arrange
        var violations = new List<ContractViolation>();

        // Act
        var result = DiagnosticFormatter.FormatSummaryLine("GET /users", violations);

        // Assert
        result.Should().Contain("PASSED");
    }

    [Test]
    public void FormatSummaryLine_WithOneViolation_ReturnsFailedMessage()
    {
        // Arrange
        var violations = new List<ContractViolation>
        {
            new("GET /users", "$.name", "Missing", ViolationType.MissingRequired)
        };

        // Act
        var result = DiagnosticFormatter.FormatSummaryLine("GET /users", violations);

        // Assert
        result.Should().Contain("MissingRequired");
        result.Should().Contain("$.name");
    }

    [Test]
    public void FormatSummaryLine_WithMultipleViolations_ShowsMoreCount()
    {
        // Arrange
        var violations = new List<ContractViolation>
        {
            new("GET /users", "$.name", "Missing", ViolationType.MissingRequired),
            new("GET /users", "$.age", "Invalid", ViolationType.InvalidType),
            new("GET /users", "$.email", "Null", ViolationType.UnexpectedNull)
        };

        // Act
        var result = DiagnosticFormatter.FormatSummaryLine("GET /users", violations);

        // Assert
        result.Should().Contain("+2 more");
    }

    #endregion
}
