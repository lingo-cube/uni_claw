#!/usr/bin/env python3
"""Render the Profile charter as product/prompt/.../01-identity.txt."""

from __future__ import annotations

import argparse
import re
from pathlib import Path


FIELDS = ("responsibilities", "boundaries", "planningPrinciples", "skillUse")


def read_charter(path: Path) -> dict[str, object]:
    lines = path.read_text(encoding="utf-8").splitlines()
    in_charter = False
    current_list: str | None = None
    charter: dict[str, object] = {}
    for line in lines:
        if not in_charter:
            if line == "charter:":
                in_charter = True
            continue
        if line and not line.startswith(" "):
            break
        match = re.fullmatch(r"  ([A-Za-z][A-Za-z0-9]*):(?:\s*(.*))?", line)
        if match:
            key, value = match.groups()
            if key == "role":
                charter[key] = (value or "").strip().strip("\"'")
                current_list = None
            elif key in FIELDS:
                current_list = key
                charter[key] = []
            else:
                raise ValueError(f"unsupported charter field: {key}")
            continue
        item = re.fullmatch(r"    -\s+(.+)", line)
        if item and current_list:
            values = charter[current_list]
            assert isinstance(values, list)
            values.append(item.group(1).strip().strip("\"'"))
            continue
        if line.strip():
            raise ValueError(f"unsupported charter syntax: {line}")
    if not in_charter:
        raise ValueError("profile has no charter")
    if not charter.get("role") or any(not charter.get(field) for field in FIELDS):
        raise ValueError("charter role and all four non-empty lists are required")
    return charter


def render(charter: dict[str, object]) -> str:
    output = ["# UniAgent", f"Role: {charter['role']}"]
    headings = (
        ("responsibilities", "Responsibilities"),
        ("boundaries", "Boundaries"),
        ("planningPrinciples", "Planning principles"),
        ("skillUse", "Skill use"),
    )
    for field, heading in headings:
        output.append(f"## {heading}")
        output.extend(f"- {item}" for item in charter[field])
    return "\n".join(output) + "\n"


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("profile", type=Path, help="canonical UniAgent Profile YAML")
    parser.add_argument("output", type=Path, nargs="?", help="identity segment (default: stdout)")
    args = parser.parse_args()
    content = render(read_charter(args.profile))
    if args.output:
        with args.output.open("w", encoding="utf-8", newline="\n") as output:
            output.write(content)
    else:
        print(content, end="")


if __name__ == "__main__":
    main()
