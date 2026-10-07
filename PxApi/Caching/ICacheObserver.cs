namespace PxApi.Caching;

/// <summary>Receives cache decisions without depending on a transport or logging provider.</summary>
public interface ICacheObserver
{
    /// <summary>Records whether a file-list lookup hit the cache.</summary>
    void RecordFileListLookup(bool hit);

    /// <summary>Records whether a metadata lookup hit the cache.</summary>
    void RecordMetadataLookup(bool hit);

    /// <summary>Records a valid exact or superset data-cache hit.</summary>
    void RecordDataCacheHit(bool isSuperset);

    /// <summary>Records a data-cache miss requiring source data.</summary>
    void RecordDataCacheMiss();
}