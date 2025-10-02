using Px.Utils.Models.Metadata;
using Px.Utils.Models.Metadata.Dimensions;
using Px.Utils.Models.Metadata.MetaProperties;
using PxApi.Models.QueryFilters;
using PxApi.UnitTests.Utils;

namespace PxApi.UnitTests.Models.QueryFilters
{
    [TestFixture]
    internal class MetaFilteringTests
    {
        [Test]
        public void ApplyToMatrixMeta_WithFiltersForAllDimensions_ReturnsFilteredDimensions()
        {
            // Arrange
            IReadOnlyMatrixMetadata meta = MatrixMetadataUtils.CreateMetadata([4, 4, 4], ["fi", "en"]);
            Dictionary<string, Filter> filters = new()
            {
                { "dim0", new CodeFilter(["dim0-val1"]) },
                { "dim1", new CodeFilter(["*"]) },
                { "dim2", new FromFilter("dim2-val2") }
            };

            // Act
            MatrixMap filtered = MetaFiltering.ApplyToMatrixMeta(meta, filters);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(filtered.DimensionMaps[0].ValueCodes, Has.Count.EqualTo(1));
                Assert.That(filtered.DimensionMaps[0].ValueCodes[0], Is.EqualTo("dim0-val1"));
                Assert.That(filtered.DimensionMaps[1].ValueCodes, Has.Count.EqualTo(4));
                Assert.That(filtered.DimensionMaps[2].ValueCodes, Has.Count.EqualTo(2));
                Assert.That(filtered.DimensionMaps[2].ValueCodes, Is.EquivalentTo(new List<string> { "dim2-val2", "dim2-val3" }));
            });
        }

        [Test]
        public void ApplyToMatrixMeta_WithNoFilters_AppliesDefaultFiltering()
        {
            // Arrange
            IReadOnlyMatrixMetadata meta = MatrixMetadataUtils.CreateMetadata([4, 4, 4], ["fi", "en"]);
            Dictionary<string, Filter> filters = [];

            // Act
            MatrixMap filtered = MetaFiltering.ApplyToMatrixMeta(meta, filters);

            // Assert
            Assert.Multiple(() =>
            {
                // Content dimension (dim0) - should use FirstFilter(1)
                Assert.That(filtered.DimensionMaps[0].ValueCodes, Has.Count.EqualTo(1));
                Assert.That(filtered.DimensionMaps[0].ValueCodes[0], Is.EqualTo("dim0-val0"));
                // Time dimension (dim1) - should use CodeFilter(["*"])
                Assert.That(filtered.DimensionMaps[1].ValueCodes, Has.Count.EqualTo(4));
                // Other dimension (dim2) - should use FirstFilter(1)
                Assert.That(filtered.DimensionMaps[2].ValueCodes, Has.Count.EqualTo(1));
                Assert.That(filtered.DimensionMaps[2].ValueCodes[0], Is.EqualTo("dim2-val0"));
            });
        }

        [Test]
        public void ApplyToMatrixMeta_WithMixOfFiltersAndNoFilters_ReturnsCorrectDimensions()
        {
            // Arrange
            IReadOnlyMatrixMetadata meta = MatrixMetadataUtils.CreateMetadata([4, 25, 4], ["fi", "en"]);
            Dictionary<string, Filter> filters = new()
            {
                { "dim0", new CodeFilter(["dim0-val2"]) }
                // No filter for dim1 (time dimension) - should apply CodeFilter(["*"])
                // No filter for dim2 (other dimension) - should apply FirstFilter(1)
            };

            // Act
            MatrixMap filtered = MetaFiltering.ApplyToMatrixMeta(meta, filters);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(filtered.DimensionMaps[0].ValueCodes, Has.Count.EqualTo(1));
                Assert.That(filtered.DimensionMaps[0].ValueCodes[0], Is.EqualTo("dim0-val2"));
                Assert.That(filtered.DimensionMaps[1].ValueCodes, Has.Count.EqualTo(25)); // Default for Time - all values
                Assert.That(filtered.DimensionMaps[2].ValueCodes, Has.Count.EqualTo(1)); // Default for Other - first value
                Assert.That(filtered.DimensionMaps[2].ValueCodes[0], Is.EqualTo("dim2-val0"));
            });
        }

        [Test]
        public void ApplyToMatrixMeta_WithEliminationProperty_UsesEliminationValue()
        {
            // Arrange
            // Create metadata with an "ELIMINATION" property on one dimension
            IReadOnlyMatrixMetadata meta = CreateMetadataWithElimination();
            Dictionary<string, Filter> filters = [];

            // Act
            MatrixMap filtered = MetaFiltering.ApplyToMatrixMeta(meta, filters);

            // Assert
            Assert.Multiple(() =>
            {
                // The dimension with ELIMINATION should only have one value
                Assert.That(filtered.DimensionMaps[0].ValueCodes, Has.Count.EqualTo(1));
                Assert.That(filtered.DimensionMaps[0].ValueCodes[0], Is.EqualTo("dim0-val1")); // The elimination value
            });
        }

        [Test]
        public void GetDefaultFilteringUrlParameters_WithBasicMetadata_ReturnsExpectedUrlParameters()
        {
            // Arrange
            IReadOnlyMatrixMetadata meta = MatrixMetadataUtils.CreateMetadata([3, 5, 2], ["fi", "en"]);

            // Act
            string result = MetaFiltering.GetDefaultFilteringUrlParameters(meta);

            // Assert
            // Expected: Content dimension uses first=1, Time dimension uses code=*, Other dimension uses first=1
            string expected = "?filters=dim0:first=1&filters=dim1:code=*&filters=dim2:first=1";
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void GetDefaultFilteringUrlParameters_WithEliminationDimension_ReturnsEliminationCode()
        {
            // Arrange
            IReadOnlyMatrixMetadata meta = CreateMetadataWithElimination();

            // Act
            string result = MetaFiltering.GetDefaultFilteringUrlParameters(meta);

            // Assert
            // Expected: Dimension with elimination uses the elimination value
            string expected = "?filters=dim0:code=dim0-val1";
            Assert.That(result, Is.EqualTo(expected));
        }

        private static MatrixMetadata CreateMetadataWithElimination()
        {
            Dimension dimension = MatrixMetadataUtils.CreateDimension(0, 3, ["fi", "en"]);

            // Add ELIMINATION property pointing to "dim0-val1"
            Dictionary<string, MetaProperty> additionalProps = new()
            {
                { "ELIMINATION", new StringProperty("dim0-val1") }
            };

            Dimension dimensionWithElimination = new(
                dimension.Code,
                dimension.Name,
                additionalProps,
                dimension.Values,
                dimension.Type
            );

            return new MatrixMetadata(
                "fi",
                ["fi", "en"],
                [dimensionWithElimination],
                []
            );
        }
    }
}
