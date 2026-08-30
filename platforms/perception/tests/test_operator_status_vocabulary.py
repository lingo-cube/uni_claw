"""Closed operator status vocabulary guard.

Every decision/verdict ``status`` emitted by a composition operator must be a
member of the frozen closed set in ``uniclaw_perception.operators.status``.

This AST-based guard scans every operator module for dict-literal
``"status"`` keys whose value is a string literal and rejects any value
outside the set, so no free-form status string can enter the deterministic
pipeline trace without explicitly extending the vocabulary (a mechanical
guard mirroring the closed-vocabulary principle discussed in
``docs/analysis/runtime-stability-engineering-landscape.md`` §13).
"""
from __future__ import annotations

import ast
from pathlib import Path

import pytest

from uniclaw_perception.operators import status as status_module

_OPERATORS_DIR = Path(status_module.__file__).resolve().parent
_STATUS_KEY = "status"


def _literal_status_values(module_path: Path) -> list[tuple[int, str]]:
    """All (line, literal) pairs for dict literals ``{"status": "<str>"}``."""
    tree = ast.parse(module_path.read_text(encoding="utf-8"), filename=str(module_path))
    found: list[tuple[int, str]] = []
    for node in ast.walk(tree):
        if not isinstance(node, ast.Dict):
            continue
        for key, value in zip(node.keys, node.values):
            if (
                isinstance(key, ast.Constant)
                and key.value == _STATUS_KEY
                and isinstance(value, ast.Constant)
                and isinstance(value.value, str)
            ):
                found.append((node.lineno, value.value))
    return found


def test_every_operator_module_uses_only_closed_status_literals():
    outliers: list[tuple[str, int, str]] = []
    for module_path in sorted(_OPERATORS_DIR.glob("*.py")):
        if module_path.name == "status.py":
            continue
        for line, value in _literal_status_values(module_path):
            if value not in status_module.ALL:
                outliers.append((module_path.name, line, value))
    assert not outliers, (
        "status literals outside the closed vocabulary: "
        + ", ".join(f"{name}:{line}={value!r}" for name, line, value in outliers)
        + f"; closed set: {sorted(status_module.ALL)}"
    )


def test_validate_status_accepts_the_closed_set():
    for value in sorted(status_module.ALL):
        assert status_module.validate_status(value) == value


@pytest.mark.parametrize("unknown", ["", "activated ", "MATCH", "partial", "yes", "1"])
def test_validate_status_rejects_unknown(unknown: str) -> None:
    with pytest.raises(ValueError):
        status_module.validate_status(unknown)