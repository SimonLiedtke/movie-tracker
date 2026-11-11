#!/usr/bin/env python3
"""Compares two ReportGenerator Summary.json files and prints a Markdown report.

Usage: coverage_comment.py CURRENT_SUMMARY.json [BASELINE_SUMMARY.json]

Purely informational: always exits 0, whatever the numbers are. Without a baseline
(first run, expired cache) only the current coverage is shown.
"""
import json
import sys
from pathlib import Path

MARKER = "<!-- backend-coverage-report -->"
# Differences below this many percentage points count as "no change" (the report rounds to 0.1).
EPSILON = 0.05


def percent(covered: int, total: int) -> float:
    return 100.0 * covered / total if total else 100.0


def load(path: str | None) -> dict | None:
    if not path or not Path(path).is_file():
        return None
    return json.loads(Path(path).read_text())


def metrics(entry: dict) -> dict[str, tuple[int, int]]:
    """Covered and total counts, so differences are computed from raw numbers, not rounded percentages."""
    return {
        "lines": (entry["coveredlines"], entry["coverablelines"]),
        "branches": (entry["coveredbranches"], entry["totalbranches"]),
    }


def delta_text(current: tuple[int, int], baseline: tuple[int, int] | None) -> str:
    now = percent(*current)
    if baseline is None:
        return f"{now:.1f}%"
    diff = now - percent(*baseline)
    if abs(diff) < EPSILON:
        return f"{now:.1f}% (no change)"
    arrow = "📈 +" if diff > 0 else "📉 "
    return f"{now:.1f}% ({arrow}{diff:.1f} pp)"


def headline(current: dict[str, tuple[int, int]], baseline: dict[str, tuple[int, int]] | None) -> str:
    if baseline is None:
        return "No baseline from `main` is available, so there is nothing to compare with."
    diff = percent(*current["lines"]) - percent(*baseline["lines"])
    before, after = percent(*baseline["lines"]), percent(*current["lines"])
    if abs(diff) < EPSILON:
        return f"Line coverage is unchanged at **{after:.1f}%**."
    verb, icon = ("increases", "📈") if diff > 0 else ("decreases", "📉")
    return f"{icon} This PR {verb} line coverage from **{before:.1f}%** to **{after:.1f}%** ({diff:+.1f} pp)."


def main() -> None:
    current = load(sys.argv[1])
    if current is None:
        sys.exit(f"Missing coverage summary: {sys.argv[1]}")
    baseline = load(sys.argv[2] if len(sys.argv) > 2 else None)

    now_total = metrics(current["summary"])
    base_total = metrics(baseline["summary"]) if baseline else None

    base_assemblies = {a["name"]: metrics(a) for a in baseline["coverage"]["assemblies"]} if baseline else {}

    lines = [
        MARKER,
        "### Backend test coverage",
        "",
        headline(now_total, base_total),
        "",
        "| | Lines | Branches |",
        "|:---|---:|---:|",
        f"| **Total** | {delta_text(now_total['lines'], base_total and base_total['lines'])}"
        f" | {delta_text(now_total['branches'], base_total and base_total['branches'])} |",
    ]
    for assembly in sorted(current["coverage"]["assemblies"], key=lambda a: a["name"]):
        now = metrics(assembly)
        before = base_assemblies.get(assembly["name"])
        lines.append(
            f"| {assembly['name']} | {delta_text(now['lines'], before and before['lines'])}"
            f" | {delta_text(now['branches'], before and before['branches'])} |"
        )
    lines += [
        "",
        "<sub>Informational only: it does not affect the result of the checks. "
        "Compared with the latest coverage of `main`; the full HTML report is the `backend-coverage` artifact of this run.</sub>",
    ]
    print("\n".join(lines))


if __name__ == "__main__":
    main()
