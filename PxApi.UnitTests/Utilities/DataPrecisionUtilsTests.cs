using Px.Utils.Models.Data;
using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Metadata;
using Px.Utils.Models.Metadata.Dimensions;
using PxApi.UnitTests.ModelBuilderTests;
using PxApi.Utilities;

namespace PxApi.UnitTests.Utilities
{
    [TestFixture]
    public class DataPrecisionUtilsTests
    {
        [Test]
        public void ApplyContentPrecision_SingleContentValueWithZeroPrecision_RoundsAllExistingValues()
        {
            MatrixMetadata baseMetadata = TestMockMetaBuilder.GetMockMetadata();
            ContentDimension contentDimension = TestMockMetaBuilder.GetMockContentDimension("content", [0]);
            ContentDimension singleValueContentDimension = new(
                contentDimension.Code,
                contentDimension.Name,
                contentDimension.AdditionalProperties,
                [contentDimension.Values[0]]);

            TimeDimension timeDimension = (TimeDimension)baseMetadata.Dimensions[1];
            List<Dimension> dimensions =
            [
                singleValueContentDimension,
                timeDimension
            ];
            MatrixMetadata metadata = CreateMetadata(baseMetadata, dimensions);

            DoubleDataValue[] data =
            [
                new DoubleDataValue(1.4, DataValueType.Exists),
                new DoubleDataValue(1.6, DataValueType.Exists)
            ];

            DoubleDataValue[] result = DataPrecisionUtils.ApplyContentPrecision(data, metadata);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result[0].UnsafeValue, Is.EqualTo(1));
                Assert.That(result[1].UnsafeValue, Is.EqualTo(2));
            }
        }

        [Test]
        public void ApplyContentPrecision_TwoContentValuesWithDifferentPrecisions_AppliesMatchingPrecisionByIndex()
        {
            MatrixMetadata baseMetadata = TestMockMetaBuilder.GetMockMetadata();
            ContentDimension contentDimension = TestMockMetaBuilder.GetMockContentDimension("content", [0, 3]);
            TimeDimension timeDimension = (TimeDimension)baseMetadata.Dimensions[1];
            List<Dimension> dimensions =
            [
                contentDimension,
                timeDimension
            ];
            MatrixMetadata metadata = CreateMetadata(baseMetadata, dimensions);

            DoubleDataValue[] data =
            [
                new DoubleDataValue(1.4, DataValueType.Exists),
                new DoubleDataValue(1.6, DataValueType.Exists),
                new DoubleDataValue(2.4444, DataValueType.Exists),
                new DoubleDataValue(2.4445, DataValueType.Exists)
            ];

            DoubleDataValue[] result = DataPrecisionUtils.ApplyContentPrecision(data, metadata);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result[0].UnsafeValue, Is.EqualTo(1));
                Assert.That(result[1].UnsafeValue, Is.EqualTo(2));
                Assert.That(result[2].UnsafeValue, Is.EqualTo(2.444));
                Assert.That(result[3].UnsafeValue, Is.EqualTo(2.445));
            }
        }

        [Test]
        public void ApplyContentPrecision_MissingValues_DoesNotModifyNonExistingValues()
        {
            MatrixMetadata baseMetadata = TestMockMetaBuilder.GetMockMetadata();
            ContentDimension contentDimension = TestMockMetaBuilder.GetMockContentDimension("content", [0, 2]);
            TimeDimension timeDimension = (TimeDimension)baseMetadata.Dimensions[1];
            List<Dimension> dimensions =
            [
                contentDimension,
                timeDimension
            ];
            MatrixMetadata metadata = CreateMetadata(baseMetadata, dimensions);

            DoubleDataValue[] data =
            [
                new DoubleDataValue(1.4, DataValueType.Exists),
                new DoubleDataValue(0.0, DataValueType.Missing),
                new DoubleDataValue(2.4444, DataValueType.Exists),
                new DoubleDataValue(99.999, DataValueType.Confidential)
            ];

            DoubleDataValue[] result = DataPrecisionUtils.ApplyContentPrecision(data, metadata);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result[0].UnsafeValue, Is.EqualTo(1));
                Assert.That(result[1].Type, Is.EqualTo(DataValueType.Missing));
                Assert.That(result[1].UnsafeValue, Is.EqualTo(0.0));
                Assert.That(result[2].UnsafeValue, Is.EqualTo(2.44));
                Assert.That(result[3].Type, Is.EqualTo(DataValueType.Confidential));
                Assert.That(result[3].UnsafeValue, Is.EqualTo(99.999));
            }
        }

        [Test]
        public void ApplyContentPrecision_ContentDimensionNotFirst_MapsPrecisionCorrectly()
        {
            MatrixMetadata baseMetadata = TestMockMetaBuilder.GetMockMetadata();
            TimeDimension timeDimension = (TimeDimension)baseMetadata.Dimensions[1];
            ContentDimension contentDimension = TestMockMetaBuilder.GetMockContentDimension("content", [0, 2]);
            Dimension dim0 = baseMetadata.Dimensions[2];
            List<Dimension> dimensions =
            [
                timeDimension,
                contentDimension,
                dim0
            ];
            MatrixMetadata metadata = CreateMetadata(baseMetadata, dimensions);

            DoubleDataValue[] data =
            [
                new DoubleDataValue(1.4, DataValueType.Exists),
                new DoubleDataValue(1.6, DataValueType.Exists),
                new DoubleDataValue(2.4444, DataValueType.Exists),
                new DoubleDataValue(2.4455, DataValueType.Exists),
                new DoubleDataValue(3.4, DataValueType.Exists),
                new DoubleDataValue(3.6, DataValueType.Exists),
                new DoubleDataValue(4.4444, DataValueType.Exists),
                new DoubleDataValue(4.4455, DataValueType.Exists)
            ];

            DoubleDataValue[] result = DataPrecisionUtils.ApplyContentPrecision(data, metadata);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result[0].UnsafeValue, Is.EqualTo(1));
                Assert.That(result[1].UnsafeValue, Is.EqualTo(2));
                Assert.That(result[2].UnsafeValue, Is.EqualTo(2.44));
                Assert.That(result[3].UnsafeValue, Is.EqualTo(2.45));
                Assert.That(result[4].UnsafeValue, Is.EqualTo(3));
                Assert.That(result[5].UnsafeValue, Is.EqualTo(4));
                Assert.That(result[6].UnsafeValue, Is.EqualTo(4.44));
                Assert.That(result[7].UnsafeValue, Is.EqualTo(4.45));
            }
        }

        [Test]
        public void ApplyContentPrecision_SingleContentValue_UsesSamePrecisionForAllDataPoints()
        {
            MatrixMetadata baseMetadata = TestMockMetaBuilder.GetMockMetadata();
            ContentDimension contentDimension = TestMockMetaBuilder.GetMockContentDimension("content", [3]);
            ContentDimension singleValueContentDimension = new(
                contentDimension.Code,
                contentDimension.Name,
                contentDimension.AdditionalProperties,
                [contentDimension.Values[0]]);

            TimeDimension timeDimension = (TimeDimension)baseMetadata.Dimensions[1];
            Dimension dim0 = baseMetadata.Dimensions[2];
            List<Dimension> dimensions =
            [
                singleValueContentDimension,
                timeDimension,
                dim0
            ];
            MatrixMetadata metadata = CreateMetadata(baseMetadata, dimensions);

            DoubleDataValue[] data =
            [
                new DoubleDataValue(1.1114, DataValueType.Exists),
                new DoubleDataValue(1.1115, DataValueType.Exists),
                new DoubleDataValue(2.2224, DataValueType.Exists),
                new DoubleDataValue(2.2225, DataValueType.Exists)
            ];

            DoubleDataValue[] result = DataPrecisionUtils.ApplyContentPrecision(data, metadata);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result[0].UnsafeValue, Is.EqualTo(1.111));
                Assert.That(result[1].UnsafeValue, Is.EqualTo(1.112));
                Assert.That(result[2].UnsafeValue, Is.EqualTo(2.222));
                Assert.That(result[3].UnsafeValue, Is.EqualTo(2.223));
            }
        }

        [Test]
        public void ApplyContentPrecision_ContentValuePrecisionExceedsDataPrecision_RoundsToHighestPossiblePrecision()
        {
            MatrixMetadata baseMetadata = TestMockMetaBuilder.GetMockMetadata();
            ContentDimension contentDimension = TestMockMetaBuilder.GetMockContentDimension("content", [3]); // Content precision is higher than data precision
            TimeDimension timeDimension = (TimeDimension)baseMetadata.Dimensions[1];
            List<Dimension> dimensions =
            [
                contentDimension,
                timeDimension
            ];
            MatrixMetadata metadata = CreateMetadata(baseMetadata, dimensions);
            DoubleDataValue[] data =
            [
                new DoubleDataValue(1.2, DataValueType.Exists),
                new DoubleDataValue(1.9, DataValueType.Exists)
            ];
            DoubleDataValue[] result = DataPrecisionUtils.ApplyContentPrecision(data, metadata);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result[0].UnsafeValue, Is.EqualTo(1.2));
                Assert.That(result[1].UnsafeValue, Is.EqualTo(1.9));
            }
        }

        [Test]
        public void ApplyContentPrecision_RoundsMiddleAwayFromZero()
        {
            MatrixMetadata metadata = TestMockMetaBuilder.GetMockMetadata();
            DoubleDataValue[] data =
            [
                new DoubleDataValue(1.555, DataValueType.Exists),
                new DoubleDataValue(2.555, DataValueType.Exists),
                new DoubleDataValue(-1.555, DataValueType.Exists),
                new DoubleDataValue(-2.555, DataValueType.Exists)
            ];

            DoubleDataValue[] result = DataPrecisionUtils.ApplyContentPrecision(data, metadata);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result[0].UnsafeValue, Is.EqualTo(1.56));
                Assert.That(result[1].UnsafeValue, Is.EqualTo(2.56));
                Assert.That(result[2].UnsafeValue, Is.EqualTo(-1.56));
                Assert.That(result[3].UnsafeValue, Is.EqualTo(-2.56));
            }
        }

        private static MatrixMetadata CreateMetadata(MatrixMetadata baseMetadata, List<Dimension> dimensions)
        {
            return new MatrixMetadata(
                baseMetadata.DefaultLanguage,
                baseMetadata.AvailableLanguages,
                dimensions,
                baseMetadata.AdditionalProperties);
        }
    }
}
