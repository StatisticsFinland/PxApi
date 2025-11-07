using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using Moq;
using PxApi.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace PxApi.UnitTests.OperationFilters
{
    [TestFixture]
    public class OperationIdOperationFilterTests
    {
        private static OperationFilterContext CreateContext(MethodInfo methodInfo)
        {
            ApiDescription apiDescription = new();
            ISchemaGenerator schemaGenerator = Mock.Of<ISchemaGenerator>();
            SchemaRepository schemaRepository = new();
            OperationFilterContext context = new(apiDescription, schemaGenerator, schemaRepository, methodInfo);
            return context;
        }

        private sealed class TestController
        {
            [OperationId("MyOp")]
#pragma warning disable CA1822 // Mark members as static - needs to be instance method for testing
            public void DecoratedAction() { }

            public void UndecoratedAction() { }
#pragma warning restore CA1822
        }

        [Test]
        public void Apply_WithOperationIdAttribute_SetsOperationId()
        {
            // Arrange
            OperationIdOperationFilter filter = new();
            OpenApiOperation operation = new();
            MethodInfo methodInfo = typeof(TestController).GetMethod(nameof(TestController.DecoratedAction))!;
            OperationFilterContext context = CreateContext(methodInfo);

            // Act
            filter.Apply(operation, context);

            // Assert
            Assert.That(operation.OperationId, Is.EqualTo("MyOp"));
        }

        [Test]
        public void Apply_WithoutOperationIdAttribute_DoesNotModifyOperationId()
        {
            // Arrange
            OperationIdOperationFilter filter = new();
            OpenApiOperation operation = new()
            {
                OperationId = "OriginalId"
            };
            MethodInfo methodInfo = typeof(TestController).GetMethod(nameof(TestController.UndecoratedAction))!;
            OperationFilterContext context = CreateContext(methodInfo);

            // Act
            filter.Apply(operation, context);

            // Assert
            Assert.That(operation.OperationId, Is.EqualTo("OriginalId"));
        }

        [Test]
        public void OperationIdAttribute_Construct_TrimmedIdStored()
        {
            // Arrange / Act
            OperationIdAttribute attribute = new(" TrimMe ");

            // Assert
            Assert.That(attribute.Id, Is.EqualTo("TrimMe"));
        }

        [Test]
        public void OperationIdAttribute_Construct_NullOrWhitespace_Throws()
        {
            // Arrange
            Assert.Multiple(() =>
            {
                Assert.That(() => new OperationIdAttribute(""), Throws.ArgumentException);
                Assert.That(() => new OperationIdAttribute(" \t"), Throws.ArgumentException);
            });
        }
    }
}
