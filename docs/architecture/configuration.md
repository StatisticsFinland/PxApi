# Configuration

all configuration is loaded from `appsettings.json` (and environment variable overrides) into typed classes in `PxApi/Configuration/`. The top-level loader is `AppSettings.Load()` called in `Program.cs`.

## AppSettings (Root)

**File**: `PxApi/Configuration/AppSettings.cs`

Top-level configuration container with these sections:

| Property | Type | Description |
|----------|------|-------------|
| `RootUrl` | `string` | Base absolute URL for generated links and OpenAPI servers |
| `DataBases` | `DataBaseConfig[]` | Array of configured database connections |
| `FeatureManagement` | config section | Feature flags (e.g., `CacheController`) |
| `Authentication` | `AuthenticationConfig` | Per-controller API key settings |
| `QueryLimits` | `QueryLimitsConfig` | Request size limits |
| `Cache` | `MemoryCacheConfig` | Global memory cache sizing |
| `OpenApi` | `OpenApiConfig` | Swagger metadata |
| `Localization` | `LocalizationConfig` | language configuration |
| `BlobReadMode` | `BlobReadModeConfig` | Binary blob read strategy tuning |
| `ApplicationInsights` | `ApplicationInsightsConfig` | Azure Application Insights |
| `Search` | `SearchConfig` | Elasticsearch connection |

## Database Configuration

**File**: `PxApi/Configuration/DataBaseConfig.cs`

Each database entry has:
- `Id` — Unique identifier, used as route parameter and DI key
- `Type` — One of `Mounted`, `FileShare`, `BlobStorage`, `BinaryBlobStorage` (see `DataBaseType.cs`)
- `CacheConfig` — Per-database cache TTLs (`DatabaseCacheConfig`)
- `Custom` — `Dictionary<string, string>` for connector-specific settings (connection strings, paths, etc.)

### Database Types

**File**: `PxApi/Configuration/DataBaseType.cs`

```
Mounted            — Local or network-mounted filesystem
FileShare          — Azure File Share
BlobStorage        — Azure Blob Storage (plain .px files)
BinaryBlobStorage  — Azure Blob Storage (pre-processed binary format)
```

## Cache Configuration

### Global: MemoryCacheConfig

**File**: `PxApi/Configuration/MemoryCacheConfig.cs`

Controls the global `IMemoryCache` instance:
- `MaxSizeBytes` — Total cache capacity (default: 524288000 = 500 MB)
- `DefaultDataCellSize` — Heuristic size per data cell for cache accounting
- `DefaultUpdateTaskSize`, `DefaultFileListSize`, `DefaultMetaSize`, `DefaultAliasSize` — Heuristic sizes for other cached entity types

### Per-Database: DatabaseCacheConfig

**File**: `PxApi/Configuration/DatabaseCacheConfig.cs`

Per-database TTL settings:
- `TableList` — TTL for file list cache
- `Meta` — TTL for metadata cache
- `Data` — TTL for data cache
- `RevalidationIntervalMs` — Optional interval for background cache revalidation

## Query Limits

**File**: `PxApi/Configuration/QueryLimitsConfig.cs`

- `JsonMaxCells` — Cell limit for future JSON minimal format
- `JsonStatMaxCells` — Cell limit for current data endpoints; exceeding returns HTTP 413

## Localization

**File**: `PxApi/Configuration/LocalizationConfig.cs`

- Default language and set of supported languages (ISO 639-1 codes)
- Controllers validate incoming `lang` parameter against this list

## Authentication

**File**: `PxApi/Configuration/AuthenticationConfig.cs`

Per-controller API key configuration. Each controller type (`Cache`, `Databases`, `Tables`, `Metadata`, `Data`, `Search`, `Health`) can have its own:
- `Key` / `Hash` + `Salt` — API key value or hashed key
- `HeaderName` — Custom HTTP header name (defaults like `X-Data-API-Key`)

Authentication is entirely optional. If no key is configured for a controller, that controller is public. The hidden `InfoController` endpoint does not currently have a dedicated authentication configuration section.

## Search

**File**: `PxApi/Configuration/SearchConfig.cs`

- `CloudId` — Elasticsearch cloud ID from config
- API key from `SEARCH_API_KEY` environment variable

## OpenAPI

**File**: `PxApi/Configuration/OpenApiConfig.cs`

Contact and license metadata injected into the OpenAPI spec.

## Application Insights

**File**: `PxApi/Configuration/ApplicationInsightsConfig.cs`

- `ConnectionString` — AI connection string; can be overridden by `APPLICATIONINSIGHTS_CONNECTION_STRING` environment variable
- When present, enables telemetry and removes default AI log filter
- SDK 3.1.2 uses native OpenTelemetry logging. Configure thresholds under
	`Logging:OpenTelemetry:LogLevel` (default `Information`). The obsolete 2.23
	logger package is not used; no additional exporter is registered.
- `SamplingRatio` defaults to `1.0` (valid range 0-1). Invalid/nonfinite values
	fall back to 1. SDK 2.x `EnableAdaptiveSampling` and `MinimumLevel` are no longer
	supported; migrate deployment overrides.
- `EnableTraceBasedLogsSampler` defaults to `false`, keeping completion logs
	independent of trace sampling. Automatic request/dependency telemetry remains
	SDK-owned. Upstream and ingestion sampling must still be checked operationally.
- `IncludeAuditLogs` defaults to `false`. Enabling it explicitly permits selected
	audit identity/IP/header properties into Azure and requires privacy approval;
	NLog's final audit rule does not isolate other providers.

## Query Logging

This section is read per request through `IConfiguration`, not `AppSettings`.

| Setting | Default | Meaning |
| --- | --- | --- |
| `QueryLogging:SelectionDetailMode` | `counts` | `counts`, `codes`, or `off`; unrecognized values fall back to counts |
| `QueryLogging:CaptureSearchText` | `false` | Approved sanitized text, at most 128 characters; sanitization is not anonymization |
| `QueryLogging:CaptureResultIds` | `false` | Optional ranked/listing file identifiers, at most 100 IDs |

Dimension JSON contains at most 64 summaries. codes are captured only in codes
mode and only for complete selections of at most 10 values. Larger selections
omit their code list with `code_capture_status=over_limit`; smaller selections
retain their complete database codes regardless of their serialized size.
Omitted detail has flags/status; absent numbers are unknown, while zero means
a measured empty result. Each JSON field is serialized once; no application
byte budget is applied. Verify sink property limits and actual ingestion before
rollout rather than estimating transport size in the application.

Identifiers are bounded to 128 characters and control characters are neutralized;
truncation is explicit. Search bodies, raw query URLs, data values, API keys and
derived key/IP identities are not completion fields. Hashes are pseudonymous
and need the same retention/access policy as other query detail. Diagnostic
exception text can still contain sensitive backend context and requires restricted
access and retention. No new header allowlist or content redaction is imposed on
maintainer-configured audit headers.

Use `event_name=query_completed` and `schema_version=1` for analytics. Do not combine
completion events, automatic requests and audit records into a request count.
If deployment sampling is enabled, use weights for aggregate counts and do not
claim exact unique subcube popularity or percentiles from partial telemetry.

## Blob Read Mode

**File**: `PxApi/Configuration/BlobReadModeConfig.cs`

Tunes the strategy for reading binary blobs:
- Thresholds for switching between sequential streaming and windowed/range reads
- Used by `BlobReadModeSelector` utility

## JSON Serialization

**Files**: `PxApi/Configuration/GlobalJsonConverterOptions.cs`, `DataValueTypeJsonConverter.cs`, `DoubleDataValueJsonConverter.cs`

Custom JSON converters for PX data types. `GlobalJsonConverterOptions` provides the shared `JsonSerializerOptions` used across the application.

## Environment Variable Overrides

Standard ASP.NET Core configuration binding applies. Use `__` as separator:
```
Authentication__Data__Key=your-key
DataBases__0__Id=MyDatabase
APPLICATIONINSIGHTS_CONNECTION_STRING=...
SEARCH_API_KEY=...
```
