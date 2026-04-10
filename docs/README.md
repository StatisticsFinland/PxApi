# PxApi

PxApi is a .NET 10.0 Web API for accessing PX statistical datasets. It provides table listings, table metadata and data retrieval with flexible dimension filtering and caching across multiple storage backends (local file system, Azure File Share, Azure Blob Storage).

## Implemented Features

- Table listing with paging (`/meta/databases/{database}/tables`)
- Table metadata in JSON-stat 2.0 (`/meta/databases/{database}/tables/{table}`)
- Data retrieval with filter semantics (code, range, positional) via GET query parameters or POST body (`/data/databases/{database}/tables/{table}`)
- Content negotiation (JSON-stat 2.0 or CSV) using the `Accept` header
- Cache management endpoints (database level and single table) (`/cache/databases/{database}` / `/cache/databases/{database}/tables/{id}`)
- Global and per-database caching (file lists, metadata, data, last updated timestamps, grouping metadata)
- Feature flags (Swagger visibility of cache endpoints)
- Controller-specific API key authentication for all endpoints
- Multiple storage types: Mounted (local / network), Azure File Share, Azure Blob Storage
- Query size limits returning HTTP 413 when exceeded
- Metadata search across tables, dimensions, and values (`/meta/search`, `/meta/databases/{database}/search`, `/meta/databases/{database}/tables/{table}/search`)
- Swagger / OpenAPI documentation with custom schema & document filters
- HEAD and OPTIONS support for discoverability and CORS pre-flight

## Endpoints

### Databases
`GET /meta/databases?lang=fi`
Returns a list of available databases with their metadata.

**Authentication**: Requires valid API key in `X-Databases-API-Key` header when databases authentication is enabled.

Query parameters:
- `lang` (optional, default `fi`): Language used for name and description resolution.

Responses:
- `200 OK` JSON array containing database listing items
- `400 Bad Request` requested language not supported
- `401 Unauthorized` missing / invalid API key (when authentication configured)

Additional methods:
- `HEAD /meta/databases` validates existence of the database collection resource
- `OPTIONS /meta/databases` returns Allow header (`GET,HEAD,OPTIONS`)

### Tables
`GET /meta/databases/{database}/tables?lang=fi&page=1&pageSize=50`
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
- `HEAD /meta/databases/{database}/tables` validates existence and paging values
- `OPTIONS /meta/databases/{database}/tables` returns Allow header (`GET,HEAD,OPTIONS`)

### Metadata
`GET /meta/databases/{database}/tables/{table}?lang=fi`
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
- `HEAD /meta/databases/{database}/tables/{table}` existence & language validation only
- `OPTIONS /meta/databases/{database}/tables/{table}` returns Allow header (`GET,HEAD,OPTIONS`)

### Data
`GET /data/databases/{database}/tables/{table}?filters=TIME:from=2020&filters=TIME:to=2024&filters=REGION:code=001,002`

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
`POST /data/databases/{database}/tables/{table}` with JSON body mapping dimension codes to filter objects.
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
- `HEAD /data/databases/{database}/tables/{table}?lang=fi` existence & language validation only
- `OPTIONS /data/databases/{database}/tables/{table}` returns Allow header (`GET,POST,HEAD,OPTIONS`)

### Search
`GET /meta/search?q=population&types=table,dimension,value&lang=fi&page=1&pageSize=20`
Searches across all databases for tables, dimensions, and values.

Query parameters:
- `q` (required): Search query string.
- `types` (optional): Comma-separated result types to include: `table`, `dimension`, `value`. Defaults to all types.
- `lang` (optional, default configured language): Language code (ISO 639-1).
- `page` (optional, >=1, default `1`): Page number.
- `pageSize` (optional, 1-100, default `20`): Items per page.

Responses:
- `200 OK` JSON object with search results and paging info
- `400 Bad Request` missing query, invalid paging, or unsupported language

Database-scoped search:
`GET /meta/databases/{database}/search?q=population&lang=fi`
Searches within a single database.

Additional responses:
- `404 Not Found` database not found

Table-scoped search:
`GET /meta/databases/{database}/tables/{table}/search?q=male&types=dimension,value&lang=fi`
Searches within a specific table for dimensions and values.

Additional responses:
- `404 Not Found` database or table not found

Additional methods:
- `HEAD /meta/search` validates the global search endpoint exists
- `HEAD /meta/databases/{database}/search` validates the database-scoped search endpoint (returns 404 if database not found)
- `HEAD /meta/databases/{database}/tables/{table}/search` validates the table-scoped search endpoint (returns 404 if database or table not found)
- `OPTIONS /meta/search` returns Allow header (`GET,HEAD,OPTIONS`)
- `OPTIONS /meta/databases/{database}/search` returns Allow header (`GET,HEAD,OPTIONS`)
- `OPTIONS /meta/databases/{database}/tables/{table}/search` returns Allow header (`GET,HEAD,OPTIONS`)

### Cache
Requires feature flag `CacheController = true` and valid API key when authentication is enabled.

**Authentication**: Requires valid API key in `X-Cache-API-Key` header when cache authentication is enabled.

- `DELETE /cache/databases/{database}` clears all cache entries (file list, metadata, data, last updated) for a database.
- `DELETE /cache/databases/{database}/tables/{id}` clears all cache entries for a single table.

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
- `ApplicationInsights` Application Insights connection configuration:
  - `ConnectionString` Application Insights connection string (can be overridden by `APPLICATIONINSIGHTS_CONNECTION_STRING` environment variable)
- `Logging` Standard .NET logging configuration for Application Insights log level filtering:
  - `ApplicationInsights:LogLevel:Default` Minimum log level to send to Application Insights (Debug, Information, Warning, Error, Critical). Defaults to Information.
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
- `LogOptions` NLog file logging configuration:
  - `Folder` Directory for log files (empty disables file logging)
  - `SysId` System identifier for log entries
  - `Level` Minimum log level for file logging (Debug, Information, Warning, Error, Critical)
  - `AuditLog` Audit logging settings

### Application Insights Configuration

Application Insights integration is optional and automatically enabled when a connection string is provided. Log level filtering uses the standard .NET `Logging` configuration section, keeping it separate from file logging controlled by `LogOptions` and NLog.

```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=your-key;IngestionEndpoint=https://..."
  },
  "Logging": {
    "ApplicationInsights": {
      "LogLevel": {
        "Default": "Information"
      }
    }
  }
}
```

**Connection String Sources (in order of priority):**
1. `APPLICATIONINSIGHTS_CONNECTION_STRING` environment variable
2. `ApplicationInsights:ConnectionString` in appsettings.json

**Settings:**
- **ApplicationInsights:ConnectionString**: Application Insights connection string. If empty or missing, Application Insights is disabled.
- **Logging:ApplicationInsights:LogLevel:Default**: Minimum log level to send to Application Insights (Debug, Information, Warning, Error, Critical). Defaults to Information. You can also set per-category log levels (e.g., `"Microsoft": "Warning"`).

**Environment Variable Configuration:**
```bash
# Enable Application Insights via environment variable
APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=your-key;IngestionEndpoint=https://..."

# Override log level via environment variable
Logging__ApplicationInsights__LogLevel__Default=Debug
```

**Logging Architecture:**
- **File Logs**: Controlled by NLog configuration and `LogOptions` section
- **Application Insights**: Connection controlled by `ApplicationInsights` section, log level controlled by `Logging:ApplicationInsights:LogLevel` section
- Both systems operate independently - you can have file logging without Application Insights, Application Insights without file logging, or both together

## Authentication

PxApi supports controller-specific API key authentication. Each controller can be independently configured with its own API key and header name. Authentication is optional and disabled by default.

### Configuration Structure

```json
{
  "Authentication": {
    "Cache": {
      "Key": "your-cache-api-key",
      "HeaderName": "X-Cache-API-Key"
    },
    "Databases": {
      "Key": "your-databases-api-key",
      "HeaderName": "X-Databases-API-Key"
    },
 "Tables": {
      "Key": "your-tables-api-key",
      "HeaderName": "X-Tables-API-Key"
    },
    "Metadata": {
      "Key": "your-metadata-api-key",
      "HeaderName": "X-Metadata-API-Key"
    },
    "Data": {
      "Key": "your-data-api-key",
      "HeaderName": "X-Data-API-Key"
    }
  }
}
```

### Authentication Rules

- Authentication is **optional** - if no key is provided for a controller, that controller's endpoints will not require authentication
- Each controller can be independently configured
- When configured, clients must provide the correct API key in the specified header
- API keys are compared directly with the configured values
- Custom header names can be configured for each controller (defaults shown above)
- Environment variables can override configuration values using the pattern: `Authentication__<Controller>__<Property>` (e.g., `Authentication__Data__Key`)

### Controller Default Headers

- **Cache**: `X-Cache-API-Key`
- **Databases**: `X-Databases-API-Key`
- **Tables**: `X-Tables-API-Key`
- **Metadata**: `X-Metadata-API-Key`
- **Data**: `X-Data-API-Key`

### Environment Variable Configuration

You can configure authentication via environment variables:

```
# Example: Configure Data controller authentication
Authentication__Data__Key=your-data-api-key
Authentication__Data__HeaderName=X-Custom-Data-Key

# Example: Configure Databases controller authentication
Authentication__Databases__Key=your-databases-api-key
```

### Security Notes

- Store API keys securely and never commit them to version control
- Use environment variables or secure configuration management for production deployments
- Rotate API keys periodically
- Use HTTPS in production to protect API keys in transit
- Consider using different API keys for different controllers based on access requirements
- Ensure API keys are sufficiently long and randomly generated for security

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