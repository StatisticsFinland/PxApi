using Microsoft.OpenApi;
using Moq;
using PxApi.OpenApi.DocumentFilters;
using PxApi.OpenApi.Examples;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PxApi.UnitTests.OpenApi.DocumentFilters
{
    [TestFixture]
    public class DataControllerGetEndpointDocumentFilterTests
    {
        private const string ApplicationJson = "application/json";
        private const string TextCsv = "text/csv";

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
                            [ApplicationJson] = new OpenApiMediaType(),
                            [TextCsv] = new OpenApiMediaType { Schema = new OpenApiSchema() }
                        }
                    },
                    ["400"] = new OpenApiResponse
                    {
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            [ApplicationJson] = new OpenApiMediaType(),
                            [TextCsv] = new OpenApiMediaType()
                        }
                    },
                    ["406"] = new OpenApiResponse
                    {
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            [ApplicationJson] = new OpenApiMediaType(),
                            [TextCsv] = new OpenApiMediaType()
                        }
                    },
                    ["500"] = new OpenApiResponse
                    {
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            [ApplicationJson] = new OpenApiMediaType(),
                            [TextCsv] = new OpenApiMediaType()
                        }
                    }
                }
            };

            OpenApiPathItem pathItem = new()
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>
                {
                    [HttpMethod.Get] = operation
                }
            };

            OpenApiDocument document = new()
            {
                Paths = new OpenApiPaths
                {
                    ["/data/databases/{database}/tables/{table}"] = pathItem
                }
            };

            DataControllerGetEndpointDocumentFilter filter = new();
            DocumentFilterContext context = CreateEmptyContext();

            // Act
            filter.Apply(document, context);

            // Assert
            OpenApiParameter? filtersParam = operation.Parameters.FirstOrDefault(p => p.Name == "filters") as OpenApiParameter;
            OpenApiParameter? langParam = operation.Parameters.FirstOrDefault(p => p.Name == "lang") as OpenApiParameter;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(filtersParam, Is.Not.Null);
                Assert.That(filtersParam!.Description, Does.Contain("Array of filter specs"));
                Assert.That(filtersParam.Examples, Is.Not.Null);
                Assert.That(filtersParam.Examples, Has.Count.EqualTo(FiltersParameterExamples.Examples.Count));
                Assert.That(operation.Description, Does.Contain("Accept header options:"));
                Assert.That(langParam, Is.Not.Null);
                Assert.That(langParam!.Description, Does.Contain("Optional language code"));
            }

            Assert.That(operation.Responses.ContainsKey("200"), Is.True);
            OpenApiResponse response200 = (OpenApiResponse)operation.Responses["200"];
            OpenApiMediaType jsonMediaType = response200.Content![ApplicationJson];
            using (Assert.EnterMultipleScope())
            {
                Assert.That(jsonMediaType.Schema, Is.Not.Null);
                Assert.That(jsonMediaType.Schema, Is.TypeOf<OpenApiSchemaReference>());
                Assert.That(jsonMediaType.Examples, Is.Not.Null);
                Assert.That(jsonMediaType.Examples!, Has.Count.EqualTo(1));
                Assert.That(jsonMediaType.Examples!.ContainsKey("default"), Is.True);
                Assert.That(response200.Description, Does.Contain("JSON-stat"));
            }

            OpenApiMediaType csvMediaType = response200.Content[TextCsv];
            OpenApiSchema csvSchema = (OpenApiSchema)csvMediaType.Schema!;
            Assert.That(csvSchema!.Description, Does.Contain("CSV dataset"));

            OpenApiResponse response400 = (OpenApiResponse)operation.Responses["400"];
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response400.Content!.ContainsKey("text/csv"), Is.False);
                Assert.That(response400.Content[ApplicationJson], Is.Not.Null);
            }

            OpenApiResponse response406 = (OpenApiResponse)operation.Responses["406"];
            Assert.That(response406.Content, Is.Empty);

            OpenApiResponse response500 = (OpenApiResponse)operation.Responses["500"];
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response500.Content!.ContainsKey("text/csv"), Is.False);
                Assert.That(response500.Content[ApplicationJson], Is.Not.Null);
            }
        }

        [Test]
        public void Apply_NonMatchingGetOperation_DoesNotModify()
        {
            // Arrange
            OpenApiOperation operation = new()
            {
                Parameters = [new OpenApiParameter { Name = "filters" }],
                Responses = new OpenApiResponses
                {
                    ["200"] = new OpenApiResponse
                    {
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            [ApplicationJson] = new OpenApiMediaType()
                        }
                    }
                }
            };

            OpenApiPathItem pathItem = new()
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>
                {
                    [HttpMethod.Get] = operation
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
            OpenApiMediaType jsonMediaType = operation.Responses["200"].Content![ApplicationJson];
            using (Assert.EnterMultipleScope())
            {
                Assert.That(operation.Description, Is.Null);
                Assert.That(jsonMediaType.Schema, Is.Null);
                Assert.That(jsonMediaType.Example, Is.Null);
            }
        }
    }
}
