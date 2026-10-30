---
name: openapi-validation
description: "Validate the OpenAPI specification document against the actual API implementation. Use when: checking OpenAPI spec, validating swagger, verifying API documentation, reviewing OpenAPI document, checking for unused schemas, OpenAPI review, spec review, API contract validation. Fetches the live OpenAPI document and validates syntax, schema, controller alignment, and unused definitions. Reports findings without making changes."
argument-hint: "Optional: specify focus area (e.g., 'syntax only', 'unused schemas', 'controller alignment') or leave empty for full validation"
---

# OpenAPI Document Validation

Perform a read-only validation of the project's OpenAPI specification against the actual API implementation. **Do not make any code changes** — only report findings.

## When to Use

- After modifying controllers, models, or OpenAPI filters
- Before merging changes that affect the API surface
- As a periodic check that the spec matches the implementation
- When asked to "check openapi", "validate spec", "review API docs"

## Background

- The OpenAPI document is generated via Swashbuckle and served at `/openapi/document.json`
- Custom document filters, schema filters, and operation filters in `PxApi/OpenApi/` shape the output
- Some controllers are excluded via `[ApiExplorerSettings(IgnoreApi = true)]` or through `ApiExplorerConventionsFactory`
- Use the tracked testing configuration and sample database; developer-local application settings and HTTPS profiles are not prerequisites.

## Procedure

Require an existing .NET 10 SDK; Node.js/npm are needed for schema validation. Follow the [shared prerequisites](../../../docs/architecture/testing.md#agent-review-prerequisites-and-fresh-clones) and the invoking review's input/safety checks before executing project code. When invoked directly rather than by a review, apply the Security policy from the committed `HEAD` version of `.github/copilot-instructions.md` (read with `git show HEAD:.github/copilot-instructions.md`), confirm that the working-tree inputs used to build and run the API are the ones being validated, and report any local differences with the results. Execute steps where their prerequisites are met; continue independent static review after blocked runtime/schema checks. Track progress using the todo list.

Create a unique task-owned directory under repository-root `agent_tmp/`, or reuse the invoking review's directory. The placeholders below refer to absolute paths under that directory. Use platform-appropriate commands and environment assignment; do not write temporary output or npm caches outside the project.

### Step 1 — Start the Application

Reuse the invoking review's verified sample API session and reported URL if one is already running. Otherwise follow [sample-database-testing](../sample-database-testing/SKILL.md#manual-endpoint-testing): build the solution in `Debug` with `--no-restore`, using its one-time solution restore only after a missing-dependency failure. Do not repeat a successful parent restore/build or launch an unrelated existing configuration.

After a successful build, start the API from `PxApi.IntegrationTests/` with both `ASPNETCORE_ENVIRONMENT` and `DOTNET_ENVIRONMENT` set to lowercase `testing`. Set `DOTNET_CLI_TELEMETRY_OPTOUT=1` for every .NET command or the invoking shell. Choose a free loopback HTTP port and substitute its URL consistently:

```text
dotnet ../PxApi/bin/Debug/net10.0/PxApi.dll --urls "<listening-url>" --RootUrl "<listening-url>/" --LogOptions:Folder "<absolute-task-directory>/logs" --ApplicationInsights:Enabled false --FeatureManagement:SearchController false
```

Keep a bounded session attached, retain its process ID and guarantee cleanup on failure. Require HTTP 200 from `/health` with a healthy `sample` database within 30 seconds; a running process or startup log alone is not readiness. Use the same-process startup/request/shutdown guidance from the sample skill. Do not inspect secret-bearing local settings, use production credentials or contact external storage/search/telemetry.

### Step 2 — Fetch the OpenAPI Document

Retrieve the generated specification from the verified sample session into the task directory. Use direct loopback requests:

```text
curl --noproxy localhost,127.0.0.1 --fail --silent --show-error --header "Accept: application/json" "<listening-url>/openapi/document.json" --output "<absolute-task-directory>/openapi-doc.json"
```

Record the HTTP result and require a JSON OpenAPI document. Failed retrieval blocks live schema/alignment checks; continue independent static controller/metadata inspection and label missing live evidence unverified. Do not fall back to another server, developer settings, or disabled certificate validation.

### Step 3 — Validate Syntax, Schema, and Unused Definitions

Run the Node.js validation script to check the document for structural correctness and unused definitions. This covers both syntax/schema validation and unused definition detection in a single pass.

Reuse existing validator dependencies if the script's imports resolve. If declared dependencies are missing and Node.js/npm are available, permit only this bounded preparation, subject to the baseline Security policy (the invoking review's, or the committed `HEAD` policy when invoked directly):

1. Copy the reviewed `package.json`, `package-lock.json`, and `validate-openapi.mjs` from this skill into `<absolute-task-directory>/validator/`. Do not modify those files or replace an existing checkout's `node_modules`.
2. Install exactly the lockfile-defined dependencies into that task-owned directory, with lifecycle scripts, audit/funding requests and update-notifier checks disabled and the cache inside the project:

```text
npm --prefix "<absolute-task-directory>/validator" ci --ignore-scripts --no-audit --no-fund --no-update-notifier --cache "<absolute-task-directory>/npm-cache"
```

Do not add dependencies, update manifests/lockfiles, change registries or credentials, inspect secret stores, or install global tools. New dependencies/sources still require approval; permission to prepare this existing validator does not grant blanket package-install permission. If Node.js/npm are missing, installation is prohibited, or preparation fails, mark schema/unused-definition validation unverified while continuing document retrieval and independent static checks.

With existing checkout dependencies, run:

```text
node .github/skills/openapi-validation/validate-openapi.mjs "<absolute-task-directory>/openapi-doc.json"
```

With task-local dependencies, run the copied script so Node resolves that installation:

```text
node "<absolute-task-directory>/validator/validate-openapi.mjs" "<absolute-task-directory>/openapi-doc.json"
```

The script performs:
- JSON parsing and OpenAPI version validation
- Required top-level field checks (`info`, `paths`, `components`)
- Path structure validation (HTTP methods, `operationId`, responses, parameters)
- All `$ref` resolution — verifies every reference points to an existing definition
- Response status code validation
- Deep validation via `@apidevtools/swagger-parser`
- Unused definition detection for schemas, parameters, request bodies, responses, and security schemes (including `discriminator.mapping` awareness)

The script exits with code 0 if the document is valid, code 1 if issues are found or the document path argument is missing, and code 2 if the document cannot be read or parsed as JSON. Record dependency preparation separately from validation. A successful install is not a successful schema check. **Record** the script output and exit code for the final report.

### Step 4 — Validate Against Controller Implementation

Compare the OpenAPI document against the actual controller code to ensure they match. If no live document is available, inspect the static controller/generated-document metadata independently and mark live alignment unverified; do not invent endpoint counts or claim a live match.

1. **Read all controller files** under `PxApi/Controllers/`.
2. **Read the API explorer conventions** in `PxApi/OpenApi/ApiExplorerConventions.cs` to understand which controllers are excluded.
3. For each controller that is **included** in OpenAPI (not excluded via `[ApiExplorerSettings(IgnoreApi = true)]` or conventions):
   - Verify every public action method has a corresponding path and HTTP method in the document
   - Verify route templates match (including route parameters)
   - Verify parameter types and names match between the controller and the spec
   - Verify response types declared via `[ProducesResponseType]` attributes match the spec's response definitions
4. For each controller that is **excluded**:
   - Verify it does **not** appear in the OpenAPI paths
5. For each path in the OpenAPI document:
   - Verify it corresponds to an actual controller action (no phantom endpoints)

**Record** all mismatches: missing endpoints, extra endpoints, parameter mismatches, response type mismatches.

### Step 5 — Stop the Application

Stop only the application process owned by this validation. If reusing the invoking review's server, leave shutdown to that owner. After shutdown, inspect task-owned Debug logs as required by the sample skill, including after failed requests. Clean up only this task's files/subdirectory, including its copied validator, `node_modules` and npm cache; never remove a pre-existing directory or another task's output.

## Report Format

After all applicable steps complete, present the findings in a single structured report. Record preparation, document retrieval, schema validation and controller alignment separately. Use **unverified** for blocked checks and **not reported** for unavailable counts; incomplete checks cannot produce a clean overall result.

```
## OpenAPI Validation Report

### 1. Document Retrieval
- **Status**: ✅ Retrieved / ❌ Failed
- **URL**: <url used>
- **OpenAPI Version**: <version>

### 2. Syntax & Schema Validation
- **Status**: ✅ Valid / ❌ Issues Found
- **Issues**: <count>
- <details per issue: location in document, description of problem>

### 3. Controller Alignment
- **Status**: ✅ Aligned / ❌ Mismatches Found
- **Controllers checked**: <count>
- **Endpoints in spec**: <count>
- **Endpoints in code**: <count>
- Missing from spec:
  - <controller.method — HTTP verb — route>
- Extra in spec (no matching controller):
  - <path — HTTP verb>
- Parameter mismatches:
  - <path — parameter — expected vs actual>
- Response mismatches:
  - <path — status code — expected vs actual>

### 4. Unused Definitions
- **Status**: ✅ Clean / ⚠️ Unused Definitions Found
- **Unused schemas**: <count>
  - <schema name>
- **Unused parameters**: <count>
- **Unused request bodies**: <count>
- **Unused responses**: <count>
- **Unused security schemes**: <count>

### Overall: ✅ Specification is valid and aligned / ❌ Issues require attention

### Suggested Fixes
<Only present if issues were found. List each issue with a concrete suggestion for the user to act on.>
```

## Important Rules

1. **Read-only**: Never modify tracked source code, project files, configuration, manifests, lockfiles or the index. Only generated task artifacts and the bounded dependency preparation above are permitted.
2. **Continue on failure**: If a prerequisite fails, mark dependent checks unverified and continue independent static review.
3. **Server ownership**: Guarantee shutdown of validation-owned servers on success/failure; do not stop unrelated or parent-owned services.
4. **No changes**: Report findings only. If the user asks to fix issues, propose changes and wait for approval.
