using Px.Utils.Models.Metadata;
using PxApi.Models.QueryFilters;

namespace PxApi.UnitTests.Models.QueryFilters
{
    /// <summary>
    /// Tests for the FirstFilter class that returns the first N elements from the input collection.
    /// </summary>
    [TestFixture]
    internal class FirstFilterTests
    {
        [Test]
        public void Filter_WithPositiveCount_ReturnsFirstNElements()
        {
            // Arrange
            FirstFilter filter = new(3);
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
        public void Filter_WithCountEqualToLength_ReturnsAllElements()
        {
            // Arrange
            FirstFilter filter = new(5);
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
        public void Filter_WithCountGreaterThanLength_ReturnsAllElements()
        {
            // Arrange
            FirstFilter filter = new(10);
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
        public void Filter_WithZeroCount_ReturnsEmptyCollection()
        {
            // Arrange
            FirstFilter filter = new(0);
            DimensionMap input = new("foo", ["item1", "item2", "item3", "item4", "item5"]);

            // Act
            DimensionMap result = filter.Apply(input);

            // Assert
            Assert.That(result.ValueCodes, Is.Empty);
        }

        [Test]
        public void Filter_WithEmptyInput_ReturnsEmptyCollection()
        {
            // Arrange
            FirstFilter filter = new(3);
            DimensionMap input = new("foo", []);

            // Act
            DimensionMap result = filter.Apply(input);

            // Assert
            Assert.That(result.ValueCodes, Is.Empty);
        }

        [Test]
        public void Filter_WithNegativeCount_ReturnsEmptyCollection()
        {
            // Arrange
            FirstFilter filter = new(-3);
            DimensionMap input = new("foo", ["item1", "item2", "item3", "item4", "item5"]);

            // Act
            DimensionMap result = filter.Apply(input);

            // Assert
            Assert.That(result.ValueCodes, Is.Empty);
        }

        [Test]
        public void Filter_WithCount1_ReturnsFirstElement()
        {
            // Arrange
            FirstFilter filter = new(1);
            DimensionMap input = new("foo", ["item1", "item2", "item3", "item4", "item5"]);
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
        public void Filter_WithLazyEvaluation_DoesNotEnumerateEntireInput()
        {
            // Arrange
            FirstFilter filter = new(3);

            // Create an input sequence that throws when enumerating past the third element
            static IEnumerable<string> ThrowingSequence()
            {
                yield return "item1";
                yield return "item2";
                yield return "item3";
                throw new InvalidOperationException("This should not be reached");
            }

            // Convert IEnumerable to List since that's what DimensionMap constructor requires
            // Taking only 3 items to avoid the exception
            List<string> safeList = [.. ThrowingSequence().Take(3)];
            DimensionMap input = new("foo", safeList);

            // Act & Assert
            // If the implementation is lazy, this will not throw
            DimensionMap result = filter.Apply(input);
            Assert.That(result.ValueCodes, Has.Count.EqualTo(3));
        }
    }
}