# Services & Utilities

## Services

### AuditLogService

**File**: `PxApi/Services/AuditLogService.cs`

Logs audit events using `IHttpContextAccessor`. Uses:
- A config-driven header whitelist to control which request headers are logged
- A logging scope with `Category=Audit` so NLog can route audit logs to a separate destination
- Structured logging with template strings

### ISearchService / ElasticSearchService

**Files**: `PxApi/Services/ISearchService.cs`, `PxApi/Services/Search/ElasticSearchService.cs`

Abstracts full-text search over Elasticsearch:
- Builds `multi_match` queries across table names, dimension names, and value labels
- Supports optional database-scoped filtering
- Requests highlights from Elasticsearch
- Maps hits into internal `SearchHit` / `SearchResultItem` models
- Controllers then enrich hits with table summaries and HATEOAS links

Registered conditionally in DI — when the `FeatureManagement:SearchController` flag is enabled, the real `ElasticSearchService` is registered together with its `ElasticsearchClient` and `SearchConfig`; otherwise a `DisabledSearchService` stub is registered so the controller can still be resolved (the `FeatureGate` filter will return 404 before any action method runs).

## MVC Filters

### LoggingScopeActionFilter

**File**: `PxApi/Filters/LoggingScopeActionFilter.cs`

Action filter that wraps every controller action in a structured logging scope containing controller name and action name. Applied globally via MVC options in `Program.cs`.

### OperationCanceledExceptionFilter

**File**: `PxApi/Filters/OperationCanceledExceptionFilter.cs`

Exception filter that converts `OperationCanceledException` (client disconnects, request cancellation) into HTTP 499 responses instead of noisy 500 errors. Applied globally.

## Utilities

All utility classes are in `PxApi/Utilities/`.

### ServiceCollectionExtensions

**File**: `PxApi/Utilities/ServiceCollectionExtensions.cs`

Part of the composition root. Registers one keyed `IDataBaseConnector` per configured database ID, selecting the correct connector implementation based on `DataBaseType`. Also registers `IDataBaseConnectorFactory` and `ICachedDataSource`.

### TableSummaryBuilder

**File**: `PxApi/Utilities/TableSummaryBuilder.cs`

Builds compact `TableSummary` DTOs from parsed PX metadata. Used by `TablesController` and `SearchController` to produce consistent table summary representations. Geographical dimensions are included in the `Dimensions` array alongside other classificatory dimensions.

### QueryFilterUtils

**File**: `PxApi/Utilities/QueryFilterUtils.cs`

Parses GET query filter syntax: `dimensionCode:filterType=value`. Supports filter types: `code`, `from`, `to`, `first`, `last`. Handles wildcards (`*`) in code and range filters.

### ContentNegotiation

**File**: `PxApi/Utilities/ContentNegotiation.cs`

Chooses between JSON and CSV output based on the `Accept` header quality values. Returns the selected format or `406 Not Acceptable` if no supported format matches.

### InputSanitizer

**File**: `PxApi/Utilities/InputSanitizer.cs`

Strips unusual/dangerous characters from search query input before it's logged or sent to Elasticsearch. Prevents log injection and search injection.

### BlobReadModeSelector

**File**: `PxApi/Utilities/BlobReadModeSelector.cs`

Decides whether a binary blob request should use sequential streaming or windowed HTTP range reads. The decision is based on:
- Request density (ratio of requested cells to total cells in the blob)
- Configured thresholds from `BlobReadModeConfig`

### LoggerScopeExtensions

**File**: `PxApi/Utilities/LoggerScopeExtensions.cs`

Extension methods for creating standardized structured logging scopes:
- `BeginDbScope(dbId)` — Adds database context
- `BeginDbNotFoundScope()` — Marks database-not-found scenarios (delegates to `BeginDbScope` with a placeholder)
- `BeginFileScope(fileId)` — Adds file context
- `BeginResourceScope(dbId, fileId)` — Adds both database and file context
- `BeginResourceNotFoundScope(dbId?)` — Marks resource-not-found 404 scenarios
- `BeginSearchScope(query, dbId?)` — Adds search query context with sanitized query

### LoggerConsts

**File**: `PxApi/Utilities/LoggerConsts.cs`

Constants for structured logging field names (`SEARCH_QUERY`, etc.).

### HttpConsts

**File**: `PxApi/Utilities/HttpConsts.cs`

Constants for standardized HTTP response headers and content types.

### UriExtensions

**File**: `PxApi/Utilities/UriExtensions.cs`

URL construction helpers for building HATEOAS links from `RootUrl` configuration.

### MatrixMetadataUtilityFunctions

**File**: `PxApi/Utilities/MatrixMetadataUtilityFunctions.cs`

Helper functions for extracting language information from PX metadata matrices.

## OpenAPI Customization

All OpenAPI filters are in `PxApi/OpenApi/`. See [controllers.md](controllers.md) for the full list.

Key patterns:
- **Document filters** modify the generated OpenAPI spec at the document level (servers, security, schemas, examples)
- **Operation filters** modify individual endpoint descriptions (operation IDs, error responses)
- **Schema filters** reshape type schemas to match actual JSON output
- `ApiExplorerConventions` hides internal endpoints (`CacheController`) from the spec
- Controllers use `[ApiExplorerSettings(IgnoreApi = true)]` to hide from spec (`HealthController`, `InfoController`, `ErrorController`)
