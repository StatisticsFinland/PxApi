using Px.Utils.Models.Metadata;
using Px.Utils.Models.Metadata.Dimensions;
using PxApi.Models;
using PxApi.UnitTests.ModelBuilderTests;
using PxApi.Utilities;

namespace PxApi.UnitTests.UtilitiesTests
{
    [TestFixture]
    public class TableSummaryBuilderTests
    {
        [Test]
        public void Build_ValidMetadata_ReturnsSummary()
        {
            // Arrange
            MatrixMetadata metadata = TestMockMetaBuilder.GetMockMetadata();

            // Act
            TableSummary summary = TableSummaryBuilder.Build(metadata, "table1", "en");

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(summary.TableId, Is.EqualTo("table-tableid"));
                Assert.That(summary.Title, Is.EqualTo("table-description.en"));
                Assert.That(summary.ContentValues, Has.Count.EqualTo(2));
                Assert.That(summary.TimeRange.From, Is.EqualTo("time-value0-name.en"));
                Assert.That(summary.TimeRange.To, Is.EqualTo("time-value1-name.en"));
                Assert.That(summary.Dimensions, Has.Count.EqualTo(2));
                Assert.That(summary.LastUpdated, Is.EqualTo(new DateTime(2024, 10, 10, 0, 0, 0, DateTimeKind.Utc)));
            }
        }

        [Test]
        public void Build_MetadataWithoutContentDimension_ThrowsInvalidOperationException()
        {
            // Arrange
            MatrixMetadata metadata = TestMockMetaBuilder.GetMockMetadata();
            List<Dimension> dimensionsWithoutContent = [.. metadata.Dimensions.Where(dimension => dimension is not ContentDimension)];
            MatrixMetadata metadataWithoutContent = new(
                metadata.DefaultLanguage,
                metadata.AvailableLanguages,
                dimensionsWithoutContent,
                metadata.AdditionalProperties);

            // Act & Assert
            Assert.That(() => TableSummaryBuilder.Build(metadataWithoutContent, "table1", "en"), Throws.InvalidOperationException);
        }

        [Test]
        public void Build_MetadataWithoutTimeDimension_ReturnsEmptyTimeRange()
        {
            // Arrange
            MatrixMetadata metadata = TestMockMetaBuilder.GetMockMetadata();
            List<Dimension> dimensionsWithoutTime = [.. metadata.Dimensions.Where(dimension => dimension is not TimeDimension)];
            MatrixMetadata metadataWithoutTime = new(
                metadata.DefaultLanguage,
                metadata.AvailableLanguages,
                dimensionsWithoutTime,
                metadata.AdditionalProperties);

            // Act
            TableSummary summary = TableSummaryBuilder.Build(metadataWithoutTime, "table1", "en");

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(summary.TimeRange.From, Is.EqualTo(string.Empty));
                Assert.That(summary.TimeRange.To, Is.EqualTo(string.Empty));
            }
        }
    }
}
