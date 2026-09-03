using Px.Utils.Language;
using Px.Utils.Models.Data;
using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Metadata;
using Px.Utils.Models.Metadata.Dimensions;
using PxApi.Configuration;
using PxApi.ModelBuilders;
using PxApi.Models.JsonStat;
using PxApi.UnitTests.ModelBuilderTests;
using System.Text.Json;

namespace PxApi.UnitTests.ConfigurationTests
{
    [TestFixture]
    public class JsonStat2JsonConverterTests
    {
        private static readonly JsonSerializerOptions SerializerOptions = GlobalJsonConverterOptions.Default;

        [Test]
        public void Serialize_ExistingValues_AppliesContentDimensionPrecision()
        {
            // Arrange: mock metadata has Content(2 values, precision=2 each), Time(2), dim0(2), dim1(2)
            // Content dimension is first → stride=8, contentSize=2
            // All 16 cells get precision=2 → values formatted as F2
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();
            DoubleDataValue[] data =
            [
                new DoubleDataValue(1.5, DataValueType.Exists),
                new DoubleDataValue(2.0, DataValueType.Exists),
                new DoubleDataValue(3.1415926535, DataValueType.Exists),
            ];

            JsonStat2 jsonStat = JsonStat2Builder.BuildJsonStat2(meta, data, "en");

            // Act
            string json = JsonSerializer.Serialize(jsonStat, SerializerOptions);

            // Assert: values are formatted with exactly 2 decimal places
            using (Assert.EnterMultipleScope())
            {
                Assert.That(json, Does.Contain("1.50"));
                Assert.That(json, Does.Contain("2.00"));
                Assert.That(json, Does.Contain("3.14"));
                Assert.That(json, Does.Not.Contain("3.1415926535"));
            }
        }

        [Test]
        public void Serialize_MixedPrecision_AppliesDifferentDecimalsPerContentValue()
        {
            // Arrange: content-value0 precision=0, content-value1 precision=3
            // Dimensions: Content(2), Time(2), dim0(2), dim1(2) → 16 cells
            // Content at position 0 → stride=8, contentSize=2
            // cells 0–7:  content-value0 (precision 0) → no decimals
            // cells 8–15: content-value1 (precision 3) → 3 decimals
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();
            ContentDimension mixedPrecisionContent = new(
                meta.Dimensions[0].Code,
                meta.Dimensions[0].Name,
                meta.Dimensions[0].AdditionalProperties,
                [
                    new ContentDimensionValue(
                        TestMockMetaBuilder.GetMockDimensionValue("content-value0"),
                        new MultilanguageString([new("en", "unit0"), new("fi", "unit0"), new("sv", "unit0")]),
                        DateTime.UtcNow,
                        precision: 0),
                    new ContentDimensionValue(
                        TestMockMetaBuilder.GetMockDimensionValue("content-value1"),
                        new MultilanguageString([new("en", "unit1"), new("fi", "unit1"), new("sv", "unit1")]),
                        DateTime.UtcNow,
                        precision: 3),
                ]
            );
            meta.Dimensions[0] = mixedPrecisionContent;

            DoubleDataValue[] data = new DoubleDataValue[16];
            for (int i = 0; i < 16; i++)
            {
                data[i] = new DoubleDataValue(1.5678, DataValueType.Exists);
            }

            JsonStat2 jsonStat = JsonStat2Builder.BuildJsonStat2(meta, data, "en");

            // Act
            string json = JsonSerializer.Serialize(jsonStat, SerializerOptions);

            // Assert: content-value0 cells (precision=0) → "2" in the value array
            //         content-value1 cells (precision=3) → "1.568"
            using (Assert.EnterMultipleScope())
            {
                Assert.That(json, Does.Contain("\"value\":[2,2,2,2,2,2,2,2,1.568,1.568,1.568,1.568,1.568,1.568,1.568,1.568]"));
            }
        }

        [Test]
        public void Serialize_MissingValues_WrittenAsNull()
        {
            // Arrange
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();
            DoubleDataValue[] data =
            [
                new DoubleDataValue(1.0, DataValueType.Exists),
                new DoubleDataValue(default, DataValueType.Missing),
                new DoubleDataValue(default, DataValueType.Confidential),
            ];
            JsonStat2 jsonStat = JsonStat2Builder.BuildJsonStat2(meta, data, "en");

            // Act
            string json = JsonSerializer.Serialize(jsonStat, SerializerOptions);

            // Assert
            Assert.That(json, Does.Contain("\"value\":[1.00,null,null]"));
        }

        [Test]
        public void Serialize_NoPrecisionInfo_UsesFullPrecision()
        {
            // Arrange: build without data so Precision is null, then set Value manually
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();
            JsonStat2 jsonStat = JsonStat2Builder.BuildJsonStat2(meta, "en");
            jsonStat.Value = new PrecisionDataArray([new DoubleDataValue(3.1415926535, DataValueType.Exists)]);
            // Precision intentionally left null

            // Act
            string json = JsonSerializer.Serialize(jsonStat, SerializerOptions);

            // Assert: full double precision used as fallback
            Assert.That(json, Does.Contain("3.1415926535"));
        }

        [TestCase(0.5, 0, "1")]
        [TestCase(-0.5, 0, "-1")]
        [TestCase(-2.5, 0, "-3")]
        [TestCase(1.25, 1, "1.3")]
        [TestCase(-1.25, 1, "-1.3")]
        [TestCase(1.005, 2, "1.00")]   // IEEE 754: 1.005 is stored as ~1.00499…, rounds down
        [TestCase(2.675, 2, "2.68")]   // IEEE 754: 2.675 is stored as ~2.67500…, rounds up
        [TestCase(9.9, 0, "10")]       // near-integer rounds up, no decimal digits in output
        [TestCase(3.14159265358979, 4, "3.1416")]  // truncated transcendental rounds last digit up
        [TestCase(0.0, 2, "0.00")]     // zero is emitted with the required number of decimal places
        [TestCase(-1.5, 0, "-2")]      // negative exact half rounds away from zero (more negative)
        public void Serialize_RoundingEdgeCases_ProducesCorrectOutput(double value, int precision, string expectedRaw)
        {
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();
            meta.Dimensions[0] = BuildUniformPrecisionContentDimension(meta, precision);

            DoubleDataValue[] data = new DoubleDataValue[16];
            for (int i = 0; i < 16; i++)
                data[i] = new DoubleDataValue(value, DataValueType.Exists);

            JsonStat2 jsonStat = JsonStat2Builder.BuildJsonStat2(meta, data, "en");
            string json = JsonSerializer.Serialize(jsonStat, SerializerOptions);
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement valueArray = doc.RootElement.GetProperty("value");

            Assert.That(valueArray[0].GetRawText(), Is.EqualTo(expectedRaw));
        }

        [Test]
        public void Serialize_AllPropertiesIncluded_JsonContainsExpectedKeys()
        {
            // Arrange
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();
            DoubleDataValue[] data = [new DoubleDataValue(1.0, DataValueType.Exists)];
            JsonStat2 jsonStat = JsonStat2Builder.BuildJsonStat2(meta, data, "en");

            // Act
            string json = JsonSerializer.Serialize(jsonStat, SerializerOptions);
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            // Assert: all required JSON-stat 2.0 properties are present
            using (Assert.EnterMultipleScope())
            {
                Assert.That(root.TryGetProperty("version", out _), Is.True);
                Assert.That(root.TryGetProperty("class", out _), Is.True);
                Assert.That(root.TryGetProperty("id", out _), Is.True);
                Assert.That(root.TryGetProperty("label", out _), Is.True);
                Assert.That(root.TryGetProperty("source", out _), Is.True);
                Assert.That(root.TryGetProperty("updated", out _), Is.True);
                Assert.That(root.TryGetProperty("dimension", out _), Is.True);
                Assert.That(root.TryGetProperty("value", out _), Is.True);
                Assert.That(root.TryGetProperty("size", out _), Is.True);
                // PrecisionInfo should NOT appear in the JSON output
                Assert.That(root.TryGetProperty("precisionInfo", out _), Is.False);
                Assert.That(root.TryGetProperty("precisioninfo", out _), Is.False);
            }
        }

        private static ContentDimension BuildUniformPrecisionContentDimension(MatrixMetadata meta, int precision)
        {
            return new ContentDimension(
                meta.Dimensions[0].Code,
                meta.Dimensions[0].Name,
                meta.Dimensions[0].AdditionalProperties,
                [
                    new ContentDimensionValue(
                        TestMockMetaBuilder.GetMockDimensionValue("content-value0"),
                        new MultilanguageString([new("en", "unit"), new("fi", "unit"), new("sv", "unit")]),
                        DateTime.UtcNow,
                        precision: precision),
                    new ContentDimensionValue(
                        TestMockMetaBuilder.GetMockDimensionValue("content-value1"),
                        new MultilanguageString([new("en", "unit"), new("fi", "unit"), new("sv", "unit")]),
                        DateTime.UtcNow,
                        precision: precision),
                ]);
        }
    }
}
