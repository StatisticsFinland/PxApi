using PxApi.Utilities;

namespace PxApi.UnitTests.UtilitiesTests
{
    [TestFixture]
    public class BlobPathHelperTests
    {
        #region NormalizeBlobPath Tests

        [Test]
        public void NormalizeBlobPath_WhenPathContainsBackslashes_ReplacesWithForwardSlashes()
        {
            // Arrange
            const string path = @"px\subdir\file.txt";

            // Act
            string result = BlobPathHelper.NormalizeBlobPath(path);

            // Assert
            Assert.That(result, Is.EqualTo("px/subdir/file.txt"));
        }

        [Test]
        public void NormalizeBlobPath_WhenPathContainsMixedSeparators_NormalizesToForwardSlashes()
        {
            // Arrange
            const string path = @"px/subdir\file.txt";

            // Act
            string result = BlobPathHelper.NormalizeBlobPath(path);

            // Assert
            Assert.That(result, Is.EqualTo("px/subdir/file.txt"));
        }

        [Test]
        public void NormalizeBlobPath_WhenPathContainsConsecutiveSlashes_CollapsesToSingle()
        {
            // Arrange
            const string path = "px//subdir///file.txt";

            // Act
            string result = BlobPathHelper.NormalizeBlobPath(path);

            // Assert
            Assert.That(result, Is.EqualTo("px/subdir/file.txt"));
        }

        [Test]
        public void NormalizeBlobPath_WhenPathContainsConsecutiveBackslashes_CollapsesToSingle()
        {
            // Arrange
            const string path = @"px\\subdir\\\file.txt";

            // Act
            string result = BlobPathHelper.NormalizeBlobPath(path);

            // Assert
            Assert.That(result, Is.EqualTo("px/subdir/file.txt"));
        }

        [Test]
        public void NormalizeBlobPath_WhenPathHasLeadingSlash_TrimsIt()
        {
            // Arrange
            const string path = "/px/subdir/file.txt";

            // Act
            string result = BlobPathHelper.NormalizeBlobPath(path);

            // Assert
            Assert.That(result, Is.EqualTo("px/subdir/file.txt"));
        }

        [Test]
        public void NormalizeBlobPath_WhenPathHasTrailingSlash_TrimsIt()
        {
            // Arrange
            const string path = "px/subdir/file.txt/";

            // Act
            string result = BlobPathHelper.NormalizeBlobPath(path);

            // Assert
            Assert.That(result, Is.EqualTo("px/subdir/file.txt"));
        }

        [Test]
        public void NormalizeBlobPath_WhenPathHasLeadingAndTrailingSlashes_TrimsAll()
        {
            // Arrange
            const string path = "///px/subdir/file.txt///";

            // Act
            string result = BlobPathHelper.NormalizeBlobPath(path);

            // Assert
            Assert.That(result, Is.EqualTo("px/subdir/file.txt"));
        }

        [Test]
        public void NormalizeBlobPath_WhenPathIsAlreadyNormalized_ReturnsSamePath()
        {
            // Arrange
            const string path = "px/subdir/file.txt";

            // Act
            string result = BlobPathHelper.NormalizeBlobPath(path);

            // Assert
            Assert.That(result, Is.EqualTo("px/subdir/file.txt"));
        }

        [Test]
        public void NormalizeBlobPath_WhenPathIsSimpleFileName_ReturnsSameFileName()
        {
            // Arrange
            const string path = "file.txt";

            // Act
            string result = BlobPathHelper.NormalizeBlobPath(path);

            // Assert
            Assert.That(result, Is.EqualTo("file.txt"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void NormalizeBlobPath_WhenPathIsNullOrWhitespace_ReturnsEmpty(string? path)
        {
            // Act
            string result = BlobPathHelper.NormalizeBlobPath(path!);

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void NormalizeBlobPath_WhenPathIsOnlySlashes_ReturnsEmpty()
        {
            // Arrange
            const string path = "///";

            // Act
            string result = BlobPathHelper.NormalizeBlobPath(path);

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void NormalizeBlobPath_WhenPathIsOnlyBackslashes_ReturnsEmpty()
        {
            // Arrange
            const string path = @"\\\";

            // Act
            string result = BlobPathHelper.NormalizeBlobPath(path);

            // Assert
            Assert.That(result, Is.Empty);
        }

        #endregion

        #region CombineBlobPath Tests

        [Test]
        public void CombineBlobPath_WhenTwoSegments_CombinesWithForwardSlash()
        {
            // Act
            string result = BlobPathHelper.CombineBlobPath("px", "file.txt");

            // Assert
            Assert.That(result, Is.EqualTo("px/file.txt"));
        }

        [Test]
        public void CombineBlobPath_WhenThreeSegments_CombinesWithForwardSlashes()
        {
            // Act
            string result = BlobPathHelper.CombineBlobPath("px", "subdir", "file.txt");

            // Assert
            Assert.That(result, Is.EqualTo("px/subdir/file.txt"));
        }

        [Test]
        public void CombineBlobPath_WhenSegmentsHaveTrailingSlashes_NormalizesResult()
        {
            // Act
            string result = BlobPathHelper.CombineBlobPath("px/", "/subdir/", "/file.txt");

            // Assert
            Assert.That(result, Is.EqualTo("px/subdir/file.txt"));
        }

        [Test]
        public void CombineBlobPath_WhenSegmentsContainBackslashes_NormalizesResult()
        {
            // Act
            string result = BlobPathHelper.CombineBlobPath(@"px\subdir", "file.txt");

            // Assert
            Assert.That(result, Is.EqualTo("px/subdir/file.txt"));
        }

        [Test]
        public void CombineBlobPath_WhenSingleSegment_ReturnsSameSegment()
        {
            // Act
            string result = BlobPathHelper.CombineBlobPath("px");

            // Assert
            Assert.That(result, Is.EqualTo("px"));
        }

        [Test]
        public void CombineBlobPath_WhenSegmentIsEmpty_SkipsEmptyParts()
        {
            // Act
            string result = BlobPathHelper.CombineBlobPath("px", "", "file.txt");

            // Assert
            Assert.That(result, Is.EqualTo("px/file.txt"));
        }

        #endregion
    }
}
