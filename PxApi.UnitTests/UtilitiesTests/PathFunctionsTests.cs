using PxApi.DataSources;
using PxApi.Utilities;

namespace PxApi.UnitTests.UtilitiesTests
{
    [TestFixture]
    internal class PathFunctionsTests
    {
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
            string[] invalidStrings = { "invalid\0Path1", "validPath2" };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => PathFunctions.CheckStringsForInvalidPathChars(invalidStrings));
        }

        [Test]
        public void BuildTableReferenceFromPath_ValidPaths_ReturnsPxTable()
        {
            // Arrange
            string fullPath = Path.Combine("root", "database", "level1", "level2", "table");
            string rootPath = "root";
            PxTable expectedTable = new("table", ["level1", "level2"], "database");

            // Act
            PxTable result = PathFunctions.BuildTableReferenceFromPath(fullPath, rootPath);

            Assert.Multiple(() =>
            {
                // Assert
                Assert.That(result.TableId, Is.EqualTo(expectedTable.TableId));
                Assert.That(result.DatabaseId, Is.EqualTo(expectedTable.DatabaseId));
                Assert.That(result.Hierarchy, Is.EqualTo(expectedTable.Hierarchy));
            });
        }

        [Test]
        public void BuildTableReferenceFromPath_InvalidPaths_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            string fullPath = Path.Combine("invalidRoot", "database", "hierarchy", "table");
            string rootPath = "root";

            // Act & Assert
            Assert.Throws<UnauthorizedAccessException>(() => PathFunctions.BuildTableReferenceFromPath(fullPath, rootPath));
        }

        [Test]
        public void GetFullPathToTable_ValidTable_ReturnsFullPath()
        {
            // Arrange
            PxTable table = new("table", [ "level1", "level2" ], "database");
            string rootPath = "root";
            string expectedFullPath = Path.Combine( "root", "database", "level1", "level2", "table");

            // Act
            string result = table.GetFullPathToTable(rootPath);

            // Assert
            Assert.That(result, Is.EqualTo(expectedFullPath));
        }
    }
}
