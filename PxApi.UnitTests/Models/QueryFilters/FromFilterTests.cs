using Px.Utils.Models.Metadata;
using PxApi.Models.QueryFilters;

namespace PxApi.UnitTests.Models.QueryFilters
{
    /// <summary>
    /// Tests for the FromFilter class that returns elements after a match.
    /// </summary>
    [TestFixture]
    internal class FromFilterTests
    {
        [Test]
        public void Filter_WithExactMatch_ReturnsElementsAfterMatch()
        {
            // Arrange
            FromFilter filter = new("item3");
            DimensionMap input = new("foo", ["item1", "item2", "item3", "item4", "item5"]);
            string[] expectedValues = ["item3", "item4", "item5"];

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
        public void Filter_WithWildcardMatch_ReturnsElementsAfterMatch()
        {
            // Arrange
            FromFilter filter = new("item*");
            DimensionMap input = new("foo", ["test1", "test2", "item3", "test4", "test5"]);
            string[] expectedValues = ["item3", "test4", "test5"];

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
        public void Filter_WithFirstElementMatch_ReturnsAllElements()
        {
            // Arrange
            FromFilter filter = new("item*");
            DimensionMap input = new("foo", ["item1", "item2", "item3", "item4"]);
            string[] expectedValues = ["item1", "item2", "item3", "item4"];

            // Act
            DimensionMap result = filter.Apply(input);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ValueCodes, Has.Count.EqualTo(4));
                Assert.That(result.ValueCodes, Is.EquivalentTo(expectedValues));
            });
        }

        [Test]
        public void Filter_WithLastElementMatch_ReturnsEmptyCollection()
        {
            // Arrange
            FromFilter filter = new("item5");
            DimensionMap input = new("foo", ["item1", "item2", "item3", "item4", "item5"]);
            string[] expectedValues = ["item5"];

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
        public void Filter_WithEmptyInput_ThrowsInvalidOperationException()
        {
            // Arrange
            FromFilter filter = new("item1");
            DimensionMap input = new("foo", []);

            // Act & Assert
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => filter.Apply(input));
            Assert.That(exception.Message, Does.Contain("No element matching the filterstring"));
        }

        [Test]
        public void Filter_WithNoMatches_ThrowsInvalidOperationException()
        {
            // Arrange
            FromFilter filter = new("nonexistent");
            DimensionMap input = new("foo", ["item1", "item2", "item3"]);

            // Act & Assert
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => filter.Apply(input));
            Assert.That(exception.Message, Does.Contain("No element matching the filterstring"));
        }

        [Test]
        public void Filter_WithComplexWildcardPattern_ReturnsCorrectElements()
        {
            // Arrange
            FromFilter filter = new("*mid*end*");
            DimensionMap input = new("foo", ["start", "middle", "midpoint-end", "after", "last"]);
            string[] expectedValues = ["midpoint-end", "after", "last"];

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
            FromFilter filter = new("ITEM2");
            DimensionMap input = new("foo", ["item1", "Item2", "item3", "item4"]);
            string[] expectedValues = ["Item2", "item3", "item4"];

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
        public void Filter_WithMultipleMatches_ReturnsElementsAfterFirstMatch()
        {
            // Arrange
            FromFilter filter = new("repeat*");
            DimensionMap input = new("foo", ["item1", "repeat1", "item2", "repeat2", "item3"]);
            string[] expectedValues = ["repeat1", "item2", "repeat2", "item3"];

            // Act
            DimensionMap result = filter.Apply(input);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ValueCodes, Has.Count.EqualTo(4));
                Assert.That(result.ValueCodes, Is.EquivalentTo(expectedValues));
            });
        }
    }
}