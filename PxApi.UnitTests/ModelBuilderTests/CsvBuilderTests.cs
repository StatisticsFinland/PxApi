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
                $"\"content-value0-name.en time-value0-name.en\",1.00,2.00,3.00,4.00{Environment.NewLine}" +
                $"\"content-value0-name.en time-value1-name.en\",5.00,6.00,7.00,8.00{Environment.NewLine}" +
                $"\"content-value1-name.en time-value0-name.en\",9.00,10.00,11.00,12.00{Environment.NewLine}" +
                "\"content-value1-name.en time-value1-name.en\",13.00,14.00,15.00,16.00";
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
                $"\"time-value0-name.en\",1.00{Environment.NewLine}" +
                "\"time-value1-name.en\",2.00";

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
            using (Assert.EnterMultipleScope())
            {
                Assert.That(lines, Has.Length.EqualTo(5));
                Assert.That(lines[0], Does.Contain("table-description.en")); // Should have the table description
                Assert.That(lines[0], Does.Contain("dim0-value0-name.en dim1-value0-name.en dim2-value0-name.en dim3-value0-name.en"));
                Assert.That(lines[0], Does.Contain("dim0-value0-name.en dim1-value0-name.en dim2-value0-name.en dim3-value1-name.en"));
                Assert.That(lines[0], Does.Contain("dim0-value0-name.en dim1-value0-name.en dim2-value1-name.en dim3-value0-name.en"));
                Assert.That(lines[0], Does.Contain("dim0-value0-name.en dim1-value0-name.en dim2-value1-name.en dim3-value1-name.en"));
                Assert.That(lines[0], Does.Contain("dim0-value1-name.en dim1-value1-name.en dim2-value1-name.en dim3-value1-name.en"));
            };
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
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Does.Contain("."));     // Missing value
                Assert.That(result, Does.Contain("..."));   // Confidential value  
                Assert.That(result, Does.Contain("1"));     // Existing value
                Assert.That(result, Does.Contain("4"));     // Existing value
            };
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

            using (Assert.EnterMultipleScope())
            {
                // Assert
                Assert.That(resultFi, Does.Contain("table-description.fi"));
                Assert.That(resultEn, Does.Contain("table-description.en"));
                // Both should have the same structure but potentially different value names
                Assert.That(resultFi.Split('\n'), Has.Length.EqualTo(resultEn.Split('\n').Length));
            };
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
            Assert.That(result, Does.Contain("3.14")); // Rounded to precision 2 from content dimension metadata
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
                $"\"content-value0-name.en time-value0-name.en dim0-value0-name.en dim1-value0-name.en\",1.00{Environment.NewLine}" +
                $"\"content-value0-name.en time-value0-name.en dim0-value0-name.en dim1-value1-name.en\",2.00{Environment.NewLine}" +
                $"\"content-value0-name.en time-value0-name.en dim0-value1-name.en dim1-value0-name.en\",3.00{Environment.NewLine}" +
                $"\"content-value0-name.en time-value0-name.en dim0-value1-name.en dim1-value1-name.en\",4.00{Environment.NewLine}" +
                $"\"content-value0-name.en time-value1-name.en dim0-value0-name.en dim1-value0-name.en\",5.00{Environment.NewLine}" +
                $"\"content-value0-name.en time-value1-name.en dim0-value0-name.en dim1-value1-name.en\",6.00{Environment.NewLine}" +
                $"\"content-value0-name.en time-value1-name.en dim0-value1-name.en dim1-value0-name.en\",7.00{Environment.NewLine}" +
                $"\"content-value0-name.en time-value1-name.en dim0-value1-name.en dim1-value1-name.en\",8.00{Environment.NewLine}" +
                $"\"content-value1-name.en time-value0-name.en dim0-value0-name.en dim1-value0-name.en\",9.00{Environment.NewLine}" +
                $"\"content-value1-name.en time-value0-name.en dim0-value0-name.en dim1-value1-name.en\",10.00{Environment.NewLine}" +
                $"\"content-value1-name.en time-value0-name.en dim0-value1-name.en dim1-value0-name.en\",11.00{Environment.NewLine}" +
                $"\"content-value1-name.en time-value0-name.en dim0-value1-name.en dim1-value1-name.en\",12.00{Environment.NewLine}" +
                $"\"content-value1-name.en time-value1-name.en dim0-value0-name.en dim1-value0-name.en\",13.00{Environment.NewLine}" +
                $"\"content-value1-name.en time-value1-name.en dim0-value0-name.en dim1-value1-name.en\",14.00{Environment.NewLine}" +
                $"\"content-value1-name.en time-value1-name.en dim0-value1-name.en dim1-value0-name.en\",15.00{Environment.NewLine}" +
                "\"content-value1-name.en time-value1-name.en dim0-value1-name.en dim1-value1-name.en\",16.00";
            Matrix<DoubleDataValue> requestMatrix = new(metadata, data);

            // Act
            string result = CsvBuilder.BuildCsvResponse(requestMatrix, lang, metadata);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.EqualTo(expected));
            };
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
                $"\"\",1.00,2.00,3.00,4.00,5.00,6.00,7.00,8.00,9.00,10.00,11.00,12.00,13.00,14.00,15.00,16.00";
            Matrix<DoubleDataValue> requestMatrix = new(metadata, data);

            // Act
            string result = CsvBuilder.BuildCsvResponse(requestMatrix, lang, metadata);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.EqualTo(expected));
            };
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
                "\"content-value1-name.fi time-value1-name.fi\",1.00";
            Matrix<DoubleDataValue> requestMatrix = new(filteredMeta, [new(1, DataValueType.Exists)]);

            // Act
            string result = CsvBuilder.BuildCsvResponse(requestMatrix, filteredMeta.DefaultLanguage, complete);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.EqualTo(expected));
            };
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
                $"\"content-value0-name.en time-value0-name.en\",1.00,2.00{Environment.NewLine}" +
                $"\"content-value0-name.en time-value1-name.en\",3.00,4.00{Environment.NewLine}" +
                $"\"content-value1-name.en time-value0-name.en\",5.00,6.00{Environment.NewLine}" +
                $"\"content-value1-name.en time-value1-name.en\",7.00,8.00";

            Matrix<DoubleDataValue> requestMatrix = new(filteredMeta, data);

            // Act
            string result = CsvBuilder.BuildCsvResponse(requestMatrix, "en", completeMeta);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.Not.Null.And.Not.Empty);
                Assert.That(result, Is.EqualTo(expected));
                // Verify that elimination dimensions are filtered out from headers
                Assert.That(result, Does.Not.Contain("dim0-value0-name.en"));
            };
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

        [Test]
        public void BuildCsvResponse_MixedPrecision_AppliesCorrectDecimalsPerContentValue()
        {
            // Arrange: Content dimension with value0=precision 0, value1=precision 3
            // Dimensions: Content(2), Time(2), dim0(2), dim1(2) → 16 cells
            // After stub/heading transform: Content is first → stride=8, contentSize=2
            // cells 0–7  belong to content-value0 (precision 0) → "X"
            // cells 8–15 belong to content-value1 (precision 3) → "X.XXX"
            MatrixMetadata metadata = TestMockMetaBuilder.GetMockMetadata();

            ContentDimension mixedPrecisionContent = new(
                metadata.Dimensions[0].Code,
                metadata.Dimensions[0].Name,
                metadata.Dimensions[0].AdditionalProperties,
                [
                    new ContentDimensionValue(
                        TestMockMetaBuilder.GetMockDimensionValue("content-value0"),
                        new MultilanguageString([new("en", "unit0")]),
                        DateTime.UtcNow,
                        precision: 0),
                    new ContentDimensionValue(
                        TestMockMetaBuilder.GetMockDimensionValue("content-value1"),
                        new MultilanguageString([new("en", "unit1")]),
                        DateTime.UtcNow,
                        precision: 3),
                ]
            );
            metadata.Dimensions[0] = mixedPrecisionContent;

            DoubleDataValue[] data = new DoubleDataValue[16];
            for (int i = 0; i < 16; i++)
            {
                data[i] = new DoubleDataValue(1.5678, DataValueType.Exists);
            }

            Matrix<DoubleDataValue> requestMatrix = new(metadata, data);

            // Act
            string result = CsvBuilder.BuildCsvResponse(requestMatrix, "en", metadata);

            // Assert: first 8 cells (content-value0, precision 0) → "2" (rounded to 0 decimals)
            //         last 8 cells (content-value1, precision 3) → "1.568" (rounded to 3 decimals)
            string[] lines = result.Split(Environment.NewLine);
            using (Assert.EnterMultipleScope())
            {
                // Rows 1–2 belong to content-value0 (precision 0): e.g. "2,2,2,2"
                Assert.That(lines[1], Does.Contain("2,2,2,2"));
                Assert.That(lines[2], Does.Contain("2,2,2,2"));
                // Rows 3–4 belong to content-value1 (precision 3): e.g. "1.568,1.568,1.568,1.568"
                Assert.That(lines[3], Does.Contain("1.568,1.568,1.568,1.568"));
                Assert.That(lines[4], Does.Contain("1.568,1.568,1.568,1.568"));
            }
        }
    }
}