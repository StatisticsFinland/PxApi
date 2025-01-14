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
    }
}
