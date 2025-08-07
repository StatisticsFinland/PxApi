using Px.Utils.Language;
using Px.Utils.Models.Metadata;
using Px.Utils.Models.Metadata.Dimensions;
using Px.Utils.Models.Metadata.Enums;
using PxApi.Models.QueryFilters;

namespace PxApi.UnitTests.Models.QueryFilters
{
    [TestFixture]
    internal class FilterDimensionsTest
    {
        [Test]
        public void FilterDimensions_WithOneFilterPerDimension_ReturnsFilteredDimensions()
        {
            // Arrange
            IReadOnlyMatrixMetadata meta = CreateMetadata([4, 4, 4], ["fi", "en"]);
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
                Assert.That(filtered.DimensionMaps[2].ValueCodes, Is.EqualTo(new List<string>(){ "dim2-val2", "dim2-val3"}));
            });
        }

        [Test]
        public void FilterDimensions_WithNoFilters_ReturnsAllDimensions()
        {
            // Arrange
            IReadOnlyMatrixMetadata meta = CreateMetadata([4, 4, 4], ["fi", "en"]);
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
            IReadOnlyMatrixMetadata meta = CreateMetadata([4], ["fi", "en"]);
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
            IReadOnlyMatrixMetadata meta = CreateMetadata([4], ["fi", "en"]);
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

        private static IReadOnlyMatrixMetadata CreateMetadata(int[] valueAmounts, string[] languages)
        {
            List<Dimension> dimensions = [];
            for (int i = 0; i < valueAmounts.Length; i++)
            {
                dimensions.Add(CreateDimension(i, valueAmounts[i], languages));
            }

            return new MatrixMetadata(
                languages[0],
                languages,
                dimensions,
                []
            );
        }

        private static Dimension CreateDimension(int index, int valuesCount, string[] languages)
        {
            string code = $"dim{index}";

            DimensionType type = index switch
            {
                0 => DimensionType.Content,
                1 => DimensionType.Time,
                _ => DimensionType.Other
            };

            return new Dimension(
                code,
                CreateMultilanguageString(code, languages),
                [],
                CreateDimensionValues(code, valuesCount, languages),
                type  
            );
        }

        private static ValueList CreateDimensionValues(string dimensionName, int amount, string[] languages)
        {
            List<DimensionValue> values = [];
            for (int i = 0; i < amount; i++)
            {
                values.Add(CreateDimensionValue(dimensionName, i, languages));
            }
            return new(values);
        }

        private static DimensionValue CreateDimensionValue(string dimensionName, int index, string[] languages)
        {
            string code = $"{dimensionName}-val{index}";
            return new DimensionValue(
                code,
                CreateMultilanguageString(code, languages)
            );
        }

        private static MultilanguageString CreateMultilanguageString(string text, string[] languages)
        {
            Dictionary<string, string> langsAndTranslations = [];
            foreach (string lang in languages)
            {
                langsAndTranslations.Add(lang, $"{text}.{lang}");
            }
            return new(langsAndTranslations);
        }
    }
}
