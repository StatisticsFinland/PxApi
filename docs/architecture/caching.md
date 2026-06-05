# Caching Architecture

All caching code is in `PxApi/Caching/`. The cache sits between controllers and storage connectors, providing an in-memory layer backed by ASP.NET Core's `IMemoryCache`.

## Components

### ICachedDataSource / CachedDataSource

**Files**: `PxApi/Caching/ICachedDataSource.cs`, `PxApi/Caching/CachedDataSource.cs`

The main cache API used by controllers. Wraps raw `IDataBaseConnector` calls and caches:
- File lists (table listings per database)
- Metadata objects (parsed PX metadata per table)
- Data arrays (parsed PX data values per table)
- Last-updated timestamps (per PX file)
- Database names

**Superset optimization**: `GetDataCachedAsync` supports superset cache hits. If a cached matrix contains a superset of the requested dimensions, it uses `DataIndexer` to slice the requested subset from the cached data without re-reading the source. This avoids redundant storage reads for overlapping queries.

### DatabaseCache

**File**: `PxApi/Caching/DatabaseCache.cs`

Low-level cache wrapper around `IMemoryCache`. Key behaviors:
- Global size limit from `MemoryCacheConfig.MaxSizeBytes`
- Cache entries are stored as `Task<T>` so concurrent callers waiting for the same key share the in-flight computation rather than triggering duplicate reads
- Supports typed get/set with size accounting for cache eviction

### MetaCacheContainer

**File**: `PxApi/Caching/MetaCacheContainer.cs`

Tracks relationships between metadata entries and their dependent data entries. When metadata is evicted from cache, all related data cache entries are also removed. This ensures data cache entries never outlive their metadata.

### DataCacheContainer

**File**: `PxApi/Caching/DataCacheContainer.cs`

Wraps cached data arrays with their associated dimension map (matrix shape). Used by the superset optimization to determine if a cached entry can satisfy a new request.

### CacheContainer

**File**: `PxApi/Caching/CacheContainer.cs`

Generic wrapper used to store cached items with metadata needed for cache management.

## Cache Key Structure

Cache keys combine the database ID, file ID, and entity type. The `DatabaseCache` class manages key construction internally.

## TTL Configuration

Cache TTLs are configured per-database via `DatabaseCacheConfig`:
- `TableList` — How long file listings are cached
- `Meta` — How long parsed metadata is cached
- `Data` — How long data arrays are cached
- `RevalidationIntervalMs` — Optional background revalidation interval

Global cache capacity is set via `MemoryCacheConfig.MaxSizeBytes`.

## Cache Invalidation

Two explicit invalidation paths via `CacheController`:

1. **Single table**: `ClearTableCache(dbRef, fileRef)` — Removes metadata, data, and last-updated timestamp for one table
2. **Entire database**: `ClearDatabaseCacheAsync(dbRef)` — Iterates all files in the database and clears metadata, data, last-updated, file-list, and database-name caches

Both are exposed as `DELETE` endpoints on `CacheController` (requires feature flag `CacheController = true`).

## Concurrency

The `Task<T>` storage pattern in `DatabaseCache` prevents cache stampede. When multiple concurrent requests hit the same cache miss, only one request triggers the actual data fetch. All other requests await the same `Task<T>`, sharing the result.

## Data Flow

```
Controller
  → CachedDataSource.GetMetaCachedAsync(dbRef, fileRef, lang)
    → DatabaseCache.GetOrCreateAsync(key)
      → Cache hit? Return cached Task<T>
      → Cache miss? 
        → IDataBaseConnector.GetMetaAsync(...)
        → Store result as Task<T> in IMemoryCache
        → MetaCacheContainer tracks dependent data keys
    → Return metadata

Controller
  → CachedDataSource.GetDataCachedAsync(dbRef, fileRef, matrixMap)
    → Check for exact cache hit
    → Check for superset cache hit (DataCacheContainer)
      → If superset found, use DataIndexer to slice subset
    → Cache miss? Fetch from connector, cache result
    → Return data array
```
