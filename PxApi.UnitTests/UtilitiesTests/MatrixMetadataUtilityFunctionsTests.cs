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
    }
}
