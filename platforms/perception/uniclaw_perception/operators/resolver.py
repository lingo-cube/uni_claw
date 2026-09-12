"""Specificity-cascade resolver, provenance, and the rule-set linter.

Implements the spec requirement *"Specificity cascade with intersection-scoped
conflict detection"* and the load-time rejection list of *"Governed, diffable
rule assets"* (unknown operators/parameters, out-of-bounds values, unsafe
validator adjustments, dead rules, complexity-budget overruns, equal-
specificity conflicts).

Conflict semantics (normative): a conflict exists ONLY between two rules that
(a) have equal specificity, (b) define the same parameter with different
values, and (c) have selectors with a non-empty reachable intersection, while
(d) no higher-specificity rule whose selector covers that intersection (pins
superset of the pairwise-compatible pin map, tags superset of the union)
defines the same parameter.  Mutually exclusive selectors (two concrete
values pinned on the same dimension, including a concrete value versus
``default``) are NOT conflicts.  File order never affects semantics; all
lint output is deterministically sorted.
"""
from __future__ import annotations

import itertools
from dataclasses import dataclass, field
from typing import Any, Mapping, Sequence

from .contracts import OperatorContract, OperatorRegistry, ParameterSpec, SafeDirection
from .selector import (
    SelectorDimensions,
    FrameContext,
    Rule,
    matches,
    specificity,
)

__all__ = [
    "LintDiagnostic",
    "ResolutionProvenance",
    "ResolvedParams",
    "ResolutionConflictError",
    "lint_rule_set",
    "resolve",
    "DEFAULT_COMPLEXITY_BUDGET",
    "DIAG_INVALID_SELECTOR",
    "DIAG_INVALID_PARAM_KEY",
    "DIAG_DUPLICATE_RULE_ID",
    "DIAG_DEAD_RULE",
    "DIAG_UNKNOWN_PARAMETER",
    "DIAG_OUT_OF_BOUNDS",
    "DIAG_INVALID_ENUM_VALUE",
    "DIAG_VALIDATOR_DISABLE",
    "DIAG_UNSAFE_VALIDATOR_ADJUSTMENT",
    "DIAG_COMPLEXITY_BUDGET",
    "DIAG_SPECIFICITY_CONFLICT",
]

# Diagnostic kinds (stable strings).
DIAG_INVALID_SELECTOR = "invalid_selector"
DIAG_INVALID_PARAM_KEY = "invalid_param_key"
DIAG_DUPLICATE_RULE_ID = "duplicate_rule_id"
DIAG_DEAD_RULE = "dead_rule"
DIAG_UNKNOWN_PARAMETER = "unknown_parameter"
DIAG_OUT_OF_BOUNDS = "out_of_bounds"
DIAG_INVALID_ENUM_VALUE = "invalid_enum_value"
DIAG_VALIDATOR_DISABLE = "validator_disable"
DIAG_UNSAFE_VALIDATOR_ADJUSTMENT = "unsafe_validator_adjustment"
DIAG_COMPLEXITY_BUDGET = "complexity_budget"
DIAG_SPECIFICITY_CONFLICT = "specificity_conflict"

#: Default per-operator non-root rule count budget (spec: "complexity-budget
#: overruns (per-operator active-rule count above the declared budget)").
DEFAULT_COMPLEXITY_BUDGET = 32


@dataclass(frozen=True)
class LintDiagnostic:
    """One load-time rejection with kind and message.

    ``rule_ids`` is an order-independent set of the rules involved (a conflict
    diagnostic carries both rules and any covering-rule hints embedded in the
    message)."""

    kind: str
    message: str
    rule_ids: frozenset[str] = field(default_factory=frozenset)


@dataclass(frozen=True)
class ResolutionProvenance:
    """Provenance of one resolved parameter value (spec: "Every resolved value
    SHALL carry provenance (rule id, pins, specificity)").

    A value that came from the operator contract default (no rule matched)
    carries ``rule_id=None``, empty pins, and specificity 0.
    """

    rule_id: str | None
    pins: Mapping[str, str]
    tags_pins: frozenset[str] = frozenset()
    specificity: int = 0

    @staticmethod
    def contract_default() -> "ResolutionProvenance":
        return ResolutionProvenance(
            rule_id=None, pins={}, tags_pins=frozenset(), specificity=0
        )


@dataclass(frozen=True)
class ResolvedParams:
    """Effective per-operator parameters with per-value provenance."""

    operator_id: str
    values: Mapping[str, Any]
    provenance: Mapping[str, ResolutionProvenance]

    def param(self, name: str) -> Any:
        return self.values[name]


class ResolutionConflictError(ValueError):
    """Two matching rules with equal specificity define the same parameter with
    different values for a concrete context.  Fail-closed: resolution refuses
    to guess.  A linted rule set can never raise this."""


# ---------------------------------------------------------------------------
# Resolution
# ---------------------------------------------------------------------------


def resolve(
    rules: Sequence[Rule],
    context: FrameContext,
    registry: OperatorRegistry,
) -> tuple[ResolvedParams, ...]:
    """Resolve the effective parameters for every registered operator
    (highest version each) against ``context``.

    Deterministic and order-independent: the effective value of a parameter is
    the value of the highest-specificity matching rule; equal-specificity
    ties on the same value pick the lexicographically smallest ``rule_id`` for
    provenance; ties on *different* values raise
    :class:`ResolutionConflictError` (fail-closed).  Defaults (no matching
    rule) carry contract-default provenance.
    """
    result: list[ResolvedParams] = []
    for contract in registry.effective_contracts():
        prefix = contract.operator_id + "."
        defining_rules = [
            rule
            for rule in rules
            if any(key.startswith(prefix) for key in rule.params)
        ]
        values: dict[str, Any] = {}
        provenance: dict[str, ResolutionProvenance] = {}
        for name in sorted(contract.parameters):
            key = prefix + name
            candidates = [
                rule
                for rule in defining_rules
                if key in rule.params and matches(rule, context)
            ]
            if not candidates:
                values[name] = contract.parameters[name].default
                provenance[name] = ResolutionProvenance.contract_default()
                continue
            best = max(specificity(rule) for rule in candidates)
            top = [rule for rule in candidates if specificity(rule) == best]
            distinct_values = {rule.params[key] for rule in top}
            if len(distinct_values) > 1:
                raise ResolutionConflictError(
                    f"parameter {key!r} is ambiguous for context "
                    f"{context.to_mapping()}: rules "
                    f"{', '.join(sorted(r.rule_id for r in top))} match at "
                    f"equal specificity {best} with different values "
                    f"{sorted(repr(v) for v in distinct_values)}; reject rule "
                    f"set (lint first)"
                )
            winner = min(top, key=lambda rule: rule.rule_id)
            values[name] = winner.params[key]
            provenance[name] = ResolutionProvenance(
                rule_id=winner.rule_id,
                pins={dim: value for dim, value in winner.pins.items()},
                tags_pins=winner.tags_pins,
                specificity=best,
            )
        result.append(
            ResolvedParams(operator_id=contract.operator_id, values=values, provenance=provenance)
        )
    return tuple(result)


# ---------------------------------------------------------------------------
# Linter
# ---------------------------------------------------------------------------


def lint_rule_set(
    rules: Sequence[Rule],
    registry: OperatorRegistry,
    complexity_budget: int = DEFAULT_COMPLEXITY_BUDGET,
) -> list[LintDiagnostic]:
    """Lint a rule set against a registry; returns diagnostics (never raises
    for rule-set content; raises ``ValueError`` only for an invalid
    ``complexity_budget`` argument).

    Rejected (each with a diagnostic): malformed selectors, malformed param
    keys, duplicate rule ids, params of unregistered operators (dead rules),
    unknown operator parameters, out-of-bounds / invalid-enum values, VALIDATOR
    disable attempts, tighten-only VALIDATOR parameters moved below their
    default, per-operator complexity-budget overruns, and intersection-scoped
    equal-specificity conflicts.  Output is sorted by (kind, message) so it is
    independent of rule file order.
    """
    diagnostics: list[LintDiagnostic] = []
    ordered = list(rules)

    _lint_selectors(ordered, diagnostics)
    _lint_duplicate_ids(ordered, diagnostics)
    namespace: dict[str, OperatorContract] = _namespace_lookup(registry)

    for rule in ordered:
        for key in sorted(rule.params):
            _lint_param_binding(rule, key, namespace, diagnostics)

    _lint_complexity(ordered, namespace, complexity_budget, diagnostics)
    _lint_specificity_conflicts(ordered, diagnostics)
    return sorted(diagnostics, key=lambda d: (d.kind, d.message))


def _lint_selectors(rules: Sequence[Rule], diagnostics: list[LintDiagnostic]) -> None:
    for rule in rules:
        for dim in sorted(rule.pins):
            if dim not in SelectorDimensions.ALL:
                diagnostics.append(
                    LintDiagnostic(
                        kind=DIAG_INVALID_SELECTOR,
                        message=(
                            f"rule {rule.rule_id!r} pins unknown selector "
                            f"dimension {dim!r}; canonical dimensions are "
                            f"{', '.join(SelectorDimensions.ALL)}; canonical "
                            f"representations (api-<N>, package names, "
                            f"hardware models, extra modes as tags) are the "
                            f"caller's responsibility"
                        ),
                        rule_ids=frozenset({rule.rule_id}),
                    )
                )
        for tag in sorted(rule.tags_pins):
            if _TAG_SEPARATOR not in tag or tag.startswith(_TAG_SEPARATOR) or tag.endswith(_TAG_SEPARATOR):
                diagnostics.append(
                    LintDiagnostic(
                        kind=DIAG_INVALID_SELECTOR,
                        message=(
                            f"rule {rule.rule_id!r} has malformed tag pin "
                            f"{tag!r}; tags must be 'key=value' strings"
                        ),
                        rule_ids=frozenset({rule.rule_id}),
                    )
                )


_TAG_SEPARATOR = "="


def _lint_duplicate_ids(rules: Sequence[Rule], diagnostics: list[LintDiagnostic]) -> None:
    seen: dict[str, list[str]] = {}
    for rule in rules:
        seen.setdefault(rule.rule_id, []).append(rule.rule_id)
    for rule_id, occurrences in seen.items():
        if len(occurrences) > 1:
            diagnostics.append(
                LintDiagnostic(
                    kind=DIAG_DUPLICATE_RULE_ID,
                    message=(
                        f"rule id {rule_id!r} is declared {len(occurrences)} "
                        f"times; rule ids must be unique for stable "
                        f"serialization"
                    ),
                    rule_ids=frozenset({rule_id}),
                )
            )


def _namespace_lookup(registry: OperatorRegistry) -> dict[str, OperatorContract]:
    """Map operator id → highest-version contract (the binding target of
    unversioned ``operatorId.param`` rule keys)."""
    return {contract.operator_id: contract for contract in registry.effective_contracts()}


def _lint_param_binding(
    rule: Rule,
    key: str,
    namespace: Mapping[str, OperatorContract],
    diagnostics: list[LintDiagnostic],
) -> None:
    operator_id, param_name = key.split(".", maxsplit=1)
    contract = namespace.get(operator_id)
    if contract is None:
        # Dead rule: rule binds a parameter of an operator that is not
        # registered (spec: dead rules — "params for unregistered operators").
        diagnostics.append(
            LintDiagnostic(
                kind=DIAG_DEAD_RULE,
                message=(
                    f"rule {rule.rule_id!r} binds {key!r} for unregistered "
                    f"operator {operator_id!r}; the rule can never take effect"
                ),
                rule_ids=frozenset({rule.rule_id}),
            )
        )
        return
    spec = contract.parameter(param_name)
    if spec is None:
        if (
            param_name == "enabled"
            and contract.authority.value != "GENERATOR"
        ):
            diagnostics.append(
                LintDiagnostic(
                    kind=DIAG_VALIDATOR_DISABLE,
                    message=(
                        f"rule {rule.rule_id!r} attempts to disable "
                        f"{operator_id!r} via 'enabled'; {contract.authority.value} "
                        f"operators cannot be disabled by configuration (spec: "
                        f"'Validators cannot be bypassed by configuration')"
                    ),
                    rule_ids=frozenset({rule.rule_id}),
                )
            )
            return
        diagnostics.append(
            LintDiagnostic(
                kind=DIAG_UNKNOWN_PARAMETER,
                message=(
                    f"rule {rule.rule_id!r} binds unknown parameter "
                    f"{param_name!r} of operator {operator_id!r}; declared "
                    f"parameters: {', '.join(sorted(contract.parameters))}"
                ),
                rule_ids=frozenset({rule.rule_id}),
            )
        )
        return
    _lint_param_value(rule, key, spec, contract, diagnostics)


def _lint_param_value(
    rule: Rule,
    key: str,
    spec: ParameterSpec,
    contract: OperatorContract,
    diagnostics: list[LintDiagnostic],
) -> None:
    value = rule.params[key]
    violation = spec.violation(value)
    if violation is not None:
        kind, detail = violation
        if kind == "type_mismatch":
            kind = DIAG_OUT_OF_BOUNDS
        diagnostics.append(
            LintDiagnostic(
                kind=kind,  # out_of_bounds | invalid_enum_value
                message=(
                    f"rule {rule.rule_id!r} binds {key!r} = {value!r}: {detail}"
                ),
                rule_ids=frozenset({rule.rule_id}),
            )
        )
        return
    if spec.safe_direction is SafeDirection.TIGHTEN_ONLY:
        if _is_number(value) and _is_number(spec.default) and value < spec.default:
            diagnostics.append(
                LintDiagnostic(
                    kind=DIAG_UNSAFE_VALIDATOR_ADJUSTMENT,
                    message=(
                        f"rule {rule.rule_id!r} loosens tighten-only VALIDATOR "
                        f"parameter {key!r} to {value!r} (default "
                        f"{spec.default!r}); values below the default are "
                        f"rejected (spec: 'a tighten_only param may only move "
                        f"in its declared tightening direction')"
                    ),
                    rule_ids=frozenset({rule.rule_id}),
                )
            )


def _is_number(value: Any) -> bool:
    return isinstance(value, (int, float)) and not isinstance(value, bool)


def _lint_complexity(
    rules: Sequence[Rule],
    namespace: Mapping[str, OperatorContract],
    budget: int,
    diagnostics: list[LintDiagnostic],
) -> None:
    """Per-operator non-root rule count over the declared budget.

    A non-root rule is a rule with at least one pin or tag pin (the root rule
    — zero pins, zero tags, matching everything — holds the defaults and does
    not count against the budget)."""
    if budget < 0:
        raise ValueError("complexity budget must be non-negative")
    counts: dict[str, int] = {}
    for rule in rules:
        if not rule.pins and not rule.tags_pins:
            continue  # root rule
        operators: set[str] = set()
        for key in rule.params:
            operator_id = key.split(".", maxsplit=1)[0]
            if operator_id in namespace:
                operators.add(operator_id)
        for operator_id in operators:
            counts[operator_id] = counts.get(operator_id, 0) + 1
    for operator_id in sorted(counts):
        if counts[operator_id] > budget:
            diagnostics.append(
                LintDiagnostic(
                    kind=DIAG_COMPLEXITY_BUDGET,
                    message=(
                        f"operator {operator_id!r} has {counts[operator_id]} "
                        f"non-root rules exceeding the budget of {budget}; "
                        f"split by dimension or deduplicate"
                    ),
                    rule_ids=frozenset(),
                )
            )


def _lint_specificity_conflicts(
    rules: Sequence[Rule],
    diagnostics: list[LintDiagnostic],
) -> None:
    """Intersection-scoped equal-specificity conflict detection.

    Algorithm (normative, order-independent): for every parameter key, compare
    every pair of rules that define it at equal specificity with different
    values.  The pair's selector intersection is reachable iff no dimension is
    pinned to *different* concrete values by both rules (mutually exclusive);
    tags never empty the intersection (free-form sets).  If reachable and no
    higher-specificity rule defines the same parameter whose pins are a
    superset of the pairwise-compatible pin map (with equal values) and whose
    tags are a superset of the union of the pair's tag pins, emit a conflict.
    The detection is exact over validated selectors; anything not provable
    would be rejected conservatively (fail-closed direction).
    """
    by_param: dict[str, list[Rule]] = {}
    for rule in rules:
        for key in rule.params:
            by_param.setdefault(key, []).append(rule)

    for key, defining in sorted(by_param.items()):
        for left, right in itertools.combinations(defining, 2):
            if specificity(left) != specificity(right):
                continue
            if left.params[key] == right.params[key]:
                continue
            map_items, exclusive = _intersection(left, right)
            if exclusive:
                continue
            if _covered_by_higher_specificity(left, right, key, map_items, rules):
                continue
            # Present the pair in deterministic order (independent of input
            # order) so conflict diagnostics are permutation-stable.
            if right.rule_id < left.rule_id:
                left, right = right, left
            suggested = ", ".join(
                f"{dim}={value}" for dim, value in sorted(map_items)
            )
            diagnostics.append(
                LintDiagnostic(
                    kind=DIAG_SPECIFICITY_CONFLICT,
                    message=(
                        f"rules {left.rule_id!r} and {right.rule_id!r} conflict "
                        f"on {key!r} (values {left.params[key]!r} vs "
                        f"{right.params[key]!r}): equal specificity "
                        f"{specificity(left)} with reachable selector "
                        f"intersection {{{suggested}}}; add an explicit "
                        f"higher-specificity rule pinning {suggested} (plus any "
                        f"tags of both rules) defining {key!r}, or deduplicate"
                    ),
                    rule_ids=frozenset({left.rule_id, right.rule_id}),
                )
            )


def _intersection(left: Rule, right: Rule) -> tuple[list[tuple[str, str]], bool]:
    """Pairwise-compatible pin map of the two selectors and whether the
    intersection is empty (mutually exclusive).

    Per-dim: a dim pinned by both with the same value is compatible (value
    retained); pinned by both with different values → mutually exclusive
    (empty intersection); pinned by one only → compatible (value retained).
    """
    compatible: dict[str, str] = {}
    for dim, value in left.pins.items():
        compatible[dim] = value
    for dim, value in right.pins.items():
        if dim in compatible:
            if compatible[dim] != value:
                return [], True
        else:
            compatible[dim] = value
    return sorted(compatible.items()), False


def _covered_by_higher_specificity(
    left: Rule,
    right: Rule,
    key: str,
    intersection_items: Sequence[tuple[str, str]],
    rules: Sequence[Rule],
) -> bool:
    """True iff a higher-specificity rule's selector covers the pair's
    reachable intersection and defines the same parameter (spec condition (d)):

    pins ⊇ pairwise-compatible pin map (equal values) and tags ⊇ union of the
    pair's tag pins, with ``specificity > pair``."""
    pair_specificity = specificity(left)
    union_tags = left.tags_pins | right.tags_pins
    for candidate in rules:
        if specificity(candidate) <= pair_specificity:
            continue
        if key not in candidate.params:
            continue
        if union_tags - candidate.tags_pins:
            continue
        if all(
            dim in candidate.pins and candidate.pins[dim] == value
            for dim, value in intersection_items
        ):
            return True
    return False