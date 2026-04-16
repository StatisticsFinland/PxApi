using System.Text.Json;
using PxApi.Configuration;
using PxApi.Models;
using PxApi.Models.Search;
using MatchType = PxApi.Models.Search.MatchType;

namespace PxApi.UnitTests.Models.Search
{
    [TestFixture]
    internal class SearchResultItemTests
    {
        private readonly JsonSerializerOptions _jsonOptions = GlobalJsonConverterOptions.Default;

        #region Table result

        [Test]
        public void Serialize_TableResult_ContainsExpectedFields()
        {
            SearchResultItem item = new()
            {
                Score = 0.98,
                Database = new SearchEntityRef { Id = "StatFin", Name = "StatFin" },
                Table = new SearchEntityRef
                {
                    Id = "statfin_vaerak_pxt_11ra",
                    Name = "Population according to age and sex",
                    Note = "Population at year-end"
                },
                Matches =
                [
                    new MatchInfo
                    {
                        Path = "table.name",
                        MatchType = MatchType.Contains,
                        MatchedText = "population"
                    }
                ]
            };

            string json = JsonSerializer.Serialize(item, _jsonOptions);
            JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(root.GetProperty("score").GetDouble(), Is.EqualTo(0.98));
                Assert.That(root.GetProperty("database").GetProperty("id").GetString(), Is.EqualTo("StatFin"));
                Assert.That(root.GetProperty("table").GetProperty("note").GetString(), Is.EqualTo("Population at year-end"));
                Assert.That(root.GetProperty("matches").GetArrayLength(), Is.EqualTo(1));
            }
        }

        [Test]
        public void Deserialize_TableResult_RoundTrips()
        {
            SearchResultItem original = new()
            {
                Database = new SearchEntityRef { Id = "db1", Name = "DB One" },
                Table = new SearchEntityRef { Id = "t1", Name = "Table One" }
            };

            string json = JsonSerializer.Serialize(original, _jsonOptions);
            SearchResultItem? deserialized = JsonSerializer.Deserialize<SearchResultItem>(json, _jsonOptions);

            Assert.That(deserialized, Is.Not.Null);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(deserialized!.Database.Id, Is.EqualTo("db1"));
                Assert.That(deserialized.Table.Id, Is.EqualTo("t1"));
                Assert.That(deserialized.Score, Is.Null);
                Assert.That(deserialized.Matches, Is.Null);
                Assert.That(deserialized.Links, Is.Null);
            }
        }

        #endregion

        #region Links

        [Test]
        public void Serialize_WithLinks_IncludesLinksArray()
        {
            SearchResultItem item = new()
            {
                Database = new SearchEntityRef { Id = "StatFin", Name = "StatFin" },
                Table = new SearchEntityRef { Id = "t1", Name = "Table" },
                Links =
                [
                    new Link
                    {
                        Rel = "metadata",
                        Href = "/meta/databases/StatFin/tables/t1",
                        Method = "GET"
                    }
                ]
            };

            string json = JsonSerializer.Serialize(item, _jsonOptions);
            JsonDocument doc = JsonDocument.Parse(json);
            JsonElement links = doc.RootElement.GetProperty("links");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(links.GetArrayLength(), Is.EqualTo(1));
                Assert.That(links[0].GetProperty("rel").GetString(), Is.EqualTo("metadata"));
                Assert.That(links[0].GetProperty("href").GetString(), Is.EqualTo("/meta/databases/StatFin/tables/t1"));
            }
        }

        #endregion

        #region Null omission

        [Test]
        public void Serialize_NullOptionalFields_OmitsFromJson()
        {
            SearchResultItem item = new()
            {
                Database = new SearchEntityRef { Id = "db", Name = "DB" },
                Table = new SearchEntityRef { Id = "t", Name = "T" }
            };

            string json = JsonSerializer.Serialize(item, _jsonOptions);
            JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(root.TryGetProperty("score", out _), Is.False);
                Assert.That(root.TryGetProperty("matches", out _), Is.False);
                Assert.That(root.TryGetProperty("links", out _), Is.False);
            }
        }

        #endregion

        #region MatchInfo

        [Test]
        public void Serialize_MatchInfo_ContainsAllFields()
        {
            MatchInfo match = new()
            {
                Path = "value.name",
                MatchType = MatchType.Contains,
                MatchedText = "male"
            };

            string json = JsonSerializer.Serialize(match, _jsonOptions);
            JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(root.GetProperty("path").GetString(), Is.EqualTo("value.name"));
                Assert.That(root.GetProperty("matchType").GetString(), Is.EqualTo("Contains"));
                Assert.That(root.GetProperty("matchedText").GetString(), Is.EqualTo("male"));
            }
        }

        [Test]
        public void Deserialize_MatchInfo_RoundTrips()
        {
            MatchInfo original = new()
            {
                Path = "dimension.name",
                MatchType = MatchType.Exact,
                MatchedText = "Sex"
            };

            string json = JsonSerializer.Serialize(original, _jsonOptions);
            MatchInfo? deserialized = JsonSerializer.Deserialize<MatchInfo>(json, _jsonOptions);

            Assert.That(deserialized, Is.Not.Null);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(deserialized!.Path, Is.EqualTo("dimension.name"));
                Assert.That(deserialized.MatchType, Is.EqualTo(MatchType.Exact));
                Assert.That(deserialized.MatchedText, Is.EqualTo("Sex"));
            }
        }

        #endregion
    }
}
