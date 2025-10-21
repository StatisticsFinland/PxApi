using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Moq;
using PxApi.Models.QueryFilters;
using PxApi.OpenApi.SchemaFilters;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PxApi.UnitTests.SchemaFilters
{
    [TestFixture]
    public class FilterSchemaFilterTests
    {
        private static SchemaFilterContext CreateContext(Type targetType)
        {
            ISchemaGenerator schemaGenerator = Mock.Of<ISchemaGenerator>();
            SchemaRepository repository = new();
            // Correct constructor order: (Type type, ISchemaGenerator schemaGenerator, SchemaRepository schemaRepository)
            SchemaFilterContext context = new(targetType, schemaGenerator, repository);
            return context;
        }

        [Test]
        public void Apply_ForFilterType_DefinesPolymorphicShapeAndExample()
        {
            // Arrange
            OpenApiSchema schema = new()
            {
                Properties = new Dictionary<string, OpenApiSchema> { { "dummy", new OpenApiSchema() } },
                AllOf = [new OpenApiSchema()],
                OneOf = [new OpenApiSchema()],
                AnyOf = [new OpenApiSchema()]
            };
            FilterSchemaFilter filter = new();
            SchemaFilterContext context = CreateContext(typeof(Filter));

            // Act
            filter.Apply(schema, context);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(schema.Type, Is.EqualTo("object"));
                Assert.That(schema.Required, Is.Not.Null);
                Assert.That(schema.Required.Contains("type"), Is.True);
                Assert.That(schema.Properties, Is.Not.Null);
                Assert.That(schema.Properties.Count, Is.EqualTo(2));
                Assert.That(schema.Properties.ContainsKey("type"), Is.True);
                Assert.That(schema.Properties.ContainsKey("query"), Is.True);
                Assert.That(schema.AllOf?.Count, Is.EqualTo(0));
                Assert.That(schema.OneOf?.Count, Is.EqualTo(0));
                Assert.That(schema.AnyOf?.Count, Is.EqualTo(0));

                OpenApiSchema typeProperty = schema.Properties["type"];
                Assert.That(typeProperty.Type, Is.EqualTo("string"));
                IList<IOpenApiAny> enumValues = typeProperty.Enum;
                Assert.That(enumValues, Is.Not.Null);
                List<string> enumStrings = enumValues.Select(v => ((OpenApiString)v).Value).ToList();
                Assert.That(enumStrings, Has.Count.EqualTo(5));
                Assert.That(enumStrings, Is.EquivalentTo(new[] { "Code", "From", "To", "First", "Last" }));

                OpenApiSchema queryProperty = schema.Properties["query"];
                Assert.That(queryProperty.OneOf, Is.Not.Null);
                Assert.That(queryProperty.OneOf, Has.Count.EqualTo(3));
                Assert.That(queryProperty.OneOf.Any(s => s.Type == "array"), Is.True);
                Assert.That(queryProperty.OneOf.Any(s => s.Type == "string"), Is.True);
                Assert.That(queryProperty.OneOf.Any(s => s.Type == "integer"), Is.True);

                Assert.That(schema.Example, Is.TypeOf<OpenApiObject>());
                OpenApiObject example = (OpenApiObject)schema.Example;
                Assert.That(example.ContainsKey("type"), Is.True);
                Assert.That(example["type"], Is.TypeOf<OpenApiString>());
                Assert.That(((OpenApiString)example["type"]).Value, Is.EqualTo("Code"));
                Assert.That(example.ContainsKey("query"), Is.True);
                Assert.That(example["query"], Is.TypeOf<OpenApiArray>());
                OpenApiArray exampleQuery = (OpenApiArray)example["query"];
                List<string> queryValues = exampleQuery.Select(v => ((OpenApiString)v).Value).ToList();
                Assert.That(queryValues, Is.EquivalentTo(new[] { "A01", "A02", "*MANUF*" }));
            });
        }

        [Test]
        public void Apply_ForFilterDictionary_DefinesAdditionalPropertiesAndExample()
        {
            // Arrange
            OpenApiSchema schema = new();
            FilterSchemaFilter filter = new();
            SchemaFilterContext context = CreateContext(typeof(Dictionary<string, Filter>));

            // Act
            filter.Apply(schema, context);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(schema.Type, Is.EqualTo("object"));
                Assert.That(schema.AdditionalProperties, Is.Not.Null);
                Assert.That(schema.AdditionalProperties.Reference, Is.Not.Null);
                Assert.That(schema.AdditionalProperties.Reference.Id, Is.EqualTo("Filter"));
                Assert.That(schema.Description, Is.EqualTo("Dictionary mapping dimension codes to filter objects (one per dimension)."));

                Assert.That(schema.Example, Is.TypeOf<OpenApiObject>());
                OpenApiObject example = (OpenApiObject)schema.Example;
                Assert.That(example.ContainsKey("gender"), Is.True);
                Assert.That(example.ContainsKey("year"), Is.True);
                Assert.That(example.ContainsKey("region"), Is.True);

                OpenApiObject gender = (OpenApiObject)example["gender"];
                Assert.That(((OpenApiString)gender["type"]).Value, Is.EqualTo("Code"));
                Assert.That(gender["query"], Is.TypeOf<OpenApiArray>());
                OpenApiArray genderQuery = (OpenApiArray)gender["query"];
                List<string> genderValues = genderQuery.Select(v => ((OpenApiString)v).Value).ToList();
                Assert.That(genderValues, Is.EquivalentTo(new[] { "1", "2" }));

                OpenApiObject year = (OpenApiObject)example["year"];
                Assert.That(((OpenApiString)year["type"]).Value, Is.EqualTo("From"));
                Assert.That(((OpenApiString)year["query"]).Value, Is.EqualTo("2020"));

                OpenApiObject region = (OpenApiObject)example["region"];
                Assert.That(((OpenApiString)region["type"]).Value, Is.EqualTo("First"));
                Assert.That(((OpenApiInteger)region["query"]).Value, Is.EqualTo(5));
            });
        }
    }
}
