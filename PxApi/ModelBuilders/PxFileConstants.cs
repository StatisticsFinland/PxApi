using System.Globalization;
using System.Collections.Generic;
using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Data;

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
        /// Key for VALUENOTE in the Px files
        /// </summary>
        public const string VALUENOTE = "VALUENOTE";

        /// <summary>
        /// Key for SOURCE in the Px files
        /// </summary>
        public const string SOURCE = "SOURCE";

        /// <summary>
        /// Key for SUBJECT-CODE in the Px files
        /// </summary>
        public const string SUBJECT_CODE = "SUBJECT-CODE";

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
        /// Key for STUB in the Px files
        /// </summary>
        public const string STUB = "STUB";

        /// <summary>
        /// Key for HEADING in the Px filestubs
        /// </summary>
        public const string HEADING = "HEADING";

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

        /// <summary>
        /// Dictionary containing translations for fi, sv and en for missing data value types.
        /// The outer dictionary key is the language code ("fi", "en", "sv").
        /// The inner dictionary maps the DataValueType enum to the human readable translation.
        /// </summary>
        public static Dictionary<string, Dictionary<DataValueType, string>> MISSING_DATA_TRANSLATIONS { get; } = new()
        {
            ["fi"] = new Dictionary<DataValueType, string>
            {
                [DataValueType.Missing] = "Tieto on puuttuva",
                [DataValueType.CanNotRepresent] = "Tieto on epälooginen esitettäväksi",
                [DataValueType.Confidential] = "Tieto on salassapitosäännön alainen",
                [DataValueType.NotAcquired] = "Tietoa ei ole saatu",
                [DataValueType.NotAsked] = "Tietoa ei ole kysytty",
                [DataValueType.Empty] = "......",
                [DataValueType.Nill] = "Ei yhtään"
            },
            ["en"] = new Dictionary<DataValueType, string>
            {
                [DataValueType.Missing] = "Missing",
                [DataValueType.CanNotRepresent] = "Not applicable",
                [DataValueType.Confidential] = "Data is subject to secrecy",
                [DataValueType.NotAcquired] = "Not available",
                [DataValueType.NotAsked] = "Not asked",
                [DataValueType.Empty] = "......",
                [DataValueType.Nill] = "Magnitude nil"
            },
            ["sv"] = new Dictionary<DataValueType, string>
            {
                [DataValueType.Missing] = "Uppgift saknas",
                [DataValueType.CanNotRepresent] = "Uppgift kan inte förekomma",
                [DataValueType.Confidential] = "Uppgift är sekretessbelagd",
                [DataValueType.NotAcquired] = "Uppgift inte tillgänglig",
                [DataValueType.NotAsked] = "Uppgift inte efterfrågad",
                [DataValueType.Empty] = "......",
                [DataValueType.Nill] = "Värdet noll"
            }
        };
    }
}
