using PxApi.Models;
using System.Collections.Concurrent;

namespace PxApi.Caching
{
    public class LastUpdateCache
    {
        private readonly ConcurrentDictionary<PxFileRef, Task<DateTime>> keyValuePairs = [];
    }
}
