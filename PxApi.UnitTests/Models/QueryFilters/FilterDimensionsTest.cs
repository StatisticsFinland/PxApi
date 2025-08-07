using Px.Utils.Models.Metadata;
using PxApi.Models.QueryFilters;
using PxApi.UnitTests.Utils;

namespace PxApi.UnitTests.Models.QueryFilters
{
    [TestFixture]
    internal class FilterDimensionsTest
    {
        [Test]
        public void FilterDimensions_WithOneFilterPerDimension_ReturnsFilteredDimensions()
        {
            // Arrange
            IReadOnlyMatrixMetadata meta = MatrixMetadataUtils.CreateMetadata([4, 4, 4], ["fi", "en"]);
            Dictionary<string, List<IFilter>> filters = new()
            {
                { "dim0", new List<IFilter> { new CodeFilter(["dim0-val1"]) } },
                { "dim1", new List<IFilter> { new AllFilter() } },
                { "dim2", new List<IFilter> { new FromFilter("dim2-val2")} }
            };

            // Act
            MatrixMap filtered = FilterUtils.FilterDimensions(meta, filters);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(filtered.DimensionMaps[0].ValueCodes, Has.Count.EqualTo(1));
                Assert.That(filtered.DimensionMaps[0].ValueCodes[0], Is.EqualTo("dim0-val1"));
                Assert.That(filtered.DimensionMaps[1].ValueCodes, Has.Count.EqualTo(4));
                Assert.That(filtered.DimensionMaps[2].ValueCodes, Has.Count.EqualTo(2));
                Assert.That(filtered.DimensionMaps[2].ValueCodes, Is.EqualTo(new List<string>() { "dim2-val2", "dim2-val3" }));
            });
        }

        [Test]
        public void FilterDimensions_WithNoFilters_ReturnsAllDimensions()
        {
            // Arrange
            IReadOnlyMatrixMetadata meta = MatrixMetadataUtils.CreateMetadata([4, 4, 4], ["fi", "en"]);
            Dictionary<string, List<IFilter>> filters = [];
            // Act
            MatrixMap filtered = FilterUtils.FilterDimensions(meta, filters);
            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(filtered.DimensionMaps[0].ValueCodes, Has.Count.EqualTo(4));
                Assert.That(filtered.DimensionMaps[1].ValueCodes, Has.Count.EqualTo(4));
                Assert.That(filtered.DimensionMaps[2].ValueCodes, Has.Count.EqualTo(4));
            });
        }

        [Test]
        public void FilterDimension_WithMultipleFilters_ReturnsFilteredDimensions()
        {
            // Arrange
            IReadOnlyMatrixMetadata meta = MatrixMetadataUtils.CreateMetadata([4], ["fi", "en"]);
            Dictionary<string, List<IFilter>> filters = new()
            {
                { "dim0", new List<IFilter> {
                    new FromFilter("dim0-val1"), // Removes first value "dim0-val0"
                    new ToFilter() {FilterString = "dim0-val2" } } // Excludes last value "dim0-val3"
                }
            };

            // Act
            MatrixMap filtered = FilterUtils.FilterDimensions(meta, filters);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(filtered.DimensionMaps[0].ValueCodes, Has.Count.EqualTo(2));
                Assert.That(filtered.DimensionMaps[0].ValueCodes, Is.EqualTo(new List<string>() { "dim0-val1", "dim0-val2" }));
            });
        }

        [Test]
        public void FilterDimension_WithConflictingFilters_ReturnsFilteredDimensions()
        {
            // Arrange
            IReadOnlyMatrixMetadata meta = MatrixMetadataUtils.CreateMetadata([4], ["fi", "en"]);
            Dictionary<string, List<IFilter>> filters = new()
            {
                { "dim0", new List<IFilter> {
                    new FromFilter("dim0-val1"), // Removes first value "dim0-val0"
                    new CodeFilter(["dim0-val0"]) // Keeps only "dim0-val0", which is already filtered out
                } }
            };

            // Act
            MatrixMap filtered = FilterUtils.FilterDimensions(meta, filters);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(filtered.DimensionMaps[0].ValueCodes, Has.Count.EqualTo(0));
            });
        }
    }
}
