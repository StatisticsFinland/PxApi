# Models & Output Builders

## Domain Models

All models are in `PxApi/Models/`.

### Identifier Types

- **`DataBaseRef`** (`DataBaseRef.cs`): Validated database identifier. Constructor rejects invalid characters to prevent path traversal and injection.
- **`PxFileRef`** (`PxFileRef.cs`): Validated PX file identifier. Same validation approach as `DataBaseRef`.

These types are used throughout routing, connectors, and caching for type-safe, validated identifier handling.

### Listing & Summary DTOs

- **`DataBaseListingItem`** (`DataBaseListingItem.cs`): Database metadata for the `/meta/databases` response (name, description, language list, table count, links).
- **`PagedTableList`** (`PagedTableList.cs`): Paginated table listing wrapper with paging metadata.
- **`TableListingItem`** (`TableListingItem.cs`): Minimal table info for listing responses.
- **`TableSummary`** (`TableSummary.cs`): Rich table metadata summary including dimensions, content values, time ranges, geographical dimension name, and links. Built by `TableSummaryBuilder`.
- **`ContentValueInfo`** (`ContentValueInfo.cs`): Information about a content/measure value in a table.
- **`DimensionInfo`** (`DimensionInfo.cs`): Dimension metadata (code, name, type, value count).
- **`TimeRange`** (`TimeRange.cs`): Start/end time period for a time dimension.
- **`Link`** (`Link.cs`): HATEOAS link with rel/href.
- **`TableGroup`** (`TableGroup.cs`): Grouping metadata for organizing tables.

### Operational DTOs

- **`HealthResponse`** (`HealthResponse.cs`): Health check result (`healthy`/`unhealthy`).
- **`InfoResponse`** (`InfoResponse.cs`): Application name and build version.

### JSON-stat 2.0 Models

Located in `PxApi/Models/JsonStat/`:

- **`JsonStat2`** (`JsonStat2.cs`): Root JSON-stat 2.0 response object with class, version, label, source, updated, id, size, role, dimension, value, status, note, and extension properties.
- **`Dimension`** (`Dimension.cs`): Dimension with label and category.
- **`Category`** (`Category.cs`): Category with index, label, unit, and note mappings.
- Unit and grouping extension types for the `extension` property.

### Search Models

Located in `PxApi/Models/Search/`:

- **`SearchResponse`**: Paginated search results wrapper.
- **`SearchHitResponse`**: Search hit enriched with table summary.
- **`SearchHit`**, **`SearchResultItem`**: Raw search hits from Elasticsearch.
- **`SearchQueryInfo`**: Query metadata in responses.
- **`SearchDatabaseRef`**: Database reference in search context.
- **`MatchInfo`**, **`MatchType`**, **`SearchTarget`**: Match detail types.

### Query Filter Models

Located in `PxApi/Models/QueryFilters/`:

- **`MetaFiltering`** (`MetaFiltering.cs`): Applies parsed filters to metadata. Defines default filtering logic when a dimension is omitted from the query:
  - Uses elimination value if present
  - Uses wildcard for time dimensions
  - Otherwise selects first value

## Output Builders

Located in `PxApi/ModelBuilders/`.

### JsonStat2Builder

**File**: `PxApi/ModelBuilders/JsonStat2Builder.cs`

Builds JSON-stat 2.0 responses from parsed PX metadata and data. Handles:
- Dimension roles (time, metric, geo)
- Units and contact information
- Notes at dataset and dimension levels
- Localized groupings
- Missing value status dictionary (maps indices to missing-value codes)
- Metadata-only responses (for the metadata endpoint)

### CsvBuilder

**File**: `PxApi/ModelBuilders/CsvBuilder.cs`

Builds CSV output from matrix data plus metadata:
- Table description as first row header
- Stub dimensions (rows) and heading dimensions (columns) based on PX file metadata
- Filters out single-value elimination/total dimensions for cleaner output
- Maps missing data values to PX-standard dot codes (`.`, `..`, `...`, etc.)
- Culture-invariant number formatting (period as decimal separator)

### PxFileConstants

**File**: `PxApi/ModelBuilders/PxFileConstants.cs`

Centralizes PX metadata keys: `DESCRIPTION`, `TABLEID`, `SOURCE`, `ELIMINATION`, localized missing-value descriptions, and other PX format constants used by both builders.

## Custom Exceptions

Located in `PxApi/Exceptions/`:

- **`InvalidModelException`**: Thrown from the custom invalid-model response factory so model-binding failures route through the global `/error` handler rather than returning default ASP.NET validation responses.
- **`BinaryBlobSynchronizationException`**: Thrown when metadata references a binary blob that hasn't been synchronized yet. `DataController` maps this to HTTP 503.
- **`SearchUnavailableException`**: Wraps Elasticsearch backend failures. `SearchController` maps this to HTTP 503.
