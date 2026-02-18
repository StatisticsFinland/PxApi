using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using Moq;
using PxApi.OpenApi.DocumentFilters;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PxApi.UnitTests.DocumentFilters
{
    [TestFixture]
    public class ProblemDetailsDocumentFilterTests
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
        public void Apply_DocumentWithProblemDetails_RemovesSchema()
        {
            // Arrange
            OpenApiDocument document = new()
            {
                Paths = [],
                Components = new OpenApiComponents
                {
                    Schemas = new Dictionary<string, OpenApiSchema>
                    {
                        ["ProblemDetails"] = new OpenApiSchema { Type = "object" },
                        ["JsonStat2"] = new OpenApiSchema { Type = "object" }
                    }
                }
            };

            ProblemDetailsDocumentFilter filter = new();
            DocumentFilterContext context = CreateEmptyContext();

            // Act
            filter.Apply(document, context);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(document.Components.Schemas.ContainsKey("ProblemDetails"), Is.False);
                Assert.That(document.Components.Schemas.ContainsKey("JsonStat2"), Is.True);
            }
        }

        [Test]
        public void Apply_DocumentWithoutProblemDetails_DoesNotThrow()
        {
            // Arrange
            OpenApiDocument document = new()
            {
                Paths = [],
                Components = new OpenApiComponents
                {
                    Schemas = new Dictionary<string, OpenApiSchema>
                    {
                        ["JsonStat2"] = new OpenApiSchema { Type = "object" }
                    }
                }
            };

            ProblemDetailsDocumentFilter filter = new();
            DocumentFilterContext context = CreateEmptyContext();

            // Act & Assert
            Assert.DoesNotThrow(() => filter.Apply(document, context));
            Assert.That(document.Components.Schemas, Has.Count.EqualTo(1));
        }

        [Test]
        public void Apply_DocumentWithNullComponents_DoesNotThrow()
        {
            // Arrange
            OpenApiDocument document = new()
            {
                Paths = []
            };

            ProblemDetailsDocumentFilter filter = new();
            DocumentFilterContext context = CreateEmptyContext();

            // Act & Assert
            Assert.DoesNotThrow(() => filter.Apply(document, context));
        }
    }
}
