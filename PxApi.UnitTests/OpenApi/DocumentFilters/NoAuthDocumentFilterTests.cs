using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;
using Moq;
using PxApi.OpenApi.DocumentFilters;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PxApi.UnitTests.OpenApi.DocumentFilters
{
    [TestFixture]
    public class NoAuthDocumentFilterTests
    {
        private NoAuthDocumentFilter _filter = null!;
        private DocumentFilterContext _context = null!;

        private static DocumentFilterContext CreateEmptyContext()
        {
            ISchemaGenerator schemaGenerator = Mock.Of<ISchemaGenerator>();
            SchemaRepository repository = new();
            List<ApiDescription> apiDescriptions = [];
            DocumentFilterContext context = new(apiDescriptions, schemaGenerator, repository);
            return context;
        }

        [SetUp]
        public void Setup()
        {
            _filter = new NoAuthDocumentFilter();
            _context = CreateEmptyContext();
        }

        [Test]
        public void Apply_ShouldRemoveSecuritySchemes_WhenSecuritySchemesExist()
        {
            // Arrange
            OpenApiDocument document = new()
            {
                Components = new OpenApiComponents
                {
                    SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
                    {
                        ["Bearer"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.Http }
                    }
                },
                Info = new OpenApiInfo { Description = "Test API" }
            };

            // Act
            _filter.Apply(document, _context);

            // Assert
            Assert.That(document.Components.SecuritySchemes, Is.Empty);
        }

        [Test]
        public void Apply_ShouldNotThrow_WhenComponentsIsNull()
        {
            // Arrange
            OpenApiDocument document = new()
            {
                Components = null,
                Info = new OpenApiInfo { Description = "Test API" }
            };

            // Act & Assert
            Assert.DoesNotThrow(() => _filter.Apply(document, _context));
        }

        [Test]
        public void Apply_ShouldNotThrow_WhenSecuritySchemesIsNull()
        {
            // Arrange
            OpenApiDocument document = new()
            {
                Components = new OpenApiComponents
                {
                    SecuritySchemes = null
                },
                Info = new OpenApiInfo { Description = "Test API" }
            };

            // Act & Assert
            Assert.DoesNotThrow(() => _filter.Apply(document, _context));
        }

        [Test]
        public void Apply_ShouldAddNoAuthNote_WhenDescriptionIsNull()
        {
            // Arrange
            OpenApiDocument document = new()
            {
                Info = new OpenApiInfo { Description = null }
            };

            // Act
            _filter.Apply(document, _context);

            // Assert
            Assert.That(document.Info.Description, Is.EqualTo("Authentication: This public API requires no authentication."));
        }

        [Test]
        public void Apply_ShouldAppendNoAuthNote_WhenDescriptionExists()
        {
            // Arrange
            const string existingDescription = "This is a test API. ";
            OpenApiDocument document = new()
            {
                Info = new OpenApiInfo { Description = existingDescription }
            };

            // Act
            _filter.Apply(document, _context);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(document.Info.Description, Does.StartWith(existingDescription));
                Assert.That(document.Info.Description, Does.Contain("requires no authentication"));
            }
        }

        [Test]
        public void Apply_ShouldNotAppendNoAuthNoteTwice_WhenAlreadyPresent()
        {
            // Arrange
            const string descriptionWithAuth = "This is a test API. Authentication: This public API requires no authentication.";
            OpenApiDocument document = new()
            {
                Info = new OpenApiInfo { Description = descriptionWithAuth }
            };

            // Act
            _filter.Apply(document, _context);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(document.Info.Description, Is.EqualTo(descriptionWithAuth));
                Assert.That(document.Info.Description.Split("requires no authentication").Length - 1, Is.EqualTo(1));
            }
        }

        [Test]
        public void Apply_ShouldNotThrow_WhenInfoIsNull()
        {
            // Arrange
            OpenApiDocument document = new()
            {
                Info = null!
            };

            // Act & Assert
            Assert.DoesNotThrow(() => _filter.Apply(document, _context));
        }

        [Test]
        public void Apply_ShouldHandleBothSecuritySchemesAndDescription()
        {
            // Arrange
            const string existingDescription = "Test API";
            OpenApiDocument document = new()
            {
                Components = new OpenApiComponents
                {
                    SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
                    {
                        ["ApiKey"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.ApiKey },
                        ["Bearer"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.Http }
                    }
                },
                Info = new OpenApiInfo { Description = existingDescription }
            };

            // Act
            _filter.Apply(document, _context);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(document.Components.SecuritySchemes, Is.Empty);
                Assert.That(document.Info.Description, Does.StartWith(existingDescription));
                Assert.That(document.Info.Description, Does.Contain("requires no authentication"));
            }
        }
    }
}
