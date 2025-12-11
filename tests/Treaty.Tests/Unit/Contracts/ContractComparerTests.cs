using System.Text;
using FluentAssertions;
using Treaty.Contracts;
using Treaty.OpenApi;

namespace Treaty.Tests.Unit.Contracts;

public class ContractComparerTests
{
    private static async Task<ContractDefinition> BuildContractAsync(string yaml)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(yaml));
        return await Contract.FromOpenApi(stream, OpenApiFormat.Yaml).BuildAsync();
    }

    [Test]
    public async Task Compare_IdenticalContracts_ReturnsNoDifferences()
    {
        // Arrange
        const string spec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  responses:
                    '200':
                      description: OK
            """;

        var oldContract = await BuildContractAsync(spec);
        var newContract = await BuildContractAsync(spec);

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.AllChanges.Should().BeEmpty();
        diff.IsCompatible.Should().BeTrue();
        diff.HasBreakingChanges.Should().BeFalse();
    }

    [Test]
    public async Task Compare_EndpointRemoved_DetectsBreakingChange()
    {
        // Arrange
        const string oldSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  responses:
                    '200':
                      description: OK
              /products:
                get:
                  responses:
                    '200':
                      description: OK
            """;

        const string newSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  responses:
                    '200':
                      description: OK
            """;

        var oldContract = await BuildContractAsync(oldSpec);
        var newContract = await BuildContractAsync(newSpec);

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.BreakingChanges.Should().HaveCount(1);
        diff.BreakingChanges[0].Type.Should().Be(ContractChangeType.EndpointRemoved);
        diff.BreakingChanges[0].Description.Should().Contain("/products");
        diff.IsCompatible.Should().BeFalse();
    }

    [Test]
    public async Task Compare_EndpointAdded_DetectsInfoChange()
    {
        // Arrange
        const string oldSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  responses:
                    '200':
                      description: OK
            """;

        const string newSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  responses:
                    '200':
                      description: OK
              /products:
                get:
                  responses:
                    '200':
                      description: OK
            """;

        var oldContract = await BuildContractAsync(oldSpec);
        var newContract = await BuildContractAsync(newSpec);

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.InfoChanges.Should().HaveCount(1);
        diff.InfoChanges[0].Type.Should().Be(ContractChangeType.EndpointAdded);
        diff.InfoChanges[0].Description.Should().Contain("/products");
        diff.IsCompatible.Should().BeTrue();
    }

    [Test]
    public async Task Compare_SuccessStatusCodeRemoved_DetectsBreakingChange()
    {
        // Arrange
        const string oldSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  responses:
                    '200':
                      description: OK
                    '201':
                      description: Created
            """;

        const string newSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  responses:
                    '200':
                      description: OK
            """;

        var oldContract = await BuildContractAsync(oldSpec);
        var newContract = await BuildContractAsync(newSpec);

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.BreakingChanges.Should().HaveCount(1);
        diff.BreakingChanges[0].Type.Should().Be(ContractChangeType.ResponseStatusCodeRemoved);
        diff.BreakingChanges[0].OldValue.Should().Be("201");
    }

    [Test]
    public async Task Compare_ErrorStatusCodeRemoved_DetectsWarning()
    {
        // Arrange
        const string oldSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  responses:
                    '200':
                      description: OK
                    '404':
                      description: Not Found
            """;

        const string newSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  responses:
                    '200':
                      description: OK
            """;

        var oldContract = await BuildContractAsync(oldSpec);
        var newContract = await BuildContractAsync(newSpec);

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.Warnings.Should().HaveCount(1);
        diff.Warnings[0].Type.Should().Be(ContractChangeType.ResponseStatusCodeRemoved);
        diff.Warnings[0].OldValue.Should().Be("404");
        diff.IsCompatible.Should().BeTrue();
    }

    [Test]
    public async Task Compare_StatusCodeAdded_DetectsInfoChange()
    {
        // Arrange
        const string oldSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  responses:
                    '200':
                      description: OK
            """;

        const string newSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  responses:
                    '200':
                      description: OK
                    '201':
                      description: Created
            """;

        var oldContract = await BuildContractAsync(oldSpec);
        var newContract = await BuildContractAsync(newSpec);

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.InfoChanges.Should().HaveCount(1);
        diff.InfoChanges[0].Type.Should().Be(ContractChangeType.ResponseStatusCodeAdded);
        diff.InfoChanges[0].NewValue.Should().Be("201");
    }

    [Test]
    public async Task Compare_RequiredRequestBodyAdded_DetectsBreakingChange()
    {
        // Arrange
        const string oldSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                post:
                  responses:
                    '201':
                      description: Created
            """;

        const string newSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                post:
                  requestBody:
                    required: true
                    content:
                      application/json:
                        schema:
                          type: object
                          properties:
                            name:
                              type: string
                  responses:
                    '201':
                      description: Created
            """;

        var oldContract = await BuildContractAsync(oldSpec);
        var newContract = await BuildContractAsync(newSpec);

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.BreakingChanges.Should().HaveCount(1);
        diff.BreakingChanges[0].Type.Should().Be(ContractChangeType.RequestFieldAdded);
        diff.BreakingChanges[0].Location.Should().Be(ChangeLocation.RequestBody);
    }

    [Test]
    public async Task Compare_OptionalRequestBodyAdded_DetectsInfoChange()
    {
        // Arrange
        const string oldSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                post:
                  responses:
                    '201':
                      description: Created
            """;

        const string newSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                post:
                  requestBody:
                    required: false
                    content:
                      application/json:
                        schema:
                          type: object
                          properties:
                            name:
                              type: string
                  responses:
                    '201':
                      description: Created
            """;

        var oldContract = await BuildContractAsync(oldSpec);
        var newContract = await BuildContractAsync(newSpec);

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.InfoChanges.Should().HaveCount(1);
        diff.InfoChanges[0].Type.Should().Be(ContractChangeType.RequestFieldAdded);
    }

    [Test]
    public async Task Compare_RequestBodyBecameRequired_DetectsBreakingChange()
    {
        // Arrange
        const string oldSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                post:
                  requestBody:
                    required: false
                    content:
                      application/json:
                        schema:
                          type: object
                          properties:
                            name:
                              type: string
                  responses:
                    '201':
                      description: Created
            """;

        const string newSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                post:
                  requestBody:
                    required: true
                    content:
                      application/json:
                        schema:
                          type: object
                          properties:
                            name:
                              type: string
                  responses:
                    '201':
                      description: Created
            """;

        var oldContract = await BuildContractAsync(oldSpec);
        var newContract = await BuildContractAsync(newSpec);

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.BreakingChanges.Should().HaveCount(1);
        diff.BreakingChanges[0].Type.Should().Be(ContractChangeType.RequestFieldMadeRequired);
    }

    [Test]
    public async Task Compare_RequestBodyBecameOptional_DetectsInfoChange()
    {
        // Arrange
        const string oldSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                post:
                  requestBody:
                    required: true
                    content:
                      application/json:
                        schema:
                          type: object
                          properties:
                            name:
                              type: string
                  responses:
                    '201':
                      description: Created
            """;

        const string newSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                post:
                  requestBody:
                    required: false
                    content:
                      application/json:
                        schema:
                          type: object
                          properties:
                            name:
                              type: string
                  responses:
                    '201':
                      description: Created
            """;

        var oldContract = await BuildContractAsync(oldSpec);
        var newContract = await BuildContractAsync(newSpec);

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.InfoChanges.Should().HaveCount(1);
        diff.InfoChanges[0].Type.Should().Be(ContractChangeType.RequestFieldMadeOptional);
    }

    [Test]
    public async Task Compare_RequiredRequestHeaderAdded_DetectsBreakingChange()
    {
        // Arrange
        const string oldSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  responses:
                    '200':
                      description: OK
            """;

        const string newSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  parameters:
                    - name: Authorization
                      in: header
                      required: true
                      schema:
                        type: string
                  responses:
                    '200':
                      description: OK
            """;

        var oldContract = await BuildContractAsync(oldSpec);
        var newContract = await BuildContractAsync(newSpec);

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.BreakingChanges.Should().HaveCount(1);
        diff.BreakingChanges[0].Type.Should().Be(ContractChangeType.RequestHeaderAdded);
        diff.BreakingChanges[0].FieldName.Should().Be("Authorization");
    }

    [Test]
    public async Task Compare_RequestHeaderRemoved_DetectsInfoChange()
    {
        // Arrange
        const string oldSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  parameters:
                    - name: Authorization
                      in: header
                      required: true
                      schema:
                        type: string
                  responses:
                    '200':
                      description: OK
            """;

        const string newSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  responses:
                    '200':
                      description: OK
            """;

        var oldContract = await BuildContractAsync(oldSpec);
        var newContract = await BuildContractAsync(newSpec);

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.InfoChanges.Should().HaveCount(1);
        diff.InfoChanges[0].Type.Should().Be(ContractChangeType.RequestHeaderRemoved);
    }

    [Test]
    public async Task Compare_ResponseBodyTypeChanged_DetectsBreakingChange()
    {
        // Arrange
        const string oldSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema:
                            type: array
                            items:
                              type: object
                              properties:
                                id:
                                  type: integer
                                name:
                                  type: string
            """;

        const string newSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema:
                            type: object
                            properties:
                              users:
                                type: array
                                items:
                                  type: object
                              total:
                                type: integer
            """;

        var oldContract = await BuildContractAsync(oldSpec);
        var newContract = await BuildContractAsync(newSpec);

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.BreakingChanges.Should().HaveCount(1);
        diff.BreakingChanges[0].Type.Should().Be(ContractChangeType.ResponseFieldTypeChanged);
        diff.BreakingChanges[0].OldValue.Should().Be("Array");
        diff.BreakingChanges[0].NewValue.Should().Be("Object");
    }

    [Test]
    public async Task Compare_ResponseBodySchemaAdded_DetectsInfoChange()
    {
        // Arrange
        const string oldSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  responses:
                    '200':
                      description: OK
            """;

        const string newSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema:
                            type: array
                            items:
                              type: object
                              properties:
                                id:
                                  type: integer
                                name:
                                  type: string
            """;

        var oldContract = await BuildContractAsync(oldSpec);
        var newContract = await BuildContractAsync(newSpec);

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.InfoChanges.Should().HaveCount(1);
        diff.InfoChanges[0].Type.Should().Be(ContractChangeType.ResponseFieldAdded);
    }

    [Test]
    public async Task Compare_ResponseBodySchemaRemoved_DetectsWarning()
    {
        // Arrange
        const string oldSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema:
                            type: array
                            items:
                              type: object
                              properties:
                                id:
                                  type: integer
                                name:
                                  type: string
            """;

        const string newSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  responses:
                    '200':
                      description: OK
            """;

        var oldContract = await BuildContractAsync(oldSpec);
        var newContract = await BuildContractAsync(newSpec);

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.Warnings.Should().HaveCount(1);
        diff.Warnings[0].Type.Should().Be(ContractChangeType.ResponseFieldRemoved);
    }

    [Test]
    public async Task Compare_PathParameterEndpoints_MatchesCorrectly()
    {
        // Arrange - endpoints with different param names but same pattern
        const string oldSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users/{userId}:
                get:
                  parameters:
                    - name: userId
                      in: path
                      required: true
                      schema:
                        type: string
                  responses:
                    '200':
                      description: OK
            """;

        const string newSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users/{id}:
                get:
                  parameters:
                    - name: id
                      in: path
                      required: true
                      schema:
                        type: string
                  responses:
                    '200':
                      description: OK
            """;

        var oldContract = await BuildContractAsync(oldSpec);
        var newContract = await BuildContractAsync(newSpec);

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert - should match as same endpoint since path pattern is identical
        diff.AllChanges.Should().BeEmpty();
    }

    [Test]
    public async Task Compare_NullOldContract_ThrowsArgumentNullException()
    {
        // Arrange
        const string spec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  responses:
                    '200':
                      description: OK
            """;

        var newContract = await BuildContractAsync(spec);

        // Act
        var act = () => ContractComparer.Compare(null!, newContract);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("oldContract");
    }

    [Test]
    public async Task Compare_NullNewContract_ThrowsArgumentNullException()
    {
        // Arrange
        const string spec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  responses:
                    '200':
                      description: OK
            """;

        var oldContract = await BuildContractAsync(spec);

        // Act
        var act = () => ContractComparer.Compare(oldContract, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("newContract");
    }

    [Test]
    public async Task GetSummary_WithBreakingChanges_ContainsBreakingSection()
    {
        // Arrange
        const string oldSpec = """
            openapi: '3.0.3'
            info:
              title: Old API
              version: '1.0'
            paths:
              /users:
                get:
                  responses:
                    '200':
                      description: OK
            """;

        const string newSpec = """
            openapi: '3.0.3'
            info:
              title: New API
              version: '1.0'
            paths: {}
            """;

        var oldContract = await BuildContractAsync(oldSpec);
        var newContract = await BuildContractAsync(newSpec);

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);
        var summary = diff.GetSummary();

        // Assert
        summary.Should().Contain("BREAKING CHANGES:");
        summary.Should().Contain("/users");
    }

    [Test]
    public async Task ThrowIfBreaking_WithBreakingChanges_ThrowsException()
    {
        // Arrange
        const string oldSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  responses:
                    '200':
                      description: OK
            """;

        const string newSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths: {}
            """;

        var oldContract = await BuildContractAsync(oldSpec);
        var newContract = await BuildContractAsync(newSpec);

        var diff = ContractComparer.Compare(oldContract, newContract);

        // Act
        var act = () => diff.ThrowIfBreaking();

        // Assert
        act.Should().Throw<ContractBreakingChangeException>()
            .Which.Diff.Should().BeSameAs(diff);
    }

    [Test]
    public async Task ThrowIfBreaking_WithoutBreakingChanges_DoesNotThrow()
    {
        // Arrange
        const string oldSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  responses:
                    '200':
                      description: OK
            """;

        const string newSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  responses:
                    '200':
                      description: OK
              /products:
                get:
                  responses:
                    '200':
                      description: OK
            """;

        var oldContract = await BuildContractAsync(oldSpec);
        var newContract = await BuildContractAsync(newSpec);

        var diff = ContractComparer.Compare(oldContract, newContract);

        // Act
        var act = () => diff.ThrowIfBreaking();

        // Assert
        act.Should().NotThrow();
    }

    [Test]
    public async Task Compare_ResponseHeaderAdded_DetectsInfoChange()
    {
        // Arrange
        const string oldSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  responses:
                    '200':
                      description: OK
            """;

        const string newSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  responses:
                    '200':
                      description: OK
                      headers:
                        X-Request-Id:
                          schema:
                            type: string
            """;

        var oldContract = await BuildContractAsync(oldSpec);
        var newContract = await BuildContractAsync(newSpec);

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.InfoChanges.Should().HaveCount(1);
        diff.InfoChanges[0].Type.Should().Be(ContractChangeType.ResponseHeaderAdded);
        diff.InfoChanges[0].FieldName.Should().Be("X-Request-Id");
    }

    [Test]
    public async Task Compare_ResponseHeaderRemoved_DetectsWarning()
    {
        // Arrange
        const string oldSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  responses:
                    '200':
                      description: OK
                      headers:
                        X-Request-Id:
                          schema:
                            type: string
            """;

        const string newSpec = """
            openapi: '3.0.3'
            info:
              title: Test API
              version: '1.0'
            paths:
              /users:
                get:
                  responses:
                    '200':
                      description: OK
            """;

        var oldContract = await BuildContractAsync(oldSpec);
        var newContract = await BuildContractAsync(newSpec);

        // Act
        var diff = ContractComparer.Compare(oldContract, newContract);

        // Assert
        diff.Warnings.Should().HaveCount(1);
        diff.Warnings[0].Type.Should().Be(ContractChangeType.ResponseHeaderRemoved);
        diff.Warnings[0].FieldName.Should().Be("X-Request-Id");
    }
}
