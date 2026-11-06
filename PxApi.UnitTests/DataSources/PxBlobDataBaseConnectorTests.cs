using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PxApi.DataSources;
using PxApi.Models;

namespace PxApi.UnitTests.DataSources
{
    [TestFixture]
    internal class PxBlobDataBaseConnectorTests
    {
        private readonly DataBaseRef _database = DataBaseRef.Create("testdb");
        private Mock<BlobClient> _blob = null!;
        private PxBlobDataBaseConnector _connector = null!;
        private PxFileRef _file = default;

        [SetUp]
        public void SetUp()
        {
            _file = PxFileRef.ValidateAndCreate("table", _database, ["level1", "level2"]);
            _blob = new(MockBehavior.Strict);
            Mock<BlobContainerClient> container = new(MockBehavior.Strict);
            container.Setup(client => client.GetBlobClient("px/testdb/level1/level2/table.px")).Returns(_blob.Object);
            Mock<BlobServiceClient> service = new(MockBehavior.Strict);
            service.Setup(client => client.GetBlobContainerClient("test-container")).Returns(container.Object);
            Mock<IAzureClientFactory<BlobServiceClient>> factory = new(MockBehavior.Strict);
            factory.Setup(clientFactory => clientFactory.CreateClient(_database.Id)).Returns(service.Object);
            _connector = new(_database, "test-container", factory.Object, NullLogger<PxBlobDataBaseConnector>.Instance);
        }

        [TestCase("\"etag-1\"")]
        [TestCase("\"etag-2\"")]
        public async Task GetSourceVersionAsync_ReturnsETag_WithOnePropertiesRequest(string etag)
        {
            using CancellationTokenSource cancellation = new();
            BlobProperties properties = BlobsModelFactory.BlobProperties(
                eTag: new ETag(etag), lastModified: new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero));
            _blob.Setup(client => client.GetPropertiesAsync(null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(properties, Mock.Of<Response>()));

            string version = await _connector.GetSourceVersionAsync(_file, cancellation.Token);

            Assert.That(version, Is.EqualTo(etag));
            VerifySinglePropertiesRequest(cancellation.Token);
        }

        [Test]
        public void GetSourceVersionAsync_NotFound_ThrowsFileNotFoundWithOriginalException()
        {
            RequestFailedException failure = new(404, "Blob not found.");
            _blob.Setup(client => client.GetPropertiesAsync(null, It.IsAny<CancellationToken>())).ThrowsAsync(failure);

            FileNotFoundException? exception = Assert.ThrowsAsync<FileNotFoundException>(
                async () => await _connector.GetSourceVersionAsync(_file));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(exception!.InnerException, Is.SameAs(failure));
                Assert.That(exception.Message, Does.Contain(_file.Id).And.Contain(_database.Id)
                    .And.Contain("test-container").And.Contain("px/testdb/level1/level2/table.px"));
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
            _blob.Setup(client => client.GetPropertiesAsync(null, It.IsAny<CancellationToken>())).ThrowsAsync(failure);

            RequestFailedException? exception = Assert.ThrowsAsync<RequestFailedException>(
                async () => await _connector.GetSourceVersionAsync(_file));

            Assert.That(exception, Is.SameAs(failure));
            VerifySinglePropertiesRequest(CancellationToken.None);
        }

        private void VerifySinglePropertiesRequest(CancellationToken ct)
        {
            _blob.Verify(client => client.GetPropertiesAsync(null, ct), Times.Once);
            _blob.Verify(client => client.ExistsAsync(It.IsAny<CancellationToken>()), Times.Never);
            _blob.VerifyNoOtherCalls();
        }
    }
}
