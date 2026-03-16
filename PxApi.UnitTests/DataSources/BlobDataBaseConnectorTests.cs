using Azure.Storage.Blobs;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using PxApi.DataSources;
using PxApi.ModelBuilders;
using PxApi.Models;

namespace PxApi.UnitTests.DataSources
{
    [TestFixture]
    internal class BlobDataBaseConnectorTests
    {
        [Test]
        public void GetBlobName_WithHierarchy_ReturnsExpectedPath()
        {
            // Arrange
            DataBaseRef db = DataBaseRef.Create("db1");
            string[] hierarchy = ["level1", "level2"];

            // Act
            string blobName = TestBlobDataBaseConnector.BuildBlobName("table", db, "px", hierarchy);

            // Assert
            Assert.That(blobName, Is.EqualTo($"px/db1/level1/level2/table{PxFileConstants.FILE_ENDING}"));
        }

        [Test]
        public void GetBlobName_WithoutHierarchy_ReturnsExpectedPath()
        {
            // Arrange
            DataBaseRef db = DataBaseRef.Create("db1");

            // Act
            string blobName = TestBlobDataBaseConnector.BuildBlobName("table", db, "px", null);

            // Assert
            Assert.That(blobName, Is.EqualTo($"px/db1/table{PxFileConstants.FILE_ENDING}"));
        }

        private sealed class TestBlobDataBaseConnector(DataBaseRef dataBase, ILogger logger, IAzureClientFactory<BlobServiceClient> blobServiceClientFactory) 
            : BlobDataBaseConnector(dataBase, "test-container", blobServiceClientFactory)
        {
            protected override ILogger Logger => logger;

            protected override bool UseShortFormNames => false;

            public override Task<DateTime> GetLastWriteTimeAsync(PxFileRef file, CancellationToken ct = default)
            {
                throw new NotImplementedException();
            }

            internal static string BuildBlobName(string fileName, DataBaseRef db, string root, string[]? hierarchy)
            {
                return GetBlobName(fileName, db, root, hierarchy);
            }
        }
    }
}
