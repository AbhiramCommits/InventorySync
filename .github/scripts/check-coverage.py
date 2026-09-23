#!/usr/bin/env python3
"""Enforce a minimum line-coverage rate for the Core and Infrastructure assemblies.

Parses the Cobertura XML produced by ``dotnet test --collect:"XPlat Code Coverage"``
and fails when the combined line rate for InventorySync.Core and
InventorySync.Infrastructure drops below the threshold.

Usage: python3 .github/scripts/check-coverage.py <results-directory> [threshold]
"""

import sys
import xml.etree.ElementTree as ET
from pathlib import Path

THRESHOLD = 70.0
TARGET_PREFIXES = ("InventorySync.Core", "InventorySync.Infrastructure")


def main() -> int:
    results_dir = Path(sys.argv[1] if len(sys.argv) > 1 else "coverage")
    threshold = float(sys.argv[2]) if len(sys.argv) > 2 else THRESHOLD

    reports = list(results_dir.rglob("coverage.cobertura.xml"))
    if not reports:
        print(f"No coverage.cobertura.xml found under {results_dir}", file=sys.stderr)
        return 2

    total_lines = 0
    total_covered = 0
    per_assembly = {}

    for report in reports:
        root = ET.parse(report).getroot()
        for package in root.iter("package"):
            name = package.get("name") or ""
            if not name.startswith(TARGET_PREFIXES):
                continue

            lines = package.findall(".//line")
            covered = sum(1 for line in lines if int(line.get("hits", 0)) > 0)
            total_lines += len(lines)
            total_covered += covered

            current_lines, current_covered = per_assembly.get(name, (0, 0))
            per_assembly[name] = (current_lines + len(lines), current_covered + covered)

    for name, (lines, covered) in sorted(per_assembly.items()):
        rate = covered / lines * 100 if lines else 100.0
        print(f"{name}: {covered}/{lines} lines = {rate:.1f}%")

    if total_lines == 0:
        print("No lines found for the target assemblies", file=sys.stderr)
        return 2

    combined = total_covered / total_lines * 100
    print(f"Combined Core+Infrastructure line coverage: {combined:.1f}% (threshold {threshold:.1f}%)")

    if combined < threshold:
        print(f"Coverage {combined:.1f}% is below the {threshold:.1f}% threshold", file=sys.stderr)
        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())
