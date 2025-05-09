# PxApi

PxApi is a .NET 9.0 web API designed to provide metadata and data access for Px files stored in a local file system. It is built with extensibility and performance in mind, leveraging caching.

## Features

- **Table Metadata**: Fetch detailed metadata about Px tables, including content, time variables, and classificatory variables.
- **Variable Metadata**: Retrieve metadata for specific variables, including content and time variables.
- **Table Listing**: List available tables in a database with essential metadata and paging support.
- **Swagger Integration**: Includes Swagger UI for API documentation and testing.

## API Endpoints

1. **Tables Endpoint** (`/tables`):
   - List tables in a database with paging and metadata.
   - Example: `/tables/{databaseId}?lang=en&page=1&pageSize=50`

2. **Metadata Endpoint** (`/meta`):
   - Retrieve metadata for a specific table.
   - Example: `/meta/{database}/{table}?lang=en&showValues=true`
   - Retrieve metadata for a specific variable in a table.
   - Example: `/meta/{database}/{table}/{varcode}?lang=en`

## Configuration

The application uses `appsettings.json` for configuration. Key settings include:
- `RootUrl`: The base URL for the API.
- `DataSource`: Configuration for the local file system data source, including root path and caching options.

## Technologies Used

- **.NET 9.0**: Modern framework for building web APIs.
- **NLog**: Logging framework for error and debug logging.
- **Swagger**: API documentation and testing.
- **Px.Utils**: Utility library for handling Px file metadata.

## Getting Started

1. Clone the repository.
2. Configure the `appsettings.json` file with the appropriate settings for your environment.
3. Build and run the project using Visual Studio or the .NET CLI.
4. Access the Swagger UI to explore the API.

## License

This project is licensed under the Apache License 2.0. See the [LICENSE.md](docs/LICENSE.md) file for details.