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
            PxFileRef pxFileRef = PxFileRef.CreateFromId(id, db);
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
        [TestCase(null, "database")]
        [TestCase("\r\n \n", "database")]
        [TestCase("AaBbCcDdEeFfGgHhIiJjKkLlMmNnOoPpQqRrRsStTuUvVwWxXyYz", "database")] // too long
        public void Create_WithInvalidId_ThrowsArgumentException(string? id, string dbId)
        {
            DataBaseRef db = DataBaseRef.Create(dbId);
            Assert.Throws<ArgumentException>(() => PxFileRef.CreateFromId(id!, db), "Id cannot be null, whitespace or too long.");
        }

        [Test]
        public void Create_WithFilePathAndConfig_StoresPropertiesCorrectly_NoSeparator()
        {
            // Arrange
            string filePath = "C:/data/abc123.px";
            string fileName = "abc123";
            DataBaseRef db = DataBaseRef.Create("db1");

            // Act
            PxFileRef pxFileRef = PxFileRef.CreateFromPath(filePath, db);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(pxFileRef.Id, Is.EqualTo(fileName));
                Assert.That(pxFileRef.FilePath, Is.EqualTo(filePath));
                Assert.That(pxFileRef.DataBase, Is.EqualTo(db));
            });
        }

        [Test]
        public void Create_WithFilePathAndConfig_StoresPropertiesCorrectly_WithSeparatorAndIndexes()
        {
            // Arrange
            string filePath = "C:/data/database-grouping-id.px";
            DataBaseRef db = DataBaseRef.Create("db2");

            // Act
            PxFileRef pxFileRef = PxFileRef.CreateFromPath(filePath, db);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(pxFileRef.Id, Is.EqualTo("database-grouping-id"));
                Assert.That(pxFileRef.FilePath, Is.EqualTo(filePath));
                Assert.That(pxFileRef.DataBase, Is.EqualTo(db));
            });
        }

        [Test]
        public void Create_WithFilePathAndConfig_StoresPropertiesCorrectly_LastPartIndexes()
        {
            // Arrange
            string filePath = "C:/data/database-grouping-id.px";
            DataBaseRef db = DataBaseRef.Create("db3");

            // Act
            PxFileRef pxFileRef = PxFileRef.CreateFromPath(filePath, db);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(pxFileRef.Id, Is.EqualTo("database-grouping-id"));
                Assert.That(pxFileRef.FilePath, Is.EqualTo(filePath));
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
            PxFileRef ref1 = PxFileRef.CreateFromId(id, db);
            PxFileRef ref2 = PxFileRef.CreateFromId(id, db);
            Assert.That(ref1.GetHashCode(), Is.EqualTo(ref2.GetHashCode()));
        }

        [Test]
        public void GetHashCode_DifferentIdsSameDatabase_ReturnsDifferentHashCode()
        {
            const string id1 = "file1";
            const string id2 = "file2";
            const string dbId = "database";
            DataBaseRef db = DataBaseRef.Create(dbId);
            PxFileRef ref1 = PxFileRef.CreateFromId(id1, db);
            PxFileRef ref2 = PxFileRef.CreateFromId(id2, db);
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
            PxFileRef ref1 = PxFileRef.CreateFromId(id, db1);
            PxFileRef ref2 = PxFileRef.CreateFromId(id, db2);
            Assert.That(ref1.GetHashCode(), Is.Not.EqualTo(ref2.GetHashCode()));
        }

        #endregion
    }
}
