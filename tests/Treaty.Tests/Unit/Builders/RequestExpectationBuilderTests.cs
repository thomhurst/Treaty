using FluentAssertions;
using Treaty.Matching;
using TreatyLib = Treaty.Treaty;

namespace Treaty.Tests.Unit.Builders;

public class RequestExpectationBuilderTests
{
    #region Basic Builder Tests

    [Test]
    public void WithJsonBody_Generic_SetsBodyType()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingRequest(req => req.WithJsonBody<CreateUserRequest>())
                .ExpectingResponse(r => r.WithStatus(201))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.RequestExpectation.Should().NotBeNull();
        endpoint.RequestExpectation!.BodyValidator.Should().NotBeNull();
        endpoint.RequestExpectation.BodyValidator!.ExpectedType.Should().Be(typeof(CreateUserRequest));
    }

    [Test]
    public void WithJsonBody_Type_SetsBodyType()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingRequest(req => req.WithJsonBody(typeof(CreateUserRequest)))
                .ExpectingResponse(r => r.WithStatus(201))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.RequestExpectation.Should().NotBeNull();
        endpoint.RequestExpectation!.BodyValidator.Should().NotBeNull();
        endpoint.RequestExpectation.BodyValidator!.ExpectedType.Should().Be(typeof(CreateUserRequest));
    }

    [Test]
    public void WithContentType_SetsContentType()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/upload")
                .WithMethod(HttpMethod.Post)
                .ExpectingRequest(req => req.WithContentType("multipart/form-data"))
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.RequestExpectation.Should().NotBeNull();
        endpoint.RequestExpectation!.ContentType.Should().Be("multipart/form-data");
    }

    [Test]
    public void Optional_SetsIsRequiredFalse()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingRequest(req => req.WithJsonBody<CreateUserRequest>().Optional())
                .ExpectingResponse(r => r.WithStatus(201))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.RequestExpectation.Should().NotBeNull();
        endpoint.RequestExpectation!.IsRequired.Should().BeFalse();
    }

    [Test]
    public void Default_IsRequiredTrue()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingRequest(req => req.WithJsonBody<CreateUserRequest>())
                .ExpectingResponse(r => r.WithStatus(201))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.RequestExpectation.Should().NotBeNull();
        endpoint.RequestExpectation!.IsRequired.Should().BeTrue();
    }

    [Test]
    public void WithJsonBody_SetsDefaultContentType()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingRequest(req => req.WithJsonBody<CreateUserRequest>())
                .ExpectingResponse(r => r.WithStatus(201))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.RequestExpectation!.ContentType.Should().Be("application/json");
    }

    [Test]
    public void NoExpectingRequest_RequestExpectationIsNull()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.RequestExpectation.Should().BeNull();
    }

    #endregion

    #region Matcher Schema Tests

    [Test]
    public void WithMatcherSchema_CreatesMatcherValidator()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingRequest(r => r
                    .WithMatcherSchema(new
                    {
                        requestId = Match.Guid(),
                        name = Match.NonEmptyString(),
                        email = Match.Email()
                    }))
                .ExpectingResponse(r => r.WithStatus(201))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.RequestExpectation.Should().NotBeNull();
        endpoint.RequestExpectation!.BodyValidator.Should().NotBeNull();
        endpoint.RequestExpectation.ContentType.Should().Be("application/json");
    }

    [Test]
    public void WithMatcherSchema_ClearsTypeBasedSchema()
    {
        // Arrange & Act - set type first, then matcher schema
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingRequest(r => r
                    .WithJsonBody<CreateUserRequest>()  // Set type first
                    .WithMatcherSchema(new { id = Match.Guid() }))  // Override with matcher
                .ExpectingResponse(r => r.WithStatus(201))
            .Build();

        // Assert - should use matcher schema, not type schema
        var endpoint = contract.Endpoints[0];
        endpoint.RequestExpectation.Should().NotBeNull();
        endpoint.RequestExpectation!.BodyValidator.Should().NotBeNull();
        // Matcher schema validator does not have ExpectedType
        endpoint.RequestExpectation.BodyValidator!.ExpectedType.Should().BeNull();
    }

    [Test]
    public void WithMatcherSchema_WithNestedObjects()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/orders")
                .WithMethod(HttpMethod.Post)
                .ExpectingRequest(r => r
                    .WithMatcherSchema(new
                    {
                        orderId = Match.Guid(),
                        customer = Match.Object(new
                        {
                            id = Match.Integer(min: 1),
                            email = Match.Email()
                        }),
                        items = Match.EachLike(new
                        {
                            sku = Match.NonEmptyString(),
                            quantity = Match.Integer(min: 1)
                        })
                    }))
                .ExpectingResponse(r => r.WithStatus(201))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.RequestExpectation.Should().NotBeNull();
        endpoint.RequestExpectation!.BodyValidator.Should().NotBeNull();
    }

    [Test]
    public void WithMatcherSchema_ThrowsOnNull()
    {
        // Arrange & Act
        var action = () => TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingRequest(r => r.WithMatcherSchema(null!))
                .ExpectingResponse(r => r.WithStatus(201))
            .Build();

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void WithMatcherSchema_WithAllMatcherTypes()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/test")
                .WithMethod(HttpMethod.Post)
                .ExpectingRequest(r => r
                    .WithMatcherSchema(new
                    {
                        guid = Match.Guid(),
                        str = Match.String(),
                        nonEmpty = Match.NonEmptyString(),
                        email = Match.Email(),
                        uri = Match.Uri(),
                        regex = Match.Regex(@"^\d+$"),
                        integer = Match.Integer(min: 0, max: 100),
                        dec = Match.Decimal(min: 0m),
                        boolean = Match.Boolean(),
                        dateTime = Match.DateTime(),
                        dateOnly = Match.DateOnly(),
                        timeOnly = Match.TimeOnly(),
                        oneOf = Match.OneOf("a", "b", "c"),
                        any = Match.Any(),
                        nullable = Match.Null()
                    }))
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.RequestExpectation.Should().NotBeNull();
        endpoint.RequestExpectation!.BodyValidator.Should().NotBeNull();
    }

    #endregion

    #region Partial Validation Tests

    [Test]
    public void WithJsonBody_PartialValidation_SetsPartialConfig()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingRequest(r => r
                    .WithJsonBody<TestRequest>(v => v
                        .WithMatcher(req => req.RequestId, Match.Guid())
                        .WithMatcher(req => req.Timestamp, Match.DateTime())))
                .ExpectingResponse(r => r.WithStatus(201))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.RequestExpectation.Should().NotBeNull();
        endpoint.RequestExpectation!.PartialValidation.Should().NotBeNull();
        endpoint.RequestExpectation.PartialValidation!.MatcherConfig.Should().NotBeNull();
        endpoint.RequestExpectation.PartialValidation.MatcherConfig!.PropertyMatchers.Should().HaveCount(2);
    }

    [Test]
    public void WithJsonBody_PartialValidation_OnlyValidate()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingRequest(r => r
                    .WithJsonBody<TestRequest>(v => v
                        .OnlyValidate(req => req.Name, req => req.Email)))
                .ExpectingResponse(r => r.WithStatus(201))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.RequestExpectation.Should().NotBeNull();
        endpoint.RequestExpectation!.PartialValidation.Should().NotBeNull();
        endpoint.RequestExpectation.PartialValidation!.PropertiesToValidate.Should().HaveCount(2);
        endpoint.RequestExpectation.PartialValidation.PropertiesToValidate.Should().Contain("Name");
        endpoint.RequestExpectation.PartialValidation.PropertiesToValidate.Should().Contain("Email");
    }

    [Test]
    public void WithJsonBody_PartialValidation_IgnoreExtraFields()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingRequest(r => r
                    .WithJsonBody<TestRequest>(v => v.IgnoreExtraFields()))
                .ExpectingResponse(r => r.WithStatus(201))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.RequestExpectation.Should().NotBeNull();
        endpoint.RequestExpectation!.PartialValidation.Should().NotBeNull();
        endpoint.RequestExpectation.PartialValidation!.IgnoreExtraFields.Should().BeTrue();
    }

    [Test]
    public void WithJsonBody_PartialValidation_CombinedOptions()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingRequest(r => r
                    .WithJsonBody<TestRequest>(v => v
                        .OnlyValidate(req => req.Name)
                        .WithMatcher(req => req.RequestId, Match.Guid())
                        .IgnoreExtraFields()))
                .ExpectingResponse(r => r.WithStatus(201))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        var partialConfig = endpoint.RequestExpectation!.PartialValidation!;

        partialConfig.PropertiesToValidate.Should().Contain("Name");
        partialConfig.IgnoreExtraFields.Should().BeTrue();
        partialConfig.MatcherConfig.Should().NotBeNull();
        partialConfig.MatcherConfig!.PropertyMatchers.Should().ContainKey("RequestId");
    }

    #endregion

    #region Test Types

    private record CreateUserRequest(string Name, string Email);

    private class TestRequest
    {
        public Guid RequestId { get; set; }
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }

    #endregion
}
