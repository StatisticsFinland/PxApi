using Microsoft.Extensions.Caching.Memory;

namespace PxApi.Caching
{
    public class TaskCache
    {
        private MemoryCache _cache;
        private TimeSpan _freshnessDuration = TimeSpan.FromMinutes(1);

        public TaskCache()
        {
            MemoryCacheOptions options = new();
            _cache = new MemoryCache(options);
        }
        public void Set<T>(TaskCacheItem<T> task, string key)
        {
            _cache.Set(key, task);
            
        }

        public TaskCacheItem<T> Get<T>(string key)
        {
            DateTime _freshnessTime = DateTime.Now.Add(_freshnessDuration);
            if (_freshnessTime <= _cache.Get<TaskCacheItem<T>>(key).Created)
            {
                _cache.Remove(key);
                return null;
            }
            return _cache.Get<TaskCacheItem<T>>(key);
        }

    }
}
