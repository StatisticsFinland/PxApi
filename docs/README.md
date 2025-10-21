# PxApi

PxApi is a .NET 9.0 Web API for accessing PX statistical datasets. It provides table listings, table metadata and data retrieval with flexible dimension filtering and caching across multiple storage backends (local file system, Azure File Share, Azure Blob Storage).

## Implemented Features

- Table listing with paging (`/tables/{database}`)
- Table metadata in JSON-stat 2.0 (`/meta/{database}/{table}`)
- Data retrieval with filter semantics (code, range, positional) via GET query parameters or POST body (`/data/{database}/{table}`)
- Content negotiation (JSON-stat 2.0 or CSV) using the `Accept` header
- Cache management endpoints (database level and single table) (`/cache/{database}` / `/cache/{database}/{id}`)
- Global and per-database caching (file lists, metadata, data, last updated timestamps, grouping metadata)
- Feature flags (Swagger visibility of cache endpoints)
- API key authentication for cache endpoints
- Multiple storage types: Mounted (local / network), Azure File Share, Azure Blob Storage
- Query size limits returning HTTP 413 when exceeded
- Swagger / OpenAPI documentation with custom schema & document filters
- HEAD and OPTIONS support for discoverability and CORS pre-flight

CSV output is currently a placeholder implementation and intended for future enhancement.

## Endpoints

### Tables
`GET /tables/{database}?lang=fi&page=1&pageSize=50`
Returns a paged list of tables ordered by PX file name.

Query parameters:
- `lang` (optional, default `fi`): Language used for metadata resolution.
- `page` (optional, >=1, default `1`)
- `pageSize` (optional, 1-100, default `50`)

Responses:
- `200 OK` JSON body containing table listing and paging info
- `400 Bad Request` invalid paging values
- `404 Not Found` database missing

Additional methods:
- `HEAD /tables/{database}` validates existence and paging values
- `OPTIONS /tables/{database}` returns Allow header (`GET,HEAD,OPTIONS`)

### Metadata
`GET /meta/{database}/{table}?lang=fi`
Returns JSON-stat 2.0 metadata (structure only, no data filtering).

Query parameters:
- `lang` (optional): If omitted uses table default language

Responses:
- `200 OK` JSON-stat 2.0 metadata
- `400 Bad Request` language not available
- `404 Not Found` database or table missing
- `500 Internal Server Error` unexpected error

Additional methods:
- `HEAD /meta/{database}/{table}` existence & language validation only
- `OPTIONS /meta/{database}/{table}` returns Allow header (`GET,HEAD,OPTIONS`)

### Data
`GET /data/{database}/{table}?filters=TIME:from=2020&filters=TIME:to=2024&filters=REGION:code=001,002`

Retrieves data values applying filters to dimensions. Content negotiation:
- `Accept: application/json` or `*/*` -> JSON-stat 2.0
- `Accept: text/csv` -> CSV (placeholder)

Filter syntax (GET query parameters):
Each filter supplied via repeated `filters` query parameter: `dimensionCode:filterType=value`
Supported `filterType` values:
- `code` one or many codes (comma-separated), supports `*` wildcard
- `from` lower bound for range (supports wildcard `*` inside value)
- `to` upper bound for range (supports wildcard `*` inside value)
- `first` selects first N positions (positive integer)
- `last` selects last N positions (positive integer)

Rules:
- One filter per dimension.
- Wildcard `*` matches zero or more characters in code/from/to values.

POST alternative:
`POST /data/{database}/{table}` with JSON body mapping dimension codes to filter objects.
Example body:
```json
{
  "TIME": { "type": "from", "query": ["2020"] },
  "REGION": { "type": "code", "query": ["001", "002"] }
}
```

Query parameters (POST):
- `lang` optional language (defaults to table default)

Responses (GET & POST):
- `200 OK` JSON-stat 2.0 object or CSV text
- `400 Bad Request` invalid filters / language not available
- `404 Not Found` database or table missing
- `406 Not Acceptable` unsupported `Accept` header value
- `413 Payload Too Large` request cell count exceeds configured limit
- `415 Unsupported Media Type` (POST invalid content type)

Additional methods:
- `HEAD /data/{database}/{table}?lang=fi` existence & language validation only
- `OPTIONS /data/{database}/{table}` returns Allow header (`GET,POST,HEAD,OPTIONS`)

### Cache
Requires feature flag `CacheController = true` and valid API key when authentication is enabled.

- `DELETE /cache/{database}` clears all cache entries (file list, metadata, data, last updated) for a database.
- `DELETE /cache/{database}/{id}` clears all cache entries for a single table.

Responses:
- `200 OK` success message
- `401 Unauthorized` missing / invalid API key (when authentication configured)
- `404 Not Found` database or table not found
- `500 Internal Server Error` unexpected error

## Filter Model (POST)
Filter object structure:
```json
{
  "<DIMENSION_CODE>": {
    "type": "code | from | to | first | last",
    "query": ["value1", "value2"]
  }
}
```
Notes:
- `first` / `last` use a single positive integer value in `query`.
- `from` / `to` use one value each.
- `code` can contain multiple codes.

## Configuration
Provided via `appsettings.json`.

Key sections:
- `RootUrl` Base absolute URL used for generated links & OpenAPI servers.
- `DataBases` Array of database definitions:
  - `Type` One of `Mounted`, `FileShare`, `BlobStorage`
  - `Id` Unique id
  - `CacheConfig` Per-database cache sizing overrides
  - `Custom` Backend-specific connection settings
- `Cache` Global memory cache sizing (applies to `MemoryCache`):
  - `MaxSizeBytes` (default 524288000)
  - `DefaultDataCellSize`
  - `DefaultUpdateTaskSize`
  - `DefaultTableGroupSize`
  - `DefaultFileListSize`
  - `DefaultMetaSize`
- `QueryLimits` Request size limits:
  - `JsonMaxCells` (used for any future JSON minimal format endpoints)
  - `JsonStatMaxCells` (enforced in current data endpoints; exceeding returns 413)
- `FeatureManagement` Feature flags (e.g. `CacheController`)
- `Authentication` API key settings (enable / key / header name)
- `OpenApi` Metadata (contact, license) for Swagger document

## Caching
Global cache size limit controlled via `Cache.MaxSizeBytes`. Individual item sizes use defaults above or per-database overrides. Cached entities:
- File lists
- Metadata objects
- Data arrays
- Last updated timestamps (per PX file)
- Groupings (if implemented via `CacheConfig.Groupings`)

## Storage Backends
- Mounted (local / network path) direct file access
- Azure File Share via Azure Storage SDK
- Azure Blob Storage via Azure Storage SDK

## Content Negotiation
Specify desired format with `Accept` header:
- `application/json` -> JSON-stat 2.0
- `text/csv` -> CSV (placeholder)
- `*/*` or empty -> JSON-stat 2.0

## Error Handling
Central exception handling returns standardized 500 responses. Specific endpoints return 400/404/406/413/415 as described.

## Development
1. Configure `appsettings.json` with databases and cache settings.
2. Run the application.
3. Access Swagger UI at root (`/`) for interactive documentation (`openapi/document.json`).


## License
Apache License 2.0. See `docs/LICENSE.md`.