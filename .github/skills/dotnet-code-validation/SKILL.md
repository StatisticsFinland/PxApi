---
name: dotnet-code-validation
description: "Validate a .NET application's code quality. Use when: validating changes, pre-merge check, CI readiness check, full validation, build and test, review changes, PR review, code review. Performs build, SonarQube scan, unit tests, and code review of changes. Reports all findings without making changes."
argument-hint: "Optional: specify focus area (e.g., 'build only', 'tests only', 'review only') or leave empty for full validation"
---

# .NET Code Validation

Perform a comprehensive, read-only validation of a .NET application's code quality. **Do not make any code changes** — only report findings and suggest fixes for the user to approve.

## When to Use

- Before merging a branch or submitting a PR
- After making changes to verify nothing is broken
- As a periodic health check on the codebase
- When asked to "validate", "verify", "check", or "run tests"

## Procedure

Execute each step in order. If a step fails, **continue with remaining steps** so the final report is complete. Track progress using the todo list.

### Step 1 — Identify Changed Files

Use the `get_changed_files` tool to determine which files have been modified. Record the list for use in SonarQube scanning (Step 3). If no changes are detected, note this and continue — all steps still apply.

### Step 2 — Build the Solution

Run the build with warnings treated seriously:

```
dotnet build <solution-file> --verbosity minimal
```

**Capture and record:**
- Total warnings count and details (file, line, message)
- Total errors count and details
- Whether build succeeded or failed

A successful validation requires **0 errors and 0 warnings**.

### Step 3 — SonarQube Scan

Use the `sonarqube_analyze_file` tool to scan each changed file identified in Step 1. Also use `sonarqube_list_potential_security_issues` to check for security issues across the workspace.

**Capture and record:**
- Issues per file (bugs, vulnerabilities, code smells)
- Severity levels (blocker, critical, major, minor, info)
- Security hotspots

If no changed files were found, scan the currently open file as a minimum check.

### Step 4 — Run Unit Tests

Run the full test suite:

```
dotnet test <test-project> --verbosity normal
```

**Capture and record:**
- Total tests, passed, failed, skipped counts
- For each failure: test name, expected vs actual values, and file location
- Test duration

### Step 5 — Security Review

Review the changed files identified in Step 1 for security concerns. If no changed files were found, skip this step.

**Security checklist:**
- **Input handling**: No user input used directly for database queries or filesystem access without proper validation and sanitization
- **Logging**: No user input logged as-is without sanitization
- **Secrets**: No hardcoded secrets (API keys, connection strings, passwords) anywhere in code, comments, or configuration
- **Dependencies**: Check used NuGet packages for known vulnerabilities by running `dotnet list <project> package --vulnerable`
- **Error responses**: No sensitive information leaked in HTTP error responses (e.g., stack traces, internal paths, connection details)

For each file, read the full content and note any violations.

**Capture and record:**
- Files reviewed count
- Issues per file with line numbers and specific concern
- Vulnerable packages (if any)

### Step 6 — Code Review of Changes

Perform a code review of the changed files identified in Step 1. For each changed file, read the file and review it against the project's coding conventions defined in `.github/copilot-instructions.md`.

**Review checklist:**
- **Coding conventions**: Explicit types (no `var`), XML docs on public members, correct member ordering (properties before methods, public before private), primary constructors where applicable, `const`/`readonly` for unmodified locals
- **Logging**: Template strings used, exceptions included in catch-block logs, logger scopes for context
- **Error handling**: Appropriate exception types, meaningful error messages
- **Naming**: Clear, consistent naming following .NET conventions
- **Design**: No over-engineering, single responsibility, appropriate abstractions

For each file, read the full content and note any violations. If no changed files were found, skip this step.

**Capture and record:**
- Files reviewed count
- Issues per file with line numbers and specific convention violated
- Severity: 🔴 Must fix (security, bugs) | 🟡 Should fix (conventions) | 🔵 Consider (suggestions)

## Report Format

After all steps complete, present the findings in a single structured report. Use this exact format:

```
## Validation Report

### 1. Build
- **Status**: ✅ Pass / ❌ Fail
- **Errors**: <count>
- **Warnings**: <count>
- <details if any>

### 2. SonarQube Analysis
- **Status**: ✅ Clean / ⚠️ Issues Found / ❌ Scan Failed
- **Files scanned**: <count>
- **Issues**: <count by severity>
- <details per file if any>

### 3. Unit Tests
- **Status**: ✅ All Passed / ❌ Failures Found
- **Total**: <count> | **Passed**: <count> | **Failed**: <count> | **Skipped**: <count>
- <failure details if any>

### 4. Security Review
- **Status**: ✅ Clean / ⚠️ Issues Found / ⏭️ Skipped (no changes)
- **Files reviewed**: <count>
- **Input handling issues**: <count>
- **Logging issues**: <count>
- **Hardcoded secrets**: <count>
- **Vulnerable packages**: <count>
- **Error response leaks**: <count>
- <details per file: line number, issue, recommendation>

### 5. Code Review
- **Status**: ✅ Clean / ⚠️ Issues Found / ⏭️ Skipped (no changes)
- **Files reviewed**: <count>
- 🔴 **Must fix**: <count>
- 🟡 **Should fix**: <count>
- 🔵 **Consider**: <count>
- <details per file: line number, issue, convention violated>

### Overall: ✅ All checks passed / ❌ Issues require attention

### Suggested Fixes
<Only present if issues were found. List each issue with a concrete suggestion.>
```

## Important Rules

1. **Read-only**: Never modify source code, project files, or configuration. Only run diagnostic commands.
2. **Continue on failure**: If any step fails, record the failure and proceed to the next step.
3. **Consult before fixing**: If the user asks to fix issues, propose specific changes and wait for approval before editing.
