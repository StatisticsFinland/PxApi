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
                Type = SearchResultType.Table,
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
                Assert.That(root.GetProperty("type").GetString(), Is.EqualTo("Table"));
                Assert.That(root.GetProperty("score").GetDouble(), Is.EqualTo(0.98));
                Assert.That(root.GetProperty("database").GetProperty("id").GetString(), Is.EqualTo("StatFin"));
                Assert.That(root.GetProperty("table").GetProperty("note").GetString(), Is.EqualTo("Population at year-end"));
                Assert.That(root.TryGetProperty("dimension", out _), Is.False);
                Assert.That(root.TryGetProperty("value", out _), Is.False);
                Assert.That(root.GetProperty("matches").GetArrayLength(), Is.EqualTo(1));
            }
        }

        [Test]
        public void Deserialize_TableResult_RoundTrips()
        {
            SearchResultItem original = new()
            {
                Type = SearchResultType.Table,
                Database = new SearchEntityRef { Id = "db1", Name = "DB One" },
                Table = new SearchEntityRef { Id = "t1", Name = "Table One" }
            };

            string json = JsonSerializer.Serialize(original, _jsonOptions);
            SearchResultItem? deserialized = JsonSerializer.Deserialize<SearchResultItem>(json, _jsonOptions);

            Assert.That(deserialized, Is.Not.Null);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(deserialized!.Type, Is.EqualTo(SearchResultType.Table));
                Assert.That(deserialized.Database.Id, Is.EqualTo("db1"));
                Assert.That(deserialized.Table.Id, Is.EqualTo("t1"));
                Assert.That(deserialized.Dimension, Is.Null);
                Assert.That(deserialized.Value, Is.Null);
                Assert.That(deserialized.Score, Is.Null);
                Assert.That(deserialized.Matches, Is.Null);
                Assert.That(deserialized.Links, Is.Null);
            }
        }

        #endregion

        #region Dimension result

        [Test]
        public void Serialize_DimensionResult_IncludesDimension()
        {
            SearchResultItem item = new()
            {
                Type = SearchResultType.Dimension,
                Score = 0.87,
                Database = new SearchEntityRef { Id = "StatFin", Name = "StatFin" },
                Table = new SearchEntityRef { Id = "statfin_vaerak_pxt_11ra", Name = "Population" },
                Dimension = new DimensionRef
                {
                    Id = "ika",
                    Name = "Age",
                    Note = "5-year age groups"
                },
                Matches =
                [
                    new MatchInfo
                    {
                        Path = "dimension.note",
                        MatchType = MatchType.Contains,
                        MatchedText = "5-year"
                    }
                ]
            };

            string json = JsonSerializer.Serialize(item, _jsonOptions);
            JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(root.GetProperty("type").GetString(), Is.EqualTo("Dimension"));
                Assert.That(root.GetProperty("dimension").GetProperty("id").GetString(), Is.EqualTo("ika"));
                Assert.That(root.GetProperty("dimension").GetProperty("note").GetString(), Is.EqualTo("5-year age groups"));
                Assert.That(root.TryGetProperty("value", out _), Is.False);
            }
        }

        #endregion

        #region Value result

        [Test]
        public void Serialize_ValueResult_IncludesDimensionAndValue()
        {
            SearchResultItem item = new()
            {
                Type = SearchResultType.Value,
                Score = 0.91,
                Database = new SearchEntityRef { Id = "StatFin", Name = "StatFin" },
                Table = new SearchEntityRef { Id = "statfin_vaerak_pxt_11ra", Name = "Population" },
                Dimension = new DimensionRef { Id = "sukupuoli", Name = "Sex" },
                Value = new SearchEntityRef { Id = "1", Name = "Males" },
                Matches =
                [
                    new MatchInfo
                    {
                        Path = "value.name",
                        MatchType = MatchType.Contains,
                        MatchedText = "male"
                    }
                ]
            };

            string json = JsonSerializer.Serialize(item, _jsonOptions);
            JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(root.GetProperty("type").GetString(), Is.EqualTo("Value"));
                Assert.That(root.GetProperty("dimension").GetProperty("id").GetString(), Is.EqualTo("sukupuoli"));
                Assert.That(root.GetProperty("value").GetProperty("id").GetString(), Is.EqualTo("1"));
                Assert.That(root.GetProperty("value").GetProperty("name").GetString(), Is.EqualTo("Males"));
            }
        }

        #endregion

        #region Dimension result with unit

        [Test]
        public void Serialize_DimensionResultWithUnit_IncludesUnitFields()
        {
            SearchResultItem item = new()
            {
                Type = SearchResultType.Dimension,
                Score = 0.84,
                Database = new SearchEntityRef { Id = "StatFin", Name = "StatFin" },
                Table = new SearchEntityRef { Id = "statfin_income_pxt_001", Name = "Income by household group" },
                Dimension = new DimensionRef
                {
                    Id = "tiedot",
                    Name = "Information",
                    Unit = "Euro"
                },
                Matches =
                [
                    new MatchInfo
                    {
                        Path = "dimension.unit",
                        MatchType = MatchType.Exact,
                        MatchedText = "Euro"
                    }
                ]
            };

            string json = JsonSerializer.Serialize(item, _jsonOptions);
            JsonDocument doc = JsonDocument.Parse(json);
            JsonElement dimension = doc.RootElement.GetProperty("dimension");

            Assert.That(dimension.GetProperty("unit").GetString(), Is.EqualTo("Euro"));
        }

        [Test]
        public void Deserialize_DimensionWithUnit_RoundTrips()
        {
            DimensionRef original = new()
            {
                Id = "tiedot",
                Name = "Information",
                Unit = "Euro"
            };

            string json = JsonSerializer.Serialize(original, _jsonOptions);
            DimensionRef? deserialized = JsonSerializer.Deserialize<DimensionRef>(json, _jsonOptions);

            Assert.That(deserialized, Is.Not.Null);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(deserialized!.Id, Is.EqualTo("tiedot"));
                Assert.That(deserialized.Unit, Is.EqualTo("Euro"));
            }
        }

        #endregion

        #region Links

        [Test]
        public void Serialize_WithLinks_IncludesLinksArray()
        {
            SearchResultItem item = new()
            {
                Type = SearchResultType.Table,
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
                Type = SearchResultType.Table,
                Database = new SearchEntityRef { Id = "db", Name = "DB" },
                Table = new SearchEntityRef { Id = "t", Name = "T" }
            };

            string json = JsonSerializer.Serialize(item, _jsonOptions);
            JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(root.TryGetProperty("score", out _), Is.False);
                Assert.That(root.TryGetProperty("dimension", out _), Is.False);
                Assert.That(root.TryGetProperty("value", out _), Is.False);
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
