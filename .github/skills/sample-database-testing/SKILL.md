---
name: sample-database-testing
description: 'Run the PxApi integration-test suite or start a manual sample API session with appsettings.testing.json. Use when testing non-search endpoints, JSON-stat, CSV, filters, cache clearing, sample PX database changes, or inspecting local Debug logs.'
---

# Test the sample database

## Automated integration tests

From the repository root:

```bash
DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet test PxApi.IntegrationTests/PxApi.IntegrationTests.csproj --no-restore
```

Set `DOTNET_CLI_TELEMETRY_OPTOUT=1` for every `dotnet` command in this workflow,
per command or shell only. .NET CLI telemetry otherwise probes the Azure
instance metadata endpoint, which sandboxes block.

If declared dependencies are missing, restore this project and retry. Do not
install tools or add dependencies for this workflow. To select a fixture, append
`--filter FullyQualifiedName~ApiTests` or
`--filter FullyQualifiedName~SampleDatabaseTests`.

- `ApiTests` uses the production host with `WebApplicationFactory<Program>` and
  an in-process `HttpClient`; no listening port or separate server is needed.
- The factory replaces application configuration sources with the testing
  settings and an absolute fixture path. Search, telemetry and authentication
  are disabled; no external services or credentials are required.
- Fixtures are nonparallel because `AppSettings.Active` and caches are shared.
- Tests cover discovery, metadata, all 89 synthetic cells, GET/POST filters,
  CSV, HEAD/OPTIONS, cache clearing, expected errors, Swagger and readable
  local Debug logs. Automated runs use a unique log directory and remove it
  during teardown.

The canonical [configuration](../../../PxApi.IntegrationTests/appsettings.testing.json),
[PX fixtures](../../../PxApi.IntegrationTests/SampleDatabases/sample) and
[HTTP tests](../../../PxApi.IntegrationTests/EndpointTests/ApiTests.cs) belong to
the integration project. Do not duplicate them in the API or unit-test project.

## Manual endpoint testing

For interactive requests or real Kestrel verification, build from the repository
root, then launch from the integration project's directory:

```bash
export DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet build PxApi/PxApi.csproj --no-restore
cd PxApi.IntegrationTests
ASPNETCORE_ENVIRONMENT=testing DOTNET_ENVIRONMENT=testing \
  dotnet ../PxApi/bin/Debug/net10.0/PxApi.dll
```

The working directory resolves `SampleDatabases`; lowercase `testing` loads
the settings filename on Linux. The API listens at `http://localhost:5080`.
Manual startup uses normal configuration merging; prefer the automated suite
for isolation. Never inspect secret-bearing local settings or use production
credentials. This unauthenticated configuration is for local testing only.

Retain the server's process/session ID and keep bounded smoke-test servers
attached. Wait up to 30 seconds for `/health` to return 200 with a healthy
`sample` database. Do not silently reuse an existing listener.

For bounded agent smoke tests, keep server startup, readiness checks, requests,
shutdown and final log inspection within one tool execution. Run the server as
a child process and guarantee cleanup on failure (for example with
`try`/`finally`); stop it before inspecting the flushed logs. Separate tool
executions can use different sandbox network and PID namespaces, so a healthy
server's loopback listener may be unreachable from another execution even when
local networking is allowed. This was verified in this sandbox, not established
as a universal tool restriction. A reported running session alone does not prove
HTTP reachability.

With sandbox local networking allowed, use direct loopback requests: the sandbox
proxy can return HTTP 502 because it cannot reach a sandbox-local listener.

```bash
curl --noproxy localhost,127.0.0.1 --fail --silent --show-error \
  -H 'Accept: application/json' http://localhost:5080/health
```

Apply `--noproxy localhost,127.0.0.1` to other local curl requests too. Do not
change global proxy settings. Proxy bypass does not resolve separate loopback
namespaces. Distinguish connection refusal from proxy HTTP errors and application
HTTP failures; do not infer namespace isolation from connection refusal alone.
If sandbox policy denies local access, report it; do not bypass the sandbox.
The in-process suite does not need network access.

Use [SampleDatabase.http](../../../PxApi.IntegrationTests/SampleDatabase.http)
for request bodies and additional examples. Swagger UI is at `/index.html`.

| Request | Expected result |
|---|---|
| `GET /info` | 200; application name and version |
| `GET /meta/databases` | 200; `sample` |
| `GET /meta/databases/sample/tables` | 200; `flows`, `observations` |
| `GET /meta/databases/sample/tables/flows` | 200; IDs `TIME,MEASURE`, size `[12,2]` |
| `GET /data/databases/sample/tables/flows?filters=MEASURE:code=*` | 200; 24 values: `100,80,110,90,...,210,190` |
| `GET /data/databases/sample/tables/observations?filters=REGION:code=*` | 200; size `[5,13,1]`, 65 values |
| `GET /data/databases/sample/tables/flows?filters=TIME:last=2`, Accept `text/csv` | 200; CSV rows for 2023/2024 with inbound values 200/210 |
| `DELETE /cache/databases/sample` | 200; repeated data query unchanged |

Assert status, content type, shape and values, not just nonempty responses.
Use `Accept: application/json` for JSON-stat. Unfiltered non-time dimensions
select one default value; explicit wildcards select all measures or regions.
POST code filters use string arrays; `first`/`last` queries use integers.
Search is excluded.

If editing PX data, separate numeric values with spaces: bare newlines are not
separators in the current reader. Update assertions for intentional value changes.

## Inspect local Debug logs

The testing configuration writes application logs at `Debug` level to
repository-root `agent_tmp/sample-database-testing/manual/nlog-YYYY-MM-DD.log`.
Audit logging and telemetry remain disabled. For agent-run manual sessions,
override `LogOptions:Folder` with an absolute path inside a unique task-specific
`agent_tmp/` subfolder (for example via `--LogOptions:Folder <absolute-path>`).
Do not share a log directory between concurrent sessions.

After exercising endpoints, read the current session's logs to discover issues
and assist debugging, even if HTTP requests succeeded:

- Confirm the file exists, is readable, and contains `"level": "debug"` entries
  from actual requests, such as `Opening file stream`.
- Search for `warn`, `error`, `fatal` levels and nonempty `exception` fields.
  Read only bounded matching sections, including nearby Debug diagnostics.
- Correlate timestamps, `query_completed` records, database/table identifiers,
  request IDs and operation scopes where present with the request being tested.
  Explain expected negative-test diagnostics separately from unexpected issues.
- Report unexpected diagnostics and their request scenario. Missing/unreadable
  logs or absent Debug records are verification failures, not evidence that
  there are no issues.

NLog JSON records span multiple lines, so do not assume one JSON object per line.
Inspect only task-owned sample logs; never inspect production logs or secrets.
Stop the server to flush/close files before final inspection and cleanup.

## Cleanup and reporting

Stop only task-owned servers. Store temporary logs and responses in a unique
subfolder of repository-root, gitignored `/agent_tmp/`, then remove that task's
subfolder. Keep user files and fixtures intact. Report test commands, pass/fail
counts, manual checks and any application or sandbox issues.

See [setup documentation](../../../docs/README.md#running-with-the-sample-database).
