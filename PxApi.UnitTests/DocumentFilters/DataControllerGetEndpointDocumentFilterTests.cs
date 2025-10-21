using Microsoft.OpenApi.Models;
using Moq;
using PxApi.OpenApi.DocumentFilters;
using PxApi.OpenApi.Examples;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PxApi.UnitTests.DocumentFilters
{
    [TestFixture]
    public class DataControllerGetEndpointDocumentFilterTests
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
        public void Apply_MatchingGetOperation_ModifiesOperation()
        {
            // Arrange
            OpenApiOperation operation = new()
            {
                Parameters = [
                    new OpenApiParameter { Name = "filters" },
                    new OpenApiParameter { Name = "lang" }
                ],
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
                    [OperationType.Get] = operation
                }
            };

            OpenApiDocument document = new()
            {
                Paths = new OpenApiPaths
                {
                    ["/data/{database}/{table}"] = pathItem
                }
            };

            DataControllerGetEndpointDocumentFilter filter = new();
            DocumentFilterContext context = CreateEmptyContext();

            // Act
            filter.Apply(document, context);

            // Assert
            OpenApiParameter? filtersParam = operation.Parameters.FirstOrDefault(p => p.Name == "filters");
            OpenApiParameter? langParam = operation.Parameters.FirstOrDefault(p => p.Name == "lang");
            Assert.Multiple(() =>
            {
                Assert.That(filtersParam, Is.Not.Null);
                Assert.That(filtersParam!.Description, Does.Contain("Array of filter specs"));
                Assert.That(filtersParam.Examples, Is.Not.Null);
                Assert.That(filtersParam.Examples, Has.Count.EqualTo(FiltersParameterExamples.Examples.Count));
                Assert.That(operation.Description, Does.Contain("Accept header options:"));
                Assert.That(langParam, Is.Not.Null);
                Assert.That(langParam!.Description, Does.Contain("Optional language code"));
            });

            Assert.That(operation.Responses.ContainsKey("200"), Is.True);
            OpenApiResponse response200 = operation.Responses["200"];
            OpenApiMediaType jsonMediaType = response200.Content["application/json"];
            Assert.Multiple(() =>
            {
                Assert.That(jsonMediaType.Schema, Is.Not.Null);
                Assert.That(jsonMediaType.Schema.Reference, Is.Not.Null);
                Assert.That(jsonMediaType.Schema.Reference.Id, Is.EqualTo("JsonStat2"));
                Assert.That(jsonMediaType.Example, Is.Not.Null);
                Assert.That(jsonMediaType.Example, Is.EqualTo(JsonStat2Example.Instance));
                Assert.That(response200.Description, Does.Contain("JSON-stat"));
            });

            OpenApiMediaType csvMediaType = response200.Content["text/csv"];
            Assert.That(csvMediaType.Schema.Description, Does.Contain("CSV dataset"));
        }

        [Test]
        public void Apply_NonMatchingGetOperation_DoesNotModify()
        {
            // Arrange
            OpenApiOperation operation = new OpenApiOperation
            {
                Parameters = [ new OpenApiParameter { Name = "filters" } ],
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
                    [OperationType.Get] = operation
                }
            };

            OpenApiDocument document = new()
            {
                Paths = new OpenApiPaths
                {
                    ["/other"] = pathItem
                }
            };

            DataControllerGetEndpointDocumentFilter filter = new();
            DocumentFilterContext context = CreateEmptyContext();

            // Act
            filter.Apply(document, context);

            // Assert (unchanged: description still null, example not set because logic not run)
            OpenApiMediaType jsonMediaType = operation.Responses["200"].Content["application/json"];
            Assert.Multiple(() =>
            {
                Assert.That(operation.Description, Is.Null);
                Assert.That(jsonMediaType.Schema, Is.Null);
                Assert.That(jsonMediaType.Example, Is.Null);
            });
        }
    }
}
