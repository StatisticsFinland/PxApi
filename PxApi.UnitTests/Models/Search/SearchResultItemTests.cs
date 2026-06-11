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
                Database = new SearchDatabaseRef { Id = "StatFin", Name = "StatFin" },
                Table = CreateSummary("11ra", "Population according to age and sex"),
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
                Assert.That(root.GetProperty("table").GetProperty("tableId").GetString(), Is.EqualTo("11ra"));
                Assert.That(root.GetProperty("table").GetProperty("title").GetString(), Is.EqualTo("Population according to age and sex"));
                Assert.That(root.GetProperty("matches").GetArrayLength(), Is.EqualTo(1));
            }
        }

        [Test]
        public void Deserialize_TableResult_RoundTrips()
        {
            SearchResultItem original = new()
            {
                Database = new SearchDatabaseRef { Id = "db1", Name = "DB One" },
                Table = CreateSummary("t1", "Table One")
            };

            string json = JsonSerializer.Serialize(original, _jsonOptions);
            SearchResultItem? deserialized = JsonSerializer.Deserialize<SearchResultItem>(json, _jsonOptions);

            Assert.That(deserialized, Is.Not.Null);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(deserialized!.Database.Id, Is.EqualTo("db1"));
                Assert.That(deserialized.Table.TableId, Is.EqualTo("t1"));
                Assert.That(deserialized.Table.Title, Is.EqualTo("Table One"));
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
                Database = new SearchDatabaseRef { Id = "StatFin", Name = "StatFin" },
                Table = CreateSummary("t1", "Table"),
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
                Database = new SearchDatabaseRef { Id = "db", Name = "DB" },
                Table = CreateSummary("t", "T")
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

        private static TableSummary CreateSummary(string code, string name)
        {
            return new TableSummary
            {
                TableId = code,
                Title = name,
                Metrics =
                [
                    new MetricInfo
                    {
                        Name = "value",
                        Unit = "unit"
                    }
                ],
                TimeRange = new TimeRange
                {
                    From = "2020",
                    To = "2024"
                },
                Dimensions =
                [
                    new DimensionInfo
                    {
                        Name = "dim",
                        Size = 1
                    }
                ],
                LastUpdated = new DateTime(2024, 10, 10, 0, 0, 0, DateTimeKind.Utc)
            };
        }
    }
}
