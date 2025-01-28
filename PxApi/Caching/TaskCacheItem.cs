namespace PxApi.Caching
{
    public class TaskCacheItem<T>(Task<T> task, DateTime created)
    {
        public Task<T> Task { get; } = task;
        public DateTime Created { get; } = created;
    }
}
