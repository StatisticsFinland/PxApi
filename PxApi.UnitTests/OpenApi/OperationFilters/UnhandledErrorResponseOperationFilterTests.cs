using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;
using Moq;
using PxApi.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace PxApi.UnitTests.OpenApi.OperationFilters
{
    [TestFixture]
    public class UnhandledErrorResponseOperationFilterTests
    {
        private UnhandledErrorResponseOperationFilter _filter = null!;
        private OperationFilterContext _context = null!;

        private static OperationFilterContext CreateContext()
        {
            ApiDescription apiDescription = new();
            ISchemaGenerator schemaGenerator = Mock.Of<ISchemaGenerator>();
            SchemaRepository schemaRepository = new();
            OpenApiDocument document = new();
            MethodInfo methodInfo = typeof(UnhandledErrorResponseOperationFilterTests).GetMethod(nameof(CreateContext))!;
            OperationFilterContext context = new(apiDescription, schemaGenerator, schemaRepository, document, methodInfo);
            return context;
        }

        [SetUp]
        public void Setup()
        {
            _filter = new UnhandledErrorResponseOperationFilter();
            _context = CreateContext();
        }

        [Test]
        public void Apply_ShouldAdd500Response_WhenNotPresent()
        {
            // Arrange
            OpenApiOperation operation = new()
            {
                Responses = []
            };

            // Act
            _filter.Apply(operation, _context);

            // Assert
            Assert.That(operation.Responses.ContainsKey("500"), Is.True);
        }

        [Test]
        public void Apply_ShouldNotAdd500Response_WhenAlreadyPresent()
        {
            // Arrange
            const string existingDescription = "Custom error response";
            OpenApiOperation operation = new()
            {
                Responses = new OpenApiResponses
                {
                    ["500"] = new OpenApiResponse { Description = existingDescription }
                }
            };

            // Act
            _filter.Apply(operation, _context);

            // Assert
            Assert.That(operation.Responses["500"].Description, Is.EqualTo(existingDescription));
        }

        [Test]
        public void Apply_ShouldNotThrow_WhenResponsesIsNull()
        {
            // Arrange
            OpenApiOperation operation = new()
            {
                Responses = null!
            };

            // Act & Assert
            Assert.DoesNotThrow(() => _filter.Apply(operation, _context));
        }

        [Test]
        public void Apply_ShouldSetCorrectDescription_On500Response()
        {
            // Arrange
            OpenApiOperation operation = new()
            {
                Responses = []
            };

            // Act
            _filter.Apply(operation, _context);

            // Assert
            Assert.That(operation.Responses["500"].Description, Is.EqualTo("Unexpected server error."));
        }

        [Test]
        public void Apply_ShouldIncludeJsonContent_In500Response()
        {
            // Arrange
            OpenApiOperation operation = new()
            {
                Responses = []
            };

            // Act
            _filter.Apply(operation, _context);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(operation.Responses["500"].Content, Is.Not.Null);
                Assert.That(operation.Responses["500"].Content!.ContainsKey("application/json"), Is.True);
            }
        }

        [Test]
        public void Apply_ShouldReferenceProblemDetailsSchema_In500Response()
        {
            // Arrange
            OpenApiOperation operation = new()
            {
                Responses = []
            };

            // Act
            _filter.Apply(operation, _context);

            // Assert
            OpenApiMediaType? mediaType = operation.Responses["500"].Content!["application/json"];
            using (Assert.EnterMultipleScope())
            {
                Assert.That(mediaType, Is.Not.Null);
                Assert.That(mediaType.Schema, Is.InstanceOf<OpenApiSchemaReference>());
                OpenApiSchemaReference schemaRef = (OpenApiSchemaReference)mediaType.Schema!;
                Assert.That(schemaRef!.Reference.Id, Is.EqualTo("ProblemDetails"));
            }
        }

        [Test]
        public void Apply_ShouldNotModifyOtherResponses_WhenAdding500()
        {
            // Arrange
            const string okDescription = "Success response";
            OpenApiOperation operation = new()
            {
                Responses = new OpenApiResponses
                {
                    ["200"] = new OpenApiResponse { Description = okDescription },
                    ["404"] = new OpenApiResponse { Description = "Not found" }
                }
            };

            // Act
            _filter.Apply(operation, _context);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(operation.Responses, Has.Count.EqualTo(3));
                Assert.That(operation.Responses["200"].Description, Is.EqualTo(okDescription));
                Assert.That(operation.Responses.ContainsKey("404"), Is.True);
                Assert.That(operation.Responses.ContainsKey("500"), Is.True);
            }
        }
    }
}
