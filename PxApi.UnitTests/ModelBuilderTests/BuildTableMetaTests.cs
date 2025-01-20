using Px.Utils.Models.Metadata;
using PxApi.ModelBuilders;
using PxApi.Models;

namespace PxApi.UnitTests.ModelBuilderTests
{
    internal static class BuildTableMetaTests
    {
        [Test]
        public static void BuildTableMeta_WhenCalled_ReturnsTableMeta()
        {
            // Arrange
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();
            Uri urlRoot = new("https://example.com/example-db/");
            string lang = "en";

            // Act
            TableMeta result = ModelBuilder.BuildTableMeta(meta, urlRoot, lang);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result.ID, Is.EqualTo("table-tableid"));
                Assert.That(result.Contents, Is.EqualTo("table-contents.en"));
                Assert.That(result.Description, Is.EqualTo("table-description.en"));
                Assert.That(result.Note, Is.EqualTo("table-note.en"));
                Assert.That(result.ContentVariable, Is.Not.Null);
                Assert.That(result.TimeVariable, Is.Not.Null);
                Assert.That(result.ClassificatoryVariables, Has.Count.EqualTo(3));
                Assert.That(result.FirstPeriod, Is.EqualTo("time-value0-name.en"));
                Assert.That(result.LastPeriod, Is.EqualTo("time-value1-name.en"));
                Assert.That(result.LastModified, Is.EqualTo(new DateTime(2024, 10, 10, 0, 0, 0, DateTimeKind.Utc)));
            });
        }

        [Test]
        public static void BuildTableMeta_WhenCalledWithNullLang_ReturnsTableMetaWithDefaultLang()
        {
            // Arrange
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();
            Uri urlRoot = new("https://example.com/example-db/");

            // Act
            TableMeta result = ModelBuilder.BuildTableMeta(meta, urlRoot);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(result.ID, Is.EqualTo("table-tableid"));
                Assert.That(result.Contents, Is.EqualTo("table-contents.fi"));
                Assert.That(result.Description, Is.EqualTo("table-description.fi"));
                Assert.That(result.Note, Is.EqualTo("table-note.fi"));
                Assert.That(result.ContentVariable, Is.Not.Null);
                Assert.That(result.TimeVariable, Is.Not.Null);
                Assert.That(result.ClassificatoryVariables, Has.Count.EqualTo(3));
                Assert.That(result.FirstPeriod, Is.EqualTo("time-value0-name.fi"));
                Assert.That(result.LastPeriod, Is.EqualTo("time-value1-name.fi"));
                Assert.That(result.LastModified, Is.EqualTo(new DateTime(2024, 10, 10, 0, 0, 0, DateTimeKind.Utc)));
            });
        }

        [Test]
        public static void BuildContentVariableTest()
        {
            // Arrange
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();
            Uri urlRoot = new("https://example.com/example-db/");
            string lang = "en";
            // Act
            ContentVariable result = ModelBuilder.BuildContentVariable(meta, urlRoot, lang);
            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Code, Is.EqualTo("content-code"));
                Assert.That(result.Name, Is.EqualTo("content-name.en"));
                Assert.That(result.Note, Is.EqualTo("content-note.en"));
                Assert.That(result.Values, Has.Count.EqualTo(2));
                Assert.That(result.Url.ToString(), Is.EqualTo("https://example.com/example-db/content-code?lang=en"));
            });
        }
    }
}
