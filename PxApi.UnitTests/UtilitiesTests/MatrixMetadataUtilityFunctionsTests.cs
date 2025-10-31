using Px.Utils.Models.Metadata;
using PxApi.UnitTests.ModelBuilderTests;
using PxApi.Utilities;
using Px.Utils.Models.Metadata.Enums;
using Px.Utils.Models.Metadata.MetaProperties;
using PxApi.ModelBuilders;
using Px.Utils.Language;

namespace PxApi.UnitTests.UtilitiesTests
{
    [TestFixture]
    internal class MatrixMetadataUtilityFunctionsTests
    {
        [Test]
        public void AssignOrdinalDimensionTypes_CalledWithMatrixMetadataWithUnknownDimensionTypes_ReturnsMetadataWithExpectedDimensionTypes()
        {
            // Arrange
            MultilanguageString ordinalMls = new(
            [
                new("fi", PxFileConstants.ORDINAL_VALUE),
                new("sv", PxFileConstants.ORDINAL_VALUE),
                new("en", PxFileConstants.ORDINAL_VALUE)
            ]);
            MultilanguageString nominalMls = new(
            [
                new("fi", PxFileConstants.NOMINAL_VALUE),
                new("sv", PxFileConstants.NOMINAL_VALUE),
                new("en", PxFileConstants.NOMINAL_VALUE)
            ]);

            MultilanguageStringProperty ordinalProperty = new(ordinalMls);
            MultilanguageStringProperty nominalProperty = new(nominalMls);

            // Metadata with three additional dimensions of Unknown type. One of them has ordinal and the other has nominal meta-id property.
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata(
                [
                    DimensionType.Unknown,
                    DimensionType.Unknown,
                    DimensionType.Unknown
                ],
                [
                    null,
                    null,
                    null,
                    null,
                    new Dictionary<string, MetaProperty> { { PxFileConstants.META_ID, ordinalProperty } },
                    new Dictionary<string, MetaProperty> { { PxFileConstants.META_ID, nominalProperty } },
                    null
                ]);

            // Act
            MatrixMetadataUtilityFunctions.AssignOrdinalDimensionTypes(meta);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(meta.Dimensions[4].Type, Is.EqualTo(DimensionType.Ordinal));
                Assert.That(meta.Dimensions[5].Type, Is.EqualTo(DimensionType.Nominal));
                Assert.That(meta.Dimensions[6].Type, Is.EqualTo(DimensionType.Unknown));
            });
        }

        [Test]
        public void GetValueByLanguage_CalledWithMultilanguageStringProperty_ReturnsValueForSpecifiedLanguage()
        {
            // Arrange
            MultilanguageString testMls = new(
            [
                new("fi", "Arvo suomeksi"),
                new("sv", "Värde på svenska"),
                new("en", "Value in English")
            ]);

            MultilanguageStringProperty testProperty = new(testMls);
            Dictionary<string, MetaProperty> propertyCollection = new()
            {
                { "TEST_KEY", testProperty }
            };

            // Act & Assert
            Assert.Multiple(() =>
            {
                Assert.That(propertyCollection.GetValueByLanguage("TEST_KEY", "fi"), Is.EqualTo("Arvo suomeksi"));
                Assert.That(propertyCollection.GetValueByLanguage("TEST_KEY", "sv"), Is.EqualTo("Värde på svenska"));
                Assert.That(propertyCollection.GetValueByLanguage("TEST_KEY", "en"), Is.EqualTo("Value in English"));
            });
        }

        [Test]
        public void GetValueByLanguage_CalledWithStringProperty_ReturnsValueIgnoringLanguage()
        {
            // Arrange
            StringProperty testProperty = new("Single language value");
            Dictionary<string, MetaProperty> propertyCollection = new()
            {
                { "TEST_KEY", testProperty }
            };

            // Act & Assert
            Assert.Multiple(() =>
            {
                Assert.That(propertyCollection.GetValueByLanguage("TEST_KEY", "fi"), Is.EqualTo("Single language value"));
                Assert.That(propertyCollection.GetValueByLanguage("TEST_KEY", "sv"), Is.EqualTo("Single language value"));
                Assert.That(propertyCollection.GetValueByLanguage("TEST_KEY", "en"), Is.EqualTo("Single language value"));
            });
        }

        [Test]
        public void GetValueByLanguage_CalledWithNonExistentKey_ReturnsNull()
        {
            // Arrange
            Dictionary<string, MetaProperty> propertyCollection = [];

            // Act & Assert
            Assert.That(propertyCollection.GetValueByLanguage("NON_EXISTENT_KEY", "fi"), Is.Null);
        }

        [Test]
        public void GetValueListByLanguage_CalledWithMultilanguageStringListProperty_ReturnsValuesForSpecifiedLanguage()
        {
            // Arrange
            List<MultilanguageString> testMlsList =
            [
                new(
                [
                    new("fi", "Ensimmäinen arvo"),
                    new("sv", "Första värdet"),
                    new("en", "First value")
                ]),
                new(
                [
                    new("fi", "Toinen arvo"),
                    new("sv", "Andra värdet"),
                    new("en", "Second value")
                ]),
                new(
                [
                    new("fi", "Kolmas arvo"),
                    new("sv", "Tredje värdet"),
                    new("en", "Third value")
                ])
            ];

            MultilanguageStringListProperty testProperty = new(testMlsList);
            Dictionary<string, MetaProperty> propertyCollection = new()
            {
                { "TEST_LIST_KEY", testProperty }
            };
            
            string[] expectedFi = ["Ensimmäinen arvo", "Toinen arvo", "Kolmas arvo"];
            string[] expectedEn = ["Första värdet", "Andra värdet", "Tredje värdet"];
            string[] expectedSv = ["First value", "Second value", "Third value"];

            // Act
            string[]? resultFi = propertyCollection.GetValueListByLanguage("TEST_LIST_KEY", "fi");
            string[]? resultSv = propertyCollection.GetValueListByLanguage("TEST_LIST_KEY", "sv");
            string[]? resultEn = propertyCollection.GetValueListByLanguage("TEST_LIST_KEY", "en");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(resultFi, Is.EqualTo(expectedFi));
                Assert.That(resultSv, Is.EqualTo(expectedEn));
                Assert.That(resultEn, Is.EqualTo(expectedSv));
            });
        }

        [Test]
        public void GetValueListByLanguage_CalledWithStringListProperty_ReturnsValuesIgnoringLanguage()
        {
            // Arrange
            List<string> testStringList = ["First item", "Second item", "Third item"];
            StringListProperty testProperty = new(testStringList);
            Dictionary<string, MetaProperty> propertyCollection = new()
            {
                { "TEST_LIST_KEY", testProperty }
            };

            // Act
            string[]? resultFi = propertyCollection.GetValueListByLanguage("TEST_LIST_KEY", "fi");
            string[]? resultSv = propertyCollection.GetValueListByLanguage("TEST_LIST_KEY", "sv");
            string[]? resultEn = propertyCollection.GetValueListByLanguage("TEST_LIST_KEY", "en");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(resultFi, Is.EqualTo(testStringList.ToArray()));
                Assert.That(resultSv, Is.EqualTo(testStringList.ToArray()));
                Assert.That(resultEn, Is.EqualTo(testStringList.ToArray()));
            });
        }

        [Test]
        public void GetValueListByLanguage_CalledWithNonExistentKey_ReturnsNull()
        {
            // Arrange
            Dictionary<string, MetaProperty> propertyCollection = [];

            // Act & Assert
            Assert.That(propertyCollection.GetValueListByLanguage("NON_EXISTENT_KEY", "fi"), Is.Null);
        }
    }
}
