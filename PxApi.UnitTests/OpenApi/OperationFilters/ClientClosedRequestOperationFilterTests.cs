using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;
using Moq;
using PxApi.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace PxApi.UnitTests.OpenApi.OperationFilters
{
    [TestFixture]
    public class ClientClosedRequestOperationFilterTests
    {
        private ClientClosedRequestOperationFilter _filter = null!;

        [SetUp]
        public void Setup()
        {
            _filter = new ClientClosedRequestOperationFilter();
        }

        private static OperationFilterContext CreateContext(MethodInfo methodInfo)
        {
            ApiDescription apiDescription = new();
            ISchemaGenerator schemaGenerator = Mock.Of<ISchemaGenerator>();
            SchemaRepository schemaRepository = new();
            OpenApiDocument document = new();
            OperationFilterContext context = new(apiDescription, schemaGenerator, schemaRepository, document, methodInfo);
            return context;
        }

        // Test helper methods with and without CancellationToken
#pragma warning disable S1172 // Remove unused parameter — parameters are required to produce the correct MethodInfo signature
        private static Task MethodWithCancellationToken(string _, CancellationToken __) => Task.CompletedTask;
        private static Task MethodWithoutCancellationToken(string _) => Task.CompletedTask;
#pragma warning restore S1172

        [Test]
        public void Apply_WithCancellationTokenParameter_Adds499Response()
        {
            // Arrange
            MethodInfo methodInfo = typeof(ClientClosedRequestOperationFilterTests)
                .GetMethod(nameof(MethodWithCancellationToken), BindingFlags.Static | BindingFlags.NonPublic)!;
            OperationFilterContext context = CreateContext(methodInfo);
            OpenApiOperation operation = new() { Responses = [] };

            // Act
            _filter.Apply(operation, context);

            // Assert
            Assert.That(operation.Responses.ContainsKey("499"), Is.True);
        }

        [Test]
        public void Apply_WithoutCancellationTokenParameter_DoesNotAdd499Response()
        {
            // Arrange
            MethodInfo methodInfo = typeof(ClientClosedRequestOperationFilterTests)
                .GetMethod(nameof(MethodWithoutCancellationToken), BindingFlags.Static | BindingFlags.NonPublic)!;
            OperationFilterContext context = CreateContext(methodInfo);
            OpenApiOperation operation = new() { Responses = [] };

            // Act
            _filter.Apply(operation, context);

            // Assert
            Assert.That(operation.Responses.ContainsKey("499"), Is.False);
        }

        [Test]
        public void Apply_WithCancellationToken_SetsCorrectDescription()
        {
            // Arrange
            MethodInfo methodInfo = typeof(ClientClosedRequestOperationFilterTests)
                .GetMethod(nameof(MethodWithCancellationToken), BindingFlags.Static | BindingFlags.NonPublic)!;
            OperationFilterContext context = CreateContext(methodInfo);
            OpenApiOperation operation = new() { Responses = [] };

            // Act
            _filter.Apply(operation, context);

            // Assert
            Assert.That(operation.Responses["499"].Description,
                Is.EqualTo("Client closed the request before the server could respond."));
        }

        [Test]
        public void Apply_When499AlreadyPresent_DoesNotOverwrite()
        {
            // Arrange
            const string existingDescription = "Custom 499 description";
            MethodInfo methodInfo = typeof(ClientClosedRequestOperationFilterTests)
                .GetMethod(nameof(MethodWithCancellationToken), BindingFlags.Static | BindingFlags.NonPublic)!;
            OperationFilterContext context = CreateContext(methodInfo);
            OpenApiOperation operation = new()
            {
                Responses = new OpenApiResponses
                {
                    ["499"] = new OpenApiResponse { Description = existingDescription }
                }
            };

            // Act
            _filter.Apply(operation, context);

            // Assert
            Assert.That(operation.Responses["499"].Description, Is.EqualTo(existingDescription));
        }

        [Test]
        public void Apply_WithNullResponses_DoesNotThrow()
        {
            // Arrange
            MethodInfo methodInfo = typeof(ClientClosedRequestOperationFilterTests)
                .GetMethod(nameof(MethodWithCancellationToken), BindingFlags.Static | BindingFlags.NonPublic)!;
            OperationFilterContext context = CreateContext(methodInfo);
            OpenApiOperation operation = new() { Responses = null! };

            // Act & Assert
            Assert.DoesNotThrow(() => _filter.Apply(operation, context));
        }

        [Test]
        public void Apply_WithCancellationToken_DoesNotModifyOtherResponses()
        {
            // Arrange
            MethodInfo methodInfo = typeof(ClientClosedRequestOperationFilterTests)
                .GetMethod(nameof(MethodWithCancellationToken), BindingFlags.Static | BindingFlags.NonPublic)!;
            OperationFilterContext context = CreateContext(methodInfo);
            OpenApiOperation operation = new()
            {
                Responses = new OpenApiResponses
                {
                    ["200"] = new OpenApiResponse { Description = "OK" },
                    ["500"] = new OpenApiResponse { Description = "Error" }
                }
            };

            // Act
            _filter.Apply(operation, context);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(operation.Responses, Has.Count.EqualTo(3));
                Assert.That(operation.Responses["200"].Description, Is.EqualTo("OK"));
                Assert.That(operation.Responses["500"].Description, Is.EqualTo("Error"));
                Assert.That(operation.Responses.ContainsKey("499"), Is.True);
            }
        }
    }
}
