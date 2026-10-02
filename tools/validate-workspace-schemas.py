#!/usr/bin/env python3
"""Deterministic validation for the language-neutral Workspace contract."""
from __future__ import annotations

import json
import sys
from pathlib import Path

from jsonschema import Draft202012Validator, FormatChecker, RefResolver

ROOT = Path(__file__).resolve().parents[1]
SCHEMA_ROOT = ROOT / "schemas" / "workspace"


def load(path: Path):
    with path.open(encoding="utf-8") as handle:
        return json.load(handle)


def main() -> int:
    schema_paths = sorted(SCHEMA_ROOT.glob("*.schema.json"))
    if not schema_paths:
        print("ERROR schemas/workspace: no schemas found")
        return 1
    schemas = {path.name: load(path) for path in schema_paths}
    validators = {}
    store = {
        schema.get("$id", name): schema
        for name, schema in schemas.items()
    }
    store.update(schemas)
    for path in schema_paths:
        schema = schemas[path.name]
        try:
            Draft202012Validator.check_schema(schema)
            validators[path.stem] = Draft202012Validator(
                schema,
                resolver=RefResolver.from_schema(schema, store=store),
                format_checker=FormatChecker(),
            )
        except Exception as exc:
            print(f"ERROR {path.relative_to(ROOT)} $: invalid schema: {exc}")
            return 1

    errors = 0
    for directory, should_pass in ((SCHEMA_ROOT / "examples" / "valid", True), (SCHEMA_ROOT / "examples" / "invalid", False)):
        for path in sorted(directory.glob("*.json")):
            try:
                instance = load(path)
            except Exception as exc:
                print(f"ERROR {path.relative_to(ROOT)} $: invalid JSON: {exc}")
                errors += 1
                continue
            matched = []
            for name, validator in validators.items():
                found = sorted(validator.iter_errors(instance), key=lambda error: list(error.absolute_path))
                if not found:
                    matched.append(name)
            if should_pass and not matched:
                print(f"ERROR {path.relative_to(ROOT)} $: positive example matched no schema")
                errors += 1
            elif not should_pass and matched:
                print(f"ERROR {path.relative_to(ROOT)} $: negative example unexpectedly matched {matched}")
                errors += 1
            elif not should_pass:
                # Emit a stable first diagnostic so failures cannot silently pass.
                diagnostics = []
                for name, validator in validators.items():
                    for error in validator.iter_errors(instance):
                        location = ".".join(str(item) for item in error.absolute_path) or "$"
                        diagnostics.append(f"{name} {location}: {error.message}")
                print(f"OK {path.relative_to(ROOT)} rejected ({sorted(diagnostics)[0]})")
            else:
                print(f"OK {path.relative_to(ROOT)} -> {matched[0]}")

    if errors:
        print(f"FAILED {errors} validation error(s)")
        return 1
    print(f"PASSED {len(schema_paths)} schema(s) and examples")
    return 0


if __name__ == "__main__":
    sys.exit(main())
