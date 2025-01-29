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
            string[] validStrings = ["validPath1", "validPath2"];

            // Act & Assert
            Assert.DoesNotThrow(() => PathFunctions.CheckStringsForInvalidPathChars(validStrings));
        }

        [Test]
        public void CheckStringsForInvalidPathChars_InvalidStrings_ThrowsArgumentException()
        {
            // Arrange
            string[] invalidStrings = ["invalid\0Path1", "validPath2"];

            // Act & Assert
            Assert.Throws<ArgumentException>(() => PathFunctions.CheckStringsForInvalidPathChars(invalidStrings));
        }
    }
}
