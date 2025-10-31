using Px.Utils.Language;
using Px.Utils.Models.Data;
using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Metadata;
using Px.Utils.Models.Metadata.Dimensions;
using Px.Utils.Models.Metadata.Enums;
using Px.Utils.Models.Metadata.MetaProperties;
using Px.Utils.Serializers.Json;
using PxApi.ModelBuilders;
using PxApi.UnitTests.Utils;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace PxApi.UnitTests.ModelBuilderTests
{
    [TestFixture]
    public class CsvBuilderTests
    {
        [Test]
        public void BuildCsvResponse_BasicMetadata_ReturnsValidCsv()
        {
            // Arrange
            IReadOnlyMatrixMetadata metadata = TestMockMetaBuilder.GetMockMetadata();
            
            DoubleDataValue[] data = [
                new DoubleDataValue(1.0, DataValueType.Exists),
                new DoubleDataValue(2.0, DataValueType.Exists),
                new DoubleDataValue(3.0, DataValueType.Exists),
                new DoubleDataValue(4.0, DataValueType.Exists),
                new DoubleDataValue(5.0, DataValueType.Exists),
                new DoubleDataValue(6.0, DataValueType.Exists),
                new DoubleDataValue(7.0, DataValueType.Exists),
                new DoubleDataValue(8.0, DataValueType.Exists),
                new DoubleDataValue(9.0, DataValueType.Exists),
                new DoubleDataValue(10.0, DataValueType.Exists),
                new DoubleDataValue(11.0, DataValueType.Exists),
                new DoubleDataValue(12.0, DataValueType.Exists),
                new DoubleDataValue(13.0, DataValueType.Exists),
                new DoubleDataValue(14.0, DataValueType.Exists),
                new DoubleDataValue(15.0, DataValueType.Exists),
                new DoubleDataValue(16.0, DataValueType.Exists)
            ];
            const string lang = "en";

            string expected =
                $"\"table-description.en\",\"time-value0-name.en\",\"time-value1-name.en\"{Environment.NewLine}" +
                $"\"content-value0-name.en dim0-value0-name.en dim1-value0-name.en\",1,2{Environment.NewLine}" +
                $"\"content-value0-name.en dim0-value0-name.en dim1-value1-name.en\",3,4{Environment.NewLine}" +
                $"\"content-value0-name.en dim0-value1-name.en dim1-value0-name.en\",5,6{Environment.NewLine}" +
                $"\"content-value0-name.en dim0-value1-name.en dim1-value1-name.en\",7,8{Environment.NewLine}" +
                $"\"content-value1-name.en dim0-value0-name.en dim1-value0-name.en\",9,10{Environment.NewLine}" +
                $"\"content-value1-name.en dim0-value0-name.en dim1-value1-name.en\",11,12{Environment.NewLine}" +
                $"\"content-value1-name.en dim0-value1-name.en dim1-value0-name.en\",13,14{Environment.NewLine}" +
                "\"content-value1-name.en dim0-value1-name.en dim1-value1-name.en\",15,16";

            // Act
            string result = CsvBuilder.BuildCsvResponse(metadata, data, lang, metadata);

            // Assert
            Assert.That(result, Is.Not.Null.And.Not.Empty);
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void BuildCsvResponse_WithFilteredMeta_ReturnsFilteredCsv()
        {
            // Arrange
            DoubleDataValue[] data = [
                new DoubleDataValue(1.0, DataValueType.Exists),
                new DoubleDataValue(2.0, DataValueType.Exists),
            ];

            MultilanguageString eliminationValueName = new(new Dictionary<string, string>()
            {
                { "en", "dim1-value0-name.en" },
                { "fi", "dim1-value0-name.fi" },
                { "sv", "dim1-value0-name.sv" }
            });
            MatrixMetadata completeMeta = TestMockMetaBuilder.GetMockMetadata(dimensionAdditionalProps: [[], [], [], new Dictionary<string, MetaProperty>() {
                { "ELIMINATION", new MultilanguageStringProperty(eliminationValueName) }
            }]); // Dimension1 with elimination value to omit name from csv

            ContentDimension singleValueContentDimension = new(
                completeMeta.Dimensions[0].Code,
                completeMeta.Dimensions[0].Name,
                completeMeta.Dimensions[0].AdditionalProperties,
                [
                    (ContentDimensionValue)completeMeta.Dimensions[0].Values[0] // Content dimension with only one value
                ]
            );

            completeMeta.Dimensions[0] = singleValueContentDimension; // Force single value content dimension

            Dimension filteredDimZero = new(
                completeMeta.Dimensions[2].Code,
                completeMeta.Dimensions[2].Name,
                completeMeta.Dimensions[2].AdditionalProperties,
                new ValueList(
                [
                    completeMeta.Dimensions[2].Values[0] // Note, only one of two dimensions
                ]),
                completeMeta.Dimensions[3].Type
            );

            Dimension filteredDimOne = new (
                completeMeta.Dimensions[3].Code,
                completeMeta.Dimensions[3].Name,
                completeMeta.Dimensions[3].AdditionalProperties,
                new ValueList(
                [
                    completeMeta.Dimensions[3].Values[0] // Note, only one of two dimensions (Elimination value)
                ]),
                completeMeta.Dimensions[3].Type
            );

            MatrixMetadata filteredMeta = new (
                completeMeta.DefaultLanguage,
                completeMeta.AvailableLanguages,
                [
                    completeMeta.Dimensions[0], // Content dimension
                    completeMeta.Dimensions[1], // Time dimension
                    filteredDimZero, // Filtered dimension with only one value
                    filteredDimOne // Filtered dimension with only one elimination value
                ],
                completeMeta.AdditionalProperties
            );

            string expected =
                $"\"table-description.en\",\"time-value0-name.en\",\"time-value1-name.en\"{Environment.NewLine}\"dim0-value0-name.en\",1,2";

            // Act
            string result = CsvBuilder.BuildCsvResponse(filteredMeta, data, "en", completeMeta);

            // Assert
            Assert.That(result, Is.Not.Null.And.Not.Empty);
            Assert.That(result, Is.EqualTo(expected)); // Should contain only filtered dimension names and values
        }

        [Test]
        public void BuildCsvResponse_MissingDescription_ThrowsInvalidOperationException()
        {
            // Arrange
            IReadOnlyMatrixMetadata metadata = CreateMetadataWithoutDescription();
            DoubleDataValue[] data = [new DoubleDataValue(1.0, DataValueType.Exists)];
            const string lang = "en";

            // Act & Assert
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            CsvBuilder.BuildCsvResponse(metadata, data, lang, metadata));

            Assert.That(exception.Message, Does.Contain("DESCRIPTION meta property is required for CSV export"));
        }

        [Test]
        public void BuildCsvResponse_MultipleHeadingDimensions_CreatesCorrectHeaderRow()
        {
            // Arrange
            IReadOnlyMatrixMetadata metadata = TestMockMetaBuilder.GetMockMetadata([DimensionType.Nominal, DimensionType.Ordinal]); // Two additional dimensions go to heading
            DoubleDataValue[] data = [
                new DoubleDataValue(1.0, DataValueType.Exists),
                new DoubleDataValue(2.0, DataValueType.Exists),
                new DoubleDataValue(3.0, DataValueType.Exists),
                new DoubleDataValue(4.0, DataValueType.Exists),
                new DoubleDataValue(5.0, DataValueType.Exists),
                new DoubleDataValue(6.0, DataValueType.Exists),
                new DoubleDataValue(7.0, DataValueType.Exists),
                new DoubleDataValue(8.0, DataValueType.Exists)
                ];
            const string lang = "en";

            // Act
            string result = CsvBuilder.BuildCsvResponse(metadata, data, lang, metadata);

            // Assert
            string[] lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Multiple(() =>
            {
                Assert.That(lines, Has.Length.EqualTo(9));
                Assert.That(lines[0], Does.Contain("table-description.en")); // Should have the table description
                Assert.That(lines[0], Does.Contain("time-value0-name.en dim2-value0-name.en dim3-value0-name.en"));
                Assert.That(lines[0], Does.Contain("time-value0-name.en dim2-value0-name.en dim3-value1-name.en"));
                Assert.That(lines[0], Does.Contain("time-value0-name.en dim2-value1-name.en dim3-value0-name.en"));
                Assert.That(lines[0], Does.Contain("time-value0-name.en dim2-value1-name.en dim3-value1-name.en"));
                Assert.That(lines[0], Does.Contain("time-value1-name.en dim2-value1-name.en dim3-value1-name.en"));
            });
        }

        [Test]
        public void BuildCsvResponse_MissingDataValues_HandlesCorrectly()
        {
            // Arrange
            IReadOnlyMatrixMetadata metadata = TestMockMetaBuilder.GetMockMetadata();
            DoubleDataValue[] data = [
                new DoubleDataValue(1.0, DataValueType.Exists),
                new DoubleDataValue(0.0, DataValueType.Missing),
                new DoubleDataValue(0.0, DataValueType.Confidential),
                new DoubleDataValue(4.0, DataValueType.Exists)
                ];
            const string lang = "en";

            // Act
            string result = CsvBuilder.BuildCsvResponse(metadata, data, lang, metadata);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Does.Contain("."));     // Missing value
                Assert.That(result, Does.Contain("..."));   // Confidential value  
                Assert.That(result, Does.Contain("1"));     // Existing value
                Assert.That(result, Does.Contain("4"));     // Existing value
            });
        }

        [Test]
        public void BuildCsvResponse_DifferentLanguages_ReturnsLocalizedHeaders()
        {
            // Arrange
            IReadOnlyMatrixMetadata metadata = TestMockMetaBuilder.GetMockMetadata();
            DoubleDataValue[] data = [
                new DoubleDataValue(1.0, DataValueType.Exists),
                new DoubleDataValue(2.0, DataValueType.Exists),
                new DoubleDataValue(3.0, DataValueType.Exists),
                new DoubleDataValue(4.0, DataValueType.Exists)
                ];

            // Act - Finnish
            string resultFi = CsvBuilder.BuildCsvResponse(metadata, data, "fi", metadata);
            // Act - English  
            string resultEn = CsvBuilder.BuildCsvResponse(metadata, data, "en", metadata);

            Assert.Multiple(() =>
            {
                // Assert
                Assert.That(resultFi, Does.Contain("table-description.fi"));
                Assert.That(resultEn, Does.Contain("table-description.en"));
                // Both should have the same structure but potentially different value names
                Assert.That(resultFi.Split('\n'), Has.Length.EqualTo(resultEn.Split('\n').Length));
            });
        }

        [Test]
        public void BuildCsvResponse_DecimalNumbers_FormatsWithInvariantCulture()
        {
            // Arrange
            IReadOnlyMatrixMetadata metadata = TestMockMetaBuilder.GetMockMetadata();
            DoubleDataValue[] data = [
                new DoubleDataValue(1.5, DataValueType.Exists),
                new DoubleDataValue(2.75, DataValueType.Exists),
                new DoubleDataValue(3.1415926535, DataValueType.Exists),
                new DoubleDataValue(4.0, DataValueType.Exists),
                new DoubleDataValue(123456789.0, DataValueType.Exists)
                ];
            const string lang = "en";

            // Act
            string result = CsvBuilder.BuildCsvResponse(metadata, data, lang, metadata);

            // Assert
            Assert.That(result, Does.Contain("1.5"));
            Assert.That(result, Does.Contain("2.75"));
            Assert.That(result, Does.Contain("3.1415926535")); // Full precision
            Assert.That(result, Does.Contain("4"));  // Should not show .0 for whole numbers
            Assert.That(result, Does.Contain("123456789"));  // No thousand separators
            // Should use period as decimal separator, not comma
            Assert.That(result.Split(','), Has.Length.GreaterThan(4)); // Should have CSV commas, but not decimal commas
        }

        [Test]
        public void BuildCsvResponse_AllMissingValues_HandlesDifferentTypes()
        {
            // Arrange
            IReadOnlyMatrixMetadata metadata = TestMockMetaBuilder.GetMockMetadata();
            DoubleDataValue[] data = [
                new DoubleDataValue(0.0, DataValueType.Missing),
                new DoubleDataValue(0.0, DataValueType.CanNotRepresent),
                new DoubleDataValue(0.0, DataValueType.Confidential),
                new DoubleDataValue(0.0, DataValueType.NotAcquired),
                new DoubleDataValue(0.0, DataValueType.NotAsked),
                new DoubleDataValue(0.0, DataValueType.Empty),
                new DoubleDataValue(0.0, DataValueType.Nill),
                new DoubleDataValue(1.0, DataValueType.Exists)
                ];
            const string lang = "en";

            // Act
            string result = CsvBuilder.BuildCsvResponse(metadata, data, lang, metadata);

            // Assert
            Assert.That(result, Does.Contain("."));      // Missing
            Assert.That(result, Does.Contain(".."));     // CanNotRepresent
            Assert.That(result, Does.Contain("..."));    // Confidential
            Assert.That(result, Does.Contain("...."));   // NotAcquired
            Assert.That(result, Does.Contain("....."));  // NotAsked
            Assert.That(result, Does.Contain("......")); // Empty
            Assert.That(result, Does.Contain("-"));    // Nill
            Assert.That(result, Does.Contain("1"));      // Exists
        }

        // Helper method to create metadata without description
        private static MatrixMetadata CreateMetadataWithoutDescription()
        {
            List<Dimension> dimensions = [
                MatrixMetadataUtils.CreateDimension(0, 2, ["en"])
                ];

            Dictionary<string, MetaProperty> properties = new()
            {
                // Missing DESCRIPTION property intentionally
                { PxFileConstants.STUB, new MultilanguageStringProperty(new MultilanguageString([
                    new("en", "dim0")
                    ])) },
                { PxFileConstants.HEADING, new MultilanguageStringProperty(new MultilanguageString([
                    new("en", "")
                    ])) }
            };

            return new MatrixMetadata("en", ["en"], dimensions, properties);
        }
    }
}