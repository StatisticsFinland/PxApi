using Microsoft.Extensions.Logging;
using Moq;
using Px.Utils.BinaryData.ValueConverters;
using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Metadata;
using Px.Utils.Models.Metadata.Dimensions;
using Px.Utils.Models.Metadata.Enums;
using Px.Utils.Models.Metadata.ExtensionMethods;
using PxApi.Configuration;
using PxApi.DataSources;
using PxApi.Exceptions;
using PxApi.Models;
using PxApi.UnitTests.ModelBuilderTests;
using PxApi.UnitTests.Utils;
using System.Text.Json;

namespace PxApi.UnitTests.DataSources
{
    [TestFixture]
    internal class BinaryBlobDataBaseConnectorTests
    {
        private Mock<ILogger<BinaryBlobDataBaseConnector>> _loggerMock = null!;
        private DataBaseRef _dbRef;

        // Lower thresholds for testing so large-blob tests can use smaller data
        const long TestSmallThreshold = 100;
        const long TestMaxWindowedReadSize = 500;
        const long TestReadWindowGap = 50;

        [SetUp]
        public void SetUp()
        {
            _loggerMock = new(MockBehavior.Loose);
            _dbRef = DataBaseRef.Create("testdb");

            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.MountedDb(0, "testdb", "C:/test"),
                new Dictionary<string, string?>
                {
                    ["BlobReadMode:SmallThreshold"] = TestSmallThreshold.ToString(),
                    ["BlobReadMode:MaxWindowedReadSize"] = TestMaxWindowedReadSize.ToString(),
                    ["BlobReadMode:ReadWindowGap"] = TestReadWindowGap.ToString(),
                }
            );
            TestConfigFactory.BuildAndLoad(configData);
        }

        [Test]
        public void UseShortFormNames_ReturnsTrue()
        {
            // Arrange
            TestableBinaryBlobConnector connector = new(_dbRef, _loggerMock.Object, []);

            // Act
            bool useShortFormNames = connector.UseShortFormNamesValue;

            // Assert
            Assert.That(useShortFormNames, Is.True);
        }

        #region ReadMetadataAsync

        [Test]
        public async Task ReadMetadataAsync_WhenSingleMetaBlobExists_ReturnsDeserializedMetadata()
        {
            // Arrange
            MatrixMetadata metadata = TestMockMetaBuilder.GetMockMetadata();
            byte[] metaBytes = JsonSerializer.SerializeToUtf8Bytes(metadata, GlobalJsonConverterOptions.Default);
            const string blobName = "meta/testdb/table1_202501010000.meta.json";

            TestableBinaryBlobConnector connector = new(_dbRef, _loggerMock.Object, [blobName]);
            connector.AddBlobContent(blobName, metaBytes);

            PxFileRef fileRef = PxFileRef.ValidateAndCreate("table1", _dbRef, ["statisticalProgram"]);

            // Act
            IReadOnlyMatrixMetadata result = await connector.ReadMetadataAsync(fileRef);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result.DefaultLanguage, Is.EqualTo(metadata.DefaultLanguage));
                Assert.That(result.Dimensions, Has.Count.EqualTo(metadata.Dimensions.Count));
            }
        }

        [Test]
        public async Task ReadMetadataAsync_WhenMultipleMetaBlobsExist_SelectsLatestByName()
        {
            // Arrange
            MatrixMetadata metadata = TestMockMetaBuilder.GetMockMetadata();
            byte[] metaBytes = JsonSerializer.SerializeToUtf8Bytes(metadata, GlobalJsonConverterOptions.Default);
            const string olderBlob = "meta/testdb/table1_202501010000.meta.json";
            const string newerBlob = "meta/testdb/table1_202502010000.meta.json";

            TestableBinaryBlobConnector connector = new(_dbRef, _loggerMock.Object, [olderBlob, newerBlob]);
            connector.AddBlobContent(olderBlob, metaBytes);
            connector.AddBlobContent(newerBlob, metaBytes);

            PxFileRef fileRef = PxFileRef.ValidateAndCreate("table1", _dbRef, ["statisticalProgram"]);

            // Act
            IReadOnlyMatrixMetadata result = await connector.ReadMetadataAsync(fileRef);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(connector.LastOpenedBlobName, Is.EqualTo(newerBlob));
            }
        }

        [Test]
        public void ReadMetadataAsync_WhenNoMetaBlobsExist_ThrowsFileNotFoundException()
        {
            // Arrange
            TestableBinaryBlobConnector connector = new(_dbRef, _loggerMock.Object, []);
            PxFileRef fileRef = PxFileRef.ValidateAndCreate("table1", _dbRef, ["statisticalProgram"]);

            // Act & Assert
            Assert.ThrowsAsync<FileNotFoundException>(async () => await connector.ReadMetadataAsync(fileRef));
        }

        [Test]
        public void ReadMetadataAsync_WhenMetadataIsNull_ThrowsInvalidDataException()
        {
            // Arrange
            const string blobName = "meta/testdb/table1_202501010000.meta.json";
            byte[] metaBytes = [];

            TestableBinaryBlobConnector connector = new(_dbRef, _loggerMock.Object, [blobName]);
            connector.AddBlobContent(blobName, metaBytes);

            PxFileRef fileRef = PxFileRef.ValidateAndCreate("table1", _dbRef, ["statisticalProgram"]);

            // Act & Assert
            Assert.ThrowsAsync<InvalidDataException>(async () => await connector.ReadMetadataAsync(fileRef));
        }

        #endregion

        #region GetLastWriteTimeAsync

        [Test]
        public async Task GetLastWriteTimeAsync_ReturnsMaxLastUpdatedFromContentDimension()
        {
            // Arrange
            MatrixMetadata metadata = TestMockMetaBuilder.GetMockMetadata();
            byte[] metaBytes = JsonSerializer.SerializeToUtf8Bytes(metadata, GlobalJsonConverterOptions.Default);
            const string blobName = "meta/testdb/table1_202501010000.meta.json";

            TestableBinaryBlobConnector connector = new(_dbRef, _loggerMock.Object, [blobName]);
            connector.AddBlobContent(blobName, metaBytes);

            PxFileRef fileRef = PxFileRef.ValidateAndCreate("table1", _dbRef, ["statisticalProgram"]);

            // Act
            DateTime result = await connector.GetLastWriteTimeAsync(fileRef);

            // Assert
            ContentDimension contentDimension = metadata.GetContentDimension();
            DateTime expected = contentDimension.Values.Map(v => v.LastUpdated).Max();
            Assert.That(result, Is.EqualTo(expected));
        }

        #endregion

        #region ReadDataAsync

        [Test]
        public void ReadDataAsync_WhenDataBlobDoesNotExist_ThrowsBinaryBlobSynchronizationException()
        {
            // Arrange
            MatrixMetadata metadata = TestMockMetaBuilder.GetMockMetadata();

            MatrixMap targetMap = new([.. metadata.Dimensions.Select(d => new DimensionMap(d.Code, [.. d.Values.Select(v => v.Code)]))]);

            TestableBinaryBlobConnector connector = new(_dbRef, _loggerMock.Object, []);
            PxFileRef fileRef = PxFileRef.ValidateAndCreate("table1", _dbRef, ["statisticalProgram"]);

            // Act & Assert
            Assert.ThrowsAsync<BinaryBlobSynchronizationException>(async () =>
                await connector.ReadDataAsync(fileRef, targetMap, metadata));
        }

        [Test]
        public async Task ReadDataAsync_WhenDataBlobExists_ReturnsDataValues()
        {
            // Arrange
            MatrixMetadata metadata = TestMockMetaBuilder.GetMockMetadata();
            ContentDimension contentDim = metadata.GetContentDimension();
            DateTime lastUpdated = contentDim.Values.Map(v => v.LastUpdated).Max();
            string timestamp = lastUpdated.ToString("yyyyMMddHHmm");

            MatrixMap targetMap = new([.. metadata.Dimensions.Select(d => new DimensionMap(d.Code, [.. d.Values.Select(v => v.Code)]))]);

            TestableBinaryBlobConnector connector = new(_dbRef, _loggerMock.Object, []);
            PxFileRef fileRef = PxFileRef.ValidateAndCreate("table1", _dbRef, ["statisticalProgram"]);

            IMatrixMap collapsedMap = metadata.CollapseDimension(contentDim.Code, contentDim.Values[0].Code);
            int collapsedSize = (int)collapsedMap.GetSize();

            foreach (ContentDimensionValue cVal in contentDim.Values)
            {
                string blobName = BinaryBlobDataBaseConnector.BuildDataBlobName(_dbRef.Id, fileRef.Id, cVal.Code, timestamp);
                byte[] pxbContent = BuildPxbBlob(collapsedSize, BinaryValueCodecType.DoubleCodec);
                connector.AddBlobContent(blobName, pxbContent);
            }

            // Act
            DoubleDataValue[] result = await connector.ReadDataAsync(fileRef, targetMap, metadata);

            // Assert
            int expectedSize = (int)targetMap.GetSize();
            Assert.That(result, Has.Length.EqualTo(expectedSize));
        }

        [Test]
        public async Task ReadDataAsync_StreamingFromStart_ReturnsCorrectValuesInOrder()
        {
            // Arrange: mock metadata has content(2) × time(2) × dim0(2) × dim1(2) = 16 total
            // Each content blob holds time(2) × dim0(2) × dim1(2) = 8 values in row-major order
            // Result layout: cv0 block (8 values) then cv1 block (8 values)
            MatrixMetadata metadata = TestMockMetaBuilder.GetMockMetadata();
            ContentDimension contentDim = metadata.GetContentDimension();
            DateTime lastUpdated = contentDim.Values.Map(v => v.LastUpdated).Max();
            string timestamp = lastUpdated.ToString("yyyyMMddHHmm");

            MatrixMap targetMap = new([.. metadata.Dimensions.Select(d =>
                new DimensionMap(d.Code, [.. d.Values.Select(v => v.Code)]))]);

            TestableBinaryBlobConnector connector = new(_dbRef, _loggerMock.Object, []);
            PxFileRef fileRef = PxFileRef.ValidateAndCreate("table1", _dbRef, ["statisticalProgram"]);

            double[] blob0Values = [10.0, 20.0, 30.0, 40.0, 50.0, 60.0, 70.0, 80.0];
            double[] blob1Values = [110.0, 120.0, 130.0, 140.0, 150.0, 160.0, 170.0, 180.0];
            double[][] blobValues = [blob0Values, blob1Values];

            for (int c = 0; c < contentDim.Values.Count; c++)
            {
                ContentDimensionValue cVal = contentDim.Values[c];
                string blobName = BinaryBlobDataBaseConnector.BuildDataBlobName(_dbRef.Id, fileRef.Id, cVal.Code, timestamp);
                connector.AddBlobContent(blobName, BuildPxbBlobWithValues(blobValues[c]));
            }

            // Act
            DoubleDataValue[] result = await connector.ReadDataAsync(fileRef, targetMap, metadata);

            // Assert: content is the first dimension in targetMap, so cv0 occupies indices 0-7 and cv1 occupies 8-15
            Assert.That(result, Has.Length.EqualTo(16));
            using (Assert.EnterMultipleScope())
            {
                for (int i = 0; i < 8; i++)
                {
                    Assert.That(result[i].UnsafeValue, Is.EqualTo(blob0Values[i]), $"result[{i}] should match blob0[{i}]");
                }
                for (int i = 0; i < 8; i++)
                {
                    Assert.That(result[8 + i].UnsafeValue, Is.EqualTo(blob1Values[i]), $"result[{8 + i}] should match blob1[{i}]");
                }
            }
        }

        [Test]
        public async Task ReadDataAsync_SingleContentValue_ReturnsCorrectValuesInOrder()
        {
            // Arrange: request only one content dimension value
            MatrixMetadata metadata = TestMockMetaBuilder.GetMockMetadata();
            ContentDimension contentDim = metadata.GetContentDimension();
            DateTime lastUpdated = contentDim.Values.Map(v => v.LastUpdated).Max();
            string timestamp = lastUpdated.ToString("yyyyMMddHHmm");

            MatrixMap targetMap = new([
                new DimensionMap(contentDim.Code, [contentDim.Values[0].Code]),
                .. metadata.Dimensions
                    .Where(d => d.Code != contentDim.Code)
                    .Select(d => new DimensionMap(d.Code, [.. d.Values.Select(v => v.Code)]))
            ]);

            TestableBinaryBlobConnector connector = new(_dbRef, _loggerMock.Object, []);
            PxFileRef fileRef = PxFileRef.ValidateAndCreate("table1", _dbRef, ["statisticalProgram"]);

            // Collapsed blob: time(2) × dim0(2) × dim1(2) = 8
            double[] blobValues = [100.0, 200.0, 300.0, 400.0, 500.0, 600.0, 700.0, 800.0];
            string blobName = BinaryBlobDataBaseConnector.BuildDataBlobName(_dbRef.Id, fileRef.Id, contentDim.Values[0].Code, timestamp);
            connector.AddBlobContent(blobName, BuildPxbBlobWithValues(blobValues));

            // Act
            DoubleDataValue[] result = await connector.ReadDataAsync(fileRef, targetMap, metadata);

            // Assert: 1 content × 2 time × 2 dim0 × 2 dim1 = 8
            Assert.That(result, Has.Length.EqualTo(8));
            using (Assert.EnterMultipleScope())
            {
                for (int i = 0; i < blobValues.Length; i++)
                {
                    Assert.That(result[i].UnsafeValue, Is.EqualTo(blobValues[i]), $"result[{i}]");
                }
            }
        }

        [Test]
        public async Task ReadDataAsync_SubsetSelection_ReturnsOnlyRequestedValuesInOrder()
        {
            // Arrange: select first content value, first time value, all dim0 and dim1
            MatrixMetadata metadata = TestMockMetaBuilder.GetMockMetadata();
            ContentDimension contentDim = metadata.GetContentDimension();
            DateTime lastUpdated = contentDim.Values.Map(v => v.LastUpdated).Max();
            string timestamp = lastUpdated.ToString("yyyyMMddHHmm");

            MatrixMap targetMap = new([
                new DimensionMap(contentDim.Code, [contentDim.Values[0].Code]),
                new DimensionMap("time-code", ["time-value0-code"]),
                new DimensionMap("dim0-code", ["dim0-value0-code", "dim0-value1-code"]),
                new DimensionMap("dim1-code", ["dim1-value0-code", "dim1-value1-code"])
            ]);

            TestableBinaryBlobConnector connector = new(_dbRef, _loggerMock.Object, []);
            PxFileRef fileRef = PxFileRef.ValidateAndCreate("table1", _dbRef, ["statisticalProgram"]);

            // Full blob: time(2) × dim0(2) × dim1(2) = 8 values in row-major order:
            // [t0d0v0, t0d0v1, t0d1v0, t0d1v1, t1d0v0, t1d0v1, t1d1v0, t1d1v1]
            double[] fullBlobValues = [1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0];
            string blobName = BinaryBlobDataBaseConnector.BuildDataBlobName(_dbRef.Id, fileRef.Id, contentDim.Values[0].Code, timestamp);
            connector.AddBlobContent(blobName, BuildPxbBlobWithValues(fullBlobValues));

            // Act
            DoubleDataValue[] result = await connector.ReadDataAsync(fileRef, targetMap, metadata);

            // Assert: selecting time-value0 gives the first 4 values from the blob
            Assert.That(result, Has.Length.EqualTo(4));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result[0].UnsafeValue, Is.EqualTo(1.0));
                Assert.That(result[1].UnsafeValue, Is.EqualTo(2.0));
                Assert.That(result[2].UnsafeValue, Is.EqualTo(3.0));
                Assert.That(result[3].UnsafeValue, Is.EqualTo(4.0));
            }
        }

        [Test]
        public async Task ReadDataAsync_MultipleContentValues_DistributesValuesInCorrectOrder()
        {
            // Arrange: 2 content values, each collapsed to time(2) × dim0(2) × dim1(2) = 8
            MatrixMetadata metadata = TestMockMetaBuilder.GetMockMetadata();
            ContentDimension contentDim = metadata.GetContentDimension();
            DateTime lastUpdated = contentDim.Values.Map(v => v.LastUpdated).Max();
            string timestamp = lastUpdated.ToString("yyyyMMddHHmm");

            MatrixMap targetMap = new([.. metadata.Dimensions.Select(d =>
                new DimensionMap(d.Code, [.. d.Values.Select(v => v.Code)]))]);

            TestableBinaryBlobConnector connector = new(_dbRef, _loggerMock.Object, []);
            PxFileRef fileRef = PxFileRef.ValidateAndCreate("table1", _dbRef, ["statisticalProgram"]);

            double[] cv0Values = [1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0];
            double[] cv1Values = [11.0, 12.0, 13.0, 14.0, 15.0, 16.0, 17.0, 18.0];

            string blob0Name = BinaryBlobDataBaseConnector.BuildDataBlobName(_dbRef.Id, fileRef.Id, contentDim.Values[0].Code, timestamp);
            string blob1Name = BinaryBlobDataBaseConnector.BuildDataBlobName(_dbRef.Id, fileRef.Id, contentDim.Values[1].Code, timestamp);
            connector.AddBlobContent(blob0Name, BuildPxbBlobWithValues(cv0Values));
            connector.AddBlobContent(blob1Name, BuildPxbBlobWithValues(cv1Values));

            // Act
            DoubleDataValue[] result = await connector.ReadDataAsync(fileRef, targetMap, metadata);

            // Assert: content is first dim → cv0 fills indices 0-7, cv1 fills indices 8-15
            Assert.That(result, Has.Length.EqualTo(16));
            using (Assert.EnterMultipleScope())
            {
                for (int i = 0; i < 8; i++)
                {
                    Assert.That(result[i].UnsafeValue, Is.EqualTo(cv0Values[i]), $"result[{i}] (cv0)");
                    Assert.That(result[8 + i].UnsafeValue, Is.EqualTo(cv1Values[i]), $"result[{8 + i}] (cv1)");
                }
            }
        }

        [Test]
        public void ReadDataAsync_WhenOnlySecondContentBlobMissing_ThrowsBinaryBlobSynchronizationException()
        {
            // Arrange: provide only the first content value blob, omit the second
            MatrixMetadata metadata = TestMockMetaBuilder.GetMockMetadata();
            ContentDimension contentDim = metadata.GetContentDimension();
            DateTime lastUpdated = contentDim.Values.Map(v => v.LastUpdated).Max();
            string timestamp = lastUpdated.ToString("yyyyMMddHHmm");

            MatrixMap targetMap = new([.. metadata.Dimensions.Select(d =>
                new DimensionMap(d.Code, [.. d.Values.Select(v => v.Code)]))]);

            TestableBinaryBlobConnector connector = new(_dbRef, _loggerMock.Object, []);
            PxFileRef fileRef = PxFileRef.ValidateAndCreate("table1", _dbRef, ["statisticalProgram"]);

            IMatrixMap collapsedMap = metadata.CollapseDimension(contentDim.Code, contentDim.Values[0].Code);
            int collapsedSize = (int)collapsedMap.GetSize();
            string blob0Name = BinaryBlobDataBaseConnector.BuildDataBlobName(_dbRef.Id, fileRef.Id, contentDim.Values[0].Code, timestamp);
            connector.AddBlobContent(blob0Name, BuildPxbBlob(collapsedSize, BinaryValueCodecType.DoubleCodec));

            // Act & Assert
            Assert.ThrowsAsync<BinaryBlobSynchronizationException>(async () =>
                await connector.ReadDataAsync(fileRef, targetMap, metadata));
        }

        [Test]
        public async Task ReadDataAsync_LargeBlob_StreamingFromStartWithStartIndexZero_ReturnsCorrectValues()
        {
            // Arrange: blob exceeding SmallThreshold (100 with test config)
            // content(1) × dim1(5) × dim2(5) × dim3(5) = 625 per blob
            // Requesting a subset → streaming from start (startIndex == 0)
            MatrixMetadata metadata = BuildLargeContentMetadata(
                contentValueCount: 1,
                otherDimSizes: [5, 5, 5, 5]);

            ContentDimension contentDim = metadata.GetContentDimension();
            DateTime lastUpdated = contentDim.Values.Map(v => v.LastUpdated).Max();
            string timestamp = lastUpdated.ToString("yyyyMMddHHmm");

            // Target selects first 3 values from each dimension
            MatrixMap targetMap = new([
                new DimensionMap(contentDim.Code, [contentDim.Values[0].Code]),
                .. metadata.Dimensions.Skip(1).Select(d =>
                    new DimensionMap(d.Code, [.. d.Values.Take(3).Select(v => v.Code)]))
            ]);

            TestableBinaryBlobConnector connector = new(_dbRef, _loggerMock.Object, []);
            PxFileRef fileRef = PxFileRef.ValidateAndCreate("table1", _dbRef, ["statisticalProgram"]);

            IMatrixMap collapsedMap = metadata.CollapseDimension(contentDim.Code, contentDim.Values[0].Code);
            int collapsedSize = (int)collapsedMap.GetSize();

            string blobName = BinaryBlobDataBaseConnector.BuildDataBlobName(_dbRef.Id, fileRef.Id, contentDim.Values[0].Code, timestamp);
            connector.AddBlobContent(blobName, BuildPxbBlob(collapsedSize, BinaryValueCodecType.DoubleCodec));

            // Act
            DoubleDataValue[] result = await connector.ReadDataAsync(fileRef, targetMap, metadata);

            // Assert: target size = 3^4 = 81, first value is at linear index 0 in the blob
            int expectedSize = (int)targetMap.GetSize();
            Assert.That(result, Has.Length.EqualTo(expectedSize));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result[0].UnsafeValue, Is.EqualTo(1.0));
                Assert.That(result[1].UnsafeValue, Is.EqualTo(2.0));
                Assert.That(result[^1].UnsafeValue, Is.EqualTo(313.0));
            }
        }

        [Test]
        public async Task ReadDataAsync_LargeBlob_StreamingWithStartIndexGreaterThanZero_ReturnsCorrectValues()
        {
            // Arrange: blob shape after content collapse: dim1(2) × dim2(25) × dim3(25) = 1,250
            // Selecting only the last dim1 value: startIndex = 625 > SmallThreshold (100)
            // readSpan = 625 >= MaxWindowedReadSize (500) → streaming with startIndex > 0
            const int dim1Size = 2;
            const int dim2Size = 25;
            const int dim3Size = 25;

            MatrixMetadata metadata = BuildLargeContentMetadata(
                contentValueCount: 1,
                otherDimSizes: [dim1Size, dim2Size, dim3Size]);

            ContentDimension contentDim = metadata.GetContentDimension();
            DateTime lastUpdated = contentDim.Values.Map(v => v.LastUpdated).Max();
            string timestamp = lastUpdated.ToString("yyyyMMddHHmm");

            // Select last dim1 value and first 5 values from other dims → result = 5 × 5 = 25
            MatrixMap targetMap = new([
                new DimensionMap(contentDim.Code, [contentDim.Values[0].Code]),
                new DimensionMap(metadata.Dimensions[1].Code, [metadata.Dimensions[1].Values[^1].Code]),
                .. metadata.Dimensions.Skip(2).Select(d =>
                    new DimensionMap(d.Code, [.. d.Values.Take(5).Select(v => v.Code)]))
            ]);

            TestableBinaryBlobConnector connector = new(_dbRef, _loggerMock.Object, []);
            PxFileRef fileRef = PxFileRef.ValidateAndCreate("table1", _dbRef, ["statisticalProgram"]);

            IMatrixMap collapsedBlobMap = metadata.CollapseDimension(contentDim.Code, contentDim.Values[0].Code);
            int fullBlobSize = (int)collapsedBlobMap.GetSize();

            string blobName = BinaryBlobDataBaseConnector.BuildDataBlobName(_dbRef.Id, fileRef.Id, contentDim.Values[0].Code, timestamp);
            connector.AddBlobContent(blobName, BuildPxbBlob(fullBlobSize, BinaryValueCodecType.DoubleCodec));

            // Act
            DoubleDataValue[] result = await connector.ReadDataAsync(fileRef, targetMap, metadata);

            // Assert
            int expectedReadSize = (int)targetMap.GetSize();
            long sliceSize = (long)dim2Size * dim3Size;
            Assert.That(result, Has.Length.EqualTo(expectedReadSize));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result[0].UnsafeValue, Is.EqualTo((double)(sliceSize + 1)));
                Assert.That(result[1].UnsafeValue, Is.EqualTo((double)(sliceSize + 2)));
                Assert.That(result[^1].UnsafeValue, Is.EqualTo((double)(sliceSize + 4 * 25 + 5)));
                Assert.That(connector.LastDownloadedBlobOffset, Is.GreaterThan(0));
            }
        }

        [Test]
        public async Task ReadDataAsync_WindowedRead_ReturnsCorrectValues()
        {
            // Arrange: blob with a dense small-span selection → windowed read
            // blob shape: dim1(50) × dim2(10) × dim3(10) = 5,000
            // Selecting dim1 vals 25..28 (4 values), all dim2 and dim3
            // first linear index = 25 × 100 = 2,500 > SmallThreshold (100)
            // readSpan = (28 - 25) × 100 + 99 = 399 < MaxWindowedReadSize (500) → windowed
            const int dim1Size = 50;
            const int dim2Size = 10;
            const int dim3Size = 10;

            MatrixMetadata metadata = BuildLargeContentMetadata(
                contentValueCount: 1,
                otherDimSizes: [dim1Size, dim2Size, dim3Size]);

            ContentDimension contentDim = metadata.GetContentDimension();
            DateTime lastUpdated = contentDim.Values.Map(v => v.LastUpdated).Max();
            string timestamp = lastUpdated.ToString("yyyyMMddHHmm");

            // Build the dim1 selection range: indices 25..28 (4 values)
            List<string> dim1Selection = [];
            for (int i = 25; i <= 28; i++)
            {
                dim1Selection.Add(metadata.Dimensions[1].Values[i].Code);
            }

            MatrixMap targetMap = new([
                new DimensionMap(contentDim.Code, [contentDim.Values[0].Code]),
                new DimensionMap(metadata.Dimensions[1].Code, dim1Selection),
                new DimensionMap(metadata.Dimensions[2].Code, [.. metadata.Dimensions[2].Values.Select(v => v.Code)]),
                new DimensionMap(metadata.Dimensions[3].Code, [.. metadata.Dimensions[3].Values.Select(v => v.Code)])
            ]);

            TestableBinaryBlobConnector connector = new(_dbRef, _loggerMock.Object, []);
            PxFileRef fileRef = PxFileRef.ValidateAndCreate("table1", _dbRef, ["statisticalProgram"]);

            IMatrixMap collapsedBlobMap = metadata.CollapseDimension(contentDim.Code, contentDim.Values[0].Code);
            int fullBlobSize = (int)collapsedBlobMap.GetSize();

            string blobName = BinaryBlobDataBaseConnector.BuildDataBlobName(_dbRef.Id, fileRef.Id, contentDim.Values[0].Code, timestamp);
            connector.AddBlobContent(blobName, BuildPxbBlob(fullBlobSize, BinaryValueCodecType.DoubleCodec));

            // Act
            DoubleDataValue[] result = await connector.ReadDataAsync(fileRef, targetMap, metadata);

            // Assert: 4 × 10 × 10 = 400
            int expectedSize = (int)targetMap.GetSize();
            long dim1Stride = (long)dim2Size * dim3Size;
            long firstLinearIndex = 25L * dim1Stride;
            long lastLinearIndex = 28L * dim1Stride + dim1Stride - 1;
            Assert.That(result, Has.Length.EqualTo(expectedSize));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result[0].UnsafeValue, Is.EqualTo((double)(firstLinearIndex + 1)));
                Assert.That(result[1].UnsafeValue, Is.EqualTo((double)(firstLinearIndex + 2)));
                Assert.That(result[^1].UnsafeValue, Is.EqualTo((double)(lastLinearIndex + 1)));
            }
        }

        #endregion

        #region Helpers

        private static byte[] BuildPxbBlob(int valueCount, BinaryValueCodecType codec)
        {
            const uint headerLength = 8u;
            const int doubleByteCount = 8;

            int totalSize = (int)headerLength + valueCount * doubleByteCount;
            byte[] buffer = new byte[totalSize];

            BitConverter.GetBytes(headerLength).CopyTo(buffer, 0);
            BitConverter.GetBytes((uint)codec).CopyTo(buffer, 4);

            for (int i = 0; i < valueCount; i++)
            {
                int offset = (int)headerLength + i * doubleByteCount;
                BitConverter.GetBytes((double)(i + 1)).CopyTo(buffer, offset);
            }

            return buffer;
        }

        private static byte[] BuildPxbBlobWithValues(double[] values)
        {
            const uint headerLength = 8u;
            const int doubleByteCount = 8;

            int totalSize = (int)headerLength + values.Length * doubleByteCount;
            byte[] buffer = new byte[totalSize];

            BitConverter.GetBytes(headerLength).CopyTo(buffer, 0);
            BitConverter.GetBytes((uint)BinaryValueCodecType.DoubleCodec).CopyTo(buffer, 4);

            for (int i = 0; i < values.Length; i++)
            {
                int offset = (int)headerLength + i * doubleByteCount;
                BitConverter.GetBytes(values[i]).CopyTo(buffer, offset);
            }

            return buffer;
        }

        private static MatrixMetadata BuildLargeContentMetadata(int contentValueCount, int[] otherDimSizes)
        {
            ContentDimension contentDim = TestMockMetaBuilder.GetMockContentDimension($"c{contentValueCount}");

            // For single content value tests, rebuild with only one value
            if (contentValueCount == 1)
            {
                ContentDimensionValue singleValue = TestMockMetaBuilder.GetMockContentValue("c1-value0");
                contentDim = new ContentDimension(
                    contentDim.Code, contentDim.Name, contentDim.AdditionalProperties, [singleValue]);
            }

            List<Dimension> dimensions = [contentDim];
            for (int d = 0; d < otherDimSizes.Length; d++)
            {
                string dimIdentifier = $"dim{d + 1}";
                List<DimensionValue> dimValues = [];
                for (int v = 0; v < otherDimSizes[d]; v++)
                {
                    dimValues.Add(TestMockMetaBuilder.GetMockDimensionValue($"{dimIdentifier}-val{v}"));
                }

                DimensionType type = d == 0 ? DimensionType.Time : DimensionType.Other;
                dimensions.Add(TestMockMetaBuilder.GetMockDimension(dimIdentifier, type) is Dimension template
                    ? new Dimension($"{dimIdentifier}-code", template.Name, template.AdditionalProperties, dimValues, type)
                    : new Dimension($"{dimIdentifier}-code", new Px.Utils.Language.MultilanguageString([new("fi", dimIdentifier)]), [], dimValues, type));
            }

            return new MatrixMetadata("fi", ["fi", "sv", "en"], dimensions, []);
        }

        #endregion

        private class TestableBinaryBlobConnector : BinaryBlobDataBaseConnector
        {
            private readonly Dictionary<string, byte[]> _blobContents = [];
            private readonly List<string> _blobNames;

            internal string? LastOpenedBlobName { get; private set; }
            internal long? LastOpenedBlobPosition { get; private set; }
            internal long? LastDownloadedBlobOffset { get; private set; }
            internal bool UseShortFormNamesValue => UseShortFormNames;

            internal TestableBinaryBlobConnector(DataBaseRef db, ILogger<BinaryBlobDataBaseConnector> logger, List<string> blobNames)
                : base(db, "test-container", null!, logger)
            {
                _blobNames = blobNames;
            }

            internal void AddBlobContent(string name, byte[] content)
            {
                _blobContents[name] = content;
                if (!_blobNames.Contains(name))
                {
                    _blobNames.Add(name);
                }
            }

            internal override Task<IReadOnlyList<string>> GetBlobItemsAsync(string prefix, CancellationToken ct = default)
            {
                IReadOnlyList<string> result = [.. _blobNames.Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))];
                return Task.FromResult(result);
            }

            internal override Task<bool> BlobExistsAsync(string blobName, CancellationToken ct = default)
            {
                return Task.FromResult(_blobContents.ContainsKey(blobName));
            }

            internal override Task<Stream> OpenBlobReadStreamAsync(string blobName, CancellationToken ct = default)
            {
                LastOpenedBlobName = blobName;
                LastOpenedBlobPosition = 0;
                return Task.FromResult<Stream>(new MemoryStream(_blobContents[blobName]));
            }

            internal override Task<Stream> OpenBlobReadStreamAsync(string blobName, long position, CancellationToken ct = default)
            {
                LastOpenedBlobName = blobName;
                LastOpenedBlobPosition = position;
                MemoryStream ms = new(_blobContents[blobName])
                {
                    Position = position
                };
                return Task.FromResult<Stream>(ms);
            }

            internal override Task<Stream> DownloadBlobRangeAsync(string blobName, long offset, long length, CancellationToken ct = default)
            {
                LastDownloadedBlobOffset = offset;
                byte[] slice = _blobContents[blobName].AsSpan((int)offset, (int)length).ToArray();
                return Task.FromResult<Stream>(new MemoryStream(slice));
            }
        }
    }
}
