using PxApi.DataSources;

namespace PxApi.UnitTests.DataSourceTests
{
    [TestFixture]
    internal class TablePathTests
    {
        [Test]
        public void Constructor_ShouldSplitPathAndAddToList()
        {
            // Arrange
            string path = Path.Combine("folder1", "folder2", "folder3");

            // Act
            TablePath tablePath = new(path);

            // Assert
            Assert.That(tablePath, Has.Count.EqualTo(3));
            Assert.Multiple(() =>
            {
                Assert.That(tablePath[0], Is.EqualTo("folder1"));
                Assert.That(tablePath[1], Is.EqualTo("folder2"));
                Assert.That(tablePath[2], Is.EqualTo("folder3"));
            });
        }

        [Test]
        public void ToPathString_ShouldCombineListToPathString()
        {
            // Arrange
            string path = Path.Combine("folder1", "folder2", "folder3");
            TablePath tablePath = new(path);

            // Act
            string result = tablePath.ToPathString();

            // Assert
            Assert.That(result, Is.EqualTo(path));
        }

        [Test]
        public void Constructor_ShouldHandleEmptyPath()
        {
            // Arrange
            string path = "";

            // Act
            TablePath tablePath = new(path);

            // Assert
            Assert.That(tablePath, Is.Empty);
        }
    }
}
