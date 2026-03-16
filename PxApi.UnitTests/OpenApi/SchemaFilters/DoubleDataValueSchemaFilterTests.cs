using Microsoft.OpenApi;
using Moq;
using Px.Utils.Models.Data.DataValue;
using PxApi.OpenApi.SchemaFilters;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PxApi.UnitTests.OpenApi.SchemaFilters
{
    [TestFixture]
    public class DoubleDataValueSchemaFilterTests
    {
        private static SchemaFilterContext CreateContext(Type targetType)
        {
            ISchemaGenerator schemaGenerator = Mock.Of<ISchemaGenerator>();
            SchemaRepository repository = new();
            SchemaFilterContext context = new(targetType, schemaGenerator, repository);
            return context;
        }

        [Test]
        public void Apply_ForSingleDoubleDataValue_SetsNumberSchemaAndClearsComposition()
        {
            // Arrange
            OpenApiSchema schema = new()
            {
                Properties = new Dictionary<string, IOpenApiSchema> { { "dummy", new OpenApiSchema() } },
                AllOf = [new OpenApiSchema()],
                OneOf = [new OpenApiSchema()],
                AnyOf = [new OpenApiSchema()],
                AdditionalProperties = new OpenApiSchema()
            };
            DoubleDataValueSchemaFilter filter = new();
            SchemaFilterContext context = CreateContext(typeof(DoubleDataValue));

            // Act
            filter.Apply(schema, context);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(schema.Type, Is.EqualTo(JsonSchemaType.Number | JsonSchemaType.Null));
                Assert.That(schema.Format, Is.EqualTo("double"));
                Assert.That(schema.Properties, Is.Not.Null);
                Assert.That(schema.Properties, Has.Count.Zero);
                Assert.That(schema.AllOf, Has.Count.Zero);
                Assert.That(schema.OneOf, Has.Count.Zero);
                Assert.That(schema.AnyOf, Has.Count.Zero);
                Assert.That(schema.AdditionalProperties, Is.Null);
                Assert.That(schema.Items, Is.Null);
            }
        }

        [Test]
        public void Apply_ForDoubleDataValueArray_SetsArraySchemaWithNumberItemsAndClearsComposition()
        {
            // Arrange
            OpenApiSchema schema = new()
            {
                Properties = new Dictionary<string, IOpenApiSchema> { { "dummy", new OpenApiSchema() } },
                AllOf = [new OpenApiSchema()],
                OneOf = [new OpenApiSchema()],
                AnyOf = [new OpenApiSchema()]
            };
            DoubleDataValueSchemaFilter filter = new();
            SchemaFilterContext context = CreateContext(typeof(DoubleDataValue[]));

            // Act
            filter.Apply(schema, context);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(schema.Type, Is.EqualTo(JsonSchemaType.Array));
                Assert.That(schema.Items, Is.Not.Null);
                OpenApiSchema itemsSchema = (OpenApiSchema)schema.Items!;
                Assert.That(itemsSchema!.Type, Is.EqualTo(JsonSchemaType.Number | JsonSchemaType.Null));
                Assert.That(itemsSchema.Format, Is.EqualTo("double"));
                Assert.That(schema.Properties, Has.Count.Zero);
                Assert.That(schema.AllOf, Has.Count.Zero);
                Assert.That(schema.OneOf, Has.Count.Zero);
                Assert.That(schema.AnyOf, Has.Count.Zero);
            }
        }
    }
}
