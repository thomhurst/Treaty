using FluentAssertions;
using Treaty.Validation;

namespace Treaty.Tests.Unit.Validation;

public class ValidationResultTests
{
    [Test]
    public void Success_IsValid_ReturnsTrue()
    {
        // Arrange & Act
        var result = ValidationResult.Success("GET /users/1");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Test]
    public void Success_Violations_ReturnsEmpty()
    {
        // Arrange & Act
        var result = ValidationResult.Success("GET /users/1");

        // Assert
        result.Violations.Should().BeEmpty();
    }

    [Test]
    public void Failure_IsValid_ReturnsFalse()
    {
        // Arrange
        var violation = new ContractViolation("GET /users/1", "$", "Test violation", ViolationType.InvalidType);

        // Act
        var result = ValidationResult.Failure("GET /users/1", violation);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Test]
    public void Failure_Violations_ContainsViolation()
    {
        // Arrange
        var violation = new ContractViolation("GET /users/1", "$", "Test violation", ViolationType.InvalidType);

        // Act
        var result = ValidationResult.Failure("GET /users/1", violation);

        // Assert
        result.Violations.Should().ContainSingle();
    }

    [Test]
    public void Failure_WithMultipleViolations_ContainsAllViolations()
    {
        // Arrange
        var violations = new[]
        {
            new ContractViolation("GET /users/1", "$.id", "Invalid id", ViolationType.InvalidType),
            new ContractViolation("GET /users/1", "$.email", "Invalid email", ViolationType.InvalidFormat)
        };

        // Act
        var result = ValidationResult.Failure("GET /users/1", violations);

        // Assert
        result.Violations.Should().HaveCount(2);
    }

    [Test]
    public void ThrowIfInvalid_WhenValid_DoesNotThrow()
    {
        // Arrange
        var result = ValidationResult.Success("GET /users/1");

        // Act & Assert
        var action = () => result.ThrowIfInvalid();
        action.Should().NotThrow();
    }

    [Test]
    public void ThrowIfInvalid_WhenInvalid_ThrowsContractViolationException()
    {
        // Arrange
        var violation = new ContractViolation("GET /users/1", "$", "Test violation", ViolationType.InvalidType);
        var result = ValidationResult.Failure("GET /users/1", violation);

        // Act & Assert
        var action = () => result.ThrowIfInvalid();
        action.Should().Throw<ContractViolationException>();
    }

    [Test]
    public void ThrowIfInvalid_WhenInvalid_ExceptionContainsViolations()
    {
        // Arrange
        var violation = new ContractViolation("GET /users/1", "$", "Test violation", ViolationType.InvalidType);
        var result = ValidationResult.Failure("GET /users/1", violation);

        // Act & Assert
        var action = () => result.ThrowIfInvalid();
        action.Should().Throw<ContractViolationException>()
            .Which.Violations.Should().ContainSingle();
    }
}

public class ContractViolationExceptionTests
{
    [Test]
    public void FormatsMessage_WithAllViolations()
    {
        // Arrange
        var violations = new[]
        {
            new ContractViolation("GET /users/1", "$.email", "Field 'email' expected format 'email'", ViolationType.InvalidFormat, "email", "not-an-email"),
            new ContractViolation("GET /users/1", "$.age", "Field 'age' expected minimum 0", ViolationType.OutOfRange, "0", "-5")
        };

        // Act
        var exception = new ContractViolationException(violations);

        // Assert
        exception.Message.Should().Contain("GET /users/1");
        exception.Message.Should().Contain("email");
        exception.Message.Should().Contain("age");
        exception.Violations.Should().HaveCount(2);
    }

    [Test]
    public void Violations_ReturnsProvidedViolations()
    {
        // Arrange
        var violations = new[]
        {
            new ContractViolation("GET /users/1", "$", "Test violation", ViolationType.InvalidType)
        };

        // Act
        var exception = new ContractViolationException(violations);

        // Assert
        exception.Violations.Should().BeEquivalentTo(violations);
    }
}
