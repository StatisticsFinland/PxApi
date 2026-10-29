# Testing

The unit-test project is `PxApi.UnitTests/` and mirrors the production structure.
[`PxApi.IntegrationTests/`](../../PxApi.IntegrationTests) contains the canonical
sample PX database, testing configuration and fixed HTTP regression suite.

## Sample database integration tests

Run `dotnet test PxApi.IntegrationTests/PxApi.IntegrationTests.csproj` from the
repository root. The project is part of the solution and existing CI test runs.

The official `Microsoft.AspNetCore.Mvc.Testing` package hosts the production
`Program.CreateHostBuilder` with an in-process server and shared `HttpClient`.
Tests do not need a port, running API, Azure or Elasticsearch. The factory
replaces application configuration sources before service registration, loads
only testing settings and resolves the fixture directory absolutely. Search,
authentication and telemetry are disabled.

Tests check discovery, metadata, all 89 synthetic data cells, GET/POST filters,
CSV, HEAD/OPTIONS, cache invalidation, error responses and Swagger. Direct
connector tests also verify fixture parsing and required alias files.
Fixtures are nonparallel because `AppSettings.Active` and HTTP caches are shared.
Unit tests run in a separate test process and do not depend on these files.

The canonical fixtures and testing settings are copied only to integration-test
output. They are not API deployment assets. See the
[manual startup instructions](../README.md#optional-manual-api-session) for an
interactive session using the same database.

## Frameworks & Tools

| Package | Purpose |
|---------|---------|
| NUnit | Test framework |
| Moq | Mocking dependencies |
| Microsoft.NET.Test.Sdk | Test runner integration |
| coverlet.collector | code coverage collection |
| NUnit.Analyzers | NUnit code analysis rules |

## Conventions

From `.github/copilot-instructions.md`:

- Use `Assert.That()` syntax for all assertions (not `Assert.AreEqual`, etc.)
- Group multiple assertions with `using (Assert.EnterMultipleScope()) { ... }`
- Use Moq for mocking dependencies
- No XML doc comments required on test classes/methods

## Test Organization

Tests mirror the production folder structure:

```
PxApi.UnitTests/
├── Authentication/          # ApiKeyAuthAttribute tests
├── Caching/                 # Cache layer tests
├── ConfigurationTests/      # Config binding tests
├── ControllerTests/         # Controller unit tests
├── DataSources/             # Connector tests
├── ExceptionTests/          # Custom exception tests
├── Filters/                 # Action/exception filter tests
├── ModelBuilderTests/       # JSON-stat and CSV builder tests
├── Models/                  # Model validation tests
├── OpenApi/                 # OpenAPI filter tests
├── Services/                # AuditLogService, search service tests
├── Utilities/               # Utility class tests
├── UtilitiesTests/          # Additional utility tests
└── Utils/                   # Shared test helpers
```

## Shared Test Helpers

**Folder**: `PxApi.UnitTests/Utils/`

- **`TestConfigFactory`**: Creates `AppSettings` instances for test scenarios, loading from test-specific config or building programmatically.

## Test Patterns

### Controller Tests

Tests build controllers with mocked `ICachedDataSource`, `IDataBaseConnectorFactory`, and other collaborators. They verify:
- HTTP status codes for valid/invalid inputs
- Content negotiation (JSON vs CSV)
- Audit logging calls
- Error handling paths

Example: `PxApi.UnitTests/ControllerTests/DataControllerTests.cs`

### Connector Tests

Use test doubles and synthetic payloads (e.g., in-memory blob data) to validate connector behavior without real Azure connections.

Example: `PxApi.UnitTests/DataSources/BinaryBlobDataBaseConnectorTests.cs`

### OpenAPI Filter Tests

Verify custom Swagger filters produce correct OpenAPI spec modifications.

Example: `PxApi.UnitTests/OpenApi/OperationFilters/OperationIdOperationFilterTests.cs`

### Service Tests

Verify audit log scope/content and search service behavior.

Example: `PxApi.UnitTests/Services/AuditLogServiceTests.cs`

### Authentication Tests

Verify per-controller API key handling, including missing keys, wrong keys, and unconfigured controllers.

Example: `PxApi.UnitTests/Authentication/ApiKeyAuthAttributeTests.cs`

## Running Tests

### Query Logging Coverage

- [Selection tests](../../PxApi.UnitTests/Services/QuerySelectionSummaryTests.cs)
	assert resolved classes, explicit/default intent, equivalent summaries,
	wildcard counts, annual chronology, 1/10/11 code limits, escaping, omission,
	metadata-only totals, concurrent counters and detail preservation.
	All detail modes preserve cell counts and the classification version.
	Captured code lists are checked in exact order.
	Long escaped database codes are retained completely, dimension summaries stop
	at 64, and result-ID lists preserve order up to the 100-ID limit. Completion
	returns stable snapshots without dropping captured JSON based on size.
	Contract tests check unique field/configuration keys and all typed rejection
	reasons against independent literal wire values, including undefined-enum fallback.
	A style guard checks fixed keys and token values for `lower_snake_case`.
	Transport tests render both production NLog JSON layouts, including lowercase
	level values. Layout tests isolate rendering from routing rules; a separate
	transport test keeps production rules intact and verifies enabled audit events
	reach only the audit file, disabled audit events create no audit file, and
	ordinary application events still reach the application file. The sample integration
	suite additionally verifies real file routing, readable Debug request logs
	and disabled audit output. Audit opt-in comparisons are case-insensitive.
	Conditional routing uses NLog filters rather than
	dynamic expressions in the Boolean `enabled` rule attribute.
- [Middleware tests](../../PxApi.UnitTests/Filters/QueryCompletionMiddlewareTests.cs)
	assert exactly-one structured event, method distinction, re-execution,
	failures, untracked endpoints and parallel request isolation.
- [HTTP tests](../../PxApi.UnitTests/Filters/QueryLoggingIntegrationTests.cs)
	use production service registration with mocked data/search services on an
	ephemeral loopback Kestrel port. They exercise JSON/CSV, GET/POST/HEAD/OPTIONS,
	malformed models, rejection, 401, feature-gated 404, 406, 413, 503, cancellation,
	serializer failure and a partially written HTTP 200. They require loopback
	socket access but no storage/search credentials.
	The collector captures application diagnostics at Debug level as well as
	completion events. Real binary-connector/factory failures and a loopback
	Elasticsearch error response verify one exception-log owner, including
	error messages without an attached exception, preserved storage context and
	no storage paths in HTTP responses. Framework logs are excluded from these
	application-level counts. Routine rejections and tracked cancellations are
	checked for absent duplicate messages.
	Diagnostic snapshots include active scopes, checking controller/action
	attribution and request correlation for audit and rejected actions with
	authentication enabled or disabled. Malformed-body cases include null
	filters/queries/code elements and undefined numeric filter types. Empty
	GET/POST selections verify 400 `invalid_filter`, no data fetch and no
	Error-level diagnostic. Table-list probes verify canonical database IDs,
	missing-database reasons and HEAD paging rejections.
	Metadata storage-file failures verify the caught exception is logged once and
	its location is absent from the 404 body. Successful loopback Elasticsearch
	responses exercise real SDK mapping for exact/lower-bound/integer/absent totals;
	malformed responses verify retained transport exceptions and bounded error types.
- [Transport tests](../../PxApi.UnitTests/Services/QueryLoggingTransportTests.cs)
	capture native SDK 3 logging attributes/scopes and NLog JSON locally, checking
	string/number/boolean/JSON fields without sending Azure telemetry. Real Azure
	ingestion, SDK retry delivery and resource retention/access remain rollout gates.
	The Application Insights tests call the same registration helper as `Main`,
	checking enabled/disabled providers, audit default exclusion and opt-in,
	configured thresholds, scopes and sampling options. An in-memory processor
	captures records and a local HTTP handler replaces the exporter transport.
	Real cache-controller success/failure calls also render through the
	production NLog layout and assert unique JSON properties with retained
	database identity and exception details.
- [Cache tests](../../PxApi.UnitTests/Caching/CachedDataBaseConnectorTests.cs)
	use Moq `ICacheObserver` implementations to verify lookup and data outcomes
	without constructing HTTP contexts or query observations. Existing tests also
	exercise construction without an observer.
- [HTTP cache observer tests](../../PxApi.UnitTests/Services/HttpCacheObserverTests.cs)
	verify counter/outcome translation, no-op behavior for absent or untracked
	contexts, and isolation when one adapter observes different requests.
- [Binary connector tests](../../PxApi.UnitTests/DataSources/BinaryBlobDataBaseConnectorTests.cs)
	check missing/corrupt/null metadata and missing data blobs retain diagnostic
	context without connector-level Error logs.
- [Cancellation filter tests](../../PxApi.UnitTests/Filters/OperationCanceledExceptionFilterTests.cs)
	check both cancellation exception types still return 499, with Debug logging
	retained for untracked requests and suppressed for tracked requests.
- Existing controller/cache/audit tests cover unchanged API/cache behavior,
	exact/superset/miss outcomes and control-character injection prevention.

Focused run:

```bash
dotnet test PxApi.UnitTests/PxApi.UnitTests.csproj --filter 'FullyQualifiedName~QueryLogging|FullyQualifiedName~QueryCompletion|FullyQualifiedName~QuerySelection|FullyQualifiedName~HttpCacheObserver|FullyQualifiedName~CachedDataBaseConnector'
```

```bash
dotnet test PxApi.UnitTests/PxApi.UnitTests.csproj
```

With coverage:
```bash
dotnet test PxApi.UnitTests/PxApi.UnitTests.csproj --collect:"XPlat code Coverage"
```
