using Moq;
using Px.Utils.Models.Data;
using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Metadata;
using PxApi.Caching;
using PxApi.DataSources;
using PxApi.Models;
using PxApi.UnitTests.Models;
using System.Text;

namespace PxApi.UnitTests.Caching
{
    [TestFixture]
    internal class PxFileReaderTests
    {

        private static readonly PxFileRef fileRef = PxFileRef.CreateFromPath(Path.Combine("C:", "foo", "test.px"), DataBaseRef.Create("testDatabase"));

        [Test]
        public async Task ReadMetadata_WhenCalledWithValidFile_ReturnsMetadata()
        {
            // Arrange
            Mock<IDataBaseConnector> mockFileSystem = new();
            mockFileSystem.Setup(fs => fs.ReadPxFileAsync(It.IsAny<PxFileRef>())).ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes(PxFixtures.MinimalPx.MINIMAL_UTF8_N)));
            PxFileReader reader = new(mockFileSystem.Object);
            string[] expectedLanguages = ["fi", "en"];

            // Act
            IReadOnlyMatrixMetadata result = await reader.ReadMetadataAsync(fileRef);

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
        public async Task GetDataSectionStart_WhenCalledWithValidFile_ReturnsCorrectValue()
        {

            // Arrange
            Mock<IDataBaseConnector> mockFileSystem = new();
            mockFileSystem.Setup(fs => fs.ReadPxFileAsync(It.IsAny<PxFileRef>())).ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes(PxFixtures.MinimalPx.MINIMAL_UTF8_N)));
            PxFileReader reader = new(mockFileSystem.Object);

            // Act
            long? result = await reader.GetDataSectionOffsetAsync(fileRef);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.InstanceOf<long>());
            Assert.That(result, Is.EqualTo(392));
        }

        [Test]
        public async Task ReadDataAsync_WhenCalledWithValidFile_ReturnsData()
        {
            // Arrange
            Mock<IDataBaseConnector> mockFileSystem = new();
            mockFileSystem
                .Setup(fs => fs.ReadPxFileAsync(It.IsAny<PxFileRef>()))
                .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(PxFixtures.MinimalPx.MINIMAL_UTF8_N)));
            PxFileReader reader = new(mockFileSystem.Object);
            long startOffset = (long)await reader.GetDataSectionOffsetAsync(fileRef);
            MatrixMap targetMap = new([
                new DimensionMap("dim1", ["value1"]),
                new DimensionMap("dim2", ["2025"])
            ]);
            MatrixMap fileMap = new([
                new DimensionMap("dim1", ["value1"]),
                new DimensionMap("dim2", ["2024", "2025"])
            ]);

            // Act
            DoubleDataValue[] result = await reader.ReadDataAsync(fileRef, startOffset, targetMap, fileMap);

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
