using FluentAssertions;
using Treaty.Matching;

namespace Treaty.Tests.Unit.Builders;

public class PartialValidationBuilderMatcherTests
{
    [Test]
    public void WithMatcher_SingleProperty_AddsMatcherToConfig()
    {
        // Arrange
        var builder = new PartialValidationBuilder<TestUser>();

        // Act
        builder.WithMatcher(u => u.Id, Match.Guid());
        var config = builder.Build();

        // Assert
        config.MatcherConfig.Should().NotBeNull();
        config.MatcherConfig!.PropertyMatchers.Should().ContainKey("Id");
        config.MatcherConfig.PropertyMatchers["Id"].Type.Should().Be(MatcherType.Guid);
    }

    [Test]
    public void WithMatcher_MultipleProperties_AddsAllMatchersToConfig()
    {
        // Arrange
        var builder = new PartialValidationBuilder<TestUser>();

        // Act
        builder
            .WithMatcher(u => u.Id, Match.Guid())
            .WithMatcher(u => u.Email, Match.Email())
            .WithMatcher(u => u.CreatedAt, Match.DateTime());
        var config = builder.Build();

        // Assert
        config.MatcherConfig.Should().NotBeNull();
        config.MatcherConfig!.PropertyMatchers.Should().HaveCount(3);
        config.MatcherConfig.PropertyMatchers["Id"].Type.Should().Be(MatcherType.Guid);
        config.MatcherConfig.PropertyMatchers["Email"].Type.Should().Be(MatcherType.Email);
        config.MatcherConfig.PropertyMatchers["CreatedAt"].Type.Should().Be(MatcherType.DateTime);
    }

    [Test]
    public void WithMatcher_CombinedWithIgnoreExtraFields_BothApplied()
    {
        // Arrange
        var builder = new PartialValidationBuilder<TestUser>();

        // Act
        builder
            .WithMatcher(u => u.Id, Match.Guid())
            .IgnoreExtraFields();
        var config = builder.Build();

        // Assert
        config.IgnoreExtraFields.Should().BeTrue();
        config.MatcherConfig.Should().NotBeNull();
        config.MatcherConfig!.PropertyMatchers.Should().ContainKey("Id");
    }

    [Test]
    public void WithMatcher_CombinedWithOnlyValidate_BothApplied()
    {
        // Arrange
        var builder = new PartialValidationBuilder<TestUser>();

        // Act
        builder
            .OnlyValidate(u => u.Id, u => u.Email)
            .WithMatcher(u => u.Id, Match.Guid());
        var config = builder.Build();

        // Assert
        config.PropertiesToValidate.Should().Contain("Id");
        config.PropertiesToValidate.Should().Contain("Email");
        config.MatcherConfig.Should().NotBeNull();
        config.MatcherConfig!.PropertyMatchers.Should().ContainKey("Id");
    }

    [Test]
    public void WithMatcher_NoMatchers_MatcherConfigIsNull()
    {
        // Arrange
        var builder = new PartialValidationBuilder<TestUser>();

        // Act
        builder.IgnoreExtraFields();
        var config = builder.Build();

        // Assert
        config.IgnoreExtraFields.Should().BeTrue();
        config.MatcherConfig.Should().BeNull();
    }

    [Test]
    public void WithMatcher_IntegerWithRange_StoresCorrectMatcher()
    {
        // Arrange
        var builder = new PartialValidationBuilder<UserWithScore>();

        // Act
        builder.WithMatcher(u => u.Score, Match.Integer(min: 0, max: 100));
        var config = builder.Build();

        // Assert
        config.MatcherConfig.Should().NotBeNull();
        config.MatcherConfig!.PropertyMatchers["Score"].Type.Should().Be(MatcherType.Integer);
    }

    [Test]
    public void WithMatcher_RegexPattern_StoresCorrectMatcher()
    {
        // Arrange
        var builder = new PartialValidationBuilder<Product>();

        // Act
        builder.WithMatcher(p => p.Sku, Match.Regex(@"^[A-Z]{3}-\d{5}$"));
        var config = builder.Build();

        // Assert
        config.MatcherConfig.Should().NotBeNull();
        config.MatcherConfig!.PropertyMatchers["Sku"].Type.Should().Be(MatcherType.Regex);
    }

    [Test]
    public void WithMatcher_OneOf_StoresCorrectMatcher()
    {
        // Arrange
        var builder = new PartialValidationBuilder<UserWithStatus>();

        // Act
        builder.WithMatcher(u => u.Status, Match.OneOf("active", "inactive", "pending"));
        var config = builder.Build();

        // Assert
        config.MatcherConfig.Should().NotBeNull();
        config.MatcherConfig!.PropertyMatchers["Status"].Type.Should().Be(MatcherType.OneOf);
    }

    [Test]
    public void WithMatcher_OverwritesPreviousMatcher()
    {
        // Arrange
        var builder = new PartialValidationBuilder<TestUser>();

        // Act - Set Id to Email matcher, then change to Guid matcher
        builder
            .WithMatcher(u => u.Id, Match.Email())
            .WithMatcher(u => u.Id, Match.Guid());
        var config = builder.Build();

        // Assert - Should use the last matcher
        config.MatcherConfig.Should().NotBeNull();
        config.MatcherConfig!.PropertyMatchers.Should().HaveCount(1);
        config.MatcherConfig.PropertyMatchers["Id"].Type.Should().Be(MatcherType.Guid);
    }

    // Test models
    private record TestUser(string Id, string Name, string Email, string CreatedAt);
    private record UserWithScore(string Name, int Score);
    private record UserWithStatus(string Name, string Status);
    private record Product(string Name, string Sku);
}
