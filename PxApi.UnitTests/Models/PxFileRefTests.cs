using PxApi.Models;

namespace PxApi.UnitTests.Models
{
    [TestFixture]
    internal class PxFileRefTests
    {
        #region Create

        [Test]
        [TestCase("file1", "database")]
        [TestCase("a", "database")]
        [TestCase("a123", "database")]
        [TestCase("AaBbCcDdEeFfGgHhIiJjKkLlMmNnOoPpQqRrRsStTuUvVwWxXy", "database")] // 50 chars
        public void Create_WithValidIdAndDatabase_ReturnsPxFileRef(string id, string dbId)
        {
            DataBaseRef db = DataBaseRef.Create(dbId);
            PxFileRef pxFileRef = PxFileRef.ValidateAndCreate(id, db, ["statisticalProgram"]);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(pxFileRef.Id, Is.EqualTo(id));
                Assert.That(pxFileRef.DataBase, Is.EqualTo(db));
            };
        }

        [Test]
        [TestCase("file#1", "database")]
        [TestCase("file 1", "database")]
        [TestCase("", "database")]
        [TestCase("   ", "database")]
        [TestCase("\r\n \n", "database")]
        [TestCase("AaBbCcDdEeFfGgHhIiJjKkLlMmNnOoPpQqRrRsStTuUvVwWxXyYz", "database")] // too long
        public void Create_WithInvalidId_ThrowsArgumentException(string? id, string dbId)
        {
            DataBaseRef db = DataBaseRef.Create(dbId);
            Assert.Throws<ArgumentException>(() => PxFileRef.ValidateAndCreate(id!, db, ["statisticalProgram"]), $"id: {id} with db: {dbId} did not throw exception.");
        }

        [Test]
        public void Create_WithValidIdAndConfig_StoresPropertiesCorrectly_NoSeparator()
        {
            // Arrange
            const string tableId = "abc123";
            DataBaseRef db = DataBaseRef.Create("db1");

            // Act
            PxFileRef pxFileRef = PxFileRef.ValidateAndCreate(tableId, db, ["statisticalProgram"]);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(pxFileRef.Id, Is.EqualTo(tableId));
                Assert.That(pxFileRef.DataBase, Is.EqualTo(db));
            };
        }

        [Test]
        public void Create_WithValidIdAndConfig_StoresPropertiesCorrectly_WithSeparatorAndIndexes()
        {
            // Arrange
            const string tableId = "database-grouping-id";
            DataBaseRef db = DataBaseRef.Create("db2");

            // Act
            PxFileRef pxFileRef = PxFileRef.ValidateAndCreate(tableId, db, ["statisticalProgram"]);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(pxFileRef.Id, Is.EqualTo(tableId));
                Assert.That(pxFileRef.DataBase, Is.EqualTo(db));
            };
        }

        [Test]
        public void Create_WithValidIdAndConfig_StoresPropertiesCorrectly_LastPartIndexes()
        {
            // Arrange
            const string tableId = "database-grouping-id";
            DataBaseRef db = DataBaseRef.Create("db3");

            // Act
            PxFileRef pxFileRef = PxFileRef.ValidateAndCreate(tableId, db, ["statisticalProgram"]);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(pxFileRef.Id, Is.EqualTo(tableId));
                Assert.That(pxFileRef.DataBase, Is.EqualTo(db));
            };
        }

        [Test]
        public void Create_WithTooLongHierarchy_ThrowsArgumentException()
        {
            // Arrange
            DataBaseRef db = DataBaseRef.Create("database");
            string[] hierarchy = [.. Enumerable.Repeat("level", 101)];

            // Act & Assert
            Assert.Throws<ArgumentException>(() => PxFileRef.ValidateAndCreate("file1", db, hierarchy));
        }

        [Test]
        public void Create_WithInvalidHierarchyCharacters_ThrowsArgumentException()
        {
            // Arrange
            DataBaseRef db = DataBaseRef.Create("database");
            string[] hierarchy = ["level1", "invalid#level"];

            // Act & Assert
            Assert.Throws<ArgumentException>(() => PxFileRef.ValidateAndCreate("file1", db, hierarchy));
        }

        [Test]
        public void Create_WithNullHierarchyLevels_ThrowsArgumentException()
        {
            // Arrange
            DataBaseRef db = DataBaseRef.Create("database");
            string[] hierarchy = ["level1", null!, "level3"];

            // Act & Assert
            Assert.Throws<ArgumentException>(() => PxFileRef.ValidateAndCreate("file1", db, hierarchy));
        }

        [Test]
        public void Create_WithEmptyHierarchyLevels_ThrowsArgumentException()
        {

            // Arrange
            DataBaseRef db = DataBaseRef.Create("database");
            string[] hierarchy = ["level1", "", "level3"];

            // Act & Assert
            Assert.Throws<ArgumentException>(() => PxFileRef.ValidateAndCreate("file1", db, hierarchy));
        }

        [Test]
        public void GetHierarchyLevels_WhenHierarchyIsNull_ReturnsNull()
        {
            // Arrange
            DataBaseRef db = DataBaseRef.Create("database");
            PxFileRef pxFileRef = PxFileRef.ValidateAndCreate("file1", db, null);

            // Act
            string[]? levels = pxFileRef.GetHierarchyLevels();

            // Assert
            Assert.That(levels, Is.Null);
        }

        [Test]
        public void GetHierarchyLevels_WhenHierarchyExists_ReturnsSplitLevels()
        {
            // Arrange
            DataBaseRef db = DataBaseRef.Create("database");
            string[] hierarchy = ["level1", "level2"];
            PxFileRef pxFileRef = PxFileRef.ValidateAndCreate("file1", db, hierarchy);

            // Act
            string[]? levels = pxFileRef.GetHierarchyLevels();

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(levels, Is.Not.Null);
                Assert.That(levels, Is.EqualTo(hierarchy));
            }
        }

        #endregion

        #region GetHashCode

        [Test]
        public void GetHashCode_SameIdAndDatabase_ReturnsSameHashCode()
        {
            const string id = "file1";
            const string dbId = "database";
            DataBaseRef db = DataBaseRef.Create(dbId);
            PxFileRef ref1 = PxFileRef.ValidateAndCreate(id, db, ["statisticalProgram"]);
            PxFileRef ref2 = PxFileRef.ValidateAndCreate(id, db, ["statisticalProgram"]);
            Assert.That(ref1.GetHashCode(), Is.EqualTo(ref2.GetHashCode()));
        }

        [Test]
        public void GetHashCode_DifferentIdsSameDatabase_ReturnsDifferentHashCode()
        {
            const string id1 = "file1";
            const string id2 = "file2";
            const string dbId = "database";
            DataBaseRef db = DataBaseRef.Create(dbId);
            PxFileRef ref1 = PxFileRef.ValidateAndCreate(id1, db, ["statisticalProgram"]);
            PxFileRef ref2 = PxFileRef.ValidateAndCreate(id2, db, ["statisticalProgram"]);
            Assert.That(ref1.GetHashCode(), Is.Not.EqualTo(ref2.GetHashCode()));
        }

        [Test]
        public void GetHashCode_SameIdsDifferentDatabase_ReturnsDifferentHashCode()
        {
            const string id = "file1";
            const string dbId1 = "database1";
            const string dbId2 = "database2";
            DataBaseRef db1 = DataBaseRef.Create(dbId1);
            DataBaseRef db2 = DataBaseRef.Create(dbId2);
            PxFileRef ref1 = PxFileRef.ValidateAndCreate(id, db1, ["statisticalProgram"]);
            PxFileRef ref2 = PxFileRef.ValidateAndCreate(id, db2, ["statisticalProgram"]);
            Assert.That(ref1.GetHashCode(), Is.Not.EqualTo(ref2.GetHashCode()));
        }

        #endregion
    }
}
