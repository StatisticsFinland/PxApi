using PxApi.DataSources;

namespace PxApi.Utilities
{
    /// <summary>
    /// Utility functions for working with paths
    /// </summary>
    public static class PathFunctions
    {
        /// <summary>
        /// Builds a table reference from paths.
        /// </summary>
        /// <param name="fullPath">Full path to the table.</param>
        /// <param name="rootPath">Path to the root of the data source.</param>
        /// <exception cref="UnauthorizedAccessException">Thrown if the table is not located under the root path.</exception>
        public static PxTable BuildTableReferenceFromPath(string fullPath, string rootPath)
        {
            if(!fullPath.StartsWith(rootPath))
            {
                throw new UnauthorizedAccessException("Access to the path is denied.");
            }

            string[] relativePath = fullPath[rootPath.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            return new PxTable(relativePath[^1], [.. relativePath[1 ..^1]], relativePath[0]);
        }

        /// <summary>
        /// Construct the full path to a table based on the root path.
        /// </summary>
        public static string GetFullPathToTable(this PxTable table, string rootPath)
        {
            return Path.Combine(rootPath, table.DatabaseId, Path.Combine([.. table.Hierarchy]), table.TableId);
        }

        /// <summary>
        /// Checks if a path string contains invalid characters.
        /// </summary>
        /// <exception cref="ArgumentException">Path string contains invalid characters.</exception>
        public static void CheckStringsForInvalidPathChars(params string[] pathStrings)
        {
            char[] invalidPathChars = Path.GetInvalidPathChars();
            foreach (string s in pathStrings)
            {
                if (s.Any(c => invalidPathChars.Contains(c)))
                {
                    throw new ArgumentException($"String {s} contains invalid characters.");
                }
            }
        }
    }
}
