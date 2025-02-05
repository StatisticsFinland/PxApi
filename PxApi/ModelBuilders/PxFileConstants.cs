namespace PxApi.ModelBuilders
{
    /// <summary>
    /// String constant for the Px file format
    /// </summary>
    public static class PxFileConstants
    {
        /// <summary>
        /// File extension for the Px file format
        /// </summary>
        public const string FILE_ENDING = ".px";

        /// <summary>
        /// Key for NOTE in the Px files
        /// </summary>
        public const string NOTE = "NOTE";

        /// <summary>
        /// Key for VARIABLE in the Px files
        /// </summary>
        public const string SOURCE = "SOURCE";

        /// <summary>
        /// Key for TABLEID in the Px files
        /// </summary>
        public const string TABLEID = "TABLEID";

        /// <summary>
        /// Key for table CONTENTS in the Px files
        /// </summary>
        public const string CONTENTS = "CONTENTS";

        /// <summary>
        /// Key for table DESCRIPTION in the Px files
        /// </summary>
        public const string DESCRIPTION = "DESCRIPTION";

        /// <summary>
        /// Key for table META-ID in the Px files
        /// </summary>
        public const string META_ID = "META-ID";

        /// <summary>
        /// Ordinal value for table META-ID in the Px files
        /// </summary>
        public const string ORDINAL_VALUE = "SCALE-TYPE=ordinal";

        /// <summary>
        /// Nominal value for table META-ID in the Px files
        /// </summary>
        public const string NOMINAL_VALUE = "SCALE-TYPE=nominal";
    }
}
