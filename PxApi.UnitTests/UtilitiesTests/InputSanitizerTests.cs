using PxApi.Utilities;

namespace PxApi.UnitTests.UtilitiesTests
{
    [TestFixture]
    public class InputSanitizerTests
    {
        [Test]
        public void SanitizeForLog_NullInput_ReturnsEmpty()
        {
            Assert.That(InputSanitizer.SanitizeForLog(null), Is.EqualTo(string.Empty));
        }

        [Test]
        public void SanitizeForLog_EmptyString_ReturnsEmpty()
        {
            Assert.That(InputSanitizer.SanitizeForLog(string.Empty), Is.EqualTo(string.Empty));
        }

        [Test]
        public void SanitizeForLog_NormalText_ReturnsUnchanged()
        {
            Assert.That(InputSanitizer.SanitizeForLog("adoptio"), Is.EqualTo("adoptio"));
        }

        [Test]
        public void SanitizeForLog_ControlCharacters_ReplacedWithSpace()
        {
            // newline and tab are control chars that could enable log injection
            string input = "line1\nline2\ttab";
            string result = InputSanitizer.SanitizeForLog(input);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Does.Not.Contain("\n"));
                Assert.That(result, Does.Not.Contain("\t"));
                Assert.That(result, Is.EqualTo("line1 line2 tab"));
            }
        }

        [Test]
        public void SanitizeForLog_LongInput_TruncatedToMaxLength()
        {
            string input = new('x', 300);
            string result = InputSanitizer.SanitizeForLog(input, maxLength: 200);

            Assert.That(result, Has.Length.EqualTo(200));
        }

        [Test]
        public void SanitizeForLog_ExactMaxLength_NotTruncated()
        {
            string input = new('x', 200);
            string result = InputSanitizer.SanitizeForLog(input, maxLength: 200);

            Assert.That(result, Has.Length.EqualTo(200));
        }
    }
}
