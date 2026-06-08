# Data Access & Storage Connectors

All data access code is in `PxApi/DataSources/`. The connector pattern abstracts storage backends behind a common interface, allowing the application to support local files, Azure File Shares, and Azure Blob Storage transparently.

## Interface: IDataBaseConnector

**File**: `PxApi/DataSources/IDataBaseConnector.cs`

The core abstraction exposing:
- Database discovery (list databases, get database names)
- File enumeration (list PX files in a database)
- Last-write timestamps (for cache invalidation)
- Metadata reads (PX metadata parsing)
- Data reads (PX data value arrays)
- Connectivity checks (for health endpoint)
- Auxiliary file reads (alias files)

## Base Class: DataBaseConnector

**File**: `PxApi/DataSources/DataBaseConnector.cs`

Abstract base that centralizes PX file parsing. It:
1. Opens a stream from the storage backend (abstract method)
2. Uses `Px.Utils` readers and builders to parse metadata
3. Reads `DoubleDataValue[]` data arrays from the stream

Subclasses only need to implement stream acquisition and file enumeration.

## Connector Types

### MountedDataBaseConnector

**File**: `PxApi/DataSources/MountedDataBaseConnector.cs`
**Config type**: `DataBaseType.Mounted`

Reads PX files from a local or network-mounted filesystem under a configured root path. Validates paths against directory traversal attacks.

**Custom config keys**: Root path to the mounted directory.

### FileShareDataBaseConnector

**File**: `PxApi/DataSources/FileShareDataBaseConnector.cs`
**Config type**: `DataBaseType.FileShare`

Reads PX files from Azure File Shares using `ShareServiceClient`. Uses Azure SDK for file listing and stream access.

### PxBlobDataBaseConnector

**File**: `PxApi/DataSources/PxBlobDataBaseConnector.cs`
**Config type**: `DataBaseType.BlobStorage`
**Inherits**: `BlobDataBaseConnector`

Reads plain `.px` files from Azure Blob Storage under the path pattern `px/{dbId}/...`. Uses the standard PX parsing flow from the base class.

### BinaryBlobDataBaseConnector

**File**: `PxApi/DataSources/BinaryBlobDataBaseConnector.cs`
**Config type**: `DataBaseType.BinaryBlobStorage`
**Inherits**: `BlobDataBaseConnector`

The most specialized connector, optimized for large datasets by separating metadata from data values:

- **Metadata**: Read from JSON files at `meta/{dbId}/{fileId}_*.meta.json`
- **Data**: Read from binary files at `bin/{dbId}/{fileId}_{contentValue}_{timestamp}.pxb`

Supports two read modes controlled by `BlobReadModeConfig`:
- **Sequential streaming**: Reads the entire binary blob
- **Windowed/range reads**: Issues HTTP range requests for specific byte offsets

The read mode is selected by `BlobReadModeSelector` based on request density (ratio of requested cells to total cells) and configured thresholds.

Throws `BinaryBlobSynchronizationException` when metadata references a binary blob that hasn't been synchronized yet.

### BlobDataBaseConnector (Base)

**File**: `PxApi/DataSources/BlobDataBaseConnector.cs`

Common base for blob-backed connectors. Provides Azure `BlobServiceClient` setup and shared blob operations.

## Connector Registration

**File**: `PxApi/Utilities/ServiceCollectionExtensions.cs`

Connectors are registered as keyed services in DI, one per configured database ID from `AppSettings.DataBases[]`. The key is the database ID string.

**File**: `PxApi/DataSources/DataBaseConnectorFactoryImpl.cs`

`IDataBaseConnectorFactory` resolves the correct keyed connector at runtime by database ID.

## Key Value Types

- **`DataBaseRef`** (`PxApi/Models/DataBaseRef.cs`): Validated database identifier. Rejects invalid characters.
- **`PxFileRef`** (`PxApi/Models/PxFileRef.cs`): Validated PX file identifier. Rejects invalid characters and path traversal.

Both are used throughout routing, connectors, and caching for type-safe identifier handling.

## Data Flow Summary

```
Controller
  → ICachedDataSource.GetMetaCachedAsync() / GetDataCachedAsync()
    → DatabaseCache (IMemoryCache)
      → IDataBaseConnector (keyed by database ID)
        → Storage backend (filesystem / Azure)
          → Px.Utils (parse PX format)
    → Returns IReadOnlyCubeMeta / DoubleDataValue[]
```
