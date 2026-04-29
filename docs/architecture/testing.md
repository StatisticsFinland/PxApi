# Testing

The test project is `PxApi.UnitTests/` and mirrors the production project structure.

## Frameworks & Tools

| Package | Purpose |
|---------|---------|
| NUnit | Test framework |
| Moq | Mocking dependencies |
| Microsoft.NET.Test.Sdk | Test runner integration |
| coverlet.collector | Code coverage collection |
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

```bash
dotnet test PxApi.UnitTests/PxApi.UnitTests.csproj
```

With coverage:
```bash
dotnet test PxApi.UnitTests/PxApi.UnitTests.csproj --collect:"XPlat Code Coverage"
```
