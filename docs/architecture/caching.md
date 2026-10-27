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

Data cache keys are composite values containing the data-entry discriminator,
the full `PxFileRef` (database, file ID, and hierarchy), and the existing matrix-map
hash. The file reference is retained for equality comparison, rather than reduced
to an integer hash. Exact reads, superset reads, submap eviction, metadata eviction,
and failed-load cleanup use the same table-scoped key construction.

Identical selections in different tables therefore cannot share data entries or
evict one another's data. The selection is still represented by a hash, so
selection-hash collisions within one table remain possible. This change does not
address same-table generation races. Other cache categories retain their existing
keys. `DatabaseCache` manages key construction internally.

## TTL Configuration

Cache TTLs are configured per-database via `DatabaseCacheConfig`:
- `TableList` — How long file listings are cached
- `Meta` — How long parsed metadata is cached
- `Data` — How long data arrays are cached
- `RevalidationIntervalMs` — Optional background revalidation interval

Global cache capacity is set via `MemoryCacheConfig.MaxSizeBytes`.

## Cache Invalidation

Three explicit invalidation paths via `CacheController`:

1. **Single table**: `ClearTableCache(fileRef)` — Removes metadata, data, and last-updated timestamp for one table
2. **Table list only**: `ClearTableListCache(dbRef)` - Removes only the cached file list. The next lookup rebuilds it; table content and database-name caches remain unchanged.
3. **Entire database**: `ClearDatabaseCacheAsync(dbRef)` — Iterates all files in the database and clears metadata, data, last-updated, file-list, and database-name caches

All are exposed as `DELETE` endpoints on `CacheController` (requires feature flag `CacheController = true`) and affect only the current API instance.

Data writes carry the metadata-container reference used for the source read.
If the related metadata entry is missing or is a different container when
`SetData` checks the cache, data caching is skipped before any submap eviction
or relationship changes and reported through the optional cache observer. On tracked requests,
the completion event includes `data_cache_write_skipped=true`; no separate
cache diagnostic is emitted. The in-flight request still awaits
the source read and returns its result; source errors and cancellation still
propagate normally.

## Concurrency

Submap filtering is materialized under the related-map list's lock, so callers
receive a snapshot rather than enumerating a list that eviction callbacks can modify.

The metadata reference check rejects writes whose metadata was already replaced
at the time of the lookup, even if the replacement contains identical metadata.
It is not atomic with data insertion or invalidation; metadata can still change
after the check.

### Request diagnostics

Tracked query requests expose `DataCacheOutcome` (`exact_hit`, `superset_hit`,
`miss`, `not_used`, or `unknown`) and thread-safe metadata/file-list hit/miss
counters on their single completion event. Counters measure internal lookup
work, not users, requested tables, or metadata endpoint requests. The first
lookup initializes both hit/miss counters; untouched cache categories remain
absent. Search enrichment can accumulate counters concurrently. Cache hits do
not imply a successfully written response.
`data_cache_write_skipped=true` is present only when a data-cache write was
skipped because its metadata entry was missing or had been replaced. This does not change the
lookup outcome (`miss`) or indicate a failed response.

[CachedDataSource](../../PxApi/Caching/CachedDataSource.cs) reports actual lookup
decisions and [DatabaseCache](../../PxApi/Caching/DatabaseCache.cs) reports skipped
data-cache writes through the optional, transport-neutral
[ICacheObserver](../../PxApi/Caching/ICacheObserver.cs). It does not depend on
`HttpContext`, `IHttpContextAccessor`, or `QueryObservation`. Manual/background
callers can omit the observer without changing cache behavior.

The composition root registers
[HttpCacheObserver](../../PxApi/Services/HttpCacheObserver.cs) as a stateless
singleton adapter. Only this adapter accesses the current HTTP feature, resolving
it on each call rather than retaining request state. It forwards decisions to
`QueryObservation` when a tracked request exists and otherwise does nothing.
Background cache operations and infrastructure endpoints emit no user completions.

Cache lifetimes, validity checks, loading and superset slicing are unchanged.
Routine hit/miss Debug messages are no longer emitted by `CachedDataSource`,
which has no logger dependency. Lookup outcomes are reported only through the
observer; without an observer, including untracked/background work using the
HTTP adapter, there are no routine outcome logs. Detailed failure diagnostics
remain at the connector and request-handling boundaries. Canonical user demand
belongs to the controller's completion fields, not each enriched table's cache
work.

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
