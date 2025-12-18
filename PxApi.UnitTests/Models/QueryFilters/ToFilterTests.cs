using Px.Utils.Models.Metadata;
using PxApi.Models.QueryFilters;

namespace PxApi.UnitTests.Models.QueryFilters
{
    /// <summary>
    /// Tests for the ToFilter class that returns elements up to and including a match.
    /// </summary>
    [TestFixture]
    internal class ToFilterTests
    {
        [Test]
        public void Filter_WithExactMatch_ReturnsElementsUpToAndIncludingMatch()
        {
            // Arrange
            ToFilter filter = new()
            {
                FilterString = "item3"
            };
            DimensionMap input = new("foo", ["item1", "item2", "item3", "item4", "item5"]);
            string[] expectedValues = ["item1", "item2", "item3"];

            // Act
            DimensionMap result = filter.Apply(input);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ValueCodes, Has.Count.EqualTo(3));
                Assert.That(result.ValueCodes, Is.EquivalentTo(expectedValues));
            });
        }

        [Test]
        public void Filter_WithWildcardMatch_ReturnsElementsUpToAndIncludingMatch()
        {
            // Arrange
            ToFilter filter = new()
            {
                FilterString = "item*"
            };
            DimensionMap input = new("foo", ["test1", "test2", "item3", "test4", "test5"]);
            string[] expectedValues = ["test1", "test2", "item3"];

            // Act
            DimensionMap result = filter.Apply(input);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ValueCodes, Has.Count.EqualTo(3));
                Assert.That(result.ValueCodes, Is.EquivalentTo(expectedValues));
            });
        }

        [Test]
        public void Filter_WithFirstElementMatch_ReturnsOnlyFirstElement()
        {
            // Arrange
            ToFilter filter = new()
            {
                FilterString = "item*"
            };
            DimensionMap input = new("foo", ["item1", "item2", "item3", "item4"]);
            string[] expectedValues = ["item1"];

            // Act
            DimensionMap result = filter.Apply(input);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ValueCodes, Has.Count.EqualTo(1));
                Assert.That(result.ValueCodes, Is.EquivalentTo(expectedValues));
            });
        }

        [Test]
        public void Filter_WithLastElementMatch_ReturnsAllElements()
        {
            // Arrange
            ToFilter filter = new()
            {
                FilterString = "item5"
            };
            DimensionMap input = new("foo", ["item1", "item2", "item3", "item4", "item5"]);
            string[] expectedValues = ["item1", "item2", "item3", "item4", "item5"];

            // Act
            DimensionMap result = filter.Apply(input);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ValueCodes, Has.Count.EqualTo(5));
                Assert.That(result.ValueCodes, Is.EquivalentTo(expectedValues));
            });
        }

        [Test]
        public void Filter_WithEmptyInput_ThrowsInvalidOperationException()
        {
            // Arrange
            ToFilter filter = new()
            {
                FilterString = "item1"
            };
            DimensionMap input = new("foo", []);

            // Act & Assert
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => filter.Apply(input));
            Assert.That(exception.Message, Does.Contain("No element matching the filterstring"));
        }

        [Test]
        public void Filter_WithNoMatches_ThrowsInvalidOperationException()
        {
            // Arrange
            ToFilter filter = new()
            {
                FilterString = "nonexistent"
            };
            DimensionMap input = new("foo", ["item1", "item2", "item3"]);

            // Act & Assert
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => filter.Apply(input));
            Assert.That(exception.Message, Does.Contain("No element matching the filterstring"));
        }

        [Test]
        public void Filter_WithComplexWildcardPattern_ReturnsCorrectElements()
        {
            // Arrange
            ToFilter filter = new()
            {
                FilterString = "*mid*end*"
            };
            DimensionMap input = new("foo", ["start", "middle", "midpoint-end", "after", "last"]);
            string[] expectedValues = ["start", "middle", "midpoint-end"];

            // Act
            DimensionMap result = filter.Apply(input);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ValueCodes, Has.Count.EqualTo(3));
                Assert.That(result.ValueCodes, Is.EquivalentTo(expectedValues));
            });
        }

        [Test]
        public void Filter_WithCaseInsensitiveMatch_ReturnsCorrectElements()
        {
            // Arrange
            ToFilter filter = new()
            {
                FilterString = "ITEM3"
            };
            DimensionMap input = new("foo", ["item1", "item2", "Item3", "item4"]);
            string[] expectedValues = ["item1", "item2", "Item3"];

            // Act
            DimensionMap result = filter.Apply(input);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ValueCodes, Has.Count.EqualTo(3));
                Assert.That(result.ValueCodes, Is.EquivalentTo(expectedValues));
            });
        }
    }
}