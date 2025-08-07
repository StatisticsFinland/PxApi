using Px.Utils.Models.Metadata;
using PxApi.Models.QueryFilters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PxApi.UnitTests.Models.QueryFilters
{
    [TestFixture]
    internal class AllFilterTests
    {
        [Test]
        public void Filter_ReturnsAllElements()
        {
            // Arrange
            AllFilter filter = new();
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
        public void Filter_WithEmptyInput_ReturnsEmptyResult()
        {
            // Arrange
            AllFilter filter = new();
            DimensionMap input = new("foo", []);
            string[] expectedValues = [];
            // Act
            DimensionMap result = filter.Apply(input);
            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ValueCodes, Has.Count.EqualTo(0));
                Assert.That(result.ValueCodes, Is.EquivalentTo(expectedValues));
            });
        }

        [Test]
        public void Filter_WithNullInput_ThrowsArgumentNullException()
        {
            // Arrange
            AllFilter filter = new();
            DimensionMap? input = null;
            // Act & Assert
            Assert.Throws<NullReferenceException>(() => filter.Apply(input!));
        }
    }
}
