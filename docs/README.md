# PxApi

PxApi is a .NET 9.0 web API designed to provide metadata and data access for Px files stored in various storage types, including local file systems, Azure File Shares, and Azure Blob Storage. It is built with extensibility and performance in mind, leveraging caching mechanisms.

## Features

- **Table Metadata**: Fetch detailed metadata about Px tables, including content, time dimensions, and classificatory dimensions.
- **Dimension Metadata**: Retrieve metadata for specific dimensions, including content and time dimensions.
- **Table Listing**: List available tables in a database with essential metadata and paging support.
- **Data Retrieval**: Query px table data with filtering options, supporting both minimal JSON and JSON-stat2 response formats.
- **Advanced Caching**: Comprehensive caching system with configurable settings for metadata, data and file lists.
- **Multiple Storage Types**: Support for local file system, Azure File Share, and Azure Blob Storage as data sources.
- **Swagger Integration**: Includes Swagger UI for API documentation and testing.

## API Endpoints

1. **Tables Endpoint** (`/tables`):
   - List tables in a database with paging and metadata.
   - Example: `/tables/{databaseId}?lang=en&page=1&pageSize=50`

2. **Metadata Endpoint** (`/meta`):
   - Retrieve metadata for a specific table.
   - Example: `/meta/json/{database}/{table}?lang=en&showValues=true`
   - Retrieve metadata for a specific dimension in a table.
   - Example: `/meta/json/{database}/{table}/{dimcode}?lang=en`

3. **Data Endpoint** (`/data`):
   - GET and POST endpoints to retrieve data in minimal JSON format.
   - Examples: 
     - GET: `/data/json/{database}/{table}?dimension1:filter=value1,value2`
     - POST: `/data/json/{database}/{table}` with filter body
   - GET and POST endpoints to retrieve data in JSON-stat2 format.
   - Examples:
     - GET: `/data/json-stat/{database}/{table}?dimension1:filter=value1,value2&lang=en`
     - POST: `/data/json-stat/{database}/{table}?lang=en` with filter body

4. **Cache Endpoints** (`/cache`):
   - **Clear all cache for a database**:
     - Deletes all cache entries (file list, metadata, data, last updated) for a specific database.
     - Endpoint: `DELETE /cache/{database}`
     - Example: `DELETE /cache/testdb`
   - **Clear cache for a specific table**:
     - Deletes all cache entries (metadata, data, last updated) for a specific table in a database.
     - Endpoint: `DELETE /cache/{database}/{id}`
     - Example: `DELETE /cache/testdb/table1`
   - **Authentication**: Cache endpoints support optional API key authentication. See [Authentication.md](docs/Authentication.md) for configuration details.

## Configuration

The application uses `appsettings.json` for configuration. Key settings include:

- `RootUrl`: The base URL for the API.
- `Cache`: Configuration for global cache management:
  - `MaxSizeBytes`: Maximum size of the global memory cache in bytes (default: 512 MB)
  - `DefaultDataCellSize`: Default size for data cell cache items in bytes (default: 16)
  - `DefaultUpdateTaskSize`: Default size for update task cache items in bytes (default: 50)
  - `DefaultTableGroupSize`: Default size for table group cache items in bytes (default: 100)
  - `DefaultFileListSize`: Default size for file list cache items in bytes (default: 350000)
  - `DefaultMetaSize`: Default size for metadata cache items in bytes (default: 200000)
- `FeatureManagement`: Configuration for feature flags that control endpoint availability:
  - `CacheController`: Boolean flag to enable/disable cache management endpoints (default: `true`)
- `DataBases`: Array of database configurations with the following properties:
  - `Type`: Type of database storage (`Mounted`, `FileShare`, or `BlobStorage`).
  - `Id`: Unique identifier for the database.
  - `CacheConfig`: Configuration for database caching:
    - `TableList`: Cache settings for table lists.
    - `Meta`: Cache settings for metadata.
    - `Data`: Cache settings for data.
    - `Modifiedtime`: Cache settings for file modification times.
    - `Groupings`: Cache settings for grouping metadata.
  - `Custom`: Custom settings specific to the database type:
    - For `Mounted`: `RootPath` to the local file system.
    - For `FileShare`: Connection parameters for Azure File Share.
    - For `BlobStorage`: Connection parameters for Azure Blob Storage.

### Cache Configuration

PxApi uses a global cache size management through the `Cache` configuration section:

```json
{
  "Cache": {
    "MaxSizeBytes": 524288000,
    "DefaultDataCellSize": 16,
    "DefaultUpdateTaskSize": 50,
    "DefaultTableGroupSize": 100,
    "DefaultFileListSize": 350000,
    "DefaultMetaSize": 200000
  }
}
```

#### Cache Configuration Options

- **`MaxSizeBytes`**: Controls the maximum memory usage of the global application cache used by the MemoryCache service for caching across the entire application. Default: 512 MB (524288000 bytes).

- **Default Cache Item Sizes**: These settings control the estimated memory footprint of different types of cached items for cache eviction calculations:
  - **`DefaultDataCellSize`**: Size estimate for individual data cells in cached data sets. Default: 16 bytes.
  - **`DefaultUpdateTaskSize`**: Size estimate for cached file update timestamp tasks. Default: 50 bytes.
  - **`DefaultTableGroupSize`**: Size estimate for cached table grouping metadata. Default: 100 bytes.
  - **`DefaultFileListSize`**: Size estimate for cached file list entries. Default: 350000 bytes.
  - **`DefaultMetaSize`**: Size estimate for cached table metadata containers. Default: 200000 bytes.

These size estimates are used by the .NET MemoryCache for making eviction decisions when the cache approaches its size limit. Adjusting these values allows fine-tuning of cache behavior based on the actual memory footprint of your data.

If not specified, all cache settings use their respective default values.

## Database Connectors

PxApi supports multiple types of database storage through specialized connectors:

### MountedDataBaseConnector

For accessing Px files stored on a local or network file system. Provides direct file access with minimal overhead.

### FileShareDataBaseConnector

For accessing Px files stored in Azure File Shares. Uses Azure Storage SDK.

### BlobStorageDataBaseConnector

For accessing Px files stored in Azure Blob Storage. Uses Azure Storage SDK.

## Technologies Used

- **.NET 9.0**: Modern framework for building web APIs.
- **Azure SDK**: Libraries for Azure File Share and Blob Storage access.
- **NLog**: Logging framework for error and debug logging.
- **Swagger/OpenAPI**: API documentation and testing.
- **Px.Utils**: Utility library for handling Px file metadata.
- **Memory Cache**: Built-in .NET caching for optimized performance.

## Getting Started

1. Clone the repository.
2. Configure the `appsettings.json` file with the appropriate settings for your environment.
3. Build and run the project using Visual Studio or the .NET CLI.
4. Access the Swagger UI to explore the API.

## License

This project is licensed under the Apache License 2.0. See the [LICENSE.md](docs/LICENSE.md) file for details.