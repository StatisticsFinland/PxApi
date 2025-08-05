using PxApi.Models;

namespace PxApi.UnitTests.Models
{
    [TestFixture]
    internal class DataBaseRefTests
    {
        #region Create

        [Test]
        [TestCase("database")]
        [TestCase("a")]
        [TestCase("a123")]
        [TestCase("AaBbCcDdEeFfGgHhIiJjKkLlMmNnOoPpQqRrRsStTuUvVwWxXy")] // 50 characters
        public void Create_WithValidId_ReturnsDataBaseRef(string id)
        {
            DataBaseRef dataBaseRef = DataBaseRef.Create(id);
            Assert.That(dataBaseRef.Id, Is.EqualTo(id));
        }

        [Test]
        [TestCase("data-base")]
        [TestCase("data base")]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        [TestCase("\r\n \n")]
        [TestCase("AaBbCcDdEeFfGgHhIiJjKkLlMmNnOoPpQqRrRsStTuUvVwWxXyYz")] // too long
        public void Create_WithInvalidId_ThrowsArgumentException(string? id)
        {
            Assert.Throws<ArgumentException>(() => DataBaseRef.Create(id!), "Id cannot be null, whitespace or too long.");
        }

        #endregion

        #region GetHashCode

        [Test]
        public void GetHashCode_SameId_ReturnsSameHashCode()
        {
            const string id = "database";
            DataBaseRef ref1 = DataBaseRef.Create(id);
            DataBaseRef ref2 = DataBaseRef.Create(id);
            Assert.That(ref1.GetHashCode(), Is.EqualTo(ref2.GetHashCode()));
        }

        [Test]
        public void GetHashCode_DifferentIds_ReturnsDifferentHashCodes()
        {
            DataBaseRef ref1 = DataBaseRef.Create("database1");
            DataBaseRef ref2 = DataBaseRef.Create("database2");
            DataBaseRef ref3 = DataBaseRef.Create("a");
            DataBaseRef ref4 = DataBaseRef.Create("b");
            Assert.Multiple(() =>
            {
                Assert.That(ref1.GetHashCode(), Is.Not.EqualTo(ref2.GetHashCode()));
                Assert.That(ref3.GetHashCode(), Is.Not.EqualTo(ref4.GetHashCode()));
                Assert.That(ref1.GetHashCode(), Is.Not.EqualTo(ref3.GetHashCode()));
            });
        }

        #endregion
    }
}
