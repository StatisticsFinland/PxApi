using Moq;
using Px.Utils.Language;
using Px.Utils.Models.Metadata.Dimensions;
using Px.Utils.Models.Metadata.MetaProperties;
using PxApi.ModelBuilders;
using PxApi.Models;

namespace PxApi.UnitTests.ModelBuilderTests
{
    [TestFixture(TestName = "BuildValue tests")]
    internal class BuildValueTests
    {
        private Mock<IReadOnlyDimensionValue> _mockDimensionValue;

        [SetUp]
        public void SetUp()
        {
            _mockDimensionValue = new Mock<IReadOnlyDimensionValue>();
        }

        [Test]
        public void BuildValue_ShouldReturnValueWithCorrectProperties_WhenValidInput()
        {
            // Arrange
            string lang = "en";

            string code = "testcode";
            MultilanguageString name = new(new Dictionary<string, string> {
                { "fi", "Name.fi" },
                { "en", "Name.en" },
                { "sv", "Name.sv" }
            });

            MultilanguageStringProperty noteProperty = new(new(new Dictionary<string, string> {
                { "fi", "Note.fi" },
                { "en", "Note.en" },
                { "sv", "Note.sv" }
            }));

            Dictionary<string, MetaProperty> additionalProperties = new()
            {
                { PxFileConstants.NOTE, noteProperty }
            };

            _mockDimensionValue.Setup(m => m.Code).Returns(code);
            _mockDimensionValue.Setup(m => m.Name).Returns(name);
            _mockDimensionValue.Setup(m => m.AdditionalProperties).Returns(additionalProperties);

            // Act
            Value result = ModelBuilder.BuildValue(_mockDimensionValue.Object, lang);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.Code, Is.EqualTo(code));
                Assert.That(result.Name, Is.EqualTo(name[lang]));
                if(additionalProperties[PxFileConstants.NOTE] is MultilanguageStringProperty mlsp)
                {
                    Assert.That(result.Note, Is.EqualTo(mlsp.Value[lang]));
                }
                else Assert.Fail("Note property was not of type MultilanguageStringProperty");
            });
        }

        [Test]
        public void BuildValue_ShouldReturnValueWithEmptyNote_WhenNoteIsNotPresent()
        {
            // Arrange
            string lang = "en";
            string code = "001";
            MultilanguageString name = new(new Dictionary<string, string> { { lang, "Test Name" } });
            Dictionary<string, MetaProperty> additionalProperties = [];

            _mockDimensionValue.Setup(m => m.Code).Returns(code);
            _mockDimensionValue.Setup(m => m.Name).Returns(name);
            _mockDimensionValue.Setup(m => m.AdditionalProperties).Returns(additionalProperties);

            // Act
            Value result = ModelBuilder.BuildValue(_mockDimensionValue.Object, lang);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.Code, Is.EqualTo(code));
                Assert.That(result.Name, Is.EqualTo(name[lang]));
                Assert.That(result.Note, Is.Null);
            });
        }
    }
}
