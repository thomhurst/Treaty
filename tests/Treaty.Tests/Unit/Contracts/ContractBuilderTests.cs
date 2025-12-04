using FluentAssertions;
using Treaty.Contracts;
using TreatyLib = Treaty.Treaty;

namespace Treaty.Tests.Unit.Contracts;

public class ContractBuilderTests
{
    [Test]
    public void DefineContract_WithSimpleEndpoint_CreatesContract()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract("TestContract")
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithJsonBody<TestUser>())
            .Build();

        // Assert
        contract.Name.Should().Be("TestContract");
        contract.Endpoints.Should().HaveCount(1);

        var endpoint = contract.Endpoints[0];
        endpoint.PathTemplate.Should().Be("/users/{id}");
        endpoint.Method.Should().Be(HttpMethod.Get);
        endpoint.ResponseExpectations.Should().HaveCount(1);
        endpoint.ResponseExpectations[0].StatusCode.Should().Be(200);
    }

    [Test]
    public void DefineContract_WithMultipleEndpoints_CreatesContract()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200).WithJsonBody<TestUser[]>())
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Post)
                .ExpectingRequest(req => req.WithJsonBody<CreateUserRequest>())
                .ExpectingResponse(r => r.WithStatus(201).WithJsonBody<TestUser>())
            .ForEndpoint("/users/{id}")
                .WithMethod(HttpMethod.Delete)
                .ExpectingResponse(r => r.WithStatus(204))
            .Build();

        // Assert
        contract.Endpoints.Should().HaveCount(3);
    }

    [Test]
    public void DefineContract_WithDefaults_AppliesDefaults()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .WithDefaults(d => d
                .AllResponsesHaveHeader("X-Request-Id")
                .AllRequestsHaveHeader("Authorization"))
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Assert
        contract.Defaults.Should().NotBeNull();
        contract.Defaults!.ResponseHeaders.Should().ContainKey("X-Request-Id");
        contract.Defaults.RequestHeaders.Should().ContainKey("Authorization");
    }

    [Test]
    public void DefineContract_WithHeaders_SetsHeaders()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .WithHeader("Accept", "application/json")
                .ExpectingResponse(r => r
                    .WithStatus(200)
                    .WithHeader("Content-Type", "application/json"))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.ExpectedHeaders.Should().ContainKey("Accept");
        endpoint.ResponseExpectations[0].ExpectedHeaders.Should().ContainKey("Content-Type");
    }

    [Test]
    public void DefineContract_WithQueryParams_SetsQueryParams()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .WithQueryParam("page", QueryParameterType.Integer)
                .WithOptionalQueryParam("limit", QueryParameterType.Integer)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Assert
        var endpoint = contract.Endpoints[0];
        endpoint.ExpectedQueryParameters.Should().ContainKey("page");
        endpoint.ExpectedQueryParameters["page"].IsRequired.Should().BeTrue();
        endpoint.ExpectedQueryParameters.Should().ContainKey("limit");
        endpoint.ExpectedQueryParameters["limit"].IsRequired.Should().BeFalse();
    }

    [Test]
    public void DefineContract_WithoutName_UsesDefaultName()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Assert
        contract.Name.Should().Be("Contract");
    }

    #region Metadata Tests

    [Test]
    public void DefineContract_WithMetadata_SetsAllProperties()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract("User API")
            .WithMetadata(m => m
                .Version("1.2.3")
                .Description("API for managing users")
                .Contact(name: "API Team", email: "api@example.com", url: "https://example.com/support")
                .License("MIT", "https://opensource.org/licenses/MIT")
                .TermsOfService("https://example.com/tos"))
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Assert
        contract.Metadata.Should().NotBeNull();
        contract.Metadata!.Version.Should().Be("1.2.3");
        contract.Metadata.Description.Should().Be("API for managing users");
        contract.Metadata.Contact.Should().NotBeNull();
        contract.Metadata.Contact!.Name.Should().Be("API Team");
        contract.Metadata.Contact.Email.Should().Be("api@example.com");
        contract.Metadata.Contact.Url.Should().Be("https://example.com/support");
        contract.Metadata.License.Should().NotBeNull();
        contract.Metadata.License!.Name.Should().Be("MIT");
        contract.Metadata.License.Url.Should().Be("https://opensource.org/licenses/MIT");
        contract.Metadata.TermsOfService.Should().Be("https://example.com/tos");
    }

    [Test]
    public void DefineContract_WithPartialMetadata_SetsOnlyProvided()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract("Simple API")
            .WithMetadata(m => m
                .Version("1.0.0")
                .Description("A simple API"))
            .ForEndpoint("/ping")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Assert
        contract.Metadata.Should().NotBeNull();
        contract.Metadata!.Version.Should().Be("1.0.0");
        contract.Metadata.Description.Should().Be("A simple API");
        contract.Metadata.Contact.Should().BeNull();
        contract.Metadata.License.Should().BeNull();
        contract.Metadata.TermsOfService.Should().BeNull();
    }

    [Test]
    public void DefineContract_WithoutMetadata_MetadataIsNull()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Assert
        contract.Metadata.Should().BeNull();
    }

    [Test]
    public void DefineContract_WithContactOnly_SetsContact()
    {
        // Arrange & Act
        var contract = TreatyLib.DefineContract()
            .WithMetadata(m => m.Contact(email: "support@example.com"))
            .ForEndpoint("/users")
                .WithMethod(HttpMethod.Get)
                .ExpectingResponse(r => r.WithStatus(200))
            .Build();

        // Assert
        contract.Metadata.Should().NotBeNull();
        contract.Metadata!.Contact.Should().NotBeNull();
        contract.Metadata.Contact!.Name.Should().BeNull();
        contract.Metadata.Contact.Email.Should().Be("support@example.com");
        contract.Metadata.Contact.Url.Should().BeNull();
    }

    #endregion

    private record TestUser(int Id, string Name, string Email);
    private record CreateUserRequest(string Name, string Email);
}
