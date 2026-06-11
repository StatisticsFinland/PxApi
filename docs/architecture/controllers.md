# Controllers & API Endpoints

All controllers are in `PxApi/Controllers/`. There is no separate service layer between controllers and data access — controllers call `ICachedDataSource` directly, then use model builders for response formatting.

## Public Metadata Endpoints (`/meta/`)

### DatabasesController

**File**: `PxApi/Controllers/DatabasesController.cs`
**Route**: `/meta/databases`
**Methods**: GET, HEAD, OPTIONS
**Auth header**: `X-Databases-API-Key`

Returns a localized list of configured databases with table counts, available languages, and HATEOAS links to table listings. Language is validated against `LocalizationConfig.SupportedLanguages`.

### TablesController

**File**: `PxApi/Controllers/TablesController.cs`
**Route**: `/meta/databases/{database}/tables`
**Methods**: GET, HEAD, OPTIONS
**Auth header**: `X-Tables-API-Key`

Returns paginated table listings sorted by PX file name. Validates language, paging parameters (page ≥ 1, pageSize 1–100), and database existence. Builds `TableSummary` objects from cached metadata.

### MetadataController

**File**: `PxApi/Controllers/MetadataController.cs`
**Route**: `/meta/databases/{database}/tables/{table}`
**Methods**: GET, HEAD, OPTIONS
**Auth header**: `X-Metadata-API-Key`

Returns table metadata in JSON-stat 2.0 format (structure only, no data values). Uses `JsonStat2Builder` for output formatting.

### SearchController

**File**: `PxApi/Controllers/SearchController.cs`
**Routes**:
- `/meta/search` — Global search across all databases
- `/meta/databases/{database}/search` — Database-scoped search

**Methods**: GET, HEAD, OPTIONS

Validates and sanitizes the query via `InputSanitizer`, delegates to `ISearchService`, then enriches hits with table summaries and links. Search types: `content`, `dimension`, `value`, `geo`, `all`.

## Data Endpoint (`/data/`)

### DataController

**File**: `PxApi/Controllers/DataController.cs`
**Route**: `/data/databases/{database}/tables/{table}`
**Methods**: GET, POST, HEAD, OPTIONS
**Auth header**: `X-Data-API-Key`

- **GET**: Parses `filters[]` query parameters using `QueryFilterUtils`
- **POST**: Accepts JSON filter dictionary in request body

Both paths validate language and query size limits (`QueryLimitsConfig`), fetch cached metadata/data, and negotiate output format:
- `Accept: application/json` or `*/*` → JSON-stat 2.0 via `JsonStat2Builder`
- `Accept: text/csv` → CSV via `CsvBuilder`
- Data values are precision-adjusted in `GenerateResponse` via `DataPrecisionUtils` based on the selected content dimension value metadata before either formatter is invoked

Content negotiation is handled by `ContentNegotiation` utility class.

**Special responses**:
- `413 Payload Too Large` — Cell count exceeds `JsonStatMaxCells`
- `503 Service Unavailable` — Binary blob not yet synchronized (`BinaryBlobSynchronizationException`)

## Internal / Operational Endpoints

### CacheController

**File**: `PxApi/Controllers/CacheController.cs`
**Route**: `/cache/databases/{database}` and `/cache/databases/{database}/tables/{id}`
**Methods**: DELETE
**Auth header**: `X-Cache-API-Key`
**Feature flag**: `CacheController` must be `true` in `FeatureManagement`

Clears cache entries for a single table or entire database. Hidden from OpenAPI spec via `ApiExplorerConventions`.

### HealthController

**File**: `PxApi/Controllers/HealthController.cs`
**Route**: `/health`
**Methods**: GET
**Hidden from OpenAPI**: Yes (`ApiExplorerSettings(IgnoreApi = true)`)

Checks connectivity to each configured database connector. Returns `{ "status": "healthy" }` or `{ "status": "unhealthy" }`.

### InfoController

**File**: `PxApi/Controllers/InfoController.cs`
**Route**: `/info`
**Methods**: GET
**Hidden from OpenAPI**: Yes

Returns application name and build version from assembly metadata.

### ErrorController

**File**: `PxApi/Controllers/ErrorController.cs`
**Route**: `/error`
**Hidden from OpenAPI**: Yes

Global exception handler endpoint. Maps:
- `InvalidModelException` / JSON parse errors → `400`
- IO failures → `500`
- Other exceptions → generic `500`

## Authentication

Authentication uses an action filter attribute (`ApiKeyAuthAttribute`) applied to controllers — not middleware. Each controller type maps to a separate config section in `AuthenticationConfig` with its own API key and header name. If no key is configured for a controller, requests pass through freely.

See `docs/Authentication.md` for full configuration details.

## Cross-Cutting Filters

- **`LoggingScopeActionFilter`** (`PxApi/Filters/`): Wraps every action in a logging scope with controller and action names.
- **`OperationCanceledExceptionFilter`** (`PxApi/Filters/`): Converts client disconnects / `OperationCanceledException` to HTTP 499.

## OpenAPI Customization

OpenAPI generation uses custom filters in `PxApi/OpenApi/`:

- **Document filters**: Inject server URL from config, strip security schemes, add JSON-stat component schemas, add endpoint examples, remove unwanted schemas, strip bodies from HEAD responses
- **Operation filters**: Add explicit operation IDs, inject `500` response descriptions, add `499` for cancellable actions
- **Schema filters**: Reshape filter and data value schemas to match actual JSON output

The OpenAPI spec is served at `/openapi/document.json` and Swagger UI at `/`.
