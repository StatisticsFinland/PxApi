using Microsoft.Net.Http.Headers;
using PxApi.Utilities;

namespace PxApi.UnitTests.UtilitiesTests
{
    [TestFixture]
    public class ContentNegotiationTests
    {
        private static readonly string[] SupportedMediaTypes = ["application/json", "text/csv"];

        [Test]
        public void GetBestMatch_EmptyAcceptHeader_ReturnsNull()
        {
            // Arrange
            IList<MediaTypeHeaderValue> acceptValues = [];

            // Act
            string? result = ContentNegotiation.GetBestMatch(acceptValues, SupportedMediaTypes);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void GetBestMatch_ExactMatch_ReturnsMatch()
        {
            // Arrange
            IList<MediaTypeHeaderValue> acceptValues = [new MediaTypeHeaderValue("application/json")];

            // Act
            string? result = ContentNegotiation.GetBestMatch(acceptValues, SupportedMediaTypes);

            // Assert
            Assert.That(result, Is.EqualTo("application/json"));
        }

        [Test]
        public void GetBestMatch_WildcardMatch_ReturnsFirstSupported()
        {
            // Arrange
            IList<MediaTypeHeaderValue> acceptValues = [new MediaTypeHeaderValue("*/*")];

            // Act
            string? result = ContentNegotiation.GetBestMatch(acceptValues, SupportedMediaTypes);

            // Assert
            Assert.That(result, Is.EqualTo("application/json")); // First in supportedMediaTypes array
        }

        [Test]
        public void GetBestMatch_TypeWildcardMatch_ReturnsMatchingType()
        {
            // Arrange
            IList<MediaTypeHeaderValue> acceptValues = [new MediaTypeHeaderValue("text/*")];

            // Act
            string? result = ContentNegotiation.GetBestMatch(acceptValues, SupportedMediaTypes);

            // Assert
            Assert.That(result, Is.EqualTo("text/csv"));
        }

        [Test]  
        public void GetBestMatch_QualityValues_ReturnsHighestQuality()
        {
            // Arrange - CSV has higher quality than JSON
            IList<MediaTypeHeaderValue> acceptValues = [
                new MediaTypeHeaderValue("application/json", 0.5),
                new MediaTypeHeaderValue("text/csv", 0.9)
            ];

            // Act
            string? result = ContentNegotiation.GetBestMatch(acceptValues, SupportedMediaTypes);

            // Assert
            Assert.That(result, Is.EqualTo("text/csv"));
        }

        [Test]
        public void GetBestMatch_EqualQualityValues_ReturnsFirstInSupportedOrder()
        {
            // Arrange - Both have same quality, should prefer order in supportedMediaTypes
            IList<MediaTypeHeaderValue> acceptValues = [
                new MediaTypeHeaderValue("text/csv", 0.8),
                new MediaTypeHeaderValue("application/json", 0.8)
            ];

            // Act
            string? result = ContentNegotiation.GetBestMatch(acceptValues, SupportedMediaTypes);

            // Assert
            Assert.That(result, Is.EqualTo("application/json")); // First in supportedMediaTypes array
        }

        [Test]
        public void GetBestMatch_NoQualitySpecified_DefaultsToOne()
        {
            // Arrange - No quality specified should default to 1.0
            IList<MediaTypeHeaderValue> acceptValues = [
                new MediaTypeHeaderValue("application/json"), // No quality = 1.0
                new MediaTypeHeaderValue("text/csv", 0.5)     // Explicit lower quality
            ];

            // Act
            string? result = ContentNegotiation.GetBestMatch(acceptValues, SupportedMediaTypes);

            // Assert
            Assert.That(result, Is.EqualTo("application/json"));
        }

        [Test]
        public void GetBestMatch_ComplexAcceptHeader_ReturnsCorrectMatch()
        {
            // Arrange - Simulates: Accept: application/json, application/xml;q=0.9, */*;q=0.1
            IList<MediaTypeHeaderValue> acceptValues = [
                new MediaTypeHeaderValue("application/json"),   // q=1.0 (default)
                new MediaTypeHeaderValue("application/xml", 0.9),
                new MediaTypeHeaderValue("*/*", 0.1)
            ];

            // Act
            string? result = ContentNegotiation.GetBestMatch(acceptValues, SupportedMediaTypes);

            // Assert
            Assert.That(result, Is.EqualTo("application/json")); // Highest quality match
        }

        [Test]
        public void GetBestMatch_NoSupportedMatch_ReturnsNull()
        {
            // Arrange
            IList<MediaTypeHeaderValue> acceptValues = [new MediaTypeHeaderValue("text/html")];

            // Act
            string? result = ContentNegotiation.GetBestMatch(acceptValues, SupportedMediaTypes);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void GetBestMatch_CaseInsensitiveMatch_ReturnsMatch() 
        {
            // Arrange
            IList<MediaTypeHeaderValue> acceptValues = [new MediaTypeHeaderValue("APPLICATION/JSON")];

            // Act
            string? result = ContentNegotiation.GetBestMatch(acceptValues, SupportedMediaTypes);

            // Assert
            Assert.That(result, Is.EqualTo("application/json"));
        }
    }
}