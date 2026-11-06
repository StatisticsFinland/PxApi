using Azure;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PxApi.DataSources;
using PxApi.Models;

namespace PxApi.UnitTests.DataSources
{
    [TestFixture]
    internal class FileShareDataBaseConnectorTests
    {
        private readonly DataBaseRef _database = DataBaseRef.Create("testdb");
        private Mock<ShareFileClient> _shareFile = null!;
        private FileShareDataBaseConnector _connector = null!;
        private PxFileRef _file = default;

        [SetUp]
        public void SetUp()
        {
            _file = PxFileRef.ValidateAndCreate("table", _database, ["level1", "level2"]);
            _shareFile = new(MockBehavior.Strict);
            Mock<ShareDirectoryClient> tableDirectory = new(MockBehavior.Strict);
            tableDirectory.Setup(client => client.GetFileClient("table.px")).Returns(_shareFile.Object);
            Mock<ShareDirectoryClient> parentDirectory = new(MockBehavior.Strict);
            parentDirectory.Setup(client => client.GetSubdirectoryClient("level2")).Returns(tableDirectory.Object);
            Mock<ShareDirectoryClient> databaseDirectory = new(MockBehavior.Strict);
            databaseDirectory.Setup(client => client.GetSubdirectoryClient("level1")).Returns(parentDirectory.Object);
            Mock<ShareDirectoryClient> rootDirectory = new(MockBehavior.Strict);
            rootDirectory.Setup(client => client.GetSubdirectoryClient(_database.Id)).Returns(databaseDirectory.Object);
            Mock<ShareClient> share = new(MockBehavior.Strict);
            share.Setup(client => client.GetRootDirectoryClient()).Returns(rootDirectory.Object);
            Mock<ShareServiceClient> service = new(MockBehavior.Strict);
            service.Setup(client => client.GetShareClient("test-share")).Returns(share.Object);
            Mock<IAzureClientFactory<ShareServiceClient>> factory = new(MockBehavior.Strict);
            factory.Setup(clientFactory => clientFactory.CreateClient(_database.Id)).Returns(service.Object);
            _connector = new(_database, "test-share", factory.Object, NullLogger<FileShareDataBaseConnector>.Instance);
        }

        [TestCase("\"etag-1\"")]
        [TestCase("\"etag-2\"")]
        public async Task GetSourceVersionAsync_ReturnsETag_WithOnePropertiesRequest(string etag)
        {
            using CancellationTokenSource cancellation = new();
            ShareFileProperties properties = FilesModelFactory.StorageFileProperties(
                eTag: new ETag(etag), lastModified: new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero));
            _shareFile.Setup(client => client.GetPropertiesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(properties, Mock.Of<Response>()));

            string version = await _connector.GetSourceVersionAsync(_file, cancellation.Token);

            Assert.That(version, Is.EqualTo(etag));
            VerifySinglePropertiesRequest(cancellation.Token);
        }

        [Test]
        public void GetSourceVersionAsync_NotFound_ThrowsFileNotFoundWithOriginalException()
        {
            RequestFailedException failure = new(404, "File not found.");
            _shareFile.Setup(client => client.GetPropertiesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);

            FileNotFoundException? exception = Assert.ThrowsAsync<FileNotFoundException>(
                async () => await _connector.GetSourceVersionAsync(_file));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(exception!.InnerException, Is.SameAs(failure));
                Assert.That(exception.Message, Does.Contain(_file.Id).And.Contain(_database.Id).And.Contain("test-share"));
            }
            VerifySinglePropertiesRequest(CancellationToken.None);
        }

        [TestCase(401)]
        [TestCase(403)]
        [TestCase(429)]
        [TestCase(500)]
        public void GetSourceVersionAsync_Non404Failure_PropagatesOriginalException(int status)
        {
            RequestFailedException failure = new(status, "Storage request failed.");
            _shareFile.Setup(client => client.GetPropertiesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);

            RequestFailedException? exception = Assert.ThrowsAsync<RequestFailedException>(
                async () => await _connector.GetSourceVersionAsync(_file));

            Assert.That(exception, Is.SameAs(failure));
            VerifySinglePropertiesRequest(CancellationToken.None);
        }

        private void VerifySinglePropertiesRequest(CancellationToken ct)
        {
            _shareFile.Verify(client => client.GetPropertiesAsync(ct), Times.Once);
            _shareFile.Verify(client => client.ExistsAsync(It.IsAny<CancellationToken>()), Times.Never);
            _shareFile.VerifyNoOtherCalls();
        }
    }
}
