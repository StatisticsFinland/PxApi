# Services & Utilities

## Services

### Query completion

[QueryObservation](../../PxApi/Services/QueryObservation.cs) holds provider-neutral
business observations on an HTTP feature. Controllers populate business fields;
cache decisions arrive through `ICacheObserver` and its HTTP-aware adapter.
Fields are fixed scalars; arrays are serialized JSON strings. The feature is
isolated per incoming request, including concurrent search enrichment.

`LoggerConsts.Query` in [LoggerConsts](../../PxApi/Utilities/LoggerConsts.cs)
contains the internal contract vocabulary alongside the existing diagnostic
scope constants. Named constants define event and dimension JSON keys, while enums define
rejection reasons, operations, outcomes, cache decisions, detail modes and
discovery scopes. Observations convert enum values to the existing lowercase
underscore-separated strings at the logging boundary. Rejection and cache
lookup APIs accept only their respective enums; do not introduce new string
literals at call sites. Application-defined field names and fixed token values
use `lower_snake_case`, including diagnostic scopes, audit fields, dimension
JSON and NLog envelopes. The completion event is `query_completed`.
Diagnostic and query database identifiers share the `database_id` constant;
`px_file` and `table_id` remain distinct because they describe different resources.
HTTP verbs, MIME types, language tags, runtime method/type names, configured
audit-header names and user/database identifiers retain their original values.
The reserved logging key `{OriginalFormat}` and configuration paths are unchanged.
Fixed log keys use explicit string values so renaming a C# constant does not
change the logging contract. Use `nameof` at a call site when recording an
actual method or property name.

[ICacheObserver](../../PxApi/Caching/ICacheObserver.cs) is the cache-specific
notification contract for file-list/metadata hits and misses and exact/superset
data hits or data misses. `CachedDataSource` depends only on that optional
contract, not HTTP or the completion-event state.
[HttpCacheObserver](../../PxApi/Services/HttpCacheObserver.cs) translates those
decisions into the existing completion fields. It is registered as a stateless
singleton alongside the scoped data source and resolves `IHttpContextAccessor`
on every notification. Without a tracked HTTP feature it is a no-op; it never
creates request state or completion events. Scopes still correlate diagnostic
entries; the observation accumulates the single request summary.

[QueryCompletionMiddleware](../../PxApi/Filters/QueryCompletionMiddleware.cs)
runs after routing and outside exception handling. It emits exactly one
Information-level `query_completed` (`schema_version=1`) after awaited response
execution for matched data, metadata, tables and search endpoints. Rejections,
authentication and feature gating are included. HEAD/OPTIONS are distinguishable
by method; unmatched and infrastructure routes are excluded. IDs are generated
locally; Activity trace/span IDs are captured when available. Controller success
messages are no longer usage events. Scopes carry diagnostic correlation only.

[QuerySelectionSummary](../../PxApi/Services/QuerySelectionSummary.cs) uses the
already-resolved map for requested/table counts, selection classes and filter
intent. It never reapplies filters or enumerates the Cartesian
product. Selected code details are opt-in, complete, bounded lists, with explicit
omission reasons. Chronological latest is only known after a full annual time
selection validates ordering; other/unvalidated ordering is left unknown.

`QueryResponseExecutionFilter` distinguishes a failing original MVC result from
execution of the error response. Prepared cells are published as `returned_cells`
only after successful non-HEAD response execution. A committed HTTP 200 with a
failed write has `outcome=failed`; completion never proves client receipt.

### Diagnostic ownership

`query_completed` describes the request outcome; exception diagnostics describe
its cause. Routine missing-database/table/language messages are not emitted
again by `DataController`. Exception-bearing filter diagnostics remain useful.

Ordinary connector failures are thrown with storage context, not logged and
then thrown. The handling controller owns caught failure diagnostics; otherwise
`ErrorController` owns the exception diagnostic. Middleware reports escaped
failures only when the error handler did not already report them, including
response failures after headers have been committed. `ExceptionReported`
coordinates the error handler and middleware, not the storage layer.

Binary synchronization failures retain an optional `BlobPath` on the exception
and are reported once at Information level by the data controller before its
unchanged 503 response. Metadata deserialization failures retain their cause
and blob/database/container context. Internal storage locations remain in
diagnostics, not HTTP error bodies. Background callers must own diagnostics for
exceptions they handle; connectors do not assume an HTTP request is present.
Metadata GET owns an Information diagnostic when it catches a missing storage
file, retaining the exception and location without changing the public 404 body.

Security/path warnings and operational details such as streaming/windowed read
selection and multiple-metadata-file recovery are separate diagnostics and stay.
Audit records are also independent of request completion. Framework and SDK
diagnostics can be generated independently; the single-owner rule concerns
application exception reporting, not a promise of one total log record.

See [the event contract and KQL](query-logging-action-plan.md) and
[privacy configuration](configuration.md#query-logging). Selection ratios can be
derived from cell counts during analysis. Client IDs and protected search
fingerprints are not inferred or emitted without an approved source.

### AuditLogService

**File**: `PxApi/Services/AuditLogService.cs`

Logs audit events using `IHttpContextAccessor`. Uses:
- A config-driven header whitelist to control which request headers are logged
- A logging scope with `category=audit` so NLog can route audit logs to a separate destination
- Structured logging with template strings

Header selection is maintainer-controlled. Header and identity control characters
are neutralized against log injection; ordinary values and JSON delimiters are
preserved as structured properties. This is not content-based redaction.
Application Insights audit export is an explicit opt-in, independent of NLog
audit-file isolation.

### ISearchService / ElasticSearchService

**Files**: `PxApi/Services/ISearchService.cs`, `PxApi/Services/Search/ElasticSearchService.cs`

Abstracts full-text search over Elasticsearch:
- Builds `multi_match` queries across table names, dimension names, and value labels
- Supports optional database-scoped filtering
- Requests highlights from Elasticsearch
- Maps hits into internal `SearchHit` / `SearchResultItem` models
- Controllers then enrich hits with table summaries and HATEOAS links
- Backend exception translation does not log a second exception; the controller
	owns the exception diagnostic. The generic invalid-response Debug message and
	unused service logger dependency are removed. Raw Elasticsearch debug payloads
	are not logged.
- Invalid backend responses retain the HTTP status, a sanitized error type of
	at most 128 characters, and the SDK transport exception when available on
	`SearchUnavailableException`. The controller logs `BackendStatusCode` and
	`BackendErrorType`; backend reason text and response bodies are not copied.
- Completion separates observed totals and their exact/lower-bound/unknown
	relation from enriched returned-page counts. Result exposures are not clicks.

Registered conditionally in DI — when the `FeatureManagement:SearchController` flag is enabled, the real `ElasticSearchService` is registered together with its `ElasticsearchClient` and `SearchConfig`; otherwise a `DisabledSearchService` stub is registered so the controller can still be resolved (the `FeatureGate` filter will return 404 before any action method runs).

## MVC Filters

### LoggingScopeActionFilter

**File**: `PxApi/Filters/LoggingScopeActionFilter.cs`

Action filter that wraps every controller action in a structured logging scope containing controller name and action name. Applied globally via MVC options in `Program.cs`.

### OperationCanceledExceptionFilter

**File**: `PxApi/Filters/OperationCanceledExceptionFilter.cs`

Exception filter that converts `OperationCanceledException` (client disconnects, request cancellation) into HTTP 499 responses instead of noisy 500 errors. Applied globally.
Tracked requests rely on completion for the cancellation outcome and do not emit
the additional generic Debug message. Untracked requests retain that diagnostic.
Cancellation is identified as a client disconnect only when the request's
aborted token confirms it.

## Utilities

all utility classes are in `PxApi/Utilities/`.

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

Strips disallowed characters from search text before it is sent to Elasticsearch.
Sanitization is not anonymization. Search text logging is separately opt-in and
bounded; controllers never include the text in diagnostic scopes.

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
- `BeginSearchScope(query, dbId?)` — Legacy search-query scope helper, not used by
	search controllers because diagnostic scopes must respect text-retention policy

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

all OpenAPI filters are in `PxApi/OpenApi/`. See [controllers.md](controllers.md) for the full list.

Key patterns:
- **Document filters** modify the generated OpenAPI spec at the document level (servers, security, schemas, examples)
- **operation filters** modify individual endpoint descriptions (operation IDs, error responses)
- **Schema filters** reshape type schemas to match actual JSON output
- `ApiExplorerConventions` hides internal endpoints (`CacheController`) from the spec
- Controllers use `[ApiExplorerSettings(IgnoreApi = true)]` to hide from spec (`HealthController`, `InfoController`, `ErrorController`)
