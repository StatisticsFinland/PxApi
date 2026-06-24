using Px.Utils.Language;
using Px.Utils.Models.Metadata;
using Px.Utils.Models.Metadata.Dimensions;
using Px.Utils.Models.Metadata.Enums;
using PxApi.UnitTests.Utils;
using PxApi.Utilities;

namespace PxApi.UnitTests.UtilitiesTests
{
    [TestFixture]
    public class PrecisionResolverTests
    {
        /// <summary>
        /// Builds a MatrixMetadata with the given dimension sizes.
        /// Dimension at <paramref name="contentIndex"/> is a ContentDimension with the given <paramref name="precisions"/>.
        /// All other dimensions are plain Other-type dimensions.
        /// </summary>
        private static IReadOnlyMatrixMetadata BuildMetadata(int[] dimensionSizes, int contentIndex, int[] precisions)
        {
            string[] langs = ["en"];
            List<Dimension> dimensions = [];

            for (int i = 0; i < dimensionSizes.Length; i++)
            {
                if (i == contentIndex)
                {
                    dimensions.Add(BuildContentDimension($"content{i}", dimensionSizes[i], precisions, langs));
                }
                else
                {
                    MultilanguageString name = MatrixMetadataUtils.CreateMultilanguageString($"dim{i}", langs);
                    ValueList values = MatrixMetadataUtils.CreateDimensionValues($"dim{i}", dimensionSizes[i], langs);
                    dimensions.Add(new Dimension($"dim{i}", name, [], values, DimensionType.Other));
                }
            }

            return new MatrixMetadata(langs[0], langs, dimensions, []);
        }

        private static ContentDimension BuildContentDimension(string code, int valueCount, int[] precisions, string[] langs)
        {
            MultilanguageString name = MatrixMetadataUtils.CreateMultilanguageString(code, langs);
            List<ContentDimensionValue> values = [];
            for (int i = 0; i < valueCount; i++)
            {
                DimensionValue baseValue = MatrixMetadataUtils.CreateDimensionValue(code, i, langs);
                MultilanguageString unit = MatrixMetadataUtils.CreateMultilanguageString($"{code}-unit{i}", langs);
                values.Add(new ContentDimensionValue(baseValue, unit, DateTime.UtcNow, precisions[i]));
            }
            return new ContentDimension(code, name, [], values);
        }

        [Test]
        public void Resolve_ContentDimensionFirst_ReturnsCorrectPrecision()
        {
            // Dimensions: Content(2), Other(3), Other(4) → stride = 3*4 = 12, contentSize = 2
            // Precisions: [1, 3]
            // data[0..11] → content val 0 (precision 1)
            // data[12..23] → content val 1 (precision 3)
            IReadOnlyMatrixMetadata meta = BuildMetadata([2, 3, 4], contentIndex: 0, precisions: [1, 3]);
            PrecisionResolver resolver = new(meta);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(resolver.Resolve(0), Is.EqualTo(1));
                Assert.That(resolver.Resolve(11), Is.EqualTo(1));
                Assert.That(resolver.Resolve(12), Is.EqualTo(3));
                Assert.That(resolver.Resolve(23), Is.EqualTo(3));
            }
        }

        [Test]
        public void Resolve_ContentDimensionMiddle_ReturnsCorrectPrecision()
        {
            // Dimensions: Other(3), Content(2), Other(4) → stride = 4, contentSize = 2
            // Precisions: [0, 2]
            // For any data index i: contentIdx = (i / 4) % 2
            // i=0..3  → contentIdx 0 (precision 0)
            // i=4..7  → contentIdx 1 (precision 2)
            // i=8..11 → contentIdx 0 (precision 0)
            // i=12..15 → contentIdx 1 (precision 2)
            IReadOnlyMatrixMetadata meta = BuildMetadata([3, 2, 4], contentIndex: 1, precisions: [0, 2]);
            PrecisionResolver resolver = new(meta);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(resolver.Resolve(0), Is.EqualTo(0));
                Assert.That(resolver.Resolve(3), Is.EqualTo(0));
                Assert.That(resolver.Resolve(4), Is.EqualTo(2));
                Assert.That(resolver.Resolve(7), Is.EqualTo(2));
                Assert.That(resolver.Resolve(8), Is.EqualTo(0));
                Assert.That(resolver.Resolve(12), Is.EqualTo(2));
            }
        }

        [Test]
        public void Resolve_ContentDimensionLast_ReturnsCorrectPrecision()
        {
            // Dimensions: Other(3), Other(4), Content(2) → stride = 1, contentSize = 2
            // Precisions: [5, 0]
            // Alternates every cell: even → precision 5, odd → precision 0
            IReadOnlyMatrixMetadata meta = BuildMetadata([3, 4, 2], contentIndex: 2, precisions: [5, 0]);
            PrecisionResolver resolver = new(meta);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(resolver.Resolve(0), Is.EqualTo(5));
                Assert.That(resolver.Resolve(1), Is.EqualTo(0));
                Assert.That(resolver.Resolve(2), Is.EqualTo(5));
                Assert.That(resolver.Resolve(3), Is.EqualTo(0));
                Assert.That(resolver.Resolve(23), Is.EqualTo(0));
            }
        }

        [Test]
        public void Resolve_SingleContentValue_AlwaysReturnsSamePrecision()
        {
            // Content dimension has only one value → all cells share the same precision
            IReadOnlyMatrixMetadata meta = BuildMetadata([1, 5, 3], contentIndex: 0, precisions: [4]);
            PrecisionResolver resolver = new(meta);

            using (Assert.EnterMultipleScope())
            {
                for (int i = 0; i < 15; i++)
                {
                    Assert.That(resolver.Resolve(i), Is.EqualTo(4));
                }
            }
        }

        [Test]
        public void Resolve_NoContentDimension_ReturnsNoPrecision()
        {
            string[] langs = ["en"];
            List<Dimension> dimensions =
            [
                new Dimension("dim0", MatrixMetadataUtils.CreateMultilanguageString("dim0", langs), [],
                    MatrixMetadataUtils.CreateDimensionValues("dim0", 3, langs), DimensionType.Other),
                new Dimension("dim1", MatrixMetadataUtils.CreateMultilanguageString("dim1", langs), [],
                    MatrixMetadataUtils.CreateDimensionValues("dim1", 4, langs), DimensionType.Other),
            ];
            IReadOnlyMatrixMetadata meta = new MatrixMetadata(langs[0], langs, dimensions, []);
            PrecisionResolver resolver = new(meta);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(resolver.Resolve(0), Is.EqualTo(PrecisionResolver.NoPrecision));
                Assert.That(resolver.Resolve(11), Is.EqualTo(PrecisionResolver.NoPrecision));
            }
        }
    }
}
