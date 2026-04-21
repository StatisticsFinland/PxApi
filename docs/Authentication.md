# API Key Authentication for PxApi

This document describes how to configure and use API Key authentication for PxApi endpoints.

## Overview

API Key authentication provides a simple way to protect endpoints. The authentication system:

- Uses direct API key comparison for each controller
- Is completely optional — if no key is configured for a controller, that controller's endpoints remain publicly accessible
- Supports custom header names per controller
- Supports independent configuration for six controllers: Cache, Databases, Tables, Metadata, Data, and Search

## Configuration

### appsettings.json

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
    },
    "Search": {
      "Key": "your-search-api-key",
      "HeaderName": "X-Search-API-Key"
    }
  }
}
```

### Configuration Properties

Each controller section supports:
- **Key** (optional): The API key value that clients must provide. If not set, authentication is disabled for that controller.
- **HeaderName** (optional): Name of the HTTP header containing the API key. Defaults to the controller-specific default shown below.

### Controller Default Headers

| Controller | Default Header Name |
|---|---|
| Cache | `X-Cache-API-Key` |
| Databases | `X-Databases-API-Key` |
| Tables | `X-Tables-API-Key` |
| Metadata | `X-Metadata-API-Key` |
| Data | `X-Data-API-Key` |
| Search | `X-Search-API-Key` |

### Environment Variables

The recommended approach for production deployments is to use environment variables following the .NET configuration pattern `Authentication__<Controller>__<Property>`:

```bash
# Configure Cache controller authentication
Authentication__Cache__Key=your-cache-api-key
Authentication__Cache__HeaderName=X-Cache-API-Key  # Optional, uses default if omitted

# Configure Data controller authentication
Authentication__Data__Key=your-data-api-key

# Configure Databases controller authentication
Authentication__Databases__Key=your-databases-api-key

# Configure Tables controller authentication
Authentication__Tables__Key=your-tables-api-key

# Configure Metadata controller authentication
Authentication__Metadata__Key=your-metadata-api-key

# Configure Search controller authentication
Authentication__Search__Key=your-search-api-key
```

## Using the API

### Making Authenticated Requests

Include the API key in the controller-specific header:

```bash
# Cache: Clear cache for a specific table
curl -X DELETE "https://yourapi.com/cache/databases/StatFin/tables/table123" \
  -H "X-Cache-API-Key: your-cache-api-key"

# Data: Retrieve data from a table
curl "https://yourapi.com/data/databases/StatFin/tables/table123" \
  -H "X-Data-API-Key: your-data-api-key"

# Databases: List databases
curl "https://yourapi.com/meta/databases" \
  -H "X-Databases-API-Key: your-databases-api-key"
```

### Response Codes

- **200 OK**: Operation completed successfully
- **401 Unauthorized**: Missing or invalid API key

Rest of the response codes as per standard API behavior.

## Disabling Authentication

To disable authentication for a controller, simply omit or remove the `Key` value from its configuration section. The system will automatically allow all requests to that controller to proceed without authentication.

To disable authentication entirely, remove or leave empty all `Key` values across all controller sections.

## Security Notes

- Store API keys securely and never commit them to version control
- Use environment variables or secure configuration management for production deployments
- Rotate API keys periodically
- Use HTTPS in production to protect API keys in transit
- Consider using different API keys for different controllers based on access requirements
- Ensure API keys are sufficiently long and randomly generated for security