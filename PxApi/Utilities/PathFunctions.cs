namespace PxApi.Utilities
{
    public static class PathFunctions
    {
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

        public static string BuildAndSecurePath(string basePath, List<string> userPath)
        {
            return BuildAndSecurePath(basePath, Path.Combine([.. userPath]));
        }

        public static List<string> BuildHierarchy(string path)
        {
            char[] invalidPathChars = Path.GetInvalidPathChars();
            if (path.Any(c => invalidPathChars.Contains(c)))
            {
                throw new ArgumentException("Path contains invalid characters.");
            }
            return [.. path.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)];
        }
    }
}
