using PxApi.Utilities;

namespace PxApi.UnitTests.UtilitiesTests
{
    [TestFixture]
    internal static class UriExtensionsTests
    {
        [Test]
        public static void AddRelativePath_ShouldAddPathSegments()
        {
            // Arrange
            Uri baseUrl = new("https://example.com/base");
            string[] relativePath = ["path1", "path2"];

            // Act
            Uri result = baseUrl.AddRelativePath(relativePath);

            // Assert
            Assert.That(result.ToString(), Is.EqualTo("https://example.com/base/path1/path2"));
        }

        [Test]
        public static void AddRelativePath_ShouldPreserveQueryParameters()
        {
            // Arrange
            Uri baseUrl = new("https://example.com/base?param1=value1&param2=value2");
            string[] relativePath = ["path1", "path2"];

            // Act
            Uri result = baseUrl.AddRelativePath(relativePath);

            // Assert
            Assert.That(result.ToString(), Is.EqualTo("https://example.com/base/path1/path2?param1=value1&param2=value2"));
        }

        [Test]
        public static void AddQueryParameters_ShouldAddQueryParameters()
        {
            // Arrange
            Uri baseUrl = new("https://example.com/base");
            var queryParams = new (string Key, object Value)[] { ("param1", "value1"), ("param2", 123), ("param3", true) };

            // Act
            Uri result = baseUrl.AddQueryParameters(queryParams);

            // Assert
            Assert.That(result.ToString(), Is.EqualTo("https://example.com/base?param1=value1&param2=123&param3=true"));
        }

        [Test]
        public static void DropQueryParameters_ShouldRemoveSpecifiedParameter()
        {
            // Arrange
            Uri baseUrl = new("https://example.com/base?param1=value1&param2=value2");
            string paramName = "param1";

            // Act
            Uri result = baseUrl.DropQueryParameters(paramName);

            // Assert
            Assert.That(result.ToString(), Is.EqualTo("https://example.com/base?param2=value2"));
        }

        [Test]
        public static void DropQueryParameters_ShouldHandleNonExistentParameter()
        {
            // Arrange
            Uri baseUrl = new("https://example.com/base?param1=value1&param2=value2");
            string paramName = "param3";

            // Act
            Uri result = baseUrl.DropQueryParameters(paramName);

            // Assert
            Assert.That(result.ToString(), Is.EqualTo("https://example.com/base?param1=value1&param2=value2"));
        }
    }
}
