using Microsoft.OpenApi.Models;
using Moq;
using PxApi.OpenApi.DocumentFilters;
using PxApi.OpenApi.Examples;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PxApi.UnitTests.DocumentFilters
{
    [TestFixture]
    public class DataControllerPostEndpointDocumentFilterTests
    {
        private static DocumentFilterContext CreateEmptyContext()
        {
            ISchemaGenerator schemaGenerator = Mock.Of<ISchemaGenerator>();
            SchemaRepository repository = new();
            List<Microsoft.AspNetCore.Mvc.ApiExplorer.ApiDescription> apiDescriptions = [];
            DocumentFilterContext context = new(apiDescriptions, schemaGenerator, repository);
            return context;
        }

        [Test]
        public void Apply_MatchingPostOperation_ModifiesOperationAndSchemas()
        {
            // Arrange
            OpenApiOperation operation = new()
            {
                Parameters = [ new OpenApiParameter { Name = "lang" } ],
                RequestBody = new OpenApiRequestBody
                {
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType { Schema = new OpenApiSchema() }
                    }
                },
                Responses = new OpenApiResponses
                {
                    ["200"] = new OpenApiResponse
                    {
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            ["application/json"] = new OpenApiMediaType(),
                            ["text/csv"] = new OpenApiMediaType { Schema = new OpenApiSchema() }
                        }
                    }
                }
            };

            OpenApiPathItem pathItem = new()
            {
                Operations = new Dictionary<OperationType, OpenApiOperation>
                {
                    [OperationType.Post] = operation
                }
            };

            OpenApiSchema filterSchema = new();
            OpenApiDocument document = new()
            {
                Paths = new OpenApiPaths { ["/data/{database}/{table}"] = pathItem },
                Components = new OpenApiComponents
                {
                    Schemas = new Dictionary<string, OpenApiSchema>
                    {
                        ["Filter"] = filterSchema
                    }
                }
            };

            DataControllerPostEndpointDocumentFilter filter = new();
            DocumentFilterContext context = CreateEmptyContext();

            // Act
            filter.Apply(document, context);

            // Assert request body examples
            OpenApiMediaType rbMediaType = operation.RequestBody!.Content["application/json"];
            Assert.Multiple(() =>
            {
                Assert.That(rbMediaType.Examples, Is.Not.Null);
                Assert.That(rbMediaType.Examples, Is.Not.Empty);
                Assert.That(rbMediaType.Examples, Has.Count.EqualTo(DataRequestBodyExamples.Examples.Count));
                Assert.That(rbMediaType.Schema.Description, Does.Contain("Dictionary mapping dimension codes"));
            });

            // Assert response
            OpenApiResponse response200 = operation.Responses["200"];
            OpenApiMediaType jsonMediaType = response200.Content["application/json"];
            Assert.Multiple(() =>
            {
                Assert.That(jsonMediaType.Schema, Is.Not.Null);
                Assert.That(jsonMediaType.Schema.Reference, Is.Not.Null);
                Assert.That(jsonMediaType.Schema.Reference.Id, Is.EqualTo("JsonStat2"));
                Assert.That(jsonMediaType.Example, Is.EqualTo(JsonStat2Example.Instance));
                Assert.That(response200.Description, Does.Contain("JSON-stat2.0"));
            });

            OpenApiMediaType csvMediaType = response200.Content["text/csv"];
            Assert.That(csvMediaType.Schema.Description, Does.Contain("CSV dataset"));

            // Accept header note and lang parameter
            Assert.Multiple(() =>
            {
                Assert.That(operation.Description, Does.Contain("Accept header options:"));
                OpenApiParameter? langParam = operation.Parameters.FirstOrDefault(p => p.Name == "lang");
                Assert.That(langParam, Is.Not.Null);
                Assert.That(langParam!.Description, Does.Contain("Optional language code"));
            });

            // Filter schema description
            Assert.That(filterSchema.Description, Does.Contain("Filter object"));
        }

        [Test]
        public void Apply_NonMatchingPostOperation_DoesNotModify()
        {
            // Arrange (path mismatch)
            OpenApiOperation operation = new()
            {
                RequestBody = new OpenApiRequestBody
                {
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType { Schema = new OpenApiSchema() }
                    }
                },
                Responses = new OpenApiResponses
                {
                    ["200"] = new OpenApiResponse
                    {
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            ["application/json"] = new OpenApiMediaType()
                        }
                    }
                }
            };

            OpenApiPathItem pathItem = new()
            {
                Operations = new Dictionary<OperationType, OpenApiOperation>
                {
                    [OperationType.Post] = operation
                }
            };

            OpenApiDocument document = new()
            {
                Paths = new OpenApiPaths
                {
                    ["/other"] = pathItem
                }
            };

            DataControllerPostEndpointDocumentFilter filter = new();
            DocumentFilterContext context = CreateEmptyContext();

            // Act
            filter.Apply(document, context);

            // Assert
            OpenApiMediaType rbMediaType = operation.RequestBody!.Content["application/json"];
            Assert.Multiple(() =>
            {
                Assert.That(rbMediaType.Examples, Is.Empty);
                Assert.That(operation.Description, Is.Null);
            });
        }
    }
}
