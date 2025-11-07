# API Key Authentication for PxApi

This document describes how to configure and use API Key authentication for the PxApi cache management endpoints.

## Overview

API Key authentication provides a simple way to protect sensitive endpoints. The authentication system:

- Uses SHA256 hashing with salt for secure key storage
- Is completely optional - if not configured, endpoints remain publicly accessible
- Supports custom header names for flexible future development
- Is designed to support multiple different API key configurations for different controllers in the future

## Configuration

### Environment Variables

The recommended approach for production deployments is to use environment variables:

```bash
# Enable API key authentication for cache endpoints
Authentication__Cache__Hash=your-base64-encoded-hash-here
Authentication__Cache__Salt=your-base64-encoded-salt-here
Authentication__Cache__HeaderName=X-Cache-API-Key # Optional, defaults to X-Cache-API-Key
```

### appsettings.json

For development or when environment variables are not preferred:

```json
{
  "Authentication": {
    "Cache": {
      "Hash": "your-base64-encoded-hash-here",
      "Salt": "your-base64-encoded-salt-here",
      "HeaderName": "X-Cache-API-Key"
    }
  }
}
```

### Configuration Properties

For cache endpoints:
- **Authentication.Cache.Hash** (required): Base64-encoded SHA256 hash of your API key combined with the salt
- **Authentication.Cache.Salt** (required): Base64-encoded random salt used for hashing
- **Authentication.Cache.HeaderName** (optional): Name of the HTTP header containing the API key. Defaults to "X-Cache-API-Key"

### Future Extensions

The configuration structure is designed to support multiple API key configurations for different controllers:

```json
{
  "Authentication": {
    "Cache": {
      "Hash": "cache-hash-here",
      "Salt": "cache-salt-here",
      "HeaderName": "X-Cache-API-Key"
    },
    "Admin": {
      "Hash": "admin-hash-here",
      "Salt": "admin-salt-here",
      "HeaderName": "X-Admin-API-Key"
    }
  }
}
```

## Generating API Key Hash and Salt

### Using PowerShell

```powershell
# Generate a random salt
$saltBytes = New-Object byte[] 32
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($saltBytes)
$salt = [Convert]::ToBase64String($saltBytes)

# Your plain text API key
$apiKey = "your-secret-api-key"

# Generate hash
$sha256 = [System.Security.Cryptography.SHA256]::Create()
$hashBytes = $sha256.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($apiKey + $salt))
$hash = [Convert]::ToBase64String($hashBytes)

Write-Host "Salt: $salt"
Write-Host "Hash: $hash"
```

## Using the API

### Making Authenticated Requests

Include your API key in the configured header (default is `X-Cache-API-Key`):

```bash
# Clear cache for a specific table
curl -X DELETE "https://yourapi.com/cache/StatFin/table123" \
  -H "X-Cache-API-Key: your-secret-api-key"

# Clear all cache for a database
curl -X DELETE "https://yourapi.com/cache/StatFin" \
  -H "X-Cache-API-Key: your-secret-api-key"
```

### Response Codes

- **200 OK**: Operation completed successfully
- **401 Unauthorized**: Invalid or missing API key

Rest of the response codes as per standard API behavior

## Disabling Authentication

To disable authentication entirely, simply remove or don't configure the Hash and Salt values in the Cache section. The system will automatically allow all requests to proceed without authentication.