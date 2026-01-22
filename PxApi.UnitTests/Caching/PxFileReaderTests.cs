using Microsoft.Extensions.Logging;
using Moq;
using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Data;
using Px.Utils.Models.Metadata;
using PxApi.DataSources;
using PxApi.Models;
using PxApi.UnitTests.Models;
using PxApi.UnitTests.Utils;
using System.Text;

namespace PxApi.UnitTests.Caching
{
    [TestFixture]
    internal class PxFileReaderTests
    {
        private static readonly PxFileRef fileRef = PxFileRef.CreateFromPath(Path.Combine("C:", "foo", "test.px"), DataBaseRef.Create("testDatabase"));

        private sealed class TestStreamConnector(DataBaseRef db, string content, string filePath) : DataBaseConnector(db)
        {
            protected override ILogger Logger { get; } = new Mock<ILogger>().Object;

            public override Task<string[]> GetAllFilesAsync(CancellationToken ct) => Task.FromResult<string[]>([filePath]);

            public override Task<DateTime> GetLastWriteTimeAsync(PxFileRef file, CancellationToken ct) => Task.FromResult(DateTime.UtcNow);

            public override Task<Stream> TryReadAuxiliaryFileAsync(string relativePath, CancellationToken ct) => throw new FileNotFoundException();

            protected override Task<Stream> OpenPxFileStreamAsync(PxFileRef file, CancellationToken ct)
            {
                MemoryStream ms = new(Encoding.UTF8.GetBytes(content));
                return Task.FromResult<Stream>(ms);
            }
        }

        [Test]
        public async Task ReadMetadata_WhenCalledWithValidFile_ReturnsMetadata()
        {
            // Arrange
            TestStreamConnector connector = new(fileRef.DataBase, PxFixtures.MinimalPx.MINIMAL_UTF8_N, fileRef.FilePath);
            string[] expectedLanguages = ["fi", "en"];

            // Act
            IReadOnlyMatrixMetadata result = await connector.ReadMetadataAsync(fileRef, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.InstanceOf<IReadOnlyMatrixMetadata>());
                Assert.That(result.DefaultLanguage, Is.EqualTo("fi"));
                Assert.That(result.AvailableLanguages, Is.EqualTo(expectedLanguages));
                Assert.That(result.Dimensions, Has.Count.EqualTo(2));
                Assert.That(result.Dimensions[0].Code, Is.EqualTo("dim1"));
                Assert.That(result.Dimensions[1].Code, Is.EqualTo("dim2"));
                Assert.That(result.Dimensions[0].Values, Has.Count.EqualTo(1));
                Assert.That(result.Dimensions[1].Values, Has.Count.EqualTo(2));
            });
        }

        [Test]
        public async Task ReadDataAsync_WhenCalledWithValidFile_ReturnsData()
        {
            // Arrange
            TestStreamConnector connector = new(fileRef.DataBase, PxFixtures.MinimalPx.MINIMAL_UTF8_N, fileRef.FilePath);
            MatrixMap targetMap = new([
                new DimensionMap("dim1", ["value1"]),
                new DimensionMap("dim2", ["2025"])
            ]);

            IReadOnlyMatrixMetadata meta = await MatrixMetadataUtils.GetMetadataFromFixture(PxFixtures.MinimalPx.MINIMAL_UTF8_N);

            // Act
            DoubleDataValue[] result = await connector.ReadDataAsync(fileRef, targetMap, meta, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Has.Length.EqualTo(1));
                Assert.That(result[0], Is.InstanceOf<DoubleDataValue>());
                Assert.That(result[0].Type, Is.EqualTo(DataValueType.Exists));
                Assert.That(result[0].UnsafeValue, Is.EqualTo(2));
            });
        }
    }
}
