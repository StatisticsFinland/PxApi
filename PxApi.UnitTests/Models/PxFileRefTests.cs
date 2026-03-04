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
            Assert.Multiple(() =>
            {
                Assert.That(pxFileRef.Id, Is.EqualTo(id));
                Assert.That(pxFileRef.DataBase, Is.EqualTo(db));
            });
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
            Assert.Multiple(() =>
            {
                Assert.That(pxFileRef.Id, Is.EqualTo(tableId));
                Assert.That(pxFileRef.DataBase, Is.EqualTo(db));
            });
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
            Assert.Multiple(() =>
            {
                Assert.That(pxFileRef.Id, Is.EqualTo(tableId));
                Assert.That(pxFileRef.DataBase, Is.EqualTo(db));
            });
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
            Assert.Multiple(() =>
            {
                Assert.That(pxFileRef.Id, Is.EqualTo(tableId));
                Assert.That(pxFileRef.DataBase, Is.EqualTo(db));
            });
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
