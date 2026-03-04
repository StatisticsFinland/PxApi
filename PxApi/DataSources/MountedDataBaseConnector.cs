using PxApi.ModelBuilders;
using PxApi.Models;
using PxApi.Utilities;
using System.Diagnostics.CodeAnalysis;

namespace PxApi.DataSources
{
    /// <summary>
    /// Data source for using database on the local file system.
    /// </summary>
    [ExcludeFromCodeCoverage] // This class is not unit tested because it relies on file system access.
    public class MountedDataBaseConnector(DataBaseRef dataBase, string rootPath, ILogger<MountedDataBaseConnector> logger) : DataBaseConnector(dataBase)
    {
        private readonly string _normalizedRootPath = NormalizeDirectoryPath(rootPath);

        /// <inheritdoc/>
        protected override ILogger Logger => logger;

        /// <inheritdoc/>
        public override Task<PxFileRef[]> GetAllFilesAsync(CancellationToken ct = default)
        {
            using (Logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.CONTROLLER] = nameof(MountedDataBaseConnector),
                    [LoggerConsts.FUNCTION] = nameof(GetAllFilesAsync)
                }))
            {
                Logger.LogDebug("Listing all files");
                string fullPath = Path.GetFullPath(Path.Combine(_normalizedRootPath, DataBase.Id));
                if (!IsWithinDirectory(fullPath, _normalizedRootPath))
                {
                    Logger.LogWarning("Unauthorized access attempt: The database is not in the root path.");
                    throw new UnauthorizedAccessException("The database is not in the root path");
                }

                return Task.Run(() => Directory.GetFiles(
                    fullPath,
                    $"*{PxFileConstants.FILE_ENDING}",
                    SearchOption.AllDirectories)
                    .Select(FileRefFromPath)
                    .ToArray(), ct);

                PxFileRef FileRefFromPath(string path)
                {
                    string fileName = Path.GetFileNameWithoutExtension(path);
                    string? directoryPath = Path.GetDirectoryName(path);
                    string[]? hierarchy = null;

                    if (directoryPath is not null)
                    {
                        string relativePath = Path.GetRelativePath(fullPath, directoryPath);
                        if (relativePath != ".")
                        {
                            hierarchy = relativePath.Split(Path.DirectorySeparatorChar);
                        }
                    }

                    return PxFileRef.ValidateAndCreate(fileName, dataBase, hierarchy);
                }
            }
        }

        /// <inheritdoc/>
        protected override async Task<Stream> OpenPxFileStreamAsync(PxFileRef file, CancellationToken ct = default)
        {
            using (Logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.CONTROLLER] = nameof(MountedDataBaseConnector),
                    [LoggerConsts.FUNCTION] = nameof(OpenPxFileStreamAsync),
                    [LoggerConsts.PX_FILE] = file.Id
                }))
            {
                Logger.LogDebug("Opening file stream");
                if(file.DataBase.Id != DataBase.Id)
                {
                    Logger.LogWarning("The file does not belong to the database.");
                    throw new InvalidOperationException("The file does not belong to the database.");
                }

                string fullPath = GetValidatedPxFilePath(file);
                return await Task.Run(() => new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read), ct);
            }
        }

        /// <inheritdoc/>
        public override async Task<DateTime> GetLastWriteTimeAsync(PxFileRef file, CancellationToken ct = default)
        {
            using (Logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.CONTROLLER] = nameof(MountedDataBaseConnector),
                    [LoggerConsts.FUNCTION] = nameof(GetLastWriteTimeAsync),
                    [LoggerConsts.PX_FILE] = file.Id
                }))
            {
                Logger.LogDebug("Getting last write time");

                string fullPath = GetValidatedPxFilePath(file);
                return await Task.Run(() => File.GetLastWriteTimeUtc(fullPath), ct);
            }
        }

        /// <inheritdoc/>
        public override async Task<Stream> TryReadAuxiliaryFileAsync(string fileName, string[]? hierarchy, CancellationToken ct = default)
        {
            using (Logger.BeginScope(new Dictionary<string, object>
            {
                [LoggerConsts.DB_ID] = DataBase.Id,
                [LoggerConsts.CONTROLLER] = nameof(MountedDataBaseConnector),
                [LoggerConsts.FUNCTION] = nameof(TryReadAuxiliaryFileAsync),
                [LoggerConsts.AUXILIARY_PATH] = fileName
            }))
            {
                string dbRoot = NormalizeDirectoryPath(Path.Combine(_normalizedRootPath, DataBase.Id));
                string[] pathSegments = hierarchy is not null
                    ? [dbRoot, .. hierarchy, fileName]
                    : [dbRoot, fileName];
                string fullPath = Path.GetFullPath(Path.Combine(pathSegments));

                if (!IsWithinDirectory(fullPath, dbRoot))
                {
                    Logger.LogWarning("Aux file path escaped database root");
                    throw new UnauthorizedAccessException("Auxiliary file path escaped database root.");
                }

                return await Task.Run(() =>
                {
                    if (!File.Exists(fullPath))
                    {
                        Logger.LogWarning("Aux file {AuxFile} not found", fullPath);
                        throw new FileNotFoundException("Auxiliary file not found", fullPath);
                    }
                    return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                }, ct);
            }
        }

        /// <summary>
        /// Builds the full file system path for the given <see cref="PxFileRef"/> using its hierarchy and ID,
        /// and validates that the resolved path is within the database root directory.
        /// </summary>
        /// <param name="file">The PX file reference containing the database, hierarchy, and file ID.</param>
        /// <returns>The validated full file path.</returns>
        /// <exception cref="UnauthorizedAccessException">If the resolved path is outside the database root directory.</exception>
        internal string GetValidatedPxFilePath(PxFileRef file)
        {
            string dbRoot = NormalizeDirectoryPath(Path.Combine(_normalizedRootPath, file.DataBase.Id));
            string[] pathSegments = file.Hierarchy is not null
                ? [dbRoot, .. file.GetHierarchyLevels()!, $"{file.Id}{PxFileConstants.FILE_ENDING}"]
                : [dbRoot, $"{file.Id}{PxFileConstants.FILE_ENDING}"];
            string fullPath = Path.GetFullPath(Path.Combine(pathSegments));

            if (!IsWithinDirectory(fullPath, dbRoot))
            {
                Logger.LogWarning("Unauthorized access attempt: The file path is outside the database root.");
                throw new UnauthorizedAccessException("The file path is outside the database root.");
            }

            return fullPath;
        }

        /// <summary>
        /// Normalizes a directory path to its full form with a trailing directory separator.
        /// </summary>
        internal static string NormalizeDirectoryPath(string path)
        {
            string fullPath = Path.GetFullPath(path);
            if (!fullPath.EndsWith(Path.DirectorySeparatorChar))
            {
                fullPath += Path.DirectorySeparatorChar;
            }
            return fullPath;
        }

        /// <summary>
        /// Checks whether the given path is within the specified directory using ordinal-ignore-case comparison
        /// to handle case-insensitive file systems.
        /// </summary>
        internal static bool IsWithinDirectory(string path, string normalizedDirectory)
        {
            string fullPath = Path.GetFullPath(path);
            return fullPath.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase);
        }
    }
}
