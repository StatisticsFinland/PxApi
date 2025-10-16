namespace PxApi.Utilities
{
    public static class MetaCodeTools
    {
        /// <summary>
        /// Extension method that converts any code to an url safe version.
        /// </summary>
        public static string Convert(this string input)
        {
            char[] buffer = new char[input.Length];
            int codeLen = 0;

            for(int i = 0; i < input.Length; i++)
            {
                char deumlaut = DeUmlaut(input[i]);
                if(char.IsAsciiLetterOrDigit(deumlaut))
                {
                    buffer[codeLen] = deumlaut;
                    codeLen++;
                }
                else if (input[i] == '-' || (char.IsWhiteSpace(input[i]) && codeLen > 0 && buffer[codeLen-1] != '-'))
                {
                    buffer[codeLen] = '-';
                    codeLen++;
                }
            }
            return new string(buffer, 0, codeLen);  
        }

        /// <summary>
        /// Extension method that allows url safe code to be compared to any other code.
        /// </summary>
        /// <param name="compareThis"></param>
        /// <param name="toThis"></param>
        /// <returns></returns>
        public static bool Compare(this string compareThis, string toThis)
        {
            return compareThis.Equals(toThis.Convert());
        }

        private static char DeUmlaut(char input)
        {
            if (char.ToLowerInvariant(input) == 'ä') return 'a';
            else if (char.ToLowerInvariant(input) == 'ö') return 'o';
            else if (char.ToLowerInvariant(input) == 'å') return 'a';
            return char.ToLowerInvariant(input);
        }
    }
}
