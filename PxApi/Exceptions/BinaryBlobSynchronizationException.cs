using System;
using PxApi.Models;

namespace PxApi.Exceptions
{
    /// <summary>
    /// Exception thrown when binary blob data for a given Px file and timestamp is not synchronized or cannot be found.
    /// </summary>
    public class BinaryBlobSynchronizationException(PxFileRef file, DateTime timestamp) : Exception
    {
        /// <summary>
        /// Gets the Px file reference associated with the synchronization error.
        /// </summary>
        public PxFileRef File { get; } = file;

        /// <summary>
        /// Gets the timestamp used for synchronization.
        /// </summary>
        public DateTime Timestamp { get; } = timestamp;

        /// <summary>
        /// Gets the message that describes the current exception.
        /// </summary>
        public override string Message => $"Binary blob not synchronized for file '{File.Id}' at '{Timestamp:O}'.";
    }
}
