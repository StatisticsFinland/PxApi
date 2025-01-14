using PxApi.Utilities;

namespace PxApi.UnitTests.UtilitiesTests
{
    [TestFixture]
    internal static class PathFunctionsTests
    {
        [Test]
        public static void BuildAndSecurePathTestWithWindowsPath()
        {
            // Arrange
            string basePath = "C:\\DataBase\\Example";
            string userPath = "Foobar\\file.px";
            string expected = Path.Combine("C:", "DataBase", "Example", "Foobar", "file.px");

            // Act
            string actual = PathFunctions.BuildAndSecurePath(basePath, userPath);

            // Assert
            Assert.That(expected, Is.EqualTo(actual));
        }

        [Test]
        public static void BuildAndSecurePathTestWithExcessiveWindowsSeparators()
        {
            // Arrange
            string basePath = "C:\\\\DataBase\\\\Example";
            string userPath = "Foobar\\\\file.px";
            string expected = Path.Combine("C:", "DataBase", "Example", "Foobar", "file.px");

            // Act
            string actual = PathFunctions.BuildAndSecurePath(basePath, userPath);

            // Assert
            Assert.That(expected, Is.EqualTo(actual));
        }

        [Test]
        public static void BuildAndSecurePathTestWithUnixPath()
        {
            // Arrange
            string basePath = "C:/DataBase/Example";
            string userPath = "Foobar/file.px";
            string expected = Path.Combine("C:", "DataBase", "Example", "Foobar", "file.px");
            
            // Act
            string actual = PathFunctions.BuildAndSecurePath(basePath, userPath);

            // Assert
            Assert.That(expected, Is.EqualTo(actual));
        }

        [Test]
        public static void BuildAndSecurePathTestWithExcessiveUnixSeparators()
        {
            // Arrange
            string basePath = "C://DataBase//Example";
            string userPath = "Foobar\\\\file.px";
            string expected = Path.Combine("C:", "DataBase", "Example", "Foobar", "file.px");

            // Act
            string actual = PathFunctions.BuildAndSecurePath(basePath, userPath);

            // Assert
            Assert.That(expected, Is.EqualTo(actual));
        }

        [Test]
        public static void BuildAndSecurePathUnauthorizedTraversalWindowsTest()
        {
            // Arrange
            string basePath = "C:\\DataBase\\Example";
            string userPath = "..\\Foobar\\file.px";

            // Act and Assert
            Assert.That(() => PathFunctions.BuildAndSecurePath(basePath, userPath), Throws.TypeOf<UnauthorizedAccessException>());
        }

        [Test]
        public static void BuildAndSecurePathUnauthorizedTraversalUnixTest()
        {
            // Arrange
            string basePath = "C:/DataBase/Example";
            string userPath = "../Foobar/file.px";

            // Act and Assert
            Assert.That(() => PathFunctions.BuildAndSecurePath(basePath, userPath), Throws.TypeOf<UnauthorizedAccessException>());
        }
    }
}
