using Px.Utils.BinaryData.ValueConverters;
using PxApi.DataSources;

namespace PxApi.UnitTests.DataSources
{
    [TestFixture]
    internal class BinaryBlobDataBaseConnector_InternalHelpersTests
    {
        [Test]
        public void TryParseFileIdFromMetaBlobName_WhenValidMetaBlobNameWithTimestamp_ReturnsFileIdWithoutTimestamp()
        {
            // Arrange
            string blobName = "meta/db1/table_202501010000.meta.json";

            // Act
            string? fileId = BinaryBlobDataBaseConnector.TryParseFileIdFromMetaBlobName(blobName);

            // Assert
            Assert.That(fileId, Is.EqualTo("db1/table"));
        }

        [Test]
        public void TryParseFileIdFromMetaBlobName_WhenValidMetaBlobNameWithoutTimestamp_ReturnsFileId()
        {
            // Arrange
            string blobName = "meta/db1/table.meta.json";

            // Act
            string? fileId = BinaryBlobDataBaseConnector.TryParseFileIdFromMetaBlobName(blobName);

            // Assert
            Assert.That(fileId, Is.EqualTo("db1/table"));
        }

        [Test]
        public void TryParseFileIdFromMetaBlobName_WhenMissingPrefix_ReturnsNull()
        {
            // Arrange
            string blobName = "db1/table_202501010000.meta.json";

            // Act
            string? fileId = BinaryBlobDataBaseConnector.TryParseFileIdFromMetaBlobName(blobName);

            // Assert
            Assert.That(fileId, Is.Null);
        }

        [Test]
        public void TryParseFileIdFromMetaBlobName_WhenMissingSuffix_ReturnsNull()
        {
            // Arrange
            string blobName = "meta/db1/table_202501010000.json";

            // Act
            string? fileId = BinaryBlobDataBaseConnector.TryParseFileIdFromMetaBlobName(blobName);

            // Assert
            Assert.That(fileId, Is.Null);
        }

        [Test]
        public void BuildMetadataPrefix_ReturnsExpectedFormat()
        {
            // Arrange
            const string dbId = "db1";
            const string fileId = "table";

            // Act
            string prefix = BinaryBlobDataBaseConnector.BuildMetadataPrefix(dbId, fileId);

            // Assert
            Assert.That(prefix, Is.EqualTo("meta/db1/table_"));
        }

        [Test]
        public void BuildDataBlobName_ReturnsExpectedFormat()
        {
            // Arrange
            const string dbId = "db1";
            const string fileId = "table";
            const string contentValueCode = "cv1";
            const string timestamp = "202501010930";

            // Act
            string blobName = BinaryBlobDataBaseConnector.BuildDataBlobName(dbId, fileId, contentValueCode, timestamp);

            // Assert
            Assert.That(blobName, Is.EqualTo("bin/db1/table_cv1_202501010930.pxb"));
        }

        [Test]
        public void ParsePxbHeader_WhenValidHeader_ReturnsHeaderLengthAndCodec()
        {
            // Arrange
            const uint expectedHeaderLength = 16U;
            BinaryValueCodecType expectedCodec = (BinaryValueCodecType)7;

            byte[] headerBytes = new byte[8];
            BitConverter.GetBytes(expectedHeaderLength).CopyTo(headerBytes, 0);
            BitConverter.GetBytes((uint)expectedCodec).CopyTo(headerBytes, 4);

            // Act
            (uint HeaderLength, BinaryValueCodecType Codec) header = BinaryBlobDataBaseConnector.ParsePxbHeader(headerBytes);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(header.HeaderLength, Is.EqualTo(expectedHeaderLength));
                Assert.That(header.Codec, Is.EqualTo(expectedCodec));
            }
        }

        [Test]
        public void ParsePxbHeader_WhenHeaderTooShort_ThrowsArgumentException()
        {
            // Arrange
            byte[] headerBytes = new byte[7];

            // Act
            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                _ = BinaryBlobDataBaseConnector.ParsePxbHeader(headerBytes))!;

            // Assert
            Assert.That(exception.ParamName, Is.EqualTo("headerBytes"));
        }
    }
}
