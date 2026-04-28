using PxApi.Utilities;

namespace PxApi.UnitTests.UtilitiesTests
{
    [TestFixture]
    public class InputSanitizerTests
    {
        [Test]
        public void SanitizeInput_NullInput_ReturnsEmpty()
        {
            Assert.That(InputSanitizer.SanitizeInput(null), Is.EqualTo(string.Empty));
        }

        [Test]
        public void SanitizeInput_EmptyString_ReturnsEmpty()
        {
            Assert.That(InputSanitizer.SanitizeInput(string.Empty), Is.EqualTo(string.Empty));
        }

        [Test]
        public void SanitizeInput_NormalText_ReturnsUnchanged()
        {
            string input = "Search query 2026, report: final! yes? it's_ready-now.";

            Assert.That(InputSanitizer.SanitizeInput(input), Is.EqualTo(input));
        }

        [Test]
        public void SanitizeInput_UnicodeLetters_Preserved()
        {
            string input = "väestö äö å 2026";

            Assert.That(InputSanitizer.SanitizeInput(input), Is.EqualTo(input));
        }

        [Test]
        public void SanitizeInput_NewlinesRemoved()
        {
            string input = "line1\r\nline2\nline3\rline4";
            string result = InputSanitizer.SanitizeInput(input);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Does.Not.Contain("\r"));
                Assert.That(result, Does.Not.Contain("\n"));
                Assert.That(result, Is.EqualTo("line1line2line3line4"));
            }
        }

        [Test]
        public void SanitizeInput_BracketsRemoved()
        {
            string input = "test(value)[list]{item}<tag>";

            Assert.That(InputSanitizer.SanitizeInput(input), Is.EqualTo("testvaluelistitemtag"));
        }

        [Test]
        public void SanitizeInput_SemicolonAndAngleBracketsRemoved()
        {
            string input = "select; drop<table>";

            Assert.That(InputSanitizer.SanitizeInput(input), Is.EqualTo("select droptable"));
        }

        [Test]
        public void SanitizeInput_AllowedSymbolsPreserved()
        {
            string input = ".,:!?'_-";

            Assert.That(InputSanitizer.SanitizeInput(input), Is.EqualTo(input));
        }

        [Test]
        public void SanitizeInput_LongInput_TruncatedToMaxLength()
        {
            string input = new('x', 300);
            string result = InputSanitizer.SanitizeInput(input, maxLength: 200);

            Assert.That(result, Has.Length.EqualTo(200));
        }

        [Test]
        public void SanitizeInput_MixedInput_OnlyAllowedCharsRemain()
        {
            string input = "Report;<admin>\nQ1_2026: valmis? kyllä/ei";

            Assert.That(InputSanitizer.SanitizeInput(input), Is.EqualTo("ReportadminQ1_2026: valmis? kylläei"));
        }

        [Test]
        public void SanitizeInput_ExactMaxLength_NotTruncated()
        {
            string input = new('x', 200);
            string result = InputSanitizer.SanitizeInput(input, maxLength: 200);

            Assert.That(result, Has.Length.EqualTo(200));
        }
    }
}
