---
name: test-coverage-analysis
description: "Collect and analyze PxApi test coverage using Coverlet Cobertura reports and diff-cover-style changed-line checks. Use when asked about test coverage, line or branch coverage, uncovered code, changed-line coverage, coverage gaps, or the CI coverage gate."
---

# Cobertura Test Coverage Analysis

Measure coverage with the existing test tooling, identify meaningful gaps, and
report reproducible evidence. Do not edit production code or tests unless asked.
Coverage measures execution, not assertion quality.

## Rules

- Both NUnit test projects reference `coverlet.collector`. Use the VSTest
  `XPlat Code Coverage` collector; do not install coverage tools or use
  Microsoft.Testing.Platform switches.
- Require an existing .NET 10 SDK and Python 3. Follow the
  [shared prerequisites](../../../docs/architecture/testing.md#agent-review-prerequisites-and-fresh-clones)
  for the one-time `dotnet restore PxApi.sln --verbosity minimal`; when invoked
  by a review, reuse its preparation and successful build instead of repeating
  restore or test runs.
- Do not fetch. Use existing local refs and warn that they may be stale.
- Integration tests run in-process with local fixtures; see
  [sample-database-testing](../sample-database-testing/SKILL.md).
- A failed or incomplete run, zero tests, a missing report, a collector warning,
  or an unresolved source path makes the affected result **unverified**. Never
  present a filtered run as full coverage.
- Members marked `[ExcludeFromCodeCoverage]` (for example in `PxApi/Program.cs`
  and `PxApi/DataSources/`) are absent from reports by design, and CI treats
  them as non-executable. Do the same, but confirm the attribute before
  attributing a missing file or line to it.
- Run `dotnet` with `DOTNET_CLI_TELEMETRY_OPTOUT=1` set for the command or
  shell only, never globally. .NET CLI telemetry otherwise probes the Azure
  instance metadata endpoint, which sandboxes block.

## 1. Define the measurement

Record the scope (production code, selected files, changed lines, or a focused
test investigation), the working-tree state, and any test filter. Default to
both test projects in `Debug`, matching CI.

For changed lines, use the base the caller specifies. For a PR into `dev`,
prefer `origin/dev`, otherwise local `dev`. If neither exists, mark changed-line
coverage unverified. For staged changes, use `--base HEAD --cached`.

## 2. Collect fresh reports

Run from the repository root in one shell so `task_dir` stays defined:

```bash
mkdir -p agent_tmp
export DOTNET_CLI_TELEMETRY_OPTOUT=1
task_dir=$(mktemp -d "$PWD/agent_tmp/cobertura-coverage.XXXXXX")
dotnet test PxApi.UnitTests/PxApi.UnitTests.csproj -c Debug --no-restore --verbosity normal --collect:"XPlat Code Coverage" --results-directory "$task_dir/unit"
dotnet test PxApi.IntegrationTests/PxApi.IntegrationTests.csproj -c Debug --no-restore --verbosity normal --collect:"XPlat Code Coverage" --results-directory "$task_dir/integration"
```

Record each exit code and the passed, failed, and skipped counts. Add `--filter`
only for an explicitly focused measurement. Use `--no-build` only when current
build outputs are verified; stale binaries produce misleading coverage.
Require at least one executed test per project and a fresh Cobertura report.
Exit code 0 without executed tests is unverified, not evidence of coverage.

## 3. Analyze

```bash
python3 .github/skills/test-coverage-analysis/scripts/cobertura_summary.py "$task_dir/unit" "$task_dir/integration" --base origin/dev
```

The [script](./scripts/cobertura_summary.py) uses only the Python standard
library. Omit `--base` for totals only.

- It reports per-report line and branch coverage for `--scope` (default
  `PxApi/`) next to the report's root totals. Root totals also count generated
  `obj/` sources, which the script excludes.
- Combined line coverage is the union across reports. Combined branch coverage
  is unavailable because Coverlet does not record which outcomes were hit, so
  report branches per report only. A covered line can still have partial
  branches; check `condition-coverage` in the XML for lines of interest.
- With `--base`, it intersects lines added or modified since the merge base with
  executable report lines and applies the [CI](../../workflows/ci.yml) gate:
  `covered * 100 > total * 75` (equality fails). With no changed executable
  lines, the gate is not applicable; with unresolved source paths, it is
  unverified. It lists uncovered changed lines and changed in-scope files absent
  from the reports.
- The default target is committed `HEAD`. `--cached` targets the index, and
  `--working-tree` includes tracked uncommitted changes (untracked files are
  ignored). For these two targets the result is labeled a local threshold, not
  the CI gate. The coverage run must have built the same tree as the diff target.

If you use an already installed `diff-cover` instead, note that it includes
staged and unstaged changes by default; pass `--ignore-staged --ignore-unstaged`
when measuring `HEAD`.

## 4. Turn gaps into findings

For each significant uncovered line or partial branch, read the production path
and existing tests. Describe the missing scenario, the assertion, and the
incorrect behavior the test would reject. Prioritize error paths, boundary
inputs, and code changed by the task. Do not recommend tests merely to raise a
percentage. Distinguish measured gaps, suspected gaps, and intentional
exclusions.

## Output and cleanup

Report:

- Commands, inputs, filters, exit codes, test counts, and limitations.
- The script's coverage table, distinguishing per-report from combined results.
- For changed lines: base ref, merge-base SHA, target, counts, gate outcome, and
  uncovered locations.
- Prioritized test recommendations with source links and concrete scenarios.

Unless the user asked to keep the reports, remove `task_dir` after summarizing,
including after failures.
