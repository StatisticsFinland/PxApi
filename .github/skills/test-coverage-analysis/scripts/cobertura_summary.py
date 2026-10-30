#!/usr/bin/env python3
"""Summarize Coverlet Cobertura reports and optionally changed-line coverage.

Standard library only. Run from the repository root.
Exit codes: 0 = analysis completed, 1 = invalid input or analysis error,
2 = invalid command-line arguments.
"""

import argparse
import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

CONDITION_RE = re.compile(r"\((\d+)/(\d+)\)")
HUNK_RE = re.compile(r"^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@")
GATE_PERCENT = 75


def find_reports(paths):
    reports = []
    for raw in paths:
        path = Path(raw)
        if path.is_dir():
            reports.extend(sorted(path.rglob("coverage.cobertura.xml")))
        elif path.is_file():
            reports.append(path)
        else:
            raise SystemExit(f"error: report path not found: {raw}")
    if not reports:
        raise SystemExit("error: no coverage.cobertura.xml files found")
    return reports


def resolve(filename, sources, repo):
    candidates = [Path(filename)] if os.path.isabs(filename) else [Path(s) / filename for s in sources]
    for candidate in candidates:
        try:
            resolved = candidate.resolve()
            relative = resolved.relative_to(repo)
        except (OSError, ValueError):
            continue
        if resolved.is_file():
            return relative.as_posix()
    return None


def parse_report(report, repo):
    try:
        root = ET.parse(report).getroot()
    except ET.ParseError as error:
        raise SystemExit(f"error: unreadable XML {report}: {error}")
    if root.tag != "coverage":
        raise SystemExit(f"error: not a Cobertura report: {report}")

    sources = [s.text or "" for s in root.iter("source")]
    lines = {}      # (path, line) -> hits
    branches = {}   # (path, line) -> (covered, total)
    unresolved = set()
    generated = set()

    for cls in root.iter("class"):
        filename = cls.get("filename", "")
        if Path(filename).parts[:1] == ("obj",) or "/obj/" in filename.replace("\\", "/"):
            generated.add(filename)
            continue
        path = resolve(filename, sources, repo)
        if path is None:
            unresolved.add(filename)
            continue
        # Class-level lines only; method-level entries duplicate them.
        for line in cls.findall("./lines/line"):
            key = (path, int(line.get("number")))
            lines[key] = max(lines.get(key, 0), int(line.get("hits", "0")))
            if line.get("branch", "").lower() == "true":
                match = CONDITION_RE.search(line.get("condition-coverage", ""))
                if match:
                    value = (int(match.group(1)), int(match.group(2)))
                    branches[key] = max(branches.get(key, (0, 0)), value, key=lambda v: (v[1], v[0]))

    root_totals = {name: root.get(name) for name in
                   ("lines-covered", "lines-valid", "branches-covered", "branches-valid")}
    return lines, branches, unresolved, generated, root_totals


def ratio(covered, total):
    if total == 0:
        return f"{covered}/{total} (n/a)"
    return f"{covered}/{total} ({covered * 100 / total:.2f}%)"


def line_summary(lines, prefix):
    scoped = [hits for (path, _), hits in lines.items() if path.startswith(prefix)]
    return sum(1 for h in scoped if h > 0), len(scoped)


def branch_summary(branches, prefix):
    scoped = [v for (path, _), v in branches.items() if path.startswith(prefix)]
    return sum(c for c, _ in scoped), sum(t for _, t in scoped)


def git(repo, *args):
    result = subprocess.run(["git", *args], cwd=repo, capture_output=True, text=True)
    if result.returncode != 0:
        raise SystemExit(f"error: git {' '.join(args)} failed: {result.stderr.strip()}")
    return result.stdout


def changed_lines(repo, base, target):
    merge_base = git(repo, "merge-base", base, "HEAD").strip()
    # Explicit prefixes keep parsing independent of diff.noprefix/mnemonicPrefix settings.
    diff_args = ["-c", "core.quotePath=false", "diff", "--unified=0", "--no-color", "--no-ext-diff",
                 "--find-renames", "--src-prefix=a/", "--dst-prefix=b/"]
    if target == "cached":
        diff_args += ["--cached", merge_base]
    elif target == "working-tree":
        diff_args.append(merge_base)
    else:
        diff_args += [merge_base, "HEAD"]
    changed = {}
    path = None
    for line in git(repo, *diff_args).splitlines():
        if line.startswith("+++ "):
            new_path = line[4:]
            path = None if new_path == "/dev/null" else new_path.removeprefix("b/")
        elif path and (match := HUNK_RE.match(line)):
            start, count = int(match.group(1)), int(match.group(2) or "1")
            changed.setdefault(path, set()).update(range(start, start + count))
    return merge_base, changed


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("reports", nargs="+", help="Cobertura files or directories searched recursively")
    parser.add_argument("--scope", default="PxApi/", help="Repository-relative production path prefix")
    parser.add_argument("--base", help="Comparison ref for changed-line coverage, e.g. origin/dev")
    target = parser.add_mutually_exclusive_group()
    target.add_argument("--cached", dest="target", action="store_const", const="cached",
                        help="Compare the merge base with the index instead of HEAD (use --base HEAD for staged changes)")
    target.add_argument("--working-tree", dest="target", action="store_const", const="working-tree",
                        help="Compare the merge base with tracked working-tree files instead of HEAD")
    parser.set_defaults(target="head")
    args = parser.parse_args()

    repo = Path(git(Path.cwd(), "rev-parse", "--show-toplevel").strip()).resolve()
    reports = find_reports(args.reports)
    combined_lines = {}
    any_unresolved = False

    print(f"HEAD: {git(repo, 'rev-parse', 'HEAD').strip()}")
    print(f"Scope: {args.scope}\n")
    print("| Report | Lines | Branches | Root lines | Root branches |")
    print("|---|---|---|---|---|")
    for report in reports:
        lines, branches, unresolved, generated, root = parse_report(report, repo)
        for key, hits in lines.items():
            combined_lines[key] = max(combined_lines.get(key, 0), hits)
        name = os.path.relpath(report, repo)
        print(f"| {name} | {ratio(*line_summary(lines, args.scope))} "
              f"| {ratio(*branch_summary(branches, args.scope))} "
              f"| {root['lines-covered']}/{root['lines-valid']} "
              f"| {root['branches-covered']}/{root['branches-valid']} |")
        if generated:
            print(f"Excluded {len(generated)} generated obj/ source(s) from {name}")
        for filename in sorted(unresolved):
            any_unresolved = True
            print(f"UNRESOLVED source path in {name}: {filename}")

    print(f"\nCombined lines (union): {ratio(*line_summary(combined_lines, args.scope))}")
    print("Combined branches: unavailable (reports do not identify which outcomes were hit)")
    if any_unresolved:
        print("WARNING: unresolved source paths; affected results are unverified")

    if not args.base:
        return 0

    merge_base, changed = changed_lines(repo, args.base, args.target)
    target = {"head": "HEAD", "cached": "index", "working-tree": "tracked working tree"}[args.target]
    print(f"\nChanged lines: {args.base} merge base {merge_base} -> {target}")
    reported_files = {path for path, _ in combined_lines}
    covered = total = 0
    uncovered, absent = [], []
    for path in sorted(changed):
        if path not in reported_files:
            if path.startswith(args.scope) and path.endswith(".cs"):
                absent.append(path)
            continue
        for number in sorted(changed[path]):
            hits = combined_lines.get((path, number))
            if hits is None:
                continue
            total += 1
            if hits > 0:
                covered += 1
            else:
                uncovered.append(f"{path}:{number}")

    print(f"Changed executable lines: {ratio(covered, total)}")
    # Only committed HEAD matches what CI measures; other targets are local thresholds.
    label = f"Gate (> {GATE_PERCENT}%)" if args.target == "head" else f"Threshold (> {GATE_PERCENT}%, not CI gate)"
    if any_unresolved:
        print(f"{label}: UNVERIFIED (unresolved source paths)")
    elif total == 0:
        print(f"{label}: not applicable")
    else:
        print(f"{label}: {'PASS' if covered * 100 > total * GATE_PERCENT else 'FAIL'}")
    for location in uncovered:
        print(f"uncovered: {location}")
    for path in absent:
        print(f"changed in-scope file absent from reports (check [ExcludeFromCodeCoverage]): {path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
