using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using Moq;
using PxApi.OpenApi.DocumentFilters;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PxApi.UnitTests.DocumentFilters
{
    [TestFixture]
    public class FilterSubclassDocumentFilterTests
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
        public void Apply_RemovesFilterSubclassSchemas()
        {
            // Arrange
            OpenApiDocument document = new()
            {
                Components = new OpenApiComponents
                {
                    Schemas = new Dictionary<string, OpenApiSchema>
                    {
                        { "CodeFilter", new OpenApiSchema() },
                        { "MyFromFilterExtra", new OpenApiSchema() },
                        { "prefixLastFilter", new OpenApiSchema() },
                        { "tofilter", new OpenApiSchema() }, // lower-case variant
                        { "Filter", new OpenApiSchema() }, // base type should remain
                        { "Unrelated", new OpenApiSchema() }
                    }
                }
            };
            FilterSubclassDocumentFilter filter = new();
            DocumentFilterContext context = CreateEmptyContext();

            // Act
            filter.Apply(document, context);

            // Assert
            ICollection<string> remainingKeys = document.Components.Schemas.Keys;
            Assert.Multiple(() =>
            {
                Assert.That(remainingKeys.Contains("CodeFilter"), Is.False);
                Assert.That(remainingKeys.Contains("MyFromFilterExtra"), Is.False);
                Assert.That(remainingKeys.Contains("prefixLastFilter"), Is.False);
                Assert.That(remainingKeys.Contains("tofilter"), Is.False);
                Assert.That(remainingKeys.Contains("Filter"), Is.True);
                Assert.That(remainingKeys.Contains("Unrelated"), Is.True);
            });
        }

        [Test]
        public void Apply_NoComponents_NoException()
        {
            // Arrange
            OpenApiDocument document = new();
            FilterSubclassDocumentFilter filter = new();
            DocumentFilterContext context = CreateEmptyContext();

            // Act / Assert
            Assert.That(() => filter.Apply(document, context), Throws.Nothing);
        }
    }
}
