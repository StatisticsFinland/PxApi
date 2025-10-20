using Px.Utils.Language;
using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Data;
using Px.Utils.Models.Metadata.Dimensions;
using Px.Utils.Models.Metadata.Enums;
using Px.Utils.Models.Metadata.MetaProperties;
using Px.Utils.Models.Metadata;
using PxApi.ModelBuilders;
using PxApi.Models.JsonStat;
using System.Globalization;
using PxApi.Models;

namespace PxApi.UnitTests.ModelBuilderTests
{
    /// <summary>
    /// Unit tests for the BuildJsonStat2 method in ModelBuilder class.
    /// </summary>
    [TestFixture]
    public static class BuildJsonStat2Tests
    {
        // Added TableGroup list fixture
        private static readonly List<TableGroup> GROUPINGS = [
            new TableGroup
            {
                Code = "grp1",
                Name = new MultilanguageString([
                    new("fi", "Ryhmä 1"),
                    new("sv", "Grupp 1"),
                    new("en", "Group 1")
                ]),
                GroupingCode = "rootGrouping",
                GroupingName = new MultilanguageString([
                    new("fi", "Pääryhmä"),
                    new("sv", "Huvudgrupp"),
                    new("en", "Main Group")
                ]),
                Links = []
            }
        ];

        [Test]
        public static void BuildJsonStat2_WhenCalled_ReturnsJsonStat2WithCorrectMetadata()
        {
            // Arrange
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();

            // Add SOURCE property to content dimension's additional properties
            if (meta.Dimensions.Find(d => d.Type == DimensionType.Content) is ContentDimension contentDim)
            {
                MultilanguageString source = new([
                    new("fi", "content-source.fi"),
                    new("sv", "content-source.sv"),
                    new("en", "content-source.en")
                ]);
                contentDim.AdditionalProperties[PxFileConstants.SOURCE] = new MultilanguageStringProperty(source);
            }

            string lang = "en";
            DoubleDataValue[] data = [
                new DoubleDataValue(1.0, DataValueType.Exists),
                new DoubleDataValue(2.0, DataValueType.Exists),
                new DoubleDataValue(3.0, DataValueType.Exists),
                new DoubleDataValue(4.0, DataValueType.Exists)
            ];

            // Act
            JsonStat2 result = JsonStat2Builder.BuildJsonStat2(meta, GROUPINGS, data, lang);

            // Assert
            DateTime expectedLastUpdated = new(2024, 10, 10, 0, 0, 0, DateTimeKind.Utc);
            string expectedUpdated = expectedLastUpdated.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Version, Is.EqualTo("2.0"));
                Assert.That(result.Class, Is.EqualTo("dataset"));
                Assert.That(result.Id, Is.EqualTo(new string[] { "content-code", "time-code", "dim0-code", "dim1-code" }));
                Assert.That(result.Label, Is.EqualTo("table-description.en"));
                Assert.That(result.Source, Is.EqualTo("content-source.en"));
                Assert.That(result.Updated, Is.EqualTo(expectedUpdated));

                Assert.That(result.Size, Has.Count.EqualTo(4));
                Assert.That(result.Size[0], Is.EqualTo(2));
                Assert.That(result.Size[1], Is.EqualTo(2));
                Assert.That(result.Size[2], Is.EqualTo(2));
                Assert.That(result.Size[3], Is.EqualTo(2));

                Assert.That(result.Dimension, Is.Not.Null);
                Assert.That(result.Dimension, Has.Count.EqualTo(4));
                Assert.That(result.Dimension.ContainsKey("content-code"));
                Assert.That(result.Dimension.ContainsKey("time-code"));
                Assert.That(result.Dimension.ContainsKey("dim0-code"));
                Assert.That(result.Dimension.ContainsKey("dim1-code"));

                Assert.That(result.Role, Is.Not.Null);
                Assert.That(result.Role!.ContainsKey("time"));
                Assert.That(result.Role["time"], Has.Count.EqualTo(1));
                Assert.That(result.Role["time"][0], Is.EqualTo("time-code"));
                Assert.That(result.Role.ContainsKey("metric"));
                Assert.That(result.Role["metric"], Has.Count.EqualTo(1));
                Assert.That(result.Role["metric"][0], Is.EqualTo("content-code"));

                Assert.That(result.Value, Is.EqualTo(data));

                Assert.That(result.Extension, Is.Not.Null);
                Assert.That(result.Extension!.ContainsKey("missingValueDescriptions"));
                Dictionary<DataValueType, string>? translations = result.Extension["missingValueDescriptions"] as Dictionary<DataValueType, string>;
                Assert.That(translations, Is.Not.Null);
                Assert.That(translations![DataValueType.Missing], Is.EqualTo("Missing"));
                Assert.That(translations[DataValueType.CanNotRepresent], Is.EqualTo("Not applicable"));
                Assert.That(translations[DataValueType.Confidential], Is.EqualTo("Data is subject to secrecy"));
                Assert.That(translations[DataValueType.NotAcquired], Is.EqualTo("Not available"));
                Assert.That(translations[DataValueType.NotAsked], Is.EqualTo("Not asked"));
                Assert.That(translations[DataValueType.Empty], Is.EqualTo("......"));
                Assert.That(translations[DataValueType.Nill], Is.EqualTo("Magnitude nil"));

                Assert.That(result.Extension.ContainsKey("groupings"));
                List<TableGroup>? extensionGroupings = result.Extension["groupings"] as List<TableGroup>;
                Assert.That(extensionGroupings, Is.Not.Null);
                Assert.That(extensionGroupings![0].Code, Is.EqualTo("grp1"));
            });
        }

        [Test]
        public static void BuildJsonStat2_WithDefaultLanguage_UsesDefaultLanguage()
        {
            // Arrange
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();

            if (meta.Dimensions.Find(d => d.Type == DimensionType.Content) is ContentDimension contentDim)
            {
                MultilanguageString source = new([
                    new("fi", "content-source.fi"),
                    new("sv", "content-source.sv"),
                    new("en", "content-source.en")
                ]);
                contentDim.AdditionalProperties[PxFileConstants.SOURCE] = new MultilanguageStringProperty(source);
            }

            DoubleDataValue[] data = [
                new DoubleDataValue(1.0, DataValueType.Exists),
                new DoubleDataValue(2.0, DataValueType.Exists)
            ];

            // Act
            JsonStat2 result = JsonStat2Builder.BuildJsonStat2(meta, GROUPINGS, data);

            // Assert
            Assert.Multiple(() =>
            {
                // Should use the default language "fi" from the mock metadata
                Assert.That(result.Label, Is.EqualTo("table-description.fi"));
                Assert.That(result.Source, Is.EqualTo("content-source.fi"));
                
                // Check a dimension label is also in the default language
                Assert.That(result.Dimension["content-code"].Label, Is.EqualTo("content-name.fi"));
                
                // Check extension contains Finnish translations
                Assert.That(result.Extension, Is.Not.Null);
                Assert.That(result.Extension!.ContainsKey("missingValueDescriptions"));
                Dictionary<DataValueType, string>? translations = result.Extension["missingValueDescriptions"] as Dictionary<DataValueType, string>;
                Assert.That(translations, Is.Not.Null);
                Assert.That(translations![DataValueType.Missing], Is.EqualTo("Tieto on puuttuva"));
                Assert.That(translations[DataValueType.CanNotRepresent], Is.EqualTo("Tieto on epälooginen esitettäväksi"));
                Assert.That(translations[DataValueType.Confidential], Is.EqualTo("Tieto on salassapitosäännön alainen"));
            });
        }

        [Test]
        public static void BuildJsonStat2_WithSwedishLanguage_IncludesSwedishTranslations()
        {
            // Arrange
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();

            // Add SOURCE property to content dimension's additional properties
            if (meta.Dimensions.Find(d => d.Type == DimensionType.Content) is ContentDimension contentDim)
            {
                MultilanguageString source = new([
                    new("fi", "content-source.fi"),
                    new("sv", "content-source.sv"),
                    new("en", "content-source.en")
                ]);
                contentDim.AdditionalProperties[PxFileConstants.SOURCE] = new MultilanguageStringProperty(source);
            }

            string lang = "sv";
            DoubleDataValue[] data = [
                new DoubleDataValue(1.0, DataValueType.Exists),
                new DoubleDataValue(2.0, DataValueType.Exists)
            ];

            // Act
            JsonStat2 result = JsonStat2Builder.BuildJsonStat2(meta, GROUPINGS, data, lang);

            // Assert
            Assert.Multiple(() =>
            {
                // Check extension contains Swedish translations
                Assert.That(result.Extension, Is.Not.Null);
                Assert.That(result.Extension!.ContainsKey("missingValueDescriptions"));
                Dictionary<DataValueType, string>? translations = result.Extension["missingValueDescriptions"] as Dictionary<DataValueType, string>;
                Assert.That(translations, Is.Not.Null);
                Assert.That(translations![DataValueType.Missing], Is.EqualTo("Uppgift saknas"));
                Assert.That(translations[DataValueType.CanNotRepresent], Is.EqualTo("Uppgift kan inte förekomma"));
                Assert.That(translations[DataValueType.Confidential], Is.EqualTo("Uppgift är sekretessbelagd"));
                Assert.That(translations[DataValueType.NotAcquired], Is.EqualTo("Uppgift inte tillgänglig"));
                Assert.That(translations[DataValueType.NotAsked], Is.EqualTo("Uppgift inte efterfrågad"));
                Assert.That(translations[DataValueType.Empty], Is.EqualTo("......"));
                Assert.That(translations[DataValueType.Nill], Is.EqualTo("Värdet noll"));
            });
        }

        [Test]
        public static void BuildJsonStat2_WithMissingValues_GeneratesStatusDictionary()
        {
            // Arrange
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();
            DoubleDataValue[] data = [
                new DoubleDataValue(1.0, DataValueType.Exists),
                new DoubleDataValue(default, DataValueType.Missing),
                new DoubleDataValue(3.0, DataValueType.Exists),
                new DoubleDataValue(default, DataValueType.Missing)
            ];

            // Act
            JsonStat2 result = JsonStat2Builder.BuildJsonStat2(meta, GROUPINGS, data, "en");

            // Assert
            Assert.Multiple(() =>
            {
                // Check the status dictionary is created
                Assert.That(result.Status, Is.Not.Null);
                Assert.That(result.Status, Has.Count.EqualTo(2));
                
                // Status values should be the indices of missing values
                Assert.That(result.Status!.ContainsKey(1));
                Assert.That(result.Status.ContainsKey(3));
                
                // Status values should match the DataValueType byte value
                Assert.That(result.Status[1], Is.EqualTo(((byte)DataValueType.Missing).ToString()));
                Assert.That(result.Status[3], Is.EqualTo(((byte)DataValueType.Missing).ToString()));
                
                // Extension should still contain translations
                Assert.That(result.Extension, Is.Not.Null);
                Assert.That(result.Extension!.ContainsKey("missingValueDescriptions"));
            });
        }

        [Test]
        public static void BuildJsonStat2_WithAllExistingValues_StatusDictionaryIsNull()
        {
            // Arrange
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();
            DoubleDataValue[] data = [
                new DoubleDataValue(1.0, DataValueType.Exists),
                new DoubleDataValue(2.0, DataValueType.Exists),
                new DoubleDataValue(3.0, DataValueType.Exists)
            ];

            // Act
            JsonStat2 result = JsonStat2Builder.BuildJsonStat2(meta, GROUPINGS, data, "en");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.Null, "Status dictionary should be null when all values exist");
                
                // Extension should still contain translations
                Assert.That(result.Extension, Is.Not.Null);
                Assert.That(result.Extension!.ContainsKey("missingValueDescriptions"));
            });
        }

        [Test]
        public static void BuildJsonStat2_CheckDimensionsStructure()
        {
            // Arrange
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();
            DoubleDataValue[] data = [
                new DoubleDataValue(1.0, DataValueType.Exists),
                new DoubleDataValue(2.0, DataValueType.Exists)
            ];
            string lang = "en";

            // Act
            JsonStat2 result = JsonStat2Builder.BuildJsonStat2(meta, GROUPINGS, data, lang);

            // Assert
            Assert.Multiple(() =>
            {
                // Check content dimension structure
                PxApi.Models.JsonStat.Dimension contentDim = result.Dimension["content-code"];
                Assert.That(contentDim.Label, Is.EqualTo("content-name.en"));
                Assert.That(contentDim.Category, Is.Not.Null);
                Assert.That(contentDim.Category.Index, Has.Count.EqualTo(2));
                Assert.That(contentDim.Category.Index![0], Is.EqualTo("content-value0-code"));
                Assert.That(contentDim.Category.Index![1], Is.EqualTo("content-value1-code"));
                Assert.That(contentDim.Category.Label, Is.Not.Null);
                Assert.That(contentDim.Category.Label!["content-value0-code"], Is.EqualTo("content-value0-name.en"));
                Assert.That(contentDim.Category.Label!["content-value1-code"], Is.EqualTo("content-value1-name.en"));
                
                // Check unit information in content dimension
                Assert.That(contentDim.Category.Unit, Is.Not.Null);
                Assert.That(contentDim.Category.Unit!["content-value0-code"].Label, Is.EqualTo("content-value0-unit.en"));
                Assert.That(contentDim.Category.Unit!["content-value0-code"].Decimals, Is.EqualTo(2));

                // Check time dimension structure
                PxApi.Models.JsonStat.Dimension timeDim = result.Dimension["time-code"];
                Assert.That(timeDim.Label, Is.EqualTo("time-name.en"));
                Assert.That(timeDim.Category.Index, Has.Count.EqualTo(2));
                Assert.That(timeDim.Category.Index![0], Is.EqualTo("time-value0-code"));
                Assert.That(timeDim.Category.Index![1], Is.EqualTo("time-value1-code"));
                Assert.That(timeDim.Category.Label!["time-value0-code"], Is.EqualTo("time-value0-name.en"));
                Assert.That(timeDim.Category.Label!["time-value1-code"], Is.EqualTo("time-value1-name.en"));
            });
        }

        [Test]
        public static void BuildJsonStat2_WhenDescriptionMissing_ThrowsArgumentException()
        {
            // Arrange
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();
            // Remove the description property
            meta.AdditionalProperties.Remove(PxFileConstants.DESCRIPTION);
            
            DoubleDataValue[] data = [
                new DoubleDataValue(1.0, DataValueType.Exists),
                new DoubleDataValue(2.0, DataValueType.Exists)
            ];

            // Act & Assert
            ArgumentException ex = Assert.Throws<ArgumentException>(() =>
                JsonStat2Builder.BuildJsonStat2(meta, GROUPINGS, data, "en"));
            Assert.That(ex.Message, Does.Contain("No DESCRIPTION found in table level metadata"));
        }
    }
}