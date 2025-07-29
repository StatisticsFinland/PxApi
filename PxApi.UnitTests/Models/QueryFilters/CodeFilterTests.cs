using Px.Utils.Models.Metadata;
using PxApi.Models.QueryFilters;

namespace PxApi.UnitTests.Models.QueryFilters
{
    /// <summary>
    /// Tests for the CodeFilter class that validate its pattern matching behavior.
    /// </summary>
    [TestFixture]
    internal class CodeFilterTests
    {
        #region Basic Filter Tests

        [Test]
        public void Filter_EmptyInput_ReturnsEmptyCollection()
        {
            // Arrange
            CodeFilter filter = new(["code1", "code2"]);
            DimensionMap input = new("foo", []);

            // Act
            DimensionMap result = filter.Apply(input);

            // Assert
            Assert.That(result.ValueCodes, Is.Empty);
        }

        [Test]
        public void Filter_EmptyCodes_ReturnsEmptyCollection()
        {
            // Arrange
            CodeFilter filter = new([]);
            DimensionMap input = new("foo", ["item1", "item2", "item3"]);

            // Act
            DimensionMap result = filter.Apply(input);

            // Assert
            Assert.That(result.ValueCodes, Is.Empty);
        }

        #endregion

        #region Pattern Matching Tests

        [Test]
        public void Filter_ExactMatch_ReturnsMatchingItems()
        {
            // Arrange
            CodeFilter filter = new(["code1", "code2"]);
            DimensionMap input = new("foo", ["code1", "code3", "code2", "code4"]);
            string[] expectedValues = ["code1", "code2"];

            // Act
            DimensionMap result = filter.Apply(input);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ValueCodes, Has.Count.EqualTo(2));
                Assert.That(result.ValueCodes, Contains.Item("code1"));
                Assert.That(result.ValueCodes, Contains.Item("code2"));
            });
        }

        [Test]
        public void Filter_Wildcard_ReturnsAllItems()
        {
            // Arrange
            CodeFilter filter = new(["*"]);
            DimensionMap input = new("foo", ["item1", "item2", "item3"]);
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
        public void Filter_StartsWith_ReturnsMatchingItems()
        {
            // Arrange
            CodeFilter filter = new(["pre*"]);
            DimensionMap input = new("foo", ["prefix1", "prefix2", "suffix3", "pre", "notpre"]);
            string[] expectedValues = ["prefix1", "prefix2", "pre"];

            // Act
            DimensionMap result = filter.Apply(input);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ValueCodes, Has.Count.EqualTo(3));
                Assert.That(result.ValueCodes, Contains.Item("prefix1"));
                Assert.That(result.ValueCodes, Contains.Item("prefix2"));
                Assert.That(result.ValueCodes, Contains.Item("pre"));
            });
        }

        [Test]
        public void Filter_EndsWith_ReturnsMatchingItems()
        {
            // Arrange
            CodeFilter filter = new(["*suffix"]);
            DimensionMap input = new("foo", ["presuffix", "suffix", "nonsüffix", "suffixextra"]);
            string[] expectedValues = ["presuffix", "suffix"];

            // Act
            DimensionMap result = filter.Apply(input);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ValueCodes, Has.Count.EqualTo(2));
                Assert.That(result.ValueCodes, Contains.Item("presuffix"));
                Assert.That(result.ValueCodes, Contains.Item("suffix"));
            });
        }

        [Test]
        public void Filter_Contains_ReturnsMatchingItems()
        {
            // Arrange
            CodeFilter filter = new(["*mid*"]);
            DimensionMap input = new("foo", ["prefix-mid-suffix", "mid", "nomud", "midd", "almostmid"]);
            string[] expectedValues = ["prefix-mid-suffix", "mid", "midd", "almostmid"];

            // Act
            DimensionMap result = filter.Apply(input);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ValueCodes, Has.Count.EqualTo(4));
                Assert.That(result.ValueCodes, Contains.Item("prefix-mid-suffix"));
                Assert.That(result.ValueCodes, Contains.Item("mid"));
                Assert.That(result.ValueCodes, Contains.Item("midd"));
                Assert.That(result.ValueCodes, Contains.Item("almostmid"));
            });
        }

        [Test]
        public void Filter_MultipleSegments_ReturnsMatchingItems()
        {
            // Arrange
            CodeFilter filter = new(["pre*mid*suffix"]);
            DimensionMap input = new("foo", ["pre-mid-suffix", "premidsuffix", "pre-nomatch-suffix", "prefix-middle-suffix"]);
            string[] expectedValues = ["pre-mid-suffix", "premidsuffix", "prefix-middle-suffix"];

            // Act
            DimensionMap result = filter.Apply(input);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ValueCodes, Has.Count.EqualTo(3));
                Assert.That(result.ValueCodes, Contains.Item("pre-mid-suffix"));
                Assert.That(result.ValueCodes, Contains.Item("premidsuffix"));
                Assert.That(result.ValueCodes, Contains.Item("prefix-middle-suffix"));
            });
        }

        #endregion

        #region Advanced Pattern Matching Tests

        [Test]
        public void Filter_CaseSensitivity_IgnoresCase()
        {
            // Arrange
            CodeFilter filter = new(["CODE*"]);
            DimensionMap input = new("foo", ["code123", "CODE456", "codE789", "not-code"]);
            string[] expectedValues = ["code123", "CODE456", "codE789"];

            // Act
            DimensionMap result = filter.Apply(input);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ValueCodes, Has.Count.EqualTo(3));
                Assert.That(result.ValueCodes, Contains.Item("code123"));
                Assert.That(result.ValueCodes, Contains.Item("CODE456"));
                Assert.That(result.ValueCodes, Contains.Item("codE789"));
            });
        }

        [Test]
        public void Filter_MultipleCodes_ReturnsMatchingItems()
        {
            // Arrange
            CodeFilter filter = new(["code*", "*suffix", "exact"]);
            DimensionMap input = new("foo", ["code123", "prefix-suffix", "exact", "nomatch", "codesuffix"]);
            string[] expectedValues = ["code123", "prefix-suffix", "exact", "codesuffix"];

            // Act
            DimensionMap result = filter.Apply(input);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ValueCodes, Has.Count.EqualTo(4));
                Assert.That(result.ValueCodes, Contains.Item("code123"));
                Assert.That(result.ValueCodes, Contains.Item("prefix-suffix"));
                Assert.That(result.ValueCodes, Contains.Item("exact"));
                Assert.That(result.ValueCodes, Contains.Item("codesuffix"));
            });
        }

        [Test]
        public void Filter_ConsecutiveWildcards_HandledCorrectly()
        {
            // Arrange
            CodeFilter filter = new(["a**b"]);

            // Act & Assert
            // These should match with consecutive wildcards treated as one
            string[] matches = ["ab", "a-b", "a123b"];
            foreach (string item in matches)
            {
                DimensionMap input = new("foo", [item]);
                DimensionMap result = filter.Apply(input);
                Assert.That(result.ValueCodes.Count, Is.EqualTo(1), $"Expected '{item}' to match 'a**b'");
            }
        }

        [Test]
        public void Filter_NonContiguousMatching_ReturnsMatchingItems()
        {
            // Arrange
            CodeFilter filter = new(["a*c*e"]);

            // Act & Assert
            string[] matches = ["ace", "abcde", "a-c-e"];
            foreach (string item in matches)
            {
                DimensionMap input = new("foo", [item]);
                DimensionMap result = filter.Apply(input);
                Assert.That(result.ValueCodes.Count, Is.EqualTo(1), $"Expected '{item}' to match 'a*c*e'");
            }

            string[] nonMatches = ["acf", "eca", "abde"];
            foreach (string item in nonMatches)
            {
                DimensionMap input = new("foo", [item]);
                DimensionMap result = filter.Apply(input);
                Assert.That(result.ValueCodes, Is.Empty, $"Expected '{item}' NOT to match 'a*c*e'");
            }
        }

        [Test]
        public void Filter_LongInputStrings_ReturnsMatch()
        {
            // Arrange
            CodeFilter filter = new(["begin*end"]);
            string longString = "begin" + new string('-', 1000) + "end";
            DimensionMap input = new("foo", [longString]);

            // Act
            DimensionMap result = filter.Apply(input);

            // Assert
            Assert.That(result.ValueCodes.Count, Is.EqualTo(1), "Should match very long strings");
        }

        [Test]
        public void Filter_MultipleFiltersMatch_ReturnsAllMatches()
        {
            // Arrange
            CodeFilter filter = new(["*match*", "*this*"]);
            DimensionMap input = new("foo", ["match", "this", "matchthis", "thismatch"]);
            string[] expectedValues = ["match", "this", "matchthis", "thismatch"];
            
            // Act
            DimensionMap result = filter.Apply(input);
            
            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ValueCodes, Has.Count.EqualTo(4));
                Assert.That(result.ValueCodes, Contains.Item("match"));
                Assert.That(result.ValueCodes, Contains.Item("this"));
                Assert.That(result.ValueCodes, Contains.Item("matchthis"));
                Assert.That(result.ValueCodes, Contains.Item("thismatch"));
            });
        }

        #endregion
    }
}