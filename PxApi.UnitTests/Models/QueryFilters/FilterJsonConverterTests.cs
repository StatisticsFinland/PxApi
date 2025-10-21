using PxApi.Models.QueryFilters;
using System.Text.Json;

namespace PxApi.UnitTests.Models.QueryFilters
{
    [TestFixture]
    public class FilterJsonConverterTests
    {
        private JsonSerializerOptions _options = null!;

        [SetUp]
        public void SetUp()
        {
            _options = new JsonSerializerOptions();
            _options.Converters.Add(new FilterJsonConverter());
        }

        [Test]
        public void Serialize_CodeFilter_WritesExpectedJson()
        {
            CodeFilter filter = new(["A", "B*"]);
            string json = JsonSerializer.Serialize<Filter>(filter, _options);
            const string expected = "{\"type\":\"Code\",\"query\":[\"A\",\"B*\"]}";
            Assert.That(JsonEquals(json, expected), Is.True);
        }

        [Test]
        public void RoundTrip_CodeFilter_PreservesValues()
        {
            CodeFilter original = new(["X1", "Y_*"]);
            string json = JsonSerializer.Serialize<Filter>(original, _options);
            Filter? deserialized = JsonSerializer.Deserialize<Filter>(json, _options);
            Assert.That(deserialized, Is.TypeOf<CodeFilter>());
            CodeFilter result = (CodeFilter)deserialized!;
            Assert.Multiple(() =>
            {
                Assert.That(result.FilterStrings, Has.Count.EqualTo(2));
                Assert.That(result.FilterStrings[0], Is.EqualTo("X1"));
                Assert.That(result.FilterStrings[1], Is.EqualTo("Y_*"));
            });
        }

        [Test]
        public void Deserialize_FromFilter_ReturnsCorrectInstance()
        {
            const string json = "{\"type\":\"From\",\"query\":\"A*\"}";
            Filter? filter = JsonSerializer.Deserialize<Filter>(json, _options);
            Assert.That(filter, Is.TypeOf<FromFilter>());
            FromFilter fromFilter = (FromFilter)filter!;
            Assert.That(fromFilter.FilterString, Is.EqualTo("A*"));
        }

        [Test]
        public void Deserialize_ToFilter_ReturnsCorrectInstance()
        {
            const string json = "{\"type\":\"To\",\"query\":\"Z_*\"}";
            Filter? filter = JsonSerializer.Deserialize<Filter>(json, _options);
            Assert.That(filter, Is.TypeOf<ToFilter>());
            ToFilter toFilter = (ToFilter)filter!;
            Assert.That(toFilter.FilterString, Is.EqualTo("Z_*"));
        }

        [Test]
        public void Deserialize_FirstFilter_ReturnsCorrectInstance()
        {
            const string json = "{\"type\":\"First\",\"query\":5}";
            Filter? filter = JsonSerializer.Deserialize<Filter>(json, _options);
            Assert.That(filter, Is.TypeOf<FirstFilter>());
            FirstFilter firstFilter = (FirstFilter)filter!;
            Assert.That(firstFilter.Count, Is.EqualTo(5));
        }

        [Test]
        public void Deserialize_LastFilter_ReturnsCorrectInstance()
        {
            const string json = "{\"type\":\"Last\",\"query\":3}";
            Filter? filter = JsonSerializer.Deserialize<Filter>(json, _options);
            Assert.That(filter, Is.TypeOf<LastFilter>());
            LastFilter lastFilter = (LastFilter)filter!;
            Assert.That(lastFilter.Count, Is.EqualTo(3));
        }

        [Test]
        public void Deserialize_CodeFilter_WithInvalidCharacter_Throws()
        {
            const string json = "{\"type\":\"Code\",\"query\":[\"Valid\",\"In-valid\"]}"; // '-' not allowed
            Assert.That(() => JsonSerializer.Deserialize<Filter>(json, _options), Throws.ArgumentException);
        }

        [Test]
        public void Deserialize_CodeFilter_WithTooLongString_Throws()
        {
            string longString = new('a', 51);
            string json = $"{{\"type\":\"Code\",\"query\":[\"{longString}\"]}}";
            Assert.That(() => JsonSerializer.Deserialize<Filter>(json, _options), Throws.ArgumentException);
        }

        private static bool JsonEquals(string a, string b)
        {
            using JsonDocument ja = JsonDocument.Parse(a);
            using JsonDocument jb = JsonDocument.Parse(b);
            return ja.RootElement.ToString() == jb.RootElement.ToString();
        }
    }
}
