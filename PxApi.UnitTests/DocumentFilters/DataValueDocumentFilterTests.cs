using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using Moq;
using PxApi.OpenApi.DocumentFilters;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PxApi.UnitTests.DocumentFilters
{
    [TestFixture]
    public class DataValueDocumentFilterTests
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
        public void Apply_RemovesMatchingSchemas()
        {
            // Arrange
            OpenApiDocument document = new()
            {
                Components = new OpenApiComponents
                {
                    Schemas = new Dictionary<string, OpenApiSchema> {
                        { "DoubleDataValue", new OpenApiSchema() },
                        { "SomePrefixDoubleDataValueSuffix", new OpenApiSchema() },
                        { "DataValueType", new OpenApiSchema() },
                        { "doubledatavalue", new OpenApiSchema() },
                        { "UnrelatedType", new OpenApiSchema() }
                    }
                }
            };
            DataValueDocumentFilter filter = new();
            DocumentFilterContext context = CreateEmptyContext();

            // Act
            filter.Apply(document, context);

            // Assert
            ICollection<string> remainingKeys = document.Components.Schemas.Keys;
            Assert.Multiple(() =>
            {
                Assert.That(remainingKeys.Contains("DoubleDataValue"), Is.False);
                Assert.That(remainingKeys.Contains("SomePrefixDoubleDataValueSuffix"), Is.False);
                Assert.That(remainingKeys.Contains("DataValueType"), Is.False);
                Assert.That(remainingKeys.Contains("doubledatavalue"), Is.False);
                Assert.That(remainingKeys.Contains("UnrelatedType"), Is.True);
                Assert.That(remainingKeys, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public void Apply_NoComponents_NoException()
        {
            // Arrange
            OpenApiDocument document = new();
            DataValueDocumentFilter filter = new();
            DocumentFilterContext context = CreateEmptyContext();

            // Act / Assert (should not throw)
            Assert.That(() => filter.Apply(document, context), Throws.Nothing);
        }
    }
}
