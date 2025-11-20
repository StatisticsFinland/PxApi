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
- Controller-specific API key authentication for all endpoints
- Multiple storage types: Mounted (local / network), Azure File Share, Azure Blob Storage
- Query size limits returning HTTP 413 when exceeded
- Swagger / OpenAPI documentation with custom schema & document filters
- HEAD and OPTIONS support for discoverability and CORS pre-flight

## Endpoints

### Databases
`GET /databases?lang=fi`
Returns a list of available databases with their metadata.

**Authentication**: Requires valid API key in `X-Databases-API-Key` header when databases authentication is enabled.

Query parameters:
- `lang` (optional, default `fi`): Language used for name and description resolution.

Responses:
- `200 OK` JSON array containing database listing items
- `400 Bad Request` requested language not supported
- `401 Unauthorized` missing / invalid API key (when authentication configured)

Additional methods:
- `HEAD /databases` validates existence of the database collection resource
- `OPTIONS /databases` returns Allow header (`GET,HEAD,OPTIONS`)

### Tables
`GET /tables/{database}?lang=fi&page=1&pageSize=50`
Returns a paged list of tables ordered by PX file name.

**Authentication**: Requires valid API key in `X-Tables-API-Key` header when tables authentication is enabled.

Query parameters:
- `lang` (optional, default `fi`): Language used for metadata resolution.
- `page` (optional, >=1, default `1`)
- `pageSize` (optional, 1-100, default `50`)

Responses:
- `200 OK` JSON body containing table listing and paging info
- `400 Bad Request` invalid paging values or unsupported language
- `401 Unauthorized` missing / invalid API key (when authentication configured)
- `404 Not Found` database missing

Additional methods:
- `HEAD /tables/{database}` validates existence and paging values
- `OPTIONS /tables/{database}` returns Allow header (`GET,HEAD,OPTIONS`)

### Metadata
`GET /meta/{database}/{table}?lang=fi`
Returns JSON-stat 2.0 metadata (structure only, no data filtering).

**Authentication**: Requires valid API key in `X-Metadata-API-Key` header when metadata authentication is enabled.

Query parameters:
- `lang` (optional): If omitted uses table default language

Responses:
- `200 OK` JSON-stat 2.0 metadata
- `400 Bad Request` language not available
- `401 Unauthorized` missing / invalid API key (when authentication configured)
- `404 Not Found` database or table missing
- `500 Internal Server Error` unexpected error

Additional methods:
- `HEAD /meta/{database}/{table}` existence & language validation only
- `OPTIONS /meta/{database}/{table}` returns Allow header (`GET,HEAD,OPTIONS`)

### Data
`GET /data/{database}/{table}?filters=TIME:from=2020&filters=TIME:to=2024&filters=REGION:code=001,002`

Retrieves data values applying filters to dimensions. Content negotiation support for json and csv:

**Authentication**: Requires valid API key in `X-Data-API-Key` header when data authentication is enabled.

- `Accept: application/json` or `*/*` -> JSON-stat 2.0
- `Accept: text/csv` -> CSV format with containing table description, selected value names and data.

####CSV Export Structure:####
- Table description as A1 cell header
- Stub dimensions (rows) and heading dimensions (columns) based on PX file metadata
- Automatic filtering of single-value elimination/total dimensions for cleaner output
- Formatting of missing values using PX-standard dot codes (`.`, `..`, `...`, etc.)
- Culture-invariant number formatting with period as decimal separator

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
- `401 Unauthorized` missing / invalid API key (when authentication configured)
- `404 Not Found` database or table missing
- `406 Not Acceptable` unsupported `Accept` header value
- `413 Payload Too Large` request cell count exceeds configured limit
- `415 Unsupported Media Type` (POST invalid content type)

Additional methods:
- `HEAD /data/{database}/{table}?lang=fi` existence & language validation only
- `OPTIONS /data/{database}/{table}` returns Allow header (`GET,POST,HEAD,OPTIONS`)

### Cache
Requires feature flag `CacheController = true` and valid API key when authentication is enabled.

**Authentication**: Requires valid API key in `X-Cache-API-Key` header when cache authentication is enabled.

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
- `Authentication` Controller-specific API key settings - see Authentication section below
- `OpenApi` Metadata (contact, license) for Swagger document

## Authentication

PxApi supports controller-specific API key authentication. Each controller can be independently configured with its own API key and header name. Authentication is optional and disabled by default.

### Configuration Structure

```json
{
  "Authentication": {
    "Cache": {
      "Hash": "base64-encoded-hash-of-api-key",
      "Salt": "base64-encoded-salt",
      "HeaderName": "X-Cache-API-Key"
    },
    "Databases": {
      "Hash": "base64-encoded-hash-of-api-key",
      "Salt": "base64-encoded-salt", 
      "HeaderName": "X-Databases-API-Key"
    },
    "Tables": {
      "Hash": "base64-encoded-hash-of-api-key",
      "Salt": "base64-encoded-salt",
      "HeaderName": "X-Tables-API-Key"
    },
    "Metadata": {
    "Hash": "base64-encoded-hash-of-api-key",
    "Salt": "base64-encoded-salt",
      "HeaderName": "X-Metadata-API-Key"
    },
    "Data": {
      "Hash": "base64-encoded-hash-of-api-key",
      "Salt": "base64-encoded-salt",
      "HeaderName": "X-Data-API-Key"
    }
  }
}
```

### Authentication Rules

- Authentication is **optional** - if no hash and salt are provided for a controller, that controller's endpoints will not require authentication
- Each controller can be independently configured
- When configured, clients must provide the correct API key in the specified header
- API keys are hashed using SHA256 with a salt for secure storage
- Custom header names can be configured for each controller (defaults shown above)
- Environment variables can override configuration values using the pattern: `Authentication__<Controller>__<Property>` (e.g., `Authentication__Data__Hash`)

### Controller Default Headers

- **Cache**: `X-Cache-API-Key`
- **Databases**: `X-Databases-API-Key` 
- **Tables**: `X-Tables-API-Key`
- **Metadata**: `X-Metadata-API-Key`
- **Data**: `X-Data-API-Key`

### Environment Variable Configuration

You can configure authentication via environment variables:

```bash
# Example: Configure Data controller authentication
Authentication__Data__Hash=your-base64-hash
Authentication__Data__Salt=your-base64-salt
Authentication__Data__HeaderName=X-Custom-Data-Key

# Example: Configure Databases controller authentication
Authentication__Databases__Hash=your-base64-hash
Authentication__Databases__Salt=your-base64-salt
```

### Security Notes

- API keys should be stored as SHA256 hashes, never as plain text
- Use a cryptographically secure salt for each installation
- Rotate API keys periodically
- Use HTTPS in production to protect API keys in transit
- Consider using different API keys for different controllers based on access requirements

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
- `text/csv` -> Table description, selected value names and data in CSV format
- `*/*` or empty -> JSON-stat 2.0

## Error Handling
Central exception handling returns standardized 500 responses and 400 responses for invalid requests. Specific endpoints return 400/404/406/413/415 as described.

## Development
1. Configure `appsettings.json` with databases, cache settings, and optionally authentication.
2. Run the application.
3. Access Swagger UI at root (`/`) for interactive documentation (`openapi/document.json`).

## License
Apache License 2.0. See `docs/LICENSE.md`.