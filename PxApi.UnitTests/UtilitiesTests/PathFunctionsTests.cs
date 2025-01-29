using PxApi.Utilities;

namespace PxApi.UnitTests.UtilitiesTests
{
    [TestFixture]
    internal class PathFunctionsTests
    {
        [Test]
        public void BuildAndSecurePath_ValidPath_ReturnsCombinedPath()
        {
            // Arrange
            string basePath = "C:\\base";
            string userPath = "folder\\file.txt";

            // Act
            string result = PathFunctions.BuildAndSecurePath(basePath, userPath);

            // Assert
            Assert.That(result, Is.EqualTo(Path.Combine(basePath, userPath)));
        }

        [Test]
        public void BuildAndSecurePath_InvalidPath_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            string basePath = "C:\\base";
            string userPath = "..\\..\\Windows\\System32";

            // Act & Assert
            Assert.Throws<UnauthorizedAccessException>(() => PathFunctions.BuildAndSecurePath(basePath, userPath));
        }

        [Test]
        public void BuildAndSecurePath_ListOfPaths_ReturnsCombinedPath()
        {
            // Arrange
            string basePath = "C:\\base";
            List<string> userPath = ["folder1", "folder2", "file.txt"];

            // Act
            string result = PathFunctions.BuildAndSecurePath(basePath, userPath);

            // Assert
            Assert.That(result, Is.EqualTo(Path.Combine(basePath, "folder1", "folder2", "file.txt")));
        }

        [Test]
        public void CheckStringsForInvalidPathChars_ValidStrings_DoesNotThrow()
        {
            // Arrange
            string[] validStrings = { "validPath1", "validPath2" };

            // Act & Assert
            Assert.DoesNotThrow(() => PathFunctions.CheckStringsForInvalidPathChars(validStrings));
        }

        [Test]
        public void CheckStringsForInvalidPathChars_InvalidStrings_ThrowsArgumentException()
        {
            // Arrange
            string[] invalidStrings = { "invalid|Path1", "validPath2" };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => PathFunctions.CheckStringsForInvalidPathChars(invalidStrings));
        }
    }
}
