using PxApi.Models.QueryFilters;
using PxApi.Utilities;

namespace PxApi.UnitTests.Utils
{
    /// <summary>
    /// Tests for the <see cref="QueryFilterUtils"/> class.
    /// </summary>
    [TestFixture]
    public class QueryFilterUtilsTests
    {
        [Test]
        public void ConvertUrlParametersToFilters_CodeFilter_CreatesCorrectFilter()
        {
            // Arrange
            Dictionary<string, string> parameters = new()
            {
                { "gender.code", "1,2,3" }
            };

            // Act
            Dictionary<string, IFilter> filters = QueryFilterUtils.ConvertUrlParametersToFilters(parameters);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(filters, Has.Count.EqualTo(1));
                Assert.That(filters.ContainsKey("gender"), Is.True);
                Assert.That(filters["gender"], Is.TypeOf<CodeFilter>());

                CodeFilter? codeFilter = filters["gender"] as CodeFilter;
                Assert.That(codeFilter!.FilterStrings, Has.Count.EqualTo(3));
                Assert.That(codeFilter.FilterStrings, Contains.Item("1"));
                Assert.That(codeFilter.FilterStrings, Contains.Item("2"));
                Assert.That(codeFilter.FilterStrings, Contains.Item("3"));
            });
        }

        [Test]
        public void ConvertUrlParametersToFilters_WildcardCodeFilter_CreatesCorrectFilter()
        {
            // Arrange
            Dictionary<string, string> parameters = new()
            {
                { "year.code", "*" }
            };

            // Act
            Dictionary<string, IFilter> filters = QueryFilterUtils.ConvertUrlParametersToFilters(parameters);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(filters, Has.Count.EqualTo(1));
                Assert.That(filters.ContainsKey("year"), Is.True);
                Assert.That(filters["year"], Is.TypeOf<CodeFilter>());

                CodeFilter? codeFilter = filters["year"] as CodeFilter;
                Assert.That(codeFilter!.FilterStrings, Has.Count.EqualTo(1));
                Assert.That(codeFilter.FilterStrings, Contains.Item("*"));
            });
        }

        [Test]
        public void ConvertUrlParametersToFilters_FromFilter_CreatesCorrectFilter()
        {
            // Arrange
            Dictionary<string, string> parameters = new()
            {
                { "year.from", "2020" }
            };

            // Act
            Dictionary<string,IFilter> filters = QueryFilterUtils.ConvertUrlParametersToFilters(parameters);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(filters, Has.Count.EqualTo(1));
                Assert.That(filters.ContainsKey("year"), Is.True);
                Assert.That(filters["year"], Is.TypeOf<FromFilter>());

                FromFilter? fromFilter = filters["year"] as FromFilter;
                Assert.That(fromFilter!.FilterString, Is.EqualTo("2020"));
            });
        }

        [Test]
        public void ConvertUrlParametersToFilters_ToFilter_CreatesCorrectFilter()
        {
            // Arrange
            Dictionary<string, string> parameters = new()
            {
                { "year.to", "2023" }
            };

            // Act
            Dictionary<string,IFilter> filters = QueryFilterUtils.ConvertUrlParametersToFilters(parameters);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(filters, Has.Count.EqualTo(1));
                Assert.That(filters.ContainsKey("year"), Is.True);
                Assert.That(filters["year"], Is.TypeOf<ToFilter>());

                ToFilter? toFilter = filters["year"] as ToFilter;
                Assert.That(toFilter!.FilterString, Is.EqualTo("2023"));
            });
        }
        
        [Test]
        public void ConvertUrlParametersToFilters_FirstFilter_CreatesCorrectFilter()
        {
            // Arrange
            Dictionary<string, string> parameters = new()
            {
                { "year.first", "5" }
            };

            // Act
            Dictionary<string,IFilter> filters = QueryFilterUtils.ConvertUrlParametersToFilters(parameters);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(filters, Has.Count.EqualTo(1));
                Assert.That(filters.ContainsKey("year"), Is.True);
                Assert.That(filters["year"], Is.TypeOf<FirstFilter>());

                FirstFilter? firstFilter = filters["year"] as FirstFilter;
                Assert.That(firstFilter!.Count, Is.EqualTo(5));
            });
        }
        
        [Test]
        public void ConvertUrlParametersToFilters_LastFilter_CreatesCorrectFilter()
        {
            // Arrange
            Dictionary<string, string> parameters = new()
            {
                { "year.last", "3" }
            };

            // Act
            Dictionary<string, IFilter> filters = QueryFilterUtils.ConvertUrlParametersToFilters(parameters);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(filters, Has.Count.EqualTo(1));
                Assert.That(filters.ContainsKey("year"), Is.True);
                Assert.That(filters["year"], Is.TypeOf<LastFilter>());

                LastFilter? lastFilter = filters["year"] as LastFilter;
                Assert.That(lastFilter!.Count, Is.EqualTo(3));
            });
        }

        [Test]
        public void ConvertUrlParametersToFilters_FirstFilterWithInvalidValue_IgnoresFilter()
        {
            // Arrange
            Dictionary<string, string> parameters = new()
            {
                { "year.first", "invalid" }
            };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => QueryFilterUtils.ConvertUrlParametersToFilters(parameters));
        }

        [Test]
        public void ConvertUrlParametersToFilters_LastFilterWithNegativeValue_IgnoresFilter()
        {
            // Arrange
            Dictionary<string, string> parameters = new()
            {
                { "year.last", "-5" }
            };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => QueryFilterUtils.ConvertUrlParametersToFilters(parameters));
        }

        [Test]
        public void ConvertUrlParametersToFilters_MultipleFilters_CreatesAllFilters()
        {
            // Arrange
            Dictionary<string, string> parameters = new()
            {
                { "gender.code", "1,2" },
                { "year.from", "2020" },
                { "region.code", "*" },
                { "age.first", "10" }
            };

            // Act
            Dictionary<string, IFilter> filters = QueryFilterUtils.ConvertUrlParametersToFilters(parameters);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(filters, Has.Count.EqualTo(4));
                Assert.That(filters.ContainsKey("gender"), Is.True);
                Assert.That(filters.ContainsKey("year"), Is.True);
                Assert.That(filters.ContainsKey("region"), Is.True);
                Assert.That(filters.ContainsKey("age"), Is.True);
                
                Assert.That(filters["gender"], Is.TypeOf<CodeFilter>());
                Assert.That(filters["year"], Is.TypeOf<FromFilter>());
                Assert.That(filters["region"], Is.TypeOf<CodeFilter>());
                Assert.That(filters["age"], Is.TypeOf<FirstFilter>());
            });
        }
        
        [Test]
        public void ConvertUrlParametersToFilters_MultipleDimensionFilters_CreatesFilterLists()
        {
            // Arrange
            Dictionary<string, string> parameters = new()
            {
                { "year.from", "2020" },
                { "year.to", "2023" },
                { "year.code", "2022" }
            };

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
            {
                Dictionary<string, IFilter> filters = QueryFilterUtils.ConvertUrlParametersToFilters(parameters);
            });
        }

        [Test]
        public void ConvertUrlParametersToFilters_InvalidFormat_IgnoresInvalidParameters()
        {
            // Arrange
            Dictionary<string, string> parameters = new()
            {
                { "gender.code", "1,2" },
                { "invalid", "value" },
                { "invalid.format.extra", "value" }
            };

            // Act
            Dictionary<string, IFilter> filters = QueryFilterUtils.ConvertUrlParametersToFilters(parameters);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(filters, Has.Count.EqualTo(1));
                Assert.That(filters.ContainsKey("gender"), Is.True);
                Assert.That(filters["gender"], Is.TypeOf<CodeFilter>());
            });
        }

        [Test]
        public void ConvertUrlParametersToFilters_EmptyParameters_ReturnsEmptyDictionary()
        {
            // Arrange
            Dictionary<string, string> parameters = [];

            // Act
            Dictionary<string, IFilter> filters = QueryFilterUtils.ConvertUrlParametersToFilters(parameters);

            // Assert
            Assert.That(filters, Is.Empty);
        }
    }
}