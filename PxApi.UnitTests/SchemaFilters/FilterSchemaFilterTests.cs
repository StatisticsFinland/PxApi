using Microsoft.OpenApi;
using Moq;
using PxApi.Models.QueryFilters;
using PxApi.OpenApi.SchemaFilters;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json.Nodes;

namespace PxApi.UnitTests.SchemaFilters
{
    [TestFixture]
    public class FilterSchemaFilterTests
    {
        private static SchemaFilterContext CreateContext(Type targetType)
        {
            ISchemaGenerator schemaGenerator = Mock.Of<ISchemaGenerator>();
            SchemaRepository repository = new();
            SchemaFilterContext context = new(targetType, schemaGenerator, repository);
            return context;
        }
        private static readonly string[] expected0 = ["Code", "From", "To", "First", "Last"];
        private static readonly string[] expected1 = ["A01", "A02", "*MANUF*"];
        private static readonly string[] expected2 = ["1", "2"];

        [Test]
        public void Apply_ForFilterType_DefinesPolymorphicShapeAndExample()
        {
            // Arrange
            OpenApiSchema schema = new()
            {
                Properties = new Dictionary<string, IOpenApiSchema> { { "dummy", new OpenApiSchema() } },
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
                Assert.That(schema.Type, Is.EqualTo(JsonSchemaType.Object));
                Assert.That(schema.Required, Is.Not.Null);
                Assert.That(schema.Required.Contains("type"), Is.True);
                Assert.That(schema.Properties, Is.Not.Null);
                Assert.That(schema.Properties, Has.Count.EqualTo(2));
                Assert.That(schema.Properties.ContainsKey("type"), Is.True);
                Assert.That(schema.Properties.ContainsKey("query"), Is.True);
                Assert.That(schema.AllOf?.Count, Is.EqualTo(0));
                Assert.That(schema.OneOf?.Count, Is.EqualTo(0));
                Assert.That(schema.AnyOf?.Count, Is.EqualTo(0));

                OpenApiSchema typeProperty = (OpenApiSchema)schema.Properties["type"];
                Assert.That(typeProperty.Type, Is.EqualTo(JsonSchemaType.String));
                IList<JsonNode?> enumValues = typeProperty.Enum;
                Assert.That(enumValues, Is.Not.Null);
                List<string> enumStrings = [.. enumValues.Select(v => v!.GetValue<string>())];
                Assert.That(enumStrings, Has.Count.EqualTo(5));
                Assert.That(enumStrings, Is.EquivalentTo(expected0));

                OpenApiSchema queryProperty = (OpenApiSchema)schema.Properties["query"];
                Assert.That(queryProperty.OneOf, Is.Not.Null);
                Assert.That(queryProperty.OneOf, Has.Count.EqualTo(3));
                Assert.That(queryProperty.OneOf.Any(s => ((OpenApiSchema)s).Type == JsonSchemaType.Array), Is.True);
                Assert.That(queryProperty.OneOf.Any(s => ((OpenApiSchema)s).Type == JsonSchemaType.String), Is.True);
                Assert.That(queryProperty.OneOf.Any(s => ((OpenApiSchema)s).Type == JsonSchemaType.Integer), Is.True);

                Assert.That(schema.Example, Is.TypeOf<JsonObject>());
                JsonObject example = (JsonObject)schema.Example;
                Assert.That(example.ContainsKey("type"), Is.True);
                Assert.That(example["type"]?.GetValue<string>(), Is.EqualTo("Code"));
                Assert.That(example.ContainsKey("query"), Is.True);
                Assert.That(example["query"], Is.TypeOf<JsonArray>());
                JsonArray queryArray = (JsonArray)example["query"]!;
                List<string> queryValues = [.. queryArray.Select(v => v!.GetValue<string>())];
                Assert.That(queryValues, Is.EquivalentTo(expected1));
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
                Assert.That(schema.Type, Is.EqualTo(JsonSchemaType.Object));
                Assert.That(schema.AdditionalProperties, Is.Not.Null);
                Assert.That(schema.AdditionalProperties, Is.TypeOf<OpenApiSchemaReference>());
                Assert.That(schema.Description, Is.EqualTo("Dictionary mapping dimension codes to filter objects (one per dimension)."));

                Assert.That(schema.Example, Is.TypeOf<JsonObject>());
                JsonObject example = (JsonObject)schema.Example;
                Assert.That(example.ContainsKey("gender"), Is.True);
                Assert.That(example.ContainsKey("year"), Is.True);
                Assert.That(example.ContainsKey("region"), Is.True);

                JsonObject gender = (JsonObject)example["gender"]!;
                Assert.That(gender["type"]?.GetValue<string>(), Is.EqualTo("Code"));
                Assert.That(gender["query"], Is.TypeOf<JsonArray>());
                JsonArray genderQuery = (JsonArray)gender["query"]!;
                List<string> genderValues = [.. genderQuery.Select(v => v!.GetValue<string>())];
                Assert.That(genderValues, Is.EquivalentTo(expected2));

                JsonObject year = (JsonObject)example["year"]!;
                Assert.That(year["type"]?.GetValue<string>(), Is.EqualTo("From"));
                Assert.That(year["query"]?.GetValue<string>(), Is.EqualTo("2020"));

                JsonObject region = (JsonObject)example["region"]!;
                Assert.That(region["type"]?.GetValue<string>(), Is.EqualTo("First"));
                Assert.That(region["query"]?.GetValue<int>(), Is.EqualTo(5));
            });
        }
    }
}
