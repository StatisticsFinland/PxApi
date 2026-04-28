# Architecture Overview

PxApi is an ASP.NET Core Web API (.NET 10.0) for accessing PX statistical datasets. It provides HTTP endpoints for database/table discovery, metadata retrieval, data querying with dimension filtering, and full-text search. The domain-level PX file parsing is delegated to the external `Px.Utils` NuGet package; this application adds HTTP routing, configuration, caching, OpenAPI documentation, and storage connector abstractions around it.

## Solution Structure

```
PxApi.sln
├── PxApi/                    # Production API project
│   ├── Program.cs            # Entry point, DI, middleware pipeline
│   ├── Controllers/          # API endpoints
│   ├── DataSources/          # Storage connector abstractions & implementations
│   ├── Caching/              # In-memory cache layer over connectors
│   ├── Configuration/        # Typed config classes bound from appsettings.json
│   ├── Models/               # Request/response DTOs, domain value objects
│   ├── ModelBuilders/        # JSON-stat 2.0 and CSV output builders
│   ├── Services/             # Audit logging, search service
│   ├── Authentication/       # API key action filter
│   ├── Filters/              # MVC action/exception filters
│   ├── OpenApi/              # Swagger/OpenAPI customization filters
│   ├── Utilities/            # Shared helpers and extensions
│   └── Exceptions/           # Custom exception types
├── PxApi.UnitTests/          # NUnit test project mirroring production structure
└── docs/                     # Documentation
```

## Key External Dependencies

| Package | Purpose |
|---------|---------|
| `Px.Utils` | PX file parsing, metadata/data model building |
| `Azure.Storage.Blobs` | Azure Blob Storage connector |
| `Azure.Storage.Files.Shares` | Azure File Share connector |
| `Azure.Identity` | Azure credential management |
| `Elastic.Clients.Elasticsearch` | Full-text search backend |
| `Swashbuckle.AspNetCore` | Swagger UI and OpenAPI generation |
| `NLog.Web.AspNetCore` | Structured file logging |
| `Microsoft.ApplicationInsights.AspNetCore` | Azure Application Insights telemetry |
| `Microsoft.FeatureManagement.AspNetCore` | Feature flags |
| `Ude.NetStandard` | Character encoding detection for PX files |

## Application Startup (Program.cs)

There is no `Startup.cs`. Everything is in `Program.cs`:

1. **Configuration**: `AppSettings.Load()` binds `appsettings.json` into typed config.
2. **Logging**: Default providers cleared, NLog configured. Application Insights added if connection string exists.
3. **Services**: Feature management, MVC controllers, JSON serialization, Swagger, memory cache, keyed database connectors, connector factory, `HttpContextAccessor`, audit logging, and optional Elasticsearch search.
4. **Pipeline**: Swagger JSON at `/openapi/document.json`, Swagger UI at `/`, exception handling via `/error`, HTTPS redirection, authorization, controller mapping.

## Request Flow

```
HTTP Request
  → Swagger/HTTPS middleware
  → Authorization (ApiKeyAuth filter if configured)
  → LoggingScopeActionFilter (adds controller/action to log scope)
  → Controller action
    → ICachedDataSource (cache layer)
      → IDataBaseConnector (storage connector)
        → Px.Utils (PX file parsing)
    → ModelBuilder (JSON-stat 2.0 / CSV)
  → Response
  → OperationCanceledExceptionFilter (client disconnect → 499)
  → ErrorController (unhandled exceptions → 500)
```

## API Surface

The API is organized into three route prefixes:

- **`/meta/`** — Discovery: databases, tables, metadata, search
- **`/data/`** — Data retrieval with dimension filtering
- **`/cache/`** — Internal cache management (behind feature flag)

Plus operational endpoints: `/health`, `/info`, `/error`

See [controllers.md](controllers.md) for detailed endpoint documentation.

## Architecture Documents

- [controllers.md](controllers.md) — API endpoints, routes, and controller details
- [data-access.md](data-access.md) — Storage connectors and data access patterns
- [caching.md](caching.md) — Cache architecture and invalidation
- [configuration.md](configuration.md) — Configuration structure and settings
- [models-and-builders.md](models-and-builders.md) — Domain models and output builders
- [services-and-utilities.md](services-and-utilities.md) — Services, filters, and utility classes
- [testing.md](testing.md) — Test project structure and conventions
