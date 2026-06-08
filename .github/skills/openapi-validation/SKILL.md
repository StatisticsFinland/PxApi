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
- The HTTPS dev profile runs on `https://localhost:7215`

## Procedure

Execute each step in order. If a step fails, **continue with remaining steps** so the final report is complete. Track progress using the todo list.

### Step 1 — Start the Application

Start the API to generate the live OpenAPI document:

```
dotnet run --project PxApi/PxApi.csproj --launch-profile https
```

Run this in async mode. Wait a few seconds for the application to start, then proceed.

### Step 2 — Fetch the OpenAPI Document

Retrieve the generated specification into the `agent_tmp/` directory:

```
mkdir -p agent_tmp && curl -k https://localhost:7215/openapi/document.json -o agent_tmp/openapi-doc.json
```

If the fetch fails, check `PxApi/Properties/launchSettings.json` for the correct port and retry. If it still fails, record the error and stop — subsequent steps depend on the document.

### Step 3 — Validate Syntax, Schema, and Unused Definitions

Run the Node.js validation script to check the document for structural correctness and unused definitions. This covers both syntax/schema validation and unused definition detection in a single pass.

First, ensure dependencies are installed (one-time setup):

```bash
cd .github/skills/openapi-validation && npm install
```

Then run the script against the fetched document:

```bash
node .github/skills/openapi-validation/validate-openapi.mjs agent_tmp/openapi-doc.json
```

The script performs:
- JSON parsing and OpenAPI version validation
- Required top-level field checks (`info`, `paths`, `components`)
- Path structure validation (HTTP methods, `operationId`, responses, parameters)
- All `$ref` resolution — verifies every reference points to an existing definition
- Response status code validation
- Deep validation via `@apidevtools/swagger-parser`
- Unused definition detection for schemas, parameters, request bodies, responses, and security schemes (including `discriminator.mapping` awareness)

The script exits with code 0 if the document is valid, or code 1 if issues are found. **Record** the script output for the final report.

### Step 4 — Validate Against Controller Implementation

Compare the OpenAPI document against the actual controller code to ensure they match.

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

**Kill the terminal process** running the application. Clean up the temporary `agent_tmp/openapi-doc.json` file and the `agent_tmp/` directory if it is empty.

## Report Format

After all steps complete, present the findings in a single structured report:

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

1. **Read-only**: Never modify source code, project files, or configuration. Only run diagnostic commands.
2. **Continue on failure**: If a step fails, record the failure and proceed to the next step (except Step 2 failure, which blocks Steps 3–4).
3. **Kill the server**: Always stop the application after validation completes.
4. **No changes**: Report findings only. If the user asks to fix issues, propose changes and wait for approval.
