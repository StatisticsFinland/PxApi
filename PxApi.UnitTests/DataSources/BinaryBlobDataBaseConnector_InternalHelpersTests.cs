using Px.Utils.BinaryData.ValueConverters;
using Px.Utils.Models.Metadata.Dimensions;
using PxApi.DataSources;
using PxApi.UnitTests.ModelBuilderTests;

namespace PxApi.UnitTests.DataSources
{
    [TestFixture]
    internal class BinaryBlobDataBaseConnector_InternalHelpersTests
    {
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

        [TestCase("db1", "table", "cv1", "202501010930", "bin/db1/table/cv1_202501010930.pxb")]
        [TestCase("Db_1", "Table_one-2", "CV_one-2", "202502281245", "bin/Db_1/Table_one-2/CV_one-2_202502281245.pxb")]
        public void BuildDataBlobName_ReturnsExpectedFormat(
            string dbId, string fileId, string contentValueCode, string timestamp, string expectedBlobName)
        {
            // Act
            string blobName = BinaryBlobDataBaseConnector.BuildDataBlobName(dbId, fileId, contentValueCode, timestamp);

            // Assert
            Assert.That(blobName, Is.EqualTo(expectedBlobName));
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

        [Test]
        public void GetTimestamp_ReturnsMaxLastUpdatedTimestamp()
        {
            // Arrange
            ContentDimension contentDimension = TestMockMetaBuilder.GetMockContentDimension("content");
            ContentValueList values = contentDimension.Values;
            const string expectedTimestamp = "202410100000";

            // Act
            string timestamp = BinaryBlobDataBaseConnector.GetTimestamp(values);

            // Assert
            Assert.That(timestamp, Is.EqualTo(expectedTimestamp));
        }
    }
}
