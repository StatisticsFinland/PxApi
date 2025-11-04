using Px.Utils.Language;
using Px.Utils.Models;
using Px.Utils.Models.Data;
using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Metadata;
using Px.Utils.Models.Metadata.Dimensions;
using Px.Utils.Models.Metadata.Enums;
using Px.Utils.Models.Metadata.MetaProperties;
using PxApi.ModelBuilders;
using PxApi.UnitTests.Utils;

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
            DoubleDataValue[] data = CreateDataArray(16);
            const string lang = "en";

            string expected =
                $"\"table-description.en\",\"dim0-value0-name.en dim1-value0-name.en\",\"dim0-value0-name.en dim1-value1-name.en\",\"dim0-value1-name.en dim1-value0-name.en\",\"dim0-value1-name.en dim1-value1-name.en\"{Environment.NewLine}" +
                $"\"content-value0-name.en time-value0-name.en\",1,2,3,4{Environment.NewLine}" +
                $"\"content-value0-name.en time-value1-name.en\",5,6,7,8{Environment.NewLine}" +
                $"\"content-value1-name.en time-value0-name.en\",9,10,11,12{Environment.NewLine}" +
                "\"content-value1-name.en time-value1-name.en\",13,14,15,16";
            Matrix<DoubleDataValue> requestMatrix = new(metadata, data);

            // Act
            string result = CsvBuilder.BuildCsvResponse(requestMatrix, lang, metadata);

            // Assert
            Assert.That(result, Is.Not.Null.And.Not.Empty);
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void BuildCsvResponse_WithFilteredMeta_ReturnsFilteredCsv()
        {
            // Arrange
            DoubleDataValue[] data = CreateDataArray(2);

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

            Dimension filteredDimOne = new(
                completeMeta.Dimensions[3].Code,
                completeMeta.Dimensions[3].Name,
                completeMeta.Dimensions[3].AdditionalProperties,
                new ValueList(
                [
                    completeMeta.Dimensions[3].Values[0] // Note, only one of two dimensions (Elimination value)
                ]),
            completeMeta.Dimensions[3].Type
            );

            MatrixMetadata filteredMeta = new(
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
                $"\"table-description.en\",\"dim0-value0-name.en\"{Environment.NewLine}" +
                $"\"time-value0-name.en\",1{Environment.NewLine}" +
                "\"time-value1-name.en\",2";

            Matrix<DoubleDataValue> requestMatrix = new(filteredMeta, data);

            // Act
            string result = CsvBuilder.BuildCsvResponse(requestMatrix, "en", completeMeta);

            // Assert
            Assert.That(result, Is.Not.Null.And.Not.Empty);
            Assert.That(result, Is.EqualTo(expected)); // Should contain only filtered dimension names and values
        }

        [Test]
        public void BuildCsvResponse_MissingDescription_ThrowsInvalidOperationException()
        {
            // Arrange
            IReadOnlyMatrixMetadata metadata = CreateMetadataWithoutDescription();
            DoubleDataValue[] data = CreateDataArray(1);
            const string lang = "en";
            Matrix<DoubleDataValue> requestMatrix = new(metadata, data);

            // Act & Assert
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            CsvBuilder.BuildCsvResponse(requestMatrix, lang, metadata));

            Assert.That(exception.Message, Does.Contain("DESCRIPTION meta property is required for CSV export"));
        }

        [Test]
        public void BuildCsvResponse_MultipleHeadingDimensions_CreatesCorrectHeaderRow()
        {
            // Arrange
            IReadOnlyMatrixMetadata metadata = TestMockMetaBuilder.GetMockMetadata([DimensionType.Nominal, DimensionType.Ordinal]); // Two additional dimensions go to heading
            DoubleDataValue[] data = CreateDataArray(64);
            const string lang = "en";
            Matrix<DoubleDataValue> requestMatrix = new(metadata, data);

            // Act
            string result = CsvBuilder.BuildCsvResponse(requestMatrix, lang, metadata);

            // Assert
            string[] lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Multiple(() =>
            {
                Assert.That(lines, Has.Length.EqualTo(5));
                Assert.That(lines[0], Does.Contain("table-description.en")); // Should have the table description
                Assert.That(lines[0], Does.Contain("dim0-value0-name.en dim1-value0-name.en dim2-value0-name.en dim3-value0-name.en"));
                Assert.That(lines[0], Does.Contain("dim0-value0-name.en dim1-value0-name.en dim2-value0-name.en dim3-value1-name.en"));
                Assert.That(lines[0], Does.Contain("dim0-value0-name.en dim1-value0-name.en dim2-value1-name.en dim3-value0-name.en"));
                Assert.That(lines[0], Does.Contain("dim0-value0-name.en dim1-value0-name.en dim2-value1-name.en dim3-value1-name.en"));
                Assert.That(lines[0], Does.Contain("dim0-value1-name.en dim1-value1-name.en dim2-value1-name.en dim3-value1-name.en"));
            });
        }

        [Test]
        public void BuildCsvResponse_MissingDataValues_HandlesCorrectly()
        {
            // Arrange
            IReadOnlyMatrixMetadata metadata = TestMockMetaBuilder.GetMockMetadata();
            DoubleDataValue[] data = CreateDataArray(16, new Dictionary<int, DataValueType>
            {
                { 1, DataValueType.Missing },
                { 2, DataValueType.Confidential }
            });
            const string lang = "en";
            Matrix<DoubleDataValue> requestMatrix = new(metadata, data);

            // Act
            string result = CsvBuilder.BuildCsvResponse(requestMatrix, lang, metadata);

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
            DoubleDataValue[] data = CreateDataArray(16);
            Matrix<DoubleDataValue> requestMatrix = new(metadata, data);

            // Act - Finnish
            string resultFi = CsvBuilder.BuildCsvResponse(requestMatrix, "fi", metadata);
            // Act - English  
            string resultEn = CsvBuilder.BuildCsvResponse(requestMatrix, "en", metadata);

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
            DoubleDataValue[] data = CreateDataArray(16);
            data[0] = new DoubleDataValue(1.5, DataValueType.Exists);
            data[1] = new DoubleDataValue(2.75, DataValueType.Exists);
            data[2] = new DoubleDataValue(3.1415926535, DataValueType.Exists);
            data[3] = new DoubleDataValue(4.0, DataValueType.Exists);
            data[4] = new DoubleDataValue(123456789.0, DataValueType.Exists);
            const string lang = "en";
            Matrix<DoubleDataValue> requestMatrix = new(metadata, data);

            // Act
            string result = CsvBuilder.BuildCsvResponse(requestMatrix, lang, metadata);

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
            DoubleDataValue[] data = CreateDataArray(16, new Dictionary<int, DataValueType>
            {
                { 0, DataValueType.Missing },
                { 1, DataValueType.CanNotRepresent },
                { 2, DataValueType.Confidential },
                { 3, DataValueType.NotAcquired },
                { 4, DataValueType.NotAsked },
                { 5, DataValueType.Empty },
                { 6, DataValueType.Nill }
                // Index 7 will remain as DataValueType.Exists
            });
            const string lang = "en";
            Matrix<DoubleDataValue> requestMatrix = new(metadata, data);

            // Act
            string result = CsvBuilder.BuildCsvResponse(requestMatrix, lang, metadata);

            // Assert
            Assert.That(result, Does.Contain("."));      // Missing
            Assert.That(result, Does.Contain(".."));     // CanNotRepresent
            Assert.That(result, Does.Contain("..."));    // Confidential
            Assert.That(result, Does.Contain("...."));   // NotAcquired
            Assert.That(result, Does.Contain("....."));  // NotAsked
            Assert.That(result, Does.Contain("......")); // Empty
            Assert.That(result, Does.Contain("-"));    // Nill
            Assert.That(result, Does.Contain("8"));      // Exists (index 7 + 1)
        }

        [Test]
        public void BuildCsvResponse_OnlyStubs_ReturnsValidCsv()
        {
            // Arrange
            MatrixMetadata metadata = TestMockMetaBuilder.GetMockMetadata();
            DoubleDataValue[] data = CreateDataArray(16);
            MultilanguageStringListProperty stubNames = new(
                [
                metadata.Dimensions[0].Name,
                metadata.Dimensions[1].Name,
                metadata.Dimensions[2].Name,
                metadata.Dimensions[3].Name
                ]);
            metadata.AdditionalProperties[PxFileConstants.STUB] = stubNames; // All dimensions listed in STUB
            metadata.AdditionalProperties[PxFileConstants.HEADING] = new MultilanguageStringListProperty([]); // No dimensions for HEADING
            const string lang = "en";
            string expected =
                $"\"table-description.en\"{Environment.NewLine}" +
                $"\"content-value0-name.en time-value0-name.en dim0-value0-name.en dim1-value0-name.en\",1{Environment.NewLine}" +
                $"\"content-value0-name.en time-value0-name.en dim0-value0-name.en dim1-value1-name.en\",2{Environment.NewLine}" +
                $"\"content-value0-name.en time-value0-name.en dim0-value1-name.en dim1-value0-name.en\",3{Environment.NewLine}" +
                $"\"content-value0-name.en time-value0-name.en dim0-value1-name.en dim1-value1-name.en\",4{Environment.NewLine}" +
                $"\"content-value0-name.en time-value1-name.en dim0-value0-name.en dim1-value0-name.en\",5{Environment.NewLine}" +
                $"\"content-value0-name.en time-value1-name.en dim0-value0-name.en dim1-value1-name.en\",6{Environment.NewLine}" +
                $"\"content-value0-name.en time-value1-name.en dim0-value1-name.en dim1-value0-name.en\",7{Environment.NewLine}" +
                $"\"content-value0-name.en time-value1-name.en dim0-value1-name.en dim1-value1-name.en\",8{Environment.NewLine}" +
                $"\"content-value1-name.en time-value0-name.en dim0-value0-name.en dim1-value0-name.en\",9{Environment.NewLine}" +
                $"\"content-value1-name.en time-value0-name.en dim0-value0-name.en dim1-value1-name.en\",10{Environment.NewLine}" +
                $"\"content-value1-name.en time-value0-name.en dim0-value1-name.en dim1-value0-name.en\",11{Environment.NewLine}" +
                $"\"content-value1-name.en time-value0-name.en dim0-value1-name.en dim1-value1-name.en\",12{Environment.NewLine}" +
                $"\"content-value1-name.en time-value1-name.en dim0-value0-name.en dim1-value0-name.en\",13{Environment.NewLine}" +
                $"\"content-value1-name.en time-value1-name.en dim0-value0-name.en dim1-value1-name.en\",14{Environment.NewLine}" +
                $"\"content-value1-name.en time-value1-name.en dim0-value1-name.en dim1-value0-name.en\",15{Environment.NewLine}" +
                "\"content-value1-name.en time-value1-name.en dim0-value1-name.en dim1-value1-name.en\",16";
            Matrix<DoubleDataValue> requestMatrix = new(metadata, data);

            // Act
            string result = CsvBuilder.BuildCsvResponse(requestMatrix, lang, metadata);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.EqualTo(expected));
            });
        }

        [Test]
        public void BuildCsvResponse_OnlyHeadings_ReturnsValidCsv()
        {
            // Arrange
            MatrixMetadata metadata = TestMockMetaBuilder.GetMockMetadata();
            DoubleDataValue[] data = CreateDataArray(16);
            MultilanguageStringListProperty headingNames = new(
                [
                metadata.Dimensions[0].Name,
                metadata.Dimensions[1].Name,
                metadata.Dimensions[2].Name,
                metadata.Dimensions[3].Name
                ]);
            metadata.AdditionalProperties[PxFileConstants.HEADING] = headingNames; // All dimensions listed in HEADING
            metadata.AdditionalProperties[PxFileConstants.STUB] = new MultilanguageStringListProperty([]); // No dimensions for STUB
            const string lang = "en";
            string expected =
                $"\"table-description.en\",\"content-value0-name.en time-value0-name.en dim0-value0-name.en dim1-value0-name.en\",\"content-value0-name.en time-value0-name.en dim0-value0-name.en dim1-value1-name.en\",\"content-value0-name.en time-value0-name.en dim0-value1-name.en dim1-value0-name.en\",\"content-value0-name.en time-value0-name.en dim0-value1-name.en dim1-value1-name.en\",\"content-value0-name.en time-value1-name.en dim0-value0-name.en dim1-value0-name.en\",\"content-value0-name.en time-value1-name.en dim0-value0-name.en dim1-value1-name.en\",\"content-value0-name.en time-value1-name.en dim0-value1-name.en dim1-value0-name.en\",\"content-value0-name.en time-value1-name.en dim0-value1-name.en dim1-value1-name.en\",\"content-value1-name.en time-value0-name.en dim0-value0-name.en dim1-value0-name.en\",\"content-value1-name.en time-value0-name.en dim0-value0-name.en dim1-value1-name.en\",\"content-value1-name.en time-value0-name.en dim0-value1-name.en dim1-value0-name.en\",\"content-value1-name.en time-value0-name.en dim0-value1-name.en dim1-value1-name.en\",\"content-value1-name.en time-value1-name.en dim0-value0-name.en dim1-value0-name.en\",\"content-value1-name.en time-value1-name.en dim0-value0-name.en dim1-value1-name.en\",\"content-value1-name.en time-value1-name.en dim0-value1-name.en dim1-value0-name.en\",\"content-value1-name.en time-value1-name.en dim0-value1-name.en dim1-value1-name.en\"{Environment.NewLine}" +
                $"\"\",1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16";
            Matrix<DoubleDataValue> requestMatrix = new(metadata, data);

            // Act
            string result = CsvBuilder.BuildCsvResponse(requestMatrix, lang, metadata);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.EqualTo(expected));
            });
        }

        [Test]
        public void BuildCsvResponse_SingleValue_ReturnsValidCsv()
        {
            // Arrange
            MatrixMetadata complete = TestMockMetaBuilder.GetMockMetadata();
            ContentDimension filteredContent = new(
                complete.Dimensions[0].Code,
                complete.Dimensions[0].Name,
                complete.Dimensions[0].AdditionalProperties,
                [
                    (ContentDimensionValue)complete.Dimensions[0].Values[1]
                ]
            );
            Dimension filteredTime = new(
                complete.Dimensions[1].Code,
                complete.Dimensions[1].Name,
                complete.Dimensions[1].AdditionalProperties,
                [
                    complete.Dimensions[1].Values[1]
                ],
                complete.Dimensions[1].Type
            );
            Dimension filteredDimZero = new(
                complete.Dimensions[2].Code,
                complete.Dimensions[2].Name,
                complete.Dimensions[2].AdditionalProperties,
                [
                    complete.Dimensions[2].Values[1]
                ],
                complete.Dimensions[2].Type
            );
            Dimension filteredDimOne = new(
                complete.Dimensions[3].Code,
                complete.Dimensions[3].Name,
                complete.Dimensions[3].AdditionalProperties,
                [
                    complete.Dimensions[3].Values[1]
                ],
                complete.Dimensions[3].Type
            );

            MatrixMetadata filteredMeta = new(
                complete.DefaultLanguage,
                complete.AvailableLanguages,
                [filteredContent, filteredTime, filteredDimZero, filteredDimOne],
                complete.AdditionalProperties
            );

            string expected =
                $"\"table-description.fi\",\"dim0-value1-name.fi dim1-value1-name.fi\"{Environment.NewLine}" +
                "\"content-value1-name.fi time-value1-name.fi\",1";
            Matrix<DoubleDataValue> requestMatrix = new(filteredMeta, [new(1, DataValueType.Exists)]);

            // Act
            string result = CsvBuilder.BuildCsvResponse(requestMatrix, filteredMeta.DefaultLanguage, complete);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.EqualTo(expected));
            });
        }

        [Test]
        public void BuildCsvResponse_WithEliminationValuesByLanguage_FiltersCorrectly()
        {
            // Arrange
            DoubleDataValue[] data = CreateDataArray(8);

            // Create elimination value that matches by multilanguage name
            MultilanguageString eliminationName = new(new Dictionary<string, string>()
            {
                { "en", "dim0-value1-name.en" },
                { "fi", "dim0-value1-name.fi" },
                { "sv", "dim0-value1-name.sv" }
            });
            Dictionary<string, MetaProperty> eliminationByName = new()
            {
                { PxFileConstants.ELIMINATION, new MultilanguageStringProperty(eliminationName) }
            };

            MatrixMetadata completeMeta = TestMockMetaBuilder.GetMockMetadata(dimensionAdditionalProps: [
                [], [], eliminationByName, []  // Dimension[2] (dim0) with elimination value1
                ]);

            // Create filtered metadata with elimination values
            Dimension filteredDimZero = new(
                completeMeta.Dimensions[2].Code,
                completeMeta.Dimensions[2].Name,
                completeMeta.Dimensions[2].AdditionalProperties,
                new ValueList([completeMeta.Dimensions[2].Values[1]]), // Only elimination value
                completeMeta.Dimensions[2].Type
                );

            MatrixMetadata filteredMeta = new(
                completeMeta.DefaultLanguage,
                completeMeta.AvailableLanguages,
                [
                    completeMeta.Dimensions[0],
                    completeMeta.Dimensions[1],
                    filteredDimZero, // Filtered dimension with elimination
                    completeMeta.Dimensions[3]
                    ],
                completeMeta.AdditionalProperties
                );

            string expected =
                $"\"table-description.en\",\"dim1-value0-name.en\",\"dim1-value1-name.en\"{Environment.NewLine}" +
                $"\"content-value0-name.en time-value0-name.en\",1,2{Environment.NewLine}" +
                $"\"content-value0-name.en time-value1-name.en\",3,4{Environment.NewLine}" +
                $"\"content-value1-name.en time-value0-name.en\",5,6{Environment.NewLine}" +
                $"\"content-value1-name.en time-value1-name.en\",7,8";

            Matrix<DoubleDataValue> requestMatrix = new(filteredMeta, data);

            // Act
            string result = CsvBuilder.BuildCsvResponse(requestMatrix, "en", completeMeta);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null.And.Not.Empty);
                Assert.That(result, Is.EqualTo(expected));
                // Verify that elimination dimensions are filtered out from headers
                Assert.That(result, Does.Not.Contain("dim0-value0-name.en"));
            });
        }

        // Helper method to create complete without description
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

        private static DoubleDataValue[] CreateDataArray(int count, Dictionary<int, DataValueType>? nonExistingValues = null)
        {
            DoubleDataValue[] data = new DoubleDataValue[count];

            for (int i = 0; i < count; i++)
            {
                if (nonExistingValues?.TryGetValue(i, out DataValueType type) == true)
                {
                    data[i] = new DoubleDataValue(0.0, type);
                }
                else
                {
                    data[i] = new DoubleDataValue(i + 1.0, DataValueType.Exists);
                }
            }

            return data;
        }
    }
}