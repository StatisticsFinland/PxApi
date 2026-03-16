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

namespace PxApi.UnitTests.DataSources
{
    [TestFixture]
    internal class DataBaseConnectorTests
    {
        private static readonly DataBaseRef TestDb = DataBaseRef.Create("testdb");
        private static readonly PxFileRef TestFileRef = PxFileRef.ValidateAndCreate("test", TestDb, ["statisticalProgram"]);

        #region TestableConnector

        private sealed class TestableConnector(DataBaseRef db, string content, bool seekable = true) : DataBaseConnector(db)
        {
            protected override ILogger Logger { get; } = new Mock<ILogger>().Object;

            public override Task<PxFileRef[]> GetAllFilesAsync(CancellationToken ct) =>
                Task.FromResult<PxFileRef[]>([]);

            public override Task<DateTime> GetLastWriteTimeAsync(PxFileRef file, CancellationToken ct) =>
                Task.FromResult(DateTime.UtcNow);

            public override Task<Stream> TryReadAuxiliaryFileAsync(string fileName, string[]? hierarchy, CancellationToken ct = default) =>
                throw new NotImplementedException();

            protected override Task<Stream> OpenPxFileStreamAsync(PxFileRef file, CancellationToken ct)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(content);
                if (seekable)
                {
                    MemoryStream ms = new(bytes);
                    return Task.FromResult<Stream>(ms);
                }
                else
                {
                    NonSeekableStream ns = new(bytes);
                    return Task.FromResult<Stream>(ns);
                }
            }
        }

        private sealed class NonSeekableStream(byte[] data) : MemoryStream(data)
        {
            public override bool CanSeek => false;
        }

        #endregion

        #region ReadMetadataAsync Tests

        [Test]
        public async Task ReadMetadataAsync_SeekableStream_ReturnsMetadata()
        {
            // Arrange
            TestableConnector connector = new(TestDb, PxFixtures.MinimalPx.MINIMAL_UTF8_N);

            // Act
            IReadOnlyMatrixMetadata result = await connector.ReadMetadataAsync(TestFileRef);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result.DefaultLanguage, Is.EqualTo("fi"));
                Assert.That(result.AvailableLanguages, Is.EquivalentTo(["fi", "en"]));
                Assert.That(result.Dimensions, Has.Count.EqualTo(2));
                Assert.That(result.Dimensions[0].Code, Is.EqualTo("dim1"));
                Assert.That(result.Dimensions[1].Code, Is.EqualTo("dim2"));
            }
        }

        [Test]
        public async Task ReadMetadataAsync_NonSeekableStream_ReturnsMetadata()
        {
            // Arrange
            TestableConnector connector = new(TestDb, PxFixtures.MinimalPx.MINIMAL_UTF8_N, seekable: false);

            // Act
            IReadOnlyMatrixMetadata result = await connector.ReadMetadataAsync(TestFileRef);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result.DefaultLanguage, Is.EqualTo("fi"));
                Assert.That(result.AvailableLanguages, Is.EquivalentTo(["fi", "en"]));
                Assert.That(result.Dimensions, Has.Count.EqualTo(2));
                Assert.That(result.Dimensions[0].Code, Is.EqualTo("dim1"));
                Assert.That(result.Dimensions[1].Code, Is.EqualTo("dim2"));
            }
        }

        [Test]
        public async Task ReadMetadataAsync_ReturnsCorrectDimensionValues()
        {
            // Arrange
            TestableConnector connector = new(TestDb, PxFixtures.MinimalPx.MINIMAL_UTF8_N);

            // Act
            IReadOnlyMatrixMetadata result = await connector.ReadMetadataAsync(TestFileRef);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Dimensions[0].Values, Has.Count.EqualTo(1));
                Assert.That(result.Dimensions[1].Values, Has.Count.EqualTo(2));
            }
        }

        [Test]
        public async Task ReadMetadataAsync_SeekableAndNonSeekableStreams_ReturnEquivalentMetadata()
        {
            // Arrange
            TestableConnector seekableConnector = new(TestDb, PxFixtures.MinimalPx.MINIMAL_UTF8_N, seekable: true);
            TestableConnector nonSeekableConnector = new(TestDb, PxFixtures.MinimalPx.MINIMAL_UTF8_N, seekable: false);

            // Act
            IReadOnlyMatrixMetadata seekableResult = await seekableConnector.ReadMetadataAsync(TestFileRef);
            IReadOnlyMatrixMetadata nonSeekableResult = await nonSeekableConnector.ReadMetadataAsync(TestFileRef);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(nonSeekableResult.DefaultLanguage, Is.EqualTo(seekableResult.DefaultLanguage));
                Assert.That(nonSeekableResult.AvailableLanguages, Is.EquivalentTo(seekableResult.AvailableLanguages));
                Assert.That(nonSeekableResult.Dimensions, Has.Count.EqualTo(seekableResult.Dimensions.Count));
                for (int i = 0; i < seekableResult.Dimensions.Count; i++)
                {
                    Assert.That(nonSeekableResult.Dimensions[i].Code, Is.EqualTo(seekableResult.Dimensions[i].Code));
                    Assert.That(nonSeekableResult.Dimensions[i].Values, Has.Count.EqualTo(seekableResult.Dimensions[i].Values.Count));
                }
            }
        }

        #endregion

        #region ReadDataAsync Tests

        [Test]
        public async Task ReadDataAsync_AllValues_ReturnsAllDataValues()
        {
            // Arrange
            TestableConnector connector = new(TestDb, PxFixtures.MinimalPx.MINIMAL_UTF8_N);
            MatrixMap targetMap = new([
                new DimensionMap("dim1", ["value1"]),
                new DimensionMap("dim2", ["2024", "2025"])
            ]);
            IReadOnlyMatrixMetadata meta = await MatrixMetadataUtils.GetMetadataFromFixture(PxFixtures.MinimalPx.MINIMAL_UTF8_N);

            // Act
            DoubleDataValue[] result = await connector.ReadDataAsync(TestFileRef, targetMap, meta);

            // Assert
            Assert.That(result, Has.Length.EqualTo(2));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result[0].Type, Is.EqualTo(DataValueType.Exists));
                Assert.That(result[0].UnsafeValue, Is.EqualTo(1.0));
                Assert.That(result[1].Type, Is.EqualTo(DataValueType.Exists));
                Assert.That(result[1].UnsafeValue, Is.EqualTo(2.0));
            }
        }

        [Test]
        public async Task ReadDataAsync_SubsetOfValues_ReturnsOnlyRequestedValues()
        {
            // Arrange
            TestableConnector connector = new(TestDb, PxFixtures.MinimalPx.MINIMAL_UTF8_N);
            MatrixMap targetMap = new([
                new DimensionMap("dim1", ["value1"]),
                new DimensionMap("dim2", ["2025"])
            ]);
            IReadOnlyMatrixMetadata meta = await MatrixMetadataUtils.GetMetadataFromFixture(PxFixtures.MinimalPx.MINIMAL_UTF8_N);

            // Act
            DoubleDataValue[] result = await connector.ReadDataAsync(TestFileRef, targetMap, meta);

            // Assert
            Assert.That(result, Has.Length.EqualTo(1));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result[0].Type, Is.EqualTo(DataValueType.Exists));
                Assert.That(result[0].UnsafeValue, Is.EqualTo(2.0));
            }
        }

        [Test]
        public async Task ReadDataAsync_FirstValueOnly_ReturnsCorrectValue()
        {
            // Arrange
            TestableConnector connector = new(TestDb, PxFixtures.MinimalPx.MINIMAL_UTF8_N);
            MatrixMap targetMap = new([
                new DimensionMap("dim1", ["value1"]),
                new DimensionMap("dim2", ["2024"])
            ]);
            IReadOnlyMatrixMetadata meta = await MatrixMetadataUtils.GetMetadataFromFixture(PxFixtures.MinimalPx.MINIMAL_UTF8_N);

            // Act
            DoubleDataValue[] result = await connector.ReadDataAsync(TestFileRef, targetMap, meta);

            // Assert
            Assert.That(result, Has.Length.EqualTo(1));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result[0].Type, Is.EqualTo(DataValueType.Exists));
                Assert.That(result[0].UnsafeValue, Is.EqualTo(1.0));
            }
        }

        #endregion

        #region GetSingleRawMetadataValueAsync Tests

        [Test]
        public async Task GetSingleRawMetadataValueAsync_SeekableStream_ExistingKey_ReturnsValue()
        {
            // Arrange
            TestableConnector connector = new(TestDb, PxFixtures.MinimalPx.MINIMAL_UTF8_N);

            // Act
            string result = await connector.GetSingleRawMetadataValueAsync("LANGUAGE", TestFileRef);

            // Assert
            Assert.That(result, Is.EqualTo("\"fi\""));
        }

        [Test]
        public async Task GetSingleRawMetadataValueAsync_NonSeekableStream_ExistingKey_ReturnsValue()
        {
            // Arrange
            TestableConnector connector = new(TestDb, PxFixtures.MinimalPx.MINIMAL_UTF8_N, seekable: false);

            // Act
            string result = await connector.GetSingleRawMetadataValueAsync("LANGUAGE", TestFileRef);

            // Assert
            Assert.That(result, Is.EqualTo("\"fi\""));
        }

        [Test]
        public void GetSingleRawMetadataValueAsync_SeekableStream_NonExistingKey_ThrowsInvalidOperationException()
        {
            // Arrange
            TestableConnector connector = new(TestDb, PxFixtures.MinimalPx.MINIMAL_UTF8_N);

            // Act & Assert
            InvalidOperationException exception = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await connector.GetSingleRawMetadataValueAsync("NONEXISTENT_KEY", TestFileRef));
            Assert.That(exception.Message, Does.Contain("NONEXISTENT_KEY"));
        }

        [Test]
        public void GetSingleRawMetadataValueAsync_NonSeekableStream_NonExistingKey_ThrowsInvalidOperationException()
        {
            // Arrange
            TestableConnector connector = new(TestDb, PxFixtures.MinimalPx.MINIMAL_UTF8_N, seekable: false);

            // Act & Assert
            InvalidOperationException exception = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await connector.GetSingleRawMetadataValueAsync("NONEXISTENT_KEY", TestFileRef));
            Assert.That(exception.Message, Does.Contain("NONEXISTENT_KEY"));
        }

        [Test]
        public async Task GetSingleRawMetadataValueAsync_ReturnsFirstMatchingKey()
        {
            // Arrange
            TestableConnector connector = new(TestDb, PxFixtures.MinimalPx.MINIMAL_UTF8_N);

            // Act
            string result = await connector.GetSingleRawMetadataValueAsync("CHARSET", TestFileRef);

            // Assert
            Assert.That(result, Is.EqualTo("\"ANSI\""));
        }

        [Test]
        public async Task GetSingleRawMetadataValueAsync_SeekableAndNonSeekableStreams_ReturnSameValue()
        {
            // Arrange
            TestableConnector seekableConnector = new(TestDb, PxFixtures.MinimalPx.MINIMAL_UTF8_N, seekable: true);
            TestableConnector nonSeekableConnector = new(TestDb, PxFixtures.MinimalPx.MINIMAL_UTF8_N, seekable: false);

            // Act
            string seekableResult = await seekableConnector.GetSingleRawMetadataValueAsync("LANGUAGE", TestFileRef);
            string nonSeekableResult = await nonSeekableConnector.GetSingleRawMetadataValueAsync("LANGUAGE", TestFileRef);

            // Assert
            Assert.That(nonSeekableResult, Is.EqualTo(seekableResult));
        }

        [Test]
        public async Task GetSingleRawMetadataValueAsync_MultiLanguageKey_ReturnsValue()
        {
            // Arrange
            TestableConnector connector = new(TestDb, PxFixtures.MinimalPx.MINIMAL_UTF8_N);

            // Act
            string result = await connector.GetSingleRawMetadataValueAsync("LANGUAGES", TestFileRef);

            // Assert
            Assert.That(result, Is.EqualTo("\"fi\",\"en\""));
        }

        #endregion

        #region DataBase Property Tests

        [Test]
        public void DataBase_ReturnsConstructorDataBase()
        {
            // Arrange
            TestableConnector connector = new(TestDb, PxFixtures.MinimalPx.MINIMAL_UTF8_N);

            // Act
            DataBaseRef result = connector.DataBase;

            // Assert
            Assert.That(result, Is.EqualTo(TestDb));
        }

        #endregion
    }
}
