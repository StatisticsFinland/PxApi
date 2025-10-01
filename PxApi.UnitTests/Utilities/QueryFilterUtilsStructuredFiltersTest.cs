using NUnit.Framework;
using PxApi.Models.QueryFilters;
using PxApi.Utilities;

namespace PxApi.UnitTests.Utilities
{
    /// <summary>
    /// Unit tests for the QueryFilterUtils class, specifically testing the filters array syntax.
    /// </summary>
    [TestFixture]
    public class QueryFilterUtilsStructuredFiltersTest
    {
        [Test]
        public void ConvertFiltersArrayToFilters_WithSingleFilter_ReturnsCorrectFilter()
        {
            // Arrange
            string[] filtersArray = ["gender:code=1,2"];

            // Act
            Dictionary<string, Filter> result = QueryFilterUtils.ConvertFiltersArrayToFilters(filtersArray);

            // Assert
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result.ContainsKey("gender"), Is.True);
            Assert.That(result["gender"], Is.TypeOf<CodeFilter>());
            
            CodeFilter codeFilter = (CodeFilter)result["gender"];
            Assert.That(codeFilter.FilterStrings, Is.EqualTo(new[] { "1", "2" }));
        }

        [Test]
        public void ConvertFiltersArrayToFilters_WithMultipleFilters_ReturnsAllFilters()
        {
            // Arrange
            string[] filtersArray = ["gender:code=1,2", "year:from=2020", "region:last=5"];

            // Act
            Dictionary<string, Filter> result = QueryFilterUtils.ConvertFiltersArrayToFilters(filtersArray);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Has.Count.EqualTo(3));
                Assert.That(result.ContainsKey("gender"), Is.True);
                Assert.That(result.ContainsKey("year"), Is.True);
                Assert.That(result.ContainsKey("region"), Is.True);

                // Check gender filter
                Assert.That(result["gender"], Is.TypeOf<CodeFilter>());
                CodeFilter genderFilter = (CodeFilter)result["gender"];
                Assert.That(genderFilter.FilterStrings, Is.EqualTo(new[] { "1", "2" }));

                // Check year filter
                Assert.That(result["year"], Is.TypeOf<FromFilter>());
                FromFilter yearFilter = (FromFilter)result["year"];
                Assert.That(yearFilter.FilterString, Is.EqualTo("2020"));

                // Check region filter
                Assert.That(result["region"], Is.TypeOf<LastFilter>());
                LastFilter regionFilter = (LastFilter)result["region"];
                Assert.That(regionFilter.Count, Is.EqualTo(5));
            });
        }

        [Test]
        public void ConvertFiltersArrayToFilters_WithAllFilterTypes_ReturnsCorrectFilters()
        {
            // Arrange
            string[] filtersArray = [
                "gender:code=1,2",
                "region:code=*",
                "year:from=2020",
                "month:to=12",
                "category:first=10",
                "area:last=5"
            ];

            // Act
            Dictionary<string, Filter> result = QueryFilterUtils.ConvertFiltersArrayToFilters(filtersArray);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Has.Count.EqualTo(6));

                // Code filter
                Assert.That(result["gender"], Is.TypeOf<CodeFilter>());
                
                // Code filter with wildcard (replaces AllFilter)
                Assert.That(result["region"], Is.TypeOf<CodeFilter>());
                CodeFilter? wildcardFilter = result["region"] as CodeFilter;
                Assert.That(wildcardFilter!.FilterStrings, Contains.Item("*"));
                
                // From filter
                Assert.That(result["year"], Is.TypeOf<FromFilter>());
                
                // To filter
                Assert.That(result["month"], Is.TypeOf<ToFilter>());
                
                // First filter
                Assert.That(result["category"], Is.TypeOf<FirstFilter>());
                FirstFilter? firstFilter = result["category"] as FirstFilter;
                Assert.That(firstFilter!.Count, Is.EqualTo(10));
                
                // Last filter
                Assert.That(result["area"], Is.TypeOf<LastFilter>());
                LastFilter? lastFilter = result["area"] as LastFilter;
                Assert.That(lastFilter!.Count, Is.EqualTo(5));
            });
        }

        [Test]
        public void ConvertFiltersArrayToFilters_WithEmptyArray_ReturnsEmptyDictionary()
        {
            // Arrange
            string[] filtersArray = [];

            // Act
            Dictionary<string, Filter> result = QueryFilterUtils.ConvertFiltersArrayToFilters(filtersArray);

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void ConvertFiltersArrayToFilters_WithWhitespaceEntries_IgnoresWhitespace()
        {
            // Arrange
            string[] filtersArray = ["gender:code=1,2", "", "  ", "year:from=2020"];

            // Act
            Dictionary<string, Filter> result = QueryFilterUtils.ConvertFiltersArrayToFilters(filtersArray);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Has.Count.EqualTo(2));
                Assert.That(result.ContainsKey("gender"), Is.True);
                Assert.That(result.ContainsKey("year"), Is.True);
            });
        }

        [Test]
        public void ConvertFiltersArrayToFilters_WithInvalidFormat_ThrowsArgumentException()
        {
            // Arrange
            string[] filtersArray = ["invalidformat"];

            // Act & Assert
            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                QueryFilterUtils.ConvertFiltersArrayToFilters(filtersArray));
            
            Assert.That(exception.Message, Does.Contain("Invalid filter format"));
        }

        [Test]
        public void ConvertFiltersArrayToFilters_WithDuplicateDimension_ThrowsArgumentException()
        {
            // Arrange
            string[] filtersArray = ["gender:code=1", "gender:from=2020"];

            // Act & Assert
            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                QueryFilterUtils.ConvertFiltersArrayToFilters(filtersArray));
            
            Assert.That(exception.Message, Does.Contain("Duplicate dimension"));
        }

        [Test]
        public void ConvertFiltersArrayToFilters_WithInvalidCount_ThrowsArgumentException()
        {
            // Arrange
            string[] filtersArray = ["region:first=invalid"];

            // Act & Assert
            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                QueryFilterUtils.ConvertFiltersArrayToFilters(filtersArray));
            
            Assert.That(exception.Message, Does.Contain("Must be a positive integer"));
        }

        [Test]
        public void ConvertFiltersArrayToFilters_WithUnknownFilterType_ThrowsArgumentException()
        {
            // Arrange
            string[] filtersArray = ["region:unknown=value"];

            // Act & Assert
            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                QueryFilterUtils.ConvertFiltersArrayToFilters(filtersArray));
            
            Assert.That(exception.Message, Does.Contain("Unsupported filter type"));
        }

        [Test]
        public void ConvertFiltersArrayToFilters_WithMissingValue_ThrowsArgumentException()
        {
            // Arrange
            string[] filtersArray = ["gender:code"];

            // Act & Assert
            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                QueryFilterUtils.ConvertFiltersArrayToFilters(filtersArray));
            
            Assert.That(exception.Message, Does.Contain("Invalid filter format"));
        }

        [Test]
        public void ConvertFiltersArrayToFilters_WithComplexValues_HandlesCorrectly()
        {
            // Arrange
            string[] filtersArray = [
                "category:code=manufacturing,services,agriculture",
                "region:code=Helsinki,Tampere,Turku",
                "year:code=2020,2021,2022,2023"
            ];

            // Act
            Dictionary<string, Filter> result = QueryFilterUtils.ConvertFiltersArrayToFilters(filtersArray);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Has.Count.EqualTo(3));
                
                CodeFilter categoryFilter = (CodeFilter)result["category"];
                Assert.That(categoryFilter.FilterStrings, Is.EqualTo(new[] { "manufacturing", "services", "agriculture" }));
                
                CodeFilter regionFilter = (CodeFilter)result["region"];
                Assert.That(regionFilter.FilterStrings, Is.EqualTo(new[] { "Helsinki", "Tampere", "Turku" }));
                
                CodeFilter yearFilter = (CodeFilter)result["year"];
                Assert.That(yearFilter.FilterStrings, Is.EqualTo(new[] { "2020", "2021", "2022", "2023" }));
            });
        }
    }
}