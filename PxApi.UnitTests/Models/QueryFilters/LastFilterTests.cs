using Px.Utils.Models.Metadata;
using PxApi.Models.QueryFilters;

namespace PxApi.UnitTests.Models.QueryFilters
{
    /// <summary>
    /// Tests for the LastFilter class that returns the last N elements from the input collection.
    /// </summary>
    [TestFixture]
    internal class LastFilterTests
    {
        [Test]
        public void Filter_WithPositiveCount_ReturnsLastNElements()
        {
            // Arrange
            LastFilter filter = new(3);
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
        public void Filter_WithCountEqualToLength_ReturnsAllElements()
        {
            // Arrange
            LastFilter filter = new(5);
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
            LastFilter filter = new(10);
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
            LastFilter filter = new(0);
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
            LastFilter filter = new(3);
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
            LastFilter filter = new(-3);
            DimensionMap input = new("foo", ["item1", "item2", "item3", "item4", "item5"]);

            // Act
            DimensionMap result = filter.Apply(input);

            // Assert
            Assert.That(result.ValueCodes, Is.Empty);
        }

        [Test]
        public void Filter_WithCount1_ReturnsLastElement()
        {
            // Arrange
            LastFilter filter = new(1);
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
        public void Filter_WithLargeInputCollection_PerformsCorrectly()
        {
            // Arrange
            LastFilter filter = new(3);
            List<string> inputList = new(10000);
            for (int i = 0; i < 10000; i++)
            {
                inputList.Add($"item{i}");
            }
            DimensionMap input = new("foo", inputList);
            string[] expectedValues = ["item9997", "item9998", "item9999"];

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
        public void Filter_WithUnsortedInput_ReturnsLastElementsInOriginalOrder()
        {
            // Arrange
            LastFilter filter = new(3);
            DimensionMap input = new("foo", ["zebra", "apple", "banana", "cherry", "date"]);
            string[] expectedValues = ["banana", "cherry", "date"];

            // Act
            DimensionMap result = filter.Apply(input);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ValueCodes, Has.Count.EqualTo(3));
                Assert.That(result.ValueCodes, Is.EquivalentTo(expectedValues));
                
                // Check order is preserved (that the elements appear in the same order as in the original input)
                for (int i = 0; i < expectedValues.Length; i++)
                {
                    Assert.That(result.ValueCodes[i], Is.EqualTo(expectedValues[i]));
                }
            });
        }
    }
}