"""Deterministic serialization and loading of cascading-rule assets.

Implements the spec requirement *"Governed, diffable rule assets"*: the active
rule set serializes to a deterministic, human-readable, diffable format with a
schema version, stable key order and NO timestamps or machine paths; a
loader/linter rejects unknown fields (strict deserialize), unknown
operators/parameters, out-of-bounds values, unsafe validator adjustments, dead
rules, complexity-budget overruns and equal-specificity conflicts.

Round-trip guarantee: ``serialize(deserialize(text))`` is byte-identical to
``text`` for any text produced by :func:`serialize_rule_set`.

Serialized shape (schemaVersion 1)::

    {
      "schemaVersion": 1,
      "rules": [
        {"ruleId": "...", "pins": {"system": "android"},
         "tags": ["display=triple-screen"], "params": {"op.minAnchors": 4}}
      ]
    }

Rules are sorted by ``ruleId``; all object keys are sorted.  UTF-8, no BOM, no
timestamps, no paths.
"""
from __future__ import annotations

import json
from dataclasses import dataclass
from typing import Any, Sequence

from .resolver import LintDiagnostic, lint_rule_set
from .selector import Rule

__all__ = [
    "SCHEMA_VERSION",
    "serialize_rule_set",
    "deserialize_rule_set",
    "load_rule_set",
    "RuleSetLoad",
]

SCHEMA_VERSION = 1

_TOP_LEVEL_FIELDS = frozenset({"schemaVersion", "rules"})
_RULE_FIELDS = frozenset({"ruleId", "pins", "tags", "params"})


def serialize_rule_set(rules: Sequence[Rule]) -> str:
    """Serialize a rule set to deterministic, diffable JSON text.

    Rules are sorted by ``rule_id``; pins/params/tags keys are sorted; a
    trailing newline terminates the document.  The result is byte-stable
    across rule input order and across serialize/deserialize round trips.
    """
    ordered = sorted(rules, key=lambda rule: rule.rule_id)
    payload = {
        "schemaVersion": SCHEMA_VERSION,
        "rules": [_rule_to_json(rule) for rule in ordered],
    }
    return json.dumps(payload, indent=2, ensure_ascii=False) + "\n"


def _rule_to_json(rule: Rule) -> dict[str, Any]:
    return {
        "ruleId": rule.rule_id,
        "pins": dict(sorted(rule.pins.items())),
        "tags": sorted(rule.tags_pins),
        "params": dict(sorted(rule.params.items())),
    }


def deserialize_rule_set(text: str) -> list[Rule]:
    """Strictly parse rule-set JSON text into rules.

    Strict means: unknown top-level or rule fields are rejected
    (``ValueError``), ``schemaVersion`` must equal 1, and container shapes
    must match.  Value-level semantics (bounds, conflicts, dead rules) are the
    linter's job — run :func:`load_rule_set` to parse *and* lint.
    """
    try:
        payload = json.loads(text)
    except json.JSONDecodeError as error:
        raise ValueError(f"rule set is not valid JSON: {error}") from None
    if not isinstance(payload, dict):
        raise ValueError("rule set root must be a JSON object")
    unknown = set(payload) - _TOP_LEVEL_FIELDS
    if unknown:
        raise ValueError(f"unknown top-level field(s): {', '.join(sorted(unknown))}")
    if payload.get("schemaVersion") != SCHEMA_VERSION:
        raise ValueError(
            f"unsupported schemaVersion {payload.get('schemaVersion')!r}; "
            f"expected {SCHEMA_VERSION}"
        )
    if "rules" not in payload:
        raise ValueError("rule set is missing the required 'rules' array")
    raw_rules = payload["rules"]
    if not isinstance(raw_rules, list):
        raise ValueError("'rules' must be a JSON array")
    rules: list[Rule] = []
    for index, raw in enumerate(raw_rules):
        if not isinstance(raw, dict):
            raise ValueError(f"rule #{index} must be a JSON object")
        unknown = set(raw) - _RULE_FIELDS
        if unknown:
            raise ValueError(
                f"rule #{index} has unknown field(s): {', '.join(sorted(unknown))}"
            )
        rule_id = raw.get("ruleId")
        if not isinstance(rule_id, str) or not rule_id:
            raise ValueError(f"rule #{index} must have a non-empty string 'ruleId'")
        pins = raw.get("pins", {})
        if not isinstance(pins, dict):
            raise ValueError(f"rule {rule_id!r}: 'pins' must be an object")
        for dim, value in pins.items():
            if not isinstance(value, str):
                raise ValueError(
                    f"rule {rule_id!r}: pin {dim!r} must have a string value"
                )
        tags = raw.get("tags", [])
        if not isinstance(tags, list) or not all(isinstance(t, str) for t in tags):
            raise ValueError(f"rule {rule_id!r}: 'tags' must be an array of strings")
        params = raw.get("params", {})
        if not isinstance(params, dict):
            raise ValueError(f"rule {rule_id!r}: 'params' must be an object")
        rules.append(
            Rule(
                rule_id=rule_id,
                pins=dict(pins),
                tags_pins=frozenset(tags),
                params=dict(params),
            )
        )
    return rules


@dataclass(frozen=True)
class RuleSetLoad:
    """Result of loading a rule set: parsed rules plus linter diagnostics.

    ``is_valid`` is True iff the rule set is loadable (no diagnostics, no
    structural errors).  A rule set with diagnostics must not enter runtime;
    unpromoted candidate sets never resolve (spec: "Unpromoted candidate
    rules never run").
    """

    rules: tuple[Rule, ...]
    diagnostics: tuple[LintDiagnostic, ...]

    @property
    def is_valid(self) -> bool:
        return not self.diagnostics


def load_rule_set(
    text: str,
    registry: Any,
    complexity_budget: int = 32,
) -> RuleSetLoad:
    """Parse strictly and lint: the loader/linter entry point.

    Raises ``ValueError`` for structurally invalid JSON/unknown fields;
    returns :class:`RuleSetLoad` whose ``diagnostics`` carry every semantic
    rejection (unknown operators/params, bounds, validator safety, dead rules,
    complexity budget, conflicts).
    """
    from .contracts import OperatorRegistry  # local import: avoid cycle

    if not isinstance(registry, OperatorRegistry):
        raise TypeError("load_rule_set requires an OperatorRegistry")
    rules = deserialize_rule_set(text)
    diagnostics = lint_rule_set(rules, registry, complexity_budget=complexity_budget)
    return RuleSetLoad(rules=tuple(rules), diagnostics=tuple(diagnostics))