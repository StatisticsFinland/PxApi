using PxApi.Utilities;
using PxApi.Caching;

namespace PxApi.Services;

/// <summary>Contributes cache decisions to the current tracked HTTP request, when present.</summary>
public sealed class HttpCacheObserver(IHttpContextAccessor httpContextAccessor) : ICacheObserver
{
    private QueryObservation? Observation => httpContextAccessor.HttpContext is HttpContext context ? QueryObservation.Get(context) : null;

    /// <inheritdoc />
    public void RecordFileListLookup(bool hit) => Observation?.CacheLookup(LoggerConsts.Query.Cache.FileList, hit);

    /// <inheritdoc />
    public void RecordMetadataLookup(bool hit) => Observation?.CacheLookup(LoggerConsts.Query.Cache.Metadata, hit);

    /// <inheritdoc />
    public void RecordDataCacheHit(bool isSuperset) => Observation?.Set(LoggerConsts.Query.Fields.DataCacheOutcome,
        isSuperset ? LoggerConsts.Query.DataCacheOutcome.SupersetHit : LoggerConsts.Query.DataCacheOutcome.ExactHit);

    /// <inheritdoc />
    public void RecordDataCacheMiss() => Observation?.Set(LoggerConsts.Query.Fields.DataCacheOutcome, LoggerConsts.Query.DataCacheOutcome.Miss);
}