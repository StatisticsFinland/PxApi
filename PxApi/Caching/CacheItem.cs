namespace PxApi.Caching
{
    /// <summary>
    /// Represents a cached item that stores a task and keeps track of the freshness of data the task is accessing.
    /// </summary>
    /// <typeparam name="T">Type of the task result.</typeparam>
    public class CacheItem<T>
    {
        /// <summary>
        /// The task that is cached.
        /// </summary>
        public Task<T> Task { get; }

        /// <summary>
        /// The last modified date of the file that the task is accessing.
        /// </summary>
        public DateTime FileModified { get; }

        /// <summary>
        /// The time that the cache item stays fresh.
        /// </summary>
        public TimeSpan StaysFresh { get; }

        /// <summary>
        /// Returns true if the cache item is fresh.
        /// </summary>
        public bool IsFresh => CreationTime < DateTime.Now.Subtract(StaysFresh);

        /// <summary>
        /// The time that the cache item was created.
        /// </summary>
        private DateTime CreationTime { get; }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public CacheItem(Task<T> task, TimeSpan staysFresh, DateTime fileModified)
        {
            Task = task;
            FileModified = fileModified;
            StaysFresh = staysFresh;
            CreationTime = DateTime.Now;
        }

        /// <summary>
        /// Copy constructor.
        /// </summary>
        public CacheItem(CacheItem<T> item)
        {
            Task = item.Task;
            FileModified = item.FileModified;
            CreationTime = DateTime.Now;
            StaysFresh = item.StaysFresh;
        }
    }
}
