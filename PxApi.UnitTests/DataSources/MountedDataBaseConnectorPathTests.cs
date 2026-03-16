using PxApi.DataSources;

namespace PxApi.UnitTests.DataSources
{
    [TestFixture]
    public class MountedDataBaseConnectorPathTests
    {
        #region NormalizeDirectoryPath Tests

        [Test]
        public void NormalizeDirectoryPath_PathWithoutTrailingSeparator_AppendsSeparator()
        {
            // Arrange
            string path = Path.Combine("C:", "data", "root");

            // Act
            string result = MountedDataBaseConnector.NormalizeDirectoryPath(path);

            // Assert
            Assert.That(result, Does.EndWith(Path.DirectorySeparatorChar.ToString()));
        }

        [Test]
        public void NormalizeDirectoryPath_PathWithTrailingSeparator_KeepsSeparator()
        {
            // Arrange
            string path = Path.Combine("C:", "data", "root") + Path.DirectorySeparatorChar;

            // Act
            string result = MountedDataBaseConnector.NormalizeDirectoryPath(path);

            // Assert
            Assert.That(result, Does.EndWith(Path.DirectorySeparatorChar.ToString()));
            Assert.That(result, Is.Not.EndsWith(
                new string(Path.DirectorySeparatorChar, 2)));
        }

        [Test]
        public void NormalizeDirectoryPath_RelativePath_ResolvesToFullPath()
        {
            // Arrange
            string path = "relative";

            // Act
            string result = MountedDataBaseConnector.NormalizeDirectoryPath(path);

            using (Assert.EnterMultipleScope())
            {
                // Assert
                Assert.That(Path.IsPathRooted(result), Is.True);
                Assert.That(result, Does.EndWith(Path.DirectorySeparatorChar.ToString()));
            }
        }

        #endregion

        #region IsWithinDirectory Tests

        [Test]
        public void IsWithinDirectory_ChildPath_ReturnsTrue()
        {
            // Arrange
            string root = MountedDataBaseConnector.NormalizeDirectoryPath(
                Path.Combine("C:", "data", "root"));
            string child = Path.Combine("C:", "data", "root", "db", "file.px");

            // Act
            bool result = MountedDataBaseConnector.IsWithinDirectory(child, root);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void IsWithinDirectory_ExactDirectoryPath_ReturnsTrue()
        {
            // Arrange
            string root = MountedDataBaseConnector.NormalizeDirectoryPath(
                Path.Combine("C:", "data", "root"));
            string child = Path.Combine("C:", "data", "root") + Path.DirectorySeparatorChar;

            // Act
            bool result = MountedDataBaseConnector.IsWithinDirectory(child, root);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void IsWithinDirectory_SiblingWithMatchingPrefix_ReturnsFalse()
        {
            // Arrange - "root-evil" starts with "root" but is a sibling, not a child
            string root = MountedDataBaseConnector.NormalizeDirectoryPath(
                Path.Combine("C:", "data", "root"));
            string sibling = Path.Combine("C:", "data", "root-evil", "file.px");

            // Act
            bool result = MountedDataBaseConnector.IsWithinDirectory(sibling, root);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void IsWithinDirectory_ParentTraversal_ReturnsFalse()
        {
            // Arrange
            string root = MountedDataBaseConnector.NormalizeDirectoryPath(
                Path.Combine("C:", "data", "root"));
            string escaped = Path.Combine("C:", "data", "root", "..", "secret", "file.px");

            // Act
            bool result = MountedDataBaseConnector.IsWithinDirectory(escaped, root);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void IsWithinDirectory_CompletelyDifferentPath_ReturnsFalse()
        {
            // Arrange
            string root = MountedDataBaseConnector.NormalizeDirectoryPath(
                Path.Combine("C:", "data", "root"));
            string other = Path.Combine("D:", "other", "path", "file.px");

            // Act
            bool result = MountedDataBaseConnector.IsWithinDirectory(other, root);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void IsWithinDirectory_DifferentCasing_ReturnsTrue()
        {
            // Arrange
            string root = MountedDataBaseConnector.NormalizeDirectoryPath(
                Path.Combine("C:", "Data", "ROOT"));
            string child = Path.Combine("C:", "data", "root", "db", "file.px");

            // Act
            bool result = MountedDataBaseConnector.IsWithinDirectory(child, root);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void IsWithinDirectory_DeeplyNestedChild_ReturnsTrue()
        {
            // Arrange
            string root = MountedDataBaseConnector.NormalizeDirectoryPath(
                Path.Combine("C:", "data", "root"));
            string child = Path.Combine("C:", "data", "root", "a", "b", "c", "d", "file.px");

            // Act
            bool result = MountedDataBaseConnector.IsWithinDirectory(child, root);

            // Assert
            Assert.That(result, Is.True);
        }

        #endregion
    }
}
