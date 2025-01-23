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
