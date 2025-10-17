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
        public void ConvertFiltersArrayToFilters_CodeFilter_CreatesCorrectFilter()
        {
            // Arrange
            string[] filtersArray = ["gender:code=1,2,3"];

            // Act
            Dictionary<string, Filter> filters = QueryFilterUtils.ConvertFiltersArrayToFilters(filtersArray);

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
        public void ConvertFiltersArrayToFilters_WildcardCodeFilter_CreatesCorrectFilter()
        {
            // Arrange
            string[] filtersArray = ["year:code=*"];

            // Act
            Dictionary<string, Filter> filters = QueryFilterUtils.ConvertFiltersArrayToFilters(filtersArray);

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
        public void ConvertFiltersArrayToFilters_FromFilter_CreatesCorrectFilter()
        {
            // Arrange
            string[] filtersArray = ["year:from=2020"];

            // Act
            Dictionary<string, Filter> filters = QueryFilterUtils.ConvertFiltersArrayToFilters(filtersArray);

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
        public void ConvertFiltersArrayToFilters_ToFilter_CreatesCorrectFilter()
        {
            // Arrange
            string[] filtersArray = ["year:to=2023"];

            // Act
            Dictionary<string, Filter> filters = QueryFilterUtils.ConvertFiltersArrayToFilters(filtersArray);

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
        public void ConvertFiltersArrayToFilters_FirstFilter_CreatesCorrectFilter()
        {
            // Arrange
            string[] filtersArray = ["year:first=5"];

            // Act
            Dictionary<string, Filter> filters = QueryFilterUtils.ConvertFiltersArrayToFilters(filtersArray);

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
        public void ConvertFiltersArrayToFilters_LastFilter_CreatesCorrectFilter()
        {
            // Arrange
            string[] filtersArray = ["year:last=3"];

            // Act
            Dictionary<string, Filter> filters = QueryFilterUtils.ConvertFiltersArrayToFilters(filtersArray);

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
        public void ConvertFiltersArrayToFilters_FirstFilterWithInvalidValue_ThrowsException()
        {
            // Arrange
            string[] filtersArray = ["year:first=invalid"];

            // Act & Assert
            Assert.Throws<ArgumentException>(() => QueryFilterUtils.ConvertFiltersArrayToFilters(filtersArray));
        }

        [Test]
        public void ConvertFiltersArrayToFilters_LastFilterWithNegativeValue_ThrowsException()
        {
            // Arrange
            string[] filtersArray = ["year:last=-5"];

            // Act & Assert
            Assert.Throws<ArgumentException>(() => QueryFilterUtils.ConvertFiltersArrayToFilters(filtersArray));
        }

        [Test]
        public void ConvertFiltersArrayToFilters_MultipleFilters_CreatesAllFilters()
        {
            // Arrange
            string[] filtersArray = [
                "gender:code=1,2",
                "year:from=2020",
                "region:code=*",
                "age:first=10"
            ];

            // Act
            Dictionary<string, Filter> filters = QueryFilterUtils.ConvertFiltersArrayToFilters(filtersArray);

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
        public void ConvertFiltersArrayToFilters_DuplicateDimensionFilters_ThrowsException()
        {
            // Arrange
            string[] filtersArray = [
                "year:from=2020",
                "year:to=2023"
            ];

            // Act & Assert
            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                QueryFilterUtils.ConvertFiltersArrayToFilters(filtersArray));
            
            Assert.That(exception.Message, Does.Contain("Duplicate dimension"));
        }

        [Test]
        public void ConvertFiltersArrayToFilters_InvalidFormat_ThrowsException()
        {
            // Arrange
            string[] filtersArray = [
                "gender:code=1,2",
                "invalid",
                "invalid:format:extra"
            ];

            // Act & Assert
            Assert.Throws<ArgumentException>(() => QueryFilterUtils.ConvertFiltersArrayToFilters(filtersArray));
        }

        [Test]
        public void ConvertFiltersArrayToFilters_EmptyArray_ReturnsEmptyDictionary()
        {
            // Arrange
            string[] filtersArray = [];

            // Act
            Dictionary<string, Filter> filters = QueryFilterUtils.ConvertFiltersArrayToFilters(filtersArray);

            // Assert
            Assert.That(filters, Is.Empty);
        }

        [Test]
        public void ConvertFiltersArrayToFilters_WhitespaceAndEmptyEntries_IgnoresInvalidEntries()
        {
            // Arrange
            string[] filtersArray = [
                "gender:code=1,2",
                "",
                "   ",
                "year:from=2020"
            ];

            // Act
            Dictionary<string, Filter> filters = QueryFilterUtils.ConvertFiltersArrayToFilters(filtersArray);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(filters, Has.Count.EqualTo(2));
                Assert.That(filters.ContainsKey("gender"), Is.True);
                Assert.That(filters.ContainsKey("year"), Is.True);
                Assert.That(filters["gender"], Is.TypeOf<CodeFilter>());
                Assert.That(filters["year"], Is.TypeOf<FromFilter>());
            });
        }

        [Test]
        public void ConvertFiltersArrayToFilters_UnsupportedFilterType_ThrowsException()
        {
            // Arrange
            string[] filtersArray = ["region:unsupported=value"];

            // Act & Assert
            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                QueryFilterUtils.ConvertFiltersArrayToFilters(filtersArray));
            
            Assert.That(exception.Message, Does.Contain("Unsupported filter type"));
        }

        [Test]
        public void ConvertFiltersArrayToFilters_MissingValue_ThrowsException()
        {
            // Arrange
            string[] filtersArray = ["gender:code"];

            // Act & Assert
            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                QueryFilterUtils.ConvertFiltersArrayToFilters(filtersArray));
            
            Assert.That(exception.Message, Does.Contain("Invalid filter format"));
        }

        [Test]
        public void ConvertFiltersArrayToFilters_ComplexCodeValues_HandlesCorrectly()
        {
            // Arrange
            string[] filtersArray = [
                "category:code=manufacturing,services,agriculture",
                "region:code=Helsinki,Tampere,Turku",
                "year:code=2020,2021,2022,2023"
            ];

            // Act
            Dictionary<string, Filter> filters = QueryFilterUtils.ConvertFiltersArrayToFilters(filtersArray);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(filters, Has.Count.EqualTo(3));
                
                CodeFilter? categoryFilter = filters["category"] as CodeFilter;
                Assert.That(categoryFilter!.FilterStrings, Is.EqualTo(new[] { "manufacturing", "services", "agriculture" }));
                
                CodeFilter? regionFilter = filters["region"] as CodeFilter;
                Assert.That(regionFilter!.FilterStrings, Is.EqualTo(new[] { "Helsinki", "Tampere", "Turku" }));
                
                CodeFilter? yearFilter = filters["year"] as CodeFilter;
                Assert.That(yearFilter!.FilterStrings, Is.EqualTo(new[] { "2020", "2021", "2022", "2023" }));
            });
        }

        [Test]
        public void ConvertFiltersArrayToFilters_AllFilterTypes_ReturnsCorrectFilters()
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
            Dictionary<string, Filter> filters = QueryFilterUtils.ConvertFiltersArrayToFilters(filtersArray);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(filters, Has.Count.EqualTo(6));

                // Code filter with specific values
                Assert.That(filters["gender"], Is.TypeOf<CodeFilter>());
                
                // Code filter with wildcard
                Assert.That(filters["region"], Is.TypeOf<CodeFilter>());
                CodeFilter? wildcardFilter = filters["region"] as CodeFilter;
                Assert.That(wildcardFilter!.FilterStrings, Contains.Item("*"));
                
                // From filter
                Assert.That(filters["year"], Is.TypeOf<FromFilter>());
                
                // To filter
                Assert.That(filters["month"], Is.TypeOf<ToFilter>());
                
                // First filter
                Assert.That(filters["category"], Is.TypeOf<FirstFilter>());
                FirstFilter? firstFilter = filters["category"] as FirstFilter;
                Assert.That(firstFilter!.Count, Is.EqualTo(10));
                
                // Last filter
                Assert.That(filters["area"], Is.TypeOf<LastFilter>());
                LastFilter? lastFilter = filters["area"] as LastFilter;
                Assert.That(lastFilter!.Count, Is.EqualTo(5));
            });
        }
    }
}