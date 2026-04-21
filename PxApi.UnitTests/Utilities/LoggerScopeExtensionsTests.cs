using Microsoft.Extensions.Logging;
using Moq;
using PxApi.Utilities;

namespace PxApi.UnitTests.Utilities
{
    [TestFixture]
    public class LoggerScopeExtensionsTests
    {
        private Mock<ILogger> _mockLogger = null!;

        [SetUp]
        public void SetUp()
        {
            _mockLogger = new Mock<ILogger>();
            _mockLogger
                .Setup(x => x.BeginScope(It.IsAny<Dictionary<string, object>>()))
                .Returns(Mock.Of<IDisposable>());
        }

        [Test]
        public void BeginDbScope_PushesDbIdIntoScope()
        {
            // Arrange
            const string dbId = "testDb";

            // Act
            _mockLogger.Object.BeginDbScope(dbId);

            // Assert
            _mockLogger.Verify(x => x.BeginScope(It.Is<Dictionary<string, object>>(d =>
                d.ContainsKey(LoggerConsts.DB_ID) && (string)d[LoggerConsts.DB_ID] == dbId)),
                Times.Once);
        }

        [Test]
        public void BeginDbNotFoundScope_PushesNotFoundPlaceholder()
        {
            // Act
            _mockLogger.Object.BeginDbNotFoundScope();

            // Assert
            _mockLogger.Verify(x => x.BeginScope(It.Is<Dictionary<string, object>>(d =>
                d.ContainsKey(LoggerConsts.DB_ID) && (string)d[LoggerConsts.DB_ID] == LoggerConsts.NOT_FOUND_PLACEHOLDER)),
                Times.Once);
        }

        [Test]
        public void BeginFileScope_PushesPxFileIntoScope()
        {
            // Arrange
            const string fileId = "table001";

            // Act
            _mockLogger.Object.BeginFileScope(fileId);

            // Assert
            _mockLogger.Verify(x => x.BeginScope(It.Is<Dictionary<string, object>>(d =>
                d.ContainsKey(LoggerConsts.PX_FILE) && (string)d[LoggerConsts.PX_FILE] == fileId)),
                Times.Once);
        }

        [Test]
        public void BeginResourceScope_PushesDbIdAndPxFileIntoScope()
        {
            // Arrange
            const string dbId = "testDb";
            const string fileId = "table001";

            // Act
            _mockLogger.Object.BeginResourceScope(dbId, fileId);

            // Assert
            _mockLogger.Verify(x => x.BeginScope(It.Is<Dictionary<string, object>>(d =>
                d.ContainsKey(LoggerConsts.DB_ID) && (string)d[LoggerConsts.DB_ID] == dbId &&
                d.ContainsKey(LoggerConsts.PX_FILE) && (string)d[LoggerConsts.PX_FILE] == fileId)),
                Times.Once);
        }

        [Test]
        public void BeginResourceNotFoundScope_NullDbId_UsesBothPlaceholders()
        {
            // Act
            _mockLogger.Object.BeginResourceNotFoundScope(null);

            // Assert
            _mockLogger.Verify(x => x.BeginScope(It.Is<Dictionary<string, object>>(d =>
                (string)d[LoggerConsts.DB_ID] == LoggerConsts.NOT_FOUND_PLACEHOLDER &&
                (string)d[LoggerConsts.PX_FILE] == LoggerConsts.NOT_FOUND_PLACEHOLDER)),
                Times.Once);
        }

        [Test]
        public void BeginResourceNotFoundScope_WithDbId_UsesProvidedDbIdAndPlaceholderForFile()
        {
            // Arrange
            const string dbId = "knownDb";

            // Act
            _mockLogger.Object.BeginResourceNotFoundScope(dbId);

            // Assert
            _mockLogger.Verify(x => x.BeginScope(It.Is<Dictionary<string, object>>(d =>
                (string)d[LoggerConsts.DB_ID] == dbId &&
                (string)d[LoggerConsts.PX_FILE] == LoggerConsts.NOT_FOUND_PLACEHOLDER)),
                Times.Once);
        }

        [Test]
        public void BeginResourceNotFoundScope_NoArgs_UsesBothPlaceholders()
        {
            // Act
            _mockLogger.Object.BeginResourceNotFoundScope();

            // Assert
            _mockLogger.Verify(x => x.BeginScope(It.Is<Dictionary<string, object>>(d =>
                (string)d[LoggerConsts.DB_ID] == LoggerConsts.NOT_FOUND_PLACEHOLDER &&
                (string)d[LoggerConsts.PX_FILE] == LoggerConsts.NOT_FOUND_PLACEHOLDER)),
                Times.Once);
        }

        [Test]
        public void BeginSearchScope_QueryOnly_PushesQueryIntoScope()
        {
            // Arrange
            const string query = "population\n<script>";

            // Act
            _mockLogger.Object.BeginSearchScope(query);

            // Assert
            _mockLogger.Verify(x => x.BeginScope(It.Is<Dictionary<string, object>>(d =>
                d.ContainsKey(LoggerConsts.SEARCH_QUERY) &&
                (string)d[LoggerConsts.SEARCH_QUERY] == query &&
                !d.ContainsKey(LoggerConsts.DB_ID))),
                Times.Once);
        }

        [Test]
        public void BeginSearchScope_QueryAndDbId_PushesQueryAndDbIdIntoScope()
        {
            // Arrange
            const string query = "population\n<script>";
            const string dbId = "db1";

            // Act
            _mockLogger.Object.BeginSearchScope(query, dbId);

            // Assert
            _mockLogger.Verify(x => x.BeginScope(It.Is<Dictionary<string, object>>(d =>
                d.ContainsKey(LoggerConsts.SEARCH_QUERY) &&
                (string)d[LoggerConsts.SEARCH_QUERY] == query &&
                d.ContainsKey(LoggerConsts.DB_ID) &&
                (string)d[LoggerConsts.DB_ID] == dbId)),
                Times.Once);
        }
    }
}
