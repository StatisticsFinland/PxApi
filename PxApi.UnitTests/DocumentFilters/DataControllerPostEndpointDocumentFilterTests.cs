using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;
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
            List<ApiDescription> apiDescriptions = [];
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
                    },
                    ["400"] = new OpenApiResponse
                    {
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            ["application/json"] = new OpenApiMediaType(),
                            ["text/csv"] = new OpenApiMediaType()
                        }
                    },
                    ["406"] = new OpenApiResponse
                    {
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            ["application/json"] = new OpenApiMediaType(),
                            ["text/csv"] = new OpenApiMediaType()
                        }
                    },
                    ["500"] = new OpenApiResponse
                    {
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            ["application/json"] = new OpenApiMediaType(),
                            ["text/csv"] = new OpenApiMediaType()
                        }
                    }
                }
            };

            OpenApiPathItem pathItem = new()
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>
                {
                    [HttpMethod.Post] = operation
                }
            };

            OpenApiSchema filterSchema = new();
            OpenApiDocument document = new()
            {
                Paths = new OpenApiPaths { ["/data/{database}/{table}"] = pathItem },
                Components = new OpenApiComponents
                {
                    Schemas = new Dictionary<string, IOpenApiSchema>
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
            OpenApiResponse response200 = (OpenApiResponse)operation.Responses["200"];
            OpenApiMediaType jsonMediaType = response200.Content["application/json"];
            using (Assert.EnterMultipleScope())
            {
                Assert.That(jsonMediaType.Schema, Is.Not.Null);
                Assert.That(jsonMediaType.Schema, Is.TypeOf<OpenApiSchemaReference>());
                Assert.That(jsonMediaType.Examples, Is.Not.Null);
                Assert.That(jsonMediaType.Examples.Count, Is.EqualTo(1));
                Assert.That(jsonMediaType.Examples.ContainsKey("default"), Is.True);
                Assert.That(response200.Description, Does.Contain("JSON-stat2.0"));
            }

            OpenApiMediaType csvMediaType = response200.Content["text/csv"];
            OpenApiSchema csvSchema = (OpenApiSchema)csvMediaType.Schema;
            Assert.That(csvSchema.Description, Does.Contain("CSV dataset"));

            // Accept header note and lang parameter
            using (Assert.EnterMultipleScope())
            {
                Assert.That(operation.Description, Does.Contain("Accept header options:"));
                OpenApiParameter? langParam = operation.Parameters.FirstOrDefault(p => p.Name == "lang") as OpenApiParameter;
                Assert.That(langParam, Is.Not.Null);
                Assert.That(langParam!.Description, Does.Contain("Optional language code"));
            }

            // Filter schema description
            Assert.That(filterSchema.Description, Does.Contain("Filter object"));

            // Error responses: text/csv removed, 406 has no content
            OpenApiResponse response400 = (OpenApiResponse)operation.Responses["400"];
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response400.Content.ContainsKey("text/csv"), Is.False);
                Assert.That(response400.Content["application/json"], Is.Not.Null);
            }

            OpenApiResponse response406 = (OpenApiResponse)operation.Responses["406"];
            Assert.That(response406.Content, Is.Empty);

            OpenApiResponse response500 = (OpenApiResponse)operation.Responses["500"];
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response500.Content.ContainsKey("text/csv"), Is.False);
                Assert.That(response500.Content["application/json"], Is.Not.Null);
            }
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
                Operations = new Dictionary<HttpMethod, OpenApiOperation>
                {
                    [HttpMethod.Post] = operation
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
            using (Assert.EnterMultipleScope())
            {
                Assert.That(rbMediaType.Examples, Is.Null.Or.Empty);
                Assert.That(operation.Description, Is.Null);
            }
        }
    }
}
