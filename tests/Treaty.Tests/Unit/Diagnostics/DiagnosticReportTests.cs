using FluentAssertions;
using Treaty.Contracts;
using Treaty.Diagnostics;
using Treaty.Validation;

namespace Treaty.Tests.Unit.Diagnostics;

/// <summary>
/// Tests for DiagnosticReport class.
/// </summary>
public class DiagnosticReportTests
{
    #region Constructor Tests

    [Test]
    public void Constructor_WithRequiredParameters_SetsProperties()
    {
        // Arrange
        var violations = new List<ContractViolation>
        {
            new("GET /users", "$.name", "Missing", ViolationType.MissingRequired)
        };

        // Act
        var report = new DiagnosticReport("GET /users", violations);

        // Assert
        report.Endpoint.Should().Be("GET /users");
        report.Violations.Should().HaveCount(1);
        report.ProviderStates.Should().BeEmpty();
        report.RequestSent.Should().BeNull();
        report.ResponseReceived.Should().BeNull();
        report.BodyDiffs.Should().BeNull();
        report.StatusCode.Should().BeNull();
        report.ExpectedStatusCodes.Should().BeNull();
    }

    [Test]
    public void Constructor_WithAllParameters_SetsAllProperties()
    {
        // Arrange
        var violations = new List<ContractViolation>
        {
            new("GET /users", "$.name", "Missing", ViolationType.MissingRequired)
        };
        var providerStates = new List<ProviderState>
        {
            new("user exists")
        };
        var bodyDiffs = new List<JsonDiff>
        {
            JsonDiff.Changed("$.name", "John", "Jane")
        };

        // Act
        var report = new DiagnosticReport(
            "GET /users/123",
            violations,
            providerStates,
            requestSent: "GET /users/123",
            responseReceived: "{\"id\": 123}",
            bodyDiffs: bodyDiffs,
            statusCode: 200,
            expectedStatusCodes: [200, 201]);

        // Assert
        report.Endpoint.Should().Be("GET /users/123");
        report.Violations.Should().HaveCount(1);
        report.ProviderStates.Should().HaveCount(1);
        report.RequestSent.Should().Be("GET /users/123");
        report.ResponseReceived.Should().Be("{\"id\": 123}");
        report.BodyDiffs.Should().HaveCount(1);
        report.StatusCode.Should().Be(200);
        report.ExpectedStatusCodes.Should().BeEquivalentTo([200, 201]);
    }

    #endregion

    #region FormatDetailed Tests

    [Test]
    public void FormatDetailed_IncludesHeader()
    {
        // Arrange
        var violations = new List<ContractViolation>
        {
            new("GET /users", "$.name", "Missing", ViolationType.MissingRequired)
        };
        var report = new DiagnosticReport("GET /users", violations);

        // Act
        var result = report.FormatDetailed();

        // Assert
        result.Should().Contain("TREATY VERIFICATION FAILED");
    }

    [Test]
    public void FormatDetailed_IncludesEndpoint()
    {
        // Arrange
        var violations = new List<ContractViolation>
        {
            new("GET /users/123", "$.name", "Missing", ViolationType.MissingRequired)
        };
        var report = new DiagnosticReport("GET /users/123", violations);

        // Act
        var result = report.FormatDetailed();

        // Assert
        result.Should().Contain("Endpoint: GET /users/123");
    }

    [Test]
    public void FormatDetailed_WithProviderStates_IncludesStates()
    {
        // Arrange
        var violations = new List<ContractViolation>
        {
            new("GET /users", "$.name", "Missing", ViolationType.MissingRequired)
        };
        var providerStates = new List<ProviderState>
        {
            new("user exists", new Dictionary<string, object> { ["userId"] = 123 }),
            new("database is ready")
        };
        var report = new DiagnosticReport("GET /users", violations, providerStates);

        // Act
        var result = report.FormatDetailed();

        // Assert
        result.Should().Contain("Provider States:");
        result.Should().Contain("user exists");
        result.Should().Contain("database is ready");
    }

    [Test]
    public void FormatDetailed_WithStatusCode_IncludesStatus()
    {
        // Arrange
        var violations = new List<ContractViolation>
        {
            new("GET /users", "$", "Wrong status", ViolationType.UnexpectedStatusCode)
        };
        var report = new DiagnosticReport(
            "GET /users",
            violations,
            statusCode: 404,
            expectedStatusCodes: [200, 201]);

        // Act
        var result = report.FormatDetailed();

        // Assert
        result.Should().Contain("Response Status: 404");
        result.Should().Contain("Expected Status: 200, 201");
    }

    [Test]
    public void FormatDetailed_WithViolations_IncludesAllViolations()
    {
        // Arrange
        var violations = new List<ContractViolation>
        {
            new("GET /users", "$.name", "Missing name field", ViolationType.MissingRequired),
            new("GET /users", "$.age", "Should be number", ViolationType.InvalidType, Expected: "number", Actual: "string"),
            new("GET /users", "$.email", "Cannot be null", ViolationType.UnexpectedNull)
        };
        var report = new DiagnosticReport("GET /users", violations);

        // Act
        var result = report.FormatDetailed();

        // Assert
        result.Should().Contain("Violations (3)");
        result.Should().Contain("$.name");
        result.Should().Contain("$.age");
        result.Should().Contain("$.email");
        result.Should().Contain("MissingRequired");
        result.Should().Contain("InvalidType");
        result.Should().Contain("UnexpectedNull");
    }

    [Test]
    public void FormatDetailed_WithExpectedAndActual_IncludesBoth()
    {
        // Arrange
        var violations = new List<ContractViolation>
        {
            new("GET /users", "$.count", "Type mismatch", ViolationType.InvalidType, Expected: "integer", Actual: "string")
        };
        var report = new DiagnosticReport("GET /users", violations);

        // Act
        var result = report.FormatDetailed();

        // Assert
        result.Should().Contain("Expected: integer");
        result.Should().Contain("Actual:   string");
    }

    [Test]
    public void FormatDetailed_WithBodyDiffs_IncludesDiffSection()
    {
        // Arrange
        var violations = new List<ContractViolation>
        {
            new("GET /users", "$.name", "Type mismatch", ViolationType.InvalidType)
        };
        var bodyDiffs = new List<JsonDiff>
        {
            JsonDiff.Changed("$.name", "John", "Jane"),
            JsonDiff.Added("$.newField", "value")
        };
        var report = new DiagnosticReport("GET /users", violations, bodyDiffs: bodyDiffs);

        // Act
        var result = report.FormatDetailed();

        // Assert
        result.Should().Contain("Body Diff:");
    }

    [Test]
    public void FormatDetailed_GeneratesSuggestions()
    {
        // Arrange
        var violations = new List<ContractViolation>
        {
            new("GET /users", "$.name", "Missing", ViolationType.MissingRequired),
            new("GET /users", "$", "Wrong status", ViolationType.UnexpectedStatusCode, Expected: "200", Actual: "404")
        };
        var report = new DiagnosticReport("GET /users", violations);

        // Act
        var result = report.FormatDetailed();

        // Assert
        result.Should().Contain("Suggestions:");
    }

    #endregion

    #region FormatSummary Tests

    [Test]
    public void FormatSummary_IncludesEndpoint()
    {
        // Arrange
        var violations = new List<ContractViolation>
        {
            new("GET /users/123", "$.name", "Missing", ViolationType.MissingRequired)
        };
        var report = new DiagnosticReport("GET /users/123", violations);

        // Act
        var result = report.FormatSummary();

        // Assert
        result.Should().Contain("GET /users/123");
    }

    [Test]
    public void FormatSummary_IncludesAllViolations()
    {
        // Arrange
        var violations = new List<ContractViolation>
        {
            new("GET /users", "$.name", "Missing name", ViolationType.MissingRequired),
            new("GET /users", "$.age", "Wrong type", ViolationType.InvalidType)
        };
        var report = new DiagnosticReport("GET /users", violations);

        // Act
        var result = report.FormatSummary();

        // Assert
        result.Should().Contain("MissingRequired");
        result.Should().Contain("$.name");
        result.Should().Contain("InvalidType");
        result.Should().Contain("$.age");
    }

    [Test]
    public void FormatSummary_IsConcise()
    {
        // Arrange
        var violations = new List<ContractViolation>
        {
            new("GET /users", "$.name", "Missing", ViolationType.MissingRequired)
        };
        var report = new DiagnosticReport("GET /users", violations);

        // Act
        var result = report.FormatSummary();

        // Assert
        // Summary should be shorter than detailed
        var detailed = report.FormatDetailed();
        result.Length.Should().BeLessThan(detailed.Length);
    }

    #endregion
}
