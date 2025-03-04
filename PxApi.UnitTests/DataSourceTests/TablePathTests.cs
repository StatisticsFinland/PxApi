using PxApi.DataSources;

namespace PxApi.UnitTests.DataSourceTests
{
    [TestFixture]
    internal class TablePathTests
    {
        [Test]
        public void ConstructorTest_AllParams()
        {
            // Arrange
            PxTable table = new("file.px", ["level1", "level2"], "database");

            // Assert
            Assert.That(table.Hierarchy, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(table.TableId, Is.EqualTo("file.px"));
                Assert.That(table.Hierarchy[0], Is.EqualTo("level1"));
                Assert.That(table.Hierarchy[1], Is.EqualTo("level2"));
                Assert.That(table.DatabaseId, Is.EqualTo("database"));
            });
        }

        [Test]
        public void ConstructorTest_NoHierarchy()
        {
            // Arrange
            PxTable table = new("file.px", [], "database");

            // Assert
            Assert.That(table.Hierarchy, Has.Count.EqualTo(0));
            Assert.Multiple(() =>
            {
                Assert.That(table.TableId, Is.EqualTo("file.px"));
                Assert.That(table.DatabaseId, Is.EqualTo("database"));
            });
        }
    }
}
