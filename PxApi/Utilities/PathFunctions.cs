using System.Diagnostics.CodeAnalysis;

namespace PxApi.Utilities
{
    /// <summary>
    /// Utility functions for working with paths
    /// </summary>
    public static class PathFunctions
    {
        /// <summary>
        /// Builds a path from a base path and a user path, and ensures that the resulting path is within the base path.
        /// </summary>
        /// <exception cref="UnauthorizedAccessException">The resulting path is outside the base path.</exception>
        [ExcludeFromCodeCoverage] // This method is not unit tested because it relies on file system access.
        public static string BuildAndSecurePath(string basePath, string userPath)
        {
            string fullBasePath = Path.GetFullPath(basePath);
            string combinedPath = Path.Combine(fullBasePath, userPath);
            combinedPath = Path.GetFullPath(combinedPath);
            if (!combinedPath.StartsWith(fullBasePath))
            {
                throw new UnauthorizedAccessException("Access to the path is denied.");
            }
            return combinedPath;
        }

        /// <summary>
        /// Builds a path from a base path and a list of user path parts, and ensures that the resulting path is within the base path.
        /// </summary>
        public static string BuildAndSecurePath(string basePath, List<string> userPath)
        {
            return BuildAndSecurePath(basePath, Path.Combine([.. userPath]));
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
