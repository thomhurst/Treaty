using System.Text.Json.Nodes;
using FluentAssertions;
using Treaty.Matching;
using Treaty.Validation;

namespace Treaty.Tests.Unit.Matching;

public class MatcherTests
{
    private const string Endpoint = "GET /test";
    private const string Path = "$";

    #region GuidMatcher Tests

    [Test]
    public void GuidMatcher_ValidGuid_ReturnsNoViolations()
    {
        var matcher = Match.Guid();
        var node = JsonNode.Parse("\"550e8400-e29b-41d4-a716-446655440000\"");

        var violations = matcher.Validate(node, Endpoint, Path);

        violations.Should().BeEmpty();
    }

    [Test]
    public void GuidMatcher_InvalidGuid_ReturnsViolation()
    {
        var matcher = Match.Guid();
        var node = JsonNode.Parse("\"not-a-guid\"");

        var violations = matcher.Validate(node, Endpoint, Path);

        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.InvalidFormat);
    }

    [Test]
    public void GuidMatcher_NullValue_ReturnsViolation()
    {
        var matcher = Match.Guid();

        var violations = matcher.Validate(null, Endpoint, Path);

        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.UnexpectedNull);
    }

    [Test]
    public void GuidMatcher_WrongType_ReturnsViolation()
    {
        var matcher = Match.Guid();
        var node = JsonNode.Parse("123");

        var violations = matcher.Validate(node, Endpoint, Path);

        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.InvalidType);
    }

    #endregion

    #region StringMatcher Tests

    [Test]
    public void StringMatcher_AnyString_ReturnsNoViolations()
    {
        var matcher = Match.String();
        var node = JsonNode.Parse("\"hello\"");

        var violations = matcher.Validate(node, Endpoint, Path);

        violations.Should().BeEmpty();
    }

    [Test]
    public void StringMatcher_EmptyString_ReturnsNoViolations()
    {
        var matcher = Match.String();
        var node = JsonNode.Parse("\"\"");

        var violations = matcher.Validate(node, Endpoint, Path);

        violations.Should().BeEmpty();
    }

    [Test]
    public void NonEmptyStringMatcher_EmptyString_ReturnsViolation()
    {
        var matcher = Match.NonEmptyString();
        var node = JsonNode.Parse("\"\"");

        var violations = matcher.Validate(node, Endpoint, Path);

        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.InvalidFormat);
    }

    [Test]
    public void NonEmptyStringMatcher_ValidString_ReturnsNoViolations()
    {
        var matcher = Match.NonEmptyString();
        var node = JsonNode.Parse("\"hello\"");

        var violations = matcher.Validate(node, Endpoint, Path);

        violations.Should().BeEmpty();
    }

    #endregion

    #region EmailMatcher Tests

    [Test]
    public void EmailMatcher_ValidEmail_ReturnsNoViolations()
    {
        var matcher = Match.Email();
        var node = JsonNode.Parse("\"user@example.com\"");

        var violations = matcher.Validate(node, Endpoint, Path);

        violations.Should().BeEmpty();
    }

    [Test]
    public void EmailMatcher_InvalidEmail_ReturnsViolation()
    {
        var matcher = Match.Email();
        var node = JsonNode.Parse("\"not-an-email\"");

        var violations = matcher.Validate(node, Endpoint, Path);

        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.InvalidFormat);
    }

    #endregion

    #region IntegerMatcher Tests

    [Test]
    public void IntegerMatcher_ValidInteger_ReturnsNoViolations()
    {
        var matcher = Match.Integer();
        var node = JsonNode.Parse("42");

        var violations = matcher.Validate(node, Endpoint, Path);

        violations.Should().BeEmpty();
    }

    [Test]
    public void IntegerMatcher_Decimal_ReturnsViolation()
    {
        var matcher = Match.Integer();
        var node = JsonNode.Parse("42.5");

        var violations = matcher.Validate(node, Endpoint, Path);

        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.InvalidType);
    }

    [Test]
    public void IntegerMatcher_WithMinMax_InRange_ReturnsNoViolations()
    {
        var matcher = Match.Integer(min: 0, max: 100);
        var node = JsonNode.Parse("50");

        var violations = matcher.Validate(node, Endpoint, Path);

        violations.Should().BeEmpty();
    }

    [Test]
    public void IntegerMatcher_BelowMin_ReturnsViolation()
    {
        var matcher = Match.Integer(min: 0);
        var node = JsonNode.Parse("-5");

        var violations = matcher.Validate(node, Endpoint, Path);

        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.OutOfRange);
    }

    [Test]
    public void IntegerMatcher_AboveMax_ReturnsViolation()
    {
        var matcher = Match.Integer(max: 100);
        var node = JsonNode.Parse("150");

        var violations = matcher.Validate(node, Endpoint, Path);

        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.OutOfRange);
    }

    #endregion

    #region DecimalMatcher Tests

    [Test]
    public void DecimalMatcher_ValidDecimal_ReturnsNoViolations()
    {
        var matcher = Match.Decimal();
        var node = JsonNode.Parse("42.5");

        var violations = matcher.Validate(node, Endpoint, Path);

        violations.Should().BeEmpty();
    }

    [Test]
    public void DecimalMatcher_Integer_ReturnsNoViolations()
    {
        var matcher = Match.Decimal();
        var node = JsonNode.Parse("42");

        var violations = matcher.Validate(node, Endpoint, Path);

        violations.Should().BeEmpty();
    }

    [Test]
    public void DecimalMatcher_WithRange_InRange_ReturnsNoViolations()
    {
        var matcher = Match.Decimal(min: 0, max: 100);
        var node = JsonNode.Parse("50.5");

        var violations = matcher.Validate(node, Endpoint, Path);

        violations.Should().BeEmpty();
    }

    #endregion

    #region BooleanMatcher Tests

    [Test]
    public void BooleanMatcher_True_ReturnsNoViolations()
    {
        var matcher = Match.Boolean();
        var node = JsonNode.Parse("true");

        var violations = matcher.Validate(node, Endpoint, Path);

        violations.Should().BeEmpty();
    }

    [Test]
    public void BooleanMatcher_False_ReturnsNoViolations()
    {
        var matcher = Match.Boolean();
        var node = JsonNode.Parse("false");

        var violations = matcher.Validate(node, Endpoint, Path);

        violations.Should().BeEmpty();
    }

    [Test]
    public void BooleanMatcher_WrongType_ReturnsViolation()
    {
        var matcher = Match.Boolean();
        var node = JsonNode.Parse("\"true\"");

        var violations = matcher.Validate(node, Endpoint, Path);

        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.InvalidType);
    }

    #endregion

    #region RegexMatcher Tests

    [Test]
    public void RegexMatcher_MatchingPattern_ReturnsNoViolations()
    {
        var matcher = Match.Regex(@"^[A-Z]{3}-\d{5}$");
        var node = JsonNode.Parse("\"ABC-12345\"");

        var violations = matcher.Validate(node, Endpoint, Path);

        violations.Should().BeEmpty();
    }

    [Test]
    public void RegexMatcher_NonMatchingPattern_ReturnsViolation()
    {
        var matcher = Match.Regex(@"^[A-Z]{3}-\d{5}$");
        var node = JsonNode.Parse("\"invalid\"");

        var violations = matcher.Validate(node, Endpoint, Path);

        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.PatternMismatch);
    }

    #endregion

    #region DateTimeMatcher Tests

    [Test]
    public void DateTimeMatcher_ValidIso8601_ReturnsNoViolations()
    {
        var matcher = Match.DateTime();
        var node = JsonNode.Parse("\"2024-01-15T10:30:00Z\"");

        var violations = matcher.Validate(node, Endpoint, Path);

        violations.Should().BeEmpty();
    }

    [Test]
    public void DateTimeMatcher_InvalidDateTime_ReturnsViolation()
    {
        var matcher = Match.DateTime();
        var node = JsonNode.Parse("\"not-a-datetime\"");

        var violations = matcher.Validate(node, Endpoint, Path);

        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.InvalidFormat);
    }

    #endregion

    #region UriMatcher Tests

    [Test]
    public void UriMatcher_ValidUri_ReturnsNoViolations()
    {
        var matcher = Match.Uri();
        var node = JsonNode.Parse("\"https://example.com/path\"");

        var violations = matcher.Validate(node, Endpoint, Path);

        violations.Should().BeEmpty();
    }

    [Test]
    public void UriMatcher_InvalidUri_ReturnsViolation()
    {
        var matcher = Match.Uri();
        var node = JsonNode.Parse("\"not-a-valid-uri\"");

        var violations = matcher.Validate(node, Endpoint, Path);

        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.InvalidFormat);
    }

    #endregion

    #region OneOfMatcher Tests

    [Test]
    public void OneOfMatcher_ValidValue_ReturnsNoViolations()
    {
        var matcher = Match.OneOf("active", "inactive", "pending");
        var node = JsonNode.Parse("\"active\"");

        var violations = matcher.Validate(node, Endpoint, Path);

        violations.Should().BeEmpty();
    }

    [Test]
    public void OneOfMatcher_InvalidValue_ReturnsViolation()
    {
        var matcher = Match.OneOf("active", "inactive", "pending");
        var node = JsonNode.Parse("\"unknown\"");

        var violations = matcher.Validate(node, Endpoint, Path);

        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.InvalidEnumValue);
    }

    [Test]
    public void OneOfMatcher_IntegerValues_ReturnsNoViolations()
    {
        var matcher = Match.OneOf(1, 2, 3);
        var node = JsonNode.Parse("2");

        var violations = matcher.Validate(node, Endpoint, Path);

        violations.Should().BeEmpty();
    }

    #endregion

    #region AnyMatcher Tests

    [Test]
    public void AnyMatcher_AnyValue_ReturnsNoViolations()
    {
        var matcher = Match.Any();

        matcher.Validate(JsonNode.Parse("\"string\""), Endpoint, Path).Should().BeEmpty();
        matcher.Validate(JsonNode.Parse("123"), Endpoint, Path).Should().BeEmpty();
        matcher.Validate(JsonNode.Parse("true"), Endpoint, Path).Should().BeEmpty();
        matcher.Validate(JsonNode.Parse("null"), Endpoint, Path).Should().BeEmpty();
        matcher.Validate(null, Endpoint, Path).Should().BeEmpty();
    }

    #endregion

    #region NullMatcher Tests

    [Test]
    public void NullMatcher_Null_ReturnsNoViolations()
    {
        var matcher = Match.Null();

        var violations = matcher.Validate(null, Endpoint, Path);

        violations.Should().BeEmpty();
    }

    [Test]
    public void NullMatcher_NonNull_ReturnsViolation()
    {
        var matcher = Match.Null();
        var node = JsonNode.Parse("\"value\"");

        var violations = matcher.Validate(node, Endpoint, Path);

        violations.Should().ContainSingle()
            .Which.Type.Should().Be(ViolationType.InvalidType);
    }

    #endregion

    #region Sample Generation Tests

    [Test]
    public void GuidMatcher_GenerateSample_ReturnsValidGuid()
    {
        var matcher = Match.Guid();
        var sample = matcher.GenerateSample();

        sample.Should().BeOfType<string>();
        Guid.TryParse((string)sample!, out _).Should().BeTrue();
    }

    [Test]
    public void EmailMatcher_GenerateSample_ReturnsValidEmail()
    {
        var matcher = Match.Email();
        var sample = matcher.GenerateSample();

        sample.Should().Be("user@example.com");
    }

    [Test]
    public void IntegerMatcher_GenerateSample_ReturnsInteger()
    {
        var matcher = Match.Integer(min: 10, max: 20);
        var sample = matcher.GenerateSample();

        sample.Should().Be(15L);
    }

    #endregion
}
