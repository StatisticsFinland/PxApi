using Px.Utils.Models.Metadata;

namespace PxApi.UnitTests.Models
{
    internal static class PxFixtures
    {
        internal static class MinimalPx
        {
            internal static string MINIMAL_UTF8_N =>
                "CHARSET=\"ANSI\";\n" +
                "AXIS-VERSION=\"2013\";\n" +
                "CODEPAGE=\"utf-8\";\n" +
                "LANGUAGE=\"fi\";\n" +
                "LANGUAGES=\"fi\",\"en\";\n" +
                "NEXT-UPDATE=\"20240131 08:00\";\n" +
                "SUBJECT-AREA=\"test\";\n" +
                "SUBJECT-AREA[en]=\"test\";\n" +
                "COPYRIGHT=YES;\n" +
                "STUB=\"dim1\";\n" +
                "HEADING=\"dim2\";\n" +
                "STUB[en]=\"dim1_en\";\n" +
                "HEADING[en]=\"dim2_en\";\n" +
                "VALUES(\"dim1\")=\"value1\";\n" +
                "VALUES(\"dim2\")=\"2024\",\"2025\";\n" +
                "VALUES[en](\"dim1_en\")=\"value1_en\";\n" +
                "VALUES[en](\"dim2_en\")=\"2024_en\",\"2025_en\";\n" +
                "DATA=1 2;";
        }
    }
}
