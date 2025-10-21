using PxApi.Models.QueryFilters;

namespace PxApi.UnitTests.Models.QueryFilters
{
    /// <summary>
    /// This test fixture specifically tests the IsCodeMatch private method
    /// of the CodeFilter class by using a test helper class.
    /// </summary>
    [TestFixture]
    internal class CodeFilterMatchingTests
    {
        [Test]
        public void IsCodeMatch_ExactMatch_ReturnsTrue()
        {
            // Arrange & Act & Assert
            Assert.That(FilterUtils.IsCodeMatch("code", "code"), Is.True);
        }

        [Test]
        public void IsCodeMatch_ExactNoMatch_ReturnsFalse()
        {
            // Arrange & Act & Assert
            Assert.That(FilterUtils.IsCodeMatch("notcode", "code"), Is.False);
        }

        [Test]
        public void IsCodeMatch_ExactRepeatingNoMatch_ReturnsFalse()
        {
            // Arrange & Act & Assert
            Assert.That(FilterUtils.IsCodeMatch("codecode", "code"), Is.False);
        }

        [Test]
        public void IsCodeMatch_Wildcard_ReturnsTrue()
        {
            // Arrange & Act & Assert
            Assert.That(FilterUtils.IsCodeMatch("anycode", "*"), Is.True);
        }

        [Test]
        public void IsCodeMatch_StartsWith_ReturnsTrue()
        {
            // Arrange & Act & Assert
            Assert.That(FilterUtils.IsCodeMatch("prefix", "pre*"), Is.True);
        }

        [Test]
        public void IsCodeMatch_StartsWith_ReturnsFalse()
        {
            // Arrange & Act & Assert
            Assert.That(FilterUtils.IsCodeMatch("notprefix", "pre*"), Is.False);
        }

        [Test]
        public void IsCodeMatch_EndsWith_ReturnsTrue()
        {
            // Arrange & Act & Assert
            Assert.That(FilterUtils.IsCodeMatch("prefix", "*fix"), Is.True);
        }

        [Test]
        public void IsCodeMatch_EndsWith_ReturnsFalse()
        {
            // Arrange & Act & Assert
            Assert.That(FilterUtils.IsCodeMatch("prefixnot", "*fix"), Is.False);
        }

        [Test]
        public void IsCodeMatch_Contains_ReturnsTrue()
        {
            // Arrange & Act & Assert
            Assert.That(FilterUtils.IsCodeMatch("premidsuffix", "*mid*"), Is.True);
        }

        [Test]
        public void IsCodeMatch_ContainsOnly_ReturnsTrue()
        {
            // Arrange & Act & Assert
            Assert.That(FilterUtils.IsCodeMatch("mid", "*mid*"), Is.True);

        }

        [Test]
        public void IsCodeMatch_ContainsEnd_ReturnsTrue()
        {
            // Arrange & Act & Assert
            Assert.That(FilterUtils.IsCodeMatch("premid", "*mid*"), Is.True);
        }

        [Test]
        public void IsCodeMatch_ContainsStart_ReturnsTrue()
        {
            // Arrange & Act & Assert
            Assert.That(FilterUtils.IsCodeMatch("midend", "*mid*"), Is.True);
        }

        [Test]
        public void IsCodeMatch_Contains_ReturnsFalse()
        {
            // Arrange & Act & Assert
            Assert.That(FilterUtils.IsCodeMatch("premidsuffix", "*not*"), Is.False);
        }

        [Test]
        public void IsCodeMatch_AlmostButShort_ReturnsFalse()
        {
            // Arrange & Act & Assert
            Assert.That(FilterUtils.IsCodeMatch("foobar", "*fob*"), Is.False);
        }

        [Test]
        public void IsCodeMatch_AlmostButLong_ReturnFalse()
        {
            // Arrange & Act & Assert
            Assert.That(FilterUtils.IsCodeMatch("foobar", "*fooob*"), Is.False);
        }

        [Test]
        public void IsCodeMatch_RepeatingPattern_ReturnTrue()
        {
            // Arrange & Act & Assert
            Assert.That(FilterUtils.IsCodeMatch("bananana", "ba*na"), Is.True);
        }

        [Test]
        public void IsCodeMatch_RepeatingAlmostMatch_ReturnsFalse()
        {
            //Arrange & Act & Assert
            Assert.That(FilterUtils.IsCodeMatch("foofobarfoobrbarfoobafoor", "*foobar*"), Is.False);
        }

        [Test]
        public void IsCodeMatch_RestartMatch_ReturnsTrue()
        {
            //Arrange & Act & Assert
            Assert.That(FilterUtils.IsCodeMatch("foobärfoobörfoobyrfoobar", "*foobar*"), Is.True);
        }

        [Test]
        public void IsCodeMatch_RestartMatchSplit_ReturnsTrue()
        {
            //Arrange & Act & Assert
            Assert.That(FilterUtils.IsCodeMatch("foobärfoobörfoobyrfoobar", "*foo*bar*"), Is.True);
        }

        [Test]
        public void IsCodeMatch_MultipleSegments_ReturnsTrue()
        {
            // Arrange & Act & Assert
            Assert.That(FilterUtils.IsCodeMatch("a123b456c", "a*b*c"), Is.True);
        }

        [Test]
        public void IsCodeMatch_MultipleSimillarSegments_ReturnsTrue()
        {
            // Arrange & Act & Assert
            Assert.That(FilterUtils.IsCodeMatch("a1aa23a4a56aa", "a*a*a*aa*"), Is.True);
        }

        [Test]
        public void IsCodeMatch_MultipleAlmostSimillarSegments_ReturnsFalse()
        {
            // Arrange & Act & Assert
            Assert.That(FilterUtils.IsCodeMatch("a1aa23a4a56a", "a*a*a*aa*"), Is.False);
        }
        
        [Test]
        public void IsCodeMatch_MultipleSegments_ReturnsFalse()
        {
            // Arrange & Act & Assert
            Assert.That(FilterUtils.IsCodeMatch("a123x456c", "a*b*c"), Is.False);
        }

        [Test]
        public void IsCodeMatch_MultipleSegementsWrongOrder_ReturnsFalse()
        {
            // Arrange & Act & Assert
            Assert.That(FilterUtils.IsCodeMatch("123a456b789", "*b*a*"), Is.False);
        }

        [Test]
        public void IsCodeMatch_MultipleSegmentsRepeating_ReturnsFalse()
        {
            // Arrange & Act & Assert
            Assert.That(FilterUtils.IsCodeMatch("fobaroofbarbarfoo", "*foo*bar*"), Is.False);
        }

        [Test]
        public void IsCodeMatch_EmptySegments_HandledCorrectly()
        {
            Assert.Multiple(() =>
            {
                // Arrange & Act & Assert
                Assert.That(FilterUtils.IsCodeMatch("anything", "**"), Is.True);
                Assert.That(FilterUtils.IsCodeMatch("ab", "a**b"), Is.True);
            });
        }

        [Test]
        public void IsCodeMatch_LotsOfWildCards_HandledCorrectly()
        {
            Assert.Multiple(() =>
            {
                // Arrange & Act & Assert
                Assert.That(FilterUtils.IsCodeMatch("anything", "**************"), Is.True);
                Assert.That(FilterUtils.IsCodeMatch("ab", "a**********b"), Is.True);
                Assert.That(FilterUtils.IsCodeMatch("ab", "**********ab*************"), Is.True);
                Assert.That(FilterUtils.IsCodeMatch("ab", "**********a******b*************"), Is.True);
            });
        }

        [Test]
        public void IsCodeMatch_CaseSensitivity_IgnoresCase()
        {
            Assert.Multiple(() =>
            {
                // Arrange & Act & Assert
                Assert.That(FilterUtils.IsCodeMatch("abcdef", "AbC*"), Is.True);
                Assert.That(FilterUtils.IsCodeMatch( "ABCDEF", "*DeF"), Is.True);
            });
        }
    }
}