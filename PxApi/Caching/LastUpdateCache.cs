using PxApi.Models;
using System.Collections.Concurrent;

namespace PxApi.Caching
{
    /// <summary>
    /// Cache for storing the last update timestamps of Px files.
    /// </summary>
    public class LastUpdateCache
    {
        /// <summary>
        /// A thread-safe dictionary that maps <see cref="PxFileRef"/> keys to tasks representing the associated <see
        /// cref="DateTime"/> values.
        /// </summary>
        /// <remarks>This dictionary is designed to handle concurrent access and is used to store and
        /// retrieve  asynchronous operations that resolve to <see cref="DateTime"/> values for a given <see
        /// cref="PxFileRef"/> key.</remarks>
        private readonly ConcurrentDictionary<PxFileRef, Task<DateTime>> keyValuePairs = [];
    }
}
