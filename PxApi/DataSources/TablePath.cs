namespace PxApi.DataSources
{
    /// <summary>
    /// Contains a list of strings for accessing a table in a datasource.
    /// </summary>
    public class TablePath : List<string>
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="path">A full path to the file.</param>
        public TablePath(string path)
        {
            foreach (string item in path.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            {
                Add(item);
            }
        }

        /// <summary>
        /// Represents the path combined to a string.
        /// </summary>
        /// <returns>A file path string</returns>
        public string ToPathString()
        {
            return Path.Combine([.. this]);
        }
    }
}
