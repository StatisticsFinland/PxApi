using System;
using PxApi.Models;

namespace PxApi.Exceptions
{
    /// <summary>
    /// Exception thrown when binary blob data for a given Px file and timestamp is not synchronized or cannot be found.
    /// </summary>
    /// <param name="file">The affected PX file.</param>
    /// <param name="timestamp">The expected synchronization timestamp.</param>
    /// <param name="blobPath">The missing blob location, including its container, when known.</param>
    public class BinaryBlobSynchronizationException(PxFileRef file, DateTime timestamp, string? blobPath = null) : Exception
    {
        /// <summary>
        /// Gets the Px file reference associated with the synchronization error.
        /// </summary>
        public PxFileRef File { get; } = file;

        /// <summary>
        /// Gets the timestamp used for synchronization.
        /// </summary>
        public DateTime Timestamp { get; } = timestamp;

        /// <summary>Gets the missing blob location, including its container, when known.</summary>
        public string? BlobPath { get; } = blobPath;

        /// <summary>
        /// Gets the message that describes the current exception.
        /// </summary>
        public override string Message => $"Binary blob not synchronized for file '{File.Id}' at '{Timestamp:O}'." +
            (BlobPath is null ? string.Empty : $" Missing blob '{BlobPath}'.");
    }
}
