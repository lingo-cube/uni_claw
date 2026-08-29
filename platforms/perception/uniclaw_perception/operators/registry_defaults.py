"""Default operator registry, pipeline topology, and root rule set (S1B).

Implements the OpenSpec change ``perception-operator-rule-framework`` S1
wiring slice: registers the retained candidate's port as the
``uniform-list-row-grouping`` GENERATOR and the new ``spacing-verifier``
VALIDATOR, declares the code-owned topology
``[uniform-list-row-grouping, spacing-verifier]`` (spec: "Pipeline topology is
code-owned; rules parameterize only"), and provides the root-only default rule
set whose resolved values equal the operator contract defaults — i.e. the
retained candidate's current constants verbatim (spec IR-G0 unblock slices:
"root-rule defaults equal to the current candidate values").

S2i registered the deterministic ``row-relation-head`` GENERATOR (frozen raw
inputs, spec S2.1) in the registry — with its root default rule, so the
default rule set resolves its bounded parameters with provenance.  S2ii wires
it: the declared topology appends ``row-relation-head`` between
``uniform-list-row-grouping`` and ``spacing-verifier``, and ``RUNNERS`` maps it
to the frozen-input runner adapter (``relation_head_router``), the operator
framework's single code-owned routing point: ≥ ``minAnchors`` confirmed anchors
delegate to the uniform-list path (candidates byte-untouched), below the floor
the adapter composes from the engine's raw visual regions.

S4 (WI-PFW-S4) registers three non-generating operators for contract
completeness: ``text-relation-check`` and ``structured-corroboration``
(VALIDATORs, appended to the executed topology AFTER ``spacing-verifier``;
both annotate-only on current corpus output — zero-veto gate, equivalence
candidates byte-untouched) and ``vlm-annotation`` (ADVISOR, ``enabled``
default ``False``, deliberately NOT in the pipeline and NOT in ``RUNNERS`` —
offline interface only, no online call path).

The fusion engine executes the pipeline through this module:
:data:`REGISTRY` (contracts + declared order), :data:`DEFAULT_RULE_SET`
(root rules pinning every parameter to its contract default), and
:data:`DEFAULT_CONTEXT` (all selector dimensions default, no tags — the S1
context; S1C/server-header wiring supplies a real context later, no C# change).
"""
from __future__ import annotations

from typing import Any, Mapping

from .contracts import (
    NumericBounds,
    OperatorAuthority,
    OperatorContract,
    OperatorRegistry,
    ParameterSpec,
    ParameterType,
    SafeDirection,
)
from .selector import FrameContext, Rule
from .relation_head_router import run_row_relation_head_routed
from .row_relation_head import (
    ROW_RELATION_HEAD_PARAM_BOUNDS,
    ROW_RELATION_HEAD_PARAM_DEFAULTS,
)
from .spacing_verifier import VERIFIER_PARAM_BOUNDS, VERIFIER_PARAM_DEFAULTS, verify
from .structured_corroboration import (
    CORROBORATION_PARAM_BOUNDS as STRUCTURED_PARAM_BOUNDS,
)
from .structured_corroboration import (
    CORROBORATION_PARAM_DEFAULTS as STRUCTURED_PARAM_DEFAULTS,
)
from .structured_corroboration import run as run_structured_corroboration
from .text_relation_check import (
    TEXT_RELATION_PARAM_BOUNDS as TEXT_RELATION_BOUNDS,
)
from .text_relation_check import (
    TEXT_RELATION_PARAM_DEFAULTS as TEXT_RELATION_DEFAULTS,
)
from .text_relation_check import run as run_text_relation_check
from .uniform_list_row_grouping import (
    GROUPING_PARAM_BOUNDS,
    GROUPING_PARAM_DEFAULTS,
    run as run_uniform_list_row_grouping,
)
from .vlm_annotation import VLM_ENABLED_DEFAULT

__all__ = [
    "REGISTRY",
    "RUNNERS",
    "DEFAULT_CONTEXT",
    "DEFAULT_RULE_SET",
    "default_rule_set",
]

# ---------------------------------------------------------------------------
# Operator contracts (defaults = the retained candidate's current constants).
# ---------------------------------------------------------------------------


def _numeric_spec(
    name: str,
    default: Any,
    bounds: tuple[float, float],
    param_type: ParameterType,
    safe_direction: SafeDirection | None = None,
) -> ParameterSpec:
    return ParameterSpec(
        name,
        param_type,
        default,
        NumericBounds(bounds[0], bounds[1]),
        safe_direction=safe_direction,
    )


def _grouping_parameter_specs() -> dict[str, ParameterSpec]:
    specs: dict[str, ParameterSpec] = {}
    for name in sorted(GROUPING_PARAM_DEFAULTS):
        default = GROUPING_PARAM_DEFAULTS[name]
        kind, bounds = GROUPING_PARAM_BOUNDS[name]
        param_type = ParameterType.INTEGER if kind is int else ParameterType.FLOAT
        specs[name] = _numeric_spec(name, default, bounds, param_type)
    return specs


def _verifier_parameter_specs() -> dict[str, ParameterSpec]:
    specs: dict[str, ParameterSpec] = {}
    for name in sorted(VERIFIER_PARAM_DEFAULTS):
        default = VERIFIER_PARAM_DEFAULTS[name]
        kind, bounds = VERIFIER_PARAM_BOUNDS[name]
        param_type = ParameterType.INTEGER if kind is int else ParameterType.FLOAT
        # VALIDATOR parameters are tighten_only: a rule may only move them in
        # their declared tightening direction (never below the default).
        specs[name] = _numeric_spec(
            name, default, bounds, param_type, SafeDirection.TIGHTEN_ONLY
        )
    return specs


def _row_relation_head_parameter_specs() -> dict[str, ParameterSpec]:
    specs: dict[str, ParameterSpec] = {}
    for name in sorted(ROW_RELATION_HEAD_PARAM_DEFAULTS):
        default = ROW_RELATION_HEAD_PARAM_DEFAULTS[name]
        kind, bounds = ROW_RELATION_HEAD_PARAM_BOUNDS[name]
        param_type = ParameterType.INTEGER if kind is int else ParameterType.FLOAT
        # GENERATOR parameters move both ways (no safe direction).
        specs[name] = _numeric_spec(name, default, bounds, param_type)
    return specs


def _text_relation_check_parameter_specs() -> dict[str, ParameterSpec]:
    """Bounded VALIDATOR params (tighten_only: a rule may only move them in
    their declared tightening direction, never below the default)."""
    specs: dict[str, ParameterSpec] = {}
    for name in sorted(TEXT_RELATION_DEFAULTS):
        default = TEXT_RELATION_DEFAULTS[name]
        kind, bounds = TEXT_RELATION_BOUNDS[name]
        param_type = ParameterType.INTEGER if kind is int else ParameterType.FLOAT
        specs[name] = _numeric_spec(
            name, default, bounds, param_type, SafeDirection.TIGHTEN_ONLY
        )
    return specs


def _structured_corroboration_parameter_specs() -> dict[str, ParameterSpec]:
    """Bounded VALIDATOR params (tighten_only)."""
    specs: dict[str, ParameterSpec] = {}
    for name in sorted(STRUCTURED_PARAM_DEFAULTS):
        default = STRUCTURED_PARAM_DEFAULTS[name]
        kind, bounds = STRUCTURED_PARAM_BOUNDS[name]
        param_type = ParameterType.INTEGER if kind is int else ParameterType.FLOAT
        specs[name] = _numeric_spec(
            name, default, bounds, param_type, SafeDirection.TIGHTEN_ONLY
        )
    return specs


REGISTRY = OperatorRegistry()
REGISTRY.register(
    OperatorContract(
        operator_id="uniform-list-row-grouping",
        version="1.0.0",
        authority=OperatorAuthority.GENERATOR,
        input_kinds=frozenset({"candidate", "detection", "ocr"}),
        output_kind="row_group",
        parameters=_grouping_parameter_specs(),
        fail_closed_description=(
            "activates only from minAnchors or more confirmed anchors with a "
            "proven uniform cadence and column; never guesses a row (spec "
            "IR-G0 unblock slices: root defaults equal the retained candidate "
            "constants)"
        ),
    )
)
REGISTRY.register(
    OperatorContract(
        operator_id="spacing-verifier",
        version="1.0.0",
        authority=OperatorAuthority.VALIDATOR,
        input_kinds=frozenset({"row_group"}),
        output_kind="verdict",
        parameters=_verifier_parameter_specs(),
        fail_closed_description=(
            "verifies generated row-group structure (same-column alignment, "
            "vertical adjacency/containment, cap/provenance compliance) and "
            "vetoes on violation; cannot be disabled by configuration; "
            "accepts every output the retained candidate produces"
        ),
    )
)
# S2.1 (WI-PFW-S2i): register the deterministic row-relation-head GENERATOR
# (frozen raw inputs per spec: uncombined detector boxes + OCR text blocks +
# derived pairwise geometry; never established row groups).  Registered in S2i
# REGISTER-ONLY; S2ii wires it into the executed topology (declared above, with
# the runner adapter in RUNNERS).
REGISTRY.register(
    OperatorContract(
        operator_id="row-relation-head",
        version="1.0.0",
        authority=OperatorAuthority.GENERATOR,
        input_kinds=frozenset({"detection", "ocr"}),
        output_kind="row_group_proposal",
        parameters=_row_relation_head_parameter_specs(),
        fail_closed_description=(
            "composes one navigation candidate per confidently elected band "
            "head from raw visual regions only; captions/icons/toggles are "
            "absorbed as NonInteractive satellites; fails closed (no "
            "candidate) on ambiguous ties, subtitle-continuation geometry, "
            "or any missing confidence (spec S2.1 frozen inputs; "
            "row-group proposal must pass spacing-verifier once routed)"
        ),
    )
)
# S4 (WI-PFW-S4): register the two new non-generating VALIDATORs and the
# offline-only ADVISOR for contract completeness.  text-relation-check
# conflict-checks composed head texts (structural anomalies only) and may
# only veto / downgrade confidence — it never generates candidates.
# structured-corroboration cross-checks composed rows against an OPTIONAL
# uiautomator-style structured tier (absent channel ⇒ trivial pass; XML is
# never an identity source).  vlm-annotation is the OFFLINE ADVISOR (enabled
# default False per the leader-frozen authority rule "ADVISOR may carry
# enabled, default false") with NO runner and NO pipeline slot — VLM use in
# the authorization path is FORBIDDEN; no online call path exists.
REGISTRY.register(
    OperatorContract(
        operator_id="text-relation-check",
        version="1.0.0",
        authority=OperatorAuthority.VALIDATOR,
        input_kinds=frozenset({"row_group", "ocr"}),
        output_kind="verdict",
        parameters=_text_relation_check_parameter_specs(),
        fail_closed_description=(
            "conflict-checks composed head texts (empty/too-short head text, "
            "verbatim duplicate head text at the same position) and may only "
            "veto or downgrade confidence — never generates candidates; "
            "cannot be disabled by configuration; annotate-only on current "
            "corpus output (zero-veto gate)"
        ),
    )
)
REGISTRY.register(
    OperatorContract(
        operator_id="structured-corroboration",
        version="1.0.0",
        authority=OperatorAuthority.VALIDATOR,
        input_kinds=frozenset({"row_group", "structured"}),
        output_kind="verdict",
        parameters=_structured_corroboration_parameter_specs(),
        fail_closed_description=(
            "cross-checks composed rows against the optional structured "
            "(uiautomator-style) tier; XML is auxiliary corroboration only — "
            "downgrades/annotates, vetoes only on a strong contradiction in a "
            "fully-available text-bearing region; absent tier passes "
            "trivially; cannot be disabled by configuration"
        ),
    )
)
REGISTRY.register(
    OperatorContract(
        operator_id="vlm-annotation",
        version="1.0.0",
        authority=OperatorAuthority.ADVISOR,
        input_kinds=frozenset({"row_group", "frames"}),
        output_kind="annotation",
        parameters={
            "enabled": ParameterSpec(
                "enabled", ParameterType.BOOLEAN, VLM_ENABLED_DEFAULT
            ),
        },
        fail_closed_description=(
            "offline annotation / parameter-adjustment interface only (S4: "
            "deterministic no-op stub; S5 deferred behind a separate post-S2 "
            "decision); enabled default False, never in the executed "
            "pipeline, no runner — no online call path"
        ),
    )
)
#: Code-owned topology: every GENERATOR output passes every applicable
#: VALIDATOR (spec: pipeline topology is code-owned; rules parameterize
#: only).  S2ii appends the row-relation-head routing between the S1 pair —
#: the adapter noops (delegated) whenever the uniform-list generator owns the
#: frame (≥ minAnchors confirmed anchors), so the ≥4-anchor path stays
#: byte-identical.  S4 appends the two non-generating VALIDATORs AFTER
#: spacing-verifier: both annotate-only on current corpus output (zero-veto
#: gate), so the equivalence candidates stay byte-untouched; vlm-annotation
#: is deliberately NOT declared here.
REGISTRY.declare_pipeline([
    "uniform-list-row-grouping",
    "row-relation-head",
    "spacing-verifier",
    "text-relation-check",
    "structured-corroboration",
])

#: Operator id → runner protocol callable ``(candidates, yolo, resolved_values)``.
#: ``row-relation-head`` is the frozen-input runner adapter (S2ii), which also
#: accepts the engine-supplied raw visual source bundle; the executor forwards
#: it only to runners marked ``handles_raw_sources`` (see operators/trace.py).
#: ``structured-corroboration`` accepts an optional ``raw_sources`` bundle
#: (adapter-side structured channel); the executor calls VALIDATOR runners
#: with three arguments, so the executed pipeline's structured tier is absent
#: (trivial pass).  ``vlm-annotation`` has NO runner: it is offline-only and
#: never executed.
RUNNERS: Mapping[str, Any] = {
    "uniform-list-row-grouping": run_uniform_list_row_grouping,
    "row-relation-head": run_row_relation_head_routed,
    "spacing-verifier": verify,
    "text-relation-check": run_text_relation_check,
    "structured-corroboration": run_structured_corroboration,
}

#: S1 context: all selector dimensions DEFAULT, no tags (S1C/server-header
#: wiring replaces this with a real per-frame context later).
DEFAULT_CONTEXT = FrameContext()


def default_rule_set(registry: OperatorRegistry) -> list[Rule]:
    """Root-only rule set: one zero-specificity root rule per operator pinning
    every declared parameter (including the GENERATOR's built-in ``enabled``)
    to its contract default.

    The root rule is the governed, diffable expression of the retained
    candidate's constants (spec: "root-rule defaults equal to the current
    candidate values"); resolution against it returns exactly the contract
    defaults with root-rule provenance.  Always lints clean.
    """
    rules: list[Rule] = []
    for contract in registry.effective_contracts():
        params = {
            f"{contract.operator_id}.{name}": spec.default
            for name, spec in sorted(contract.parameters.items())
        }
        rules.append(
            Rule(
                rule_id=f"root-{contract.operator_id}",
                pins={},
                tags_pins=frozenset(),
                params=params,
            )
        )
    return rules


#: Active rule set for the S1 wiring: roots only, resolved values == contract
#: defaults == the retained candidate's constants.  Built once at module init;
#: the S1 equivalence gate therefore exercises byte-identical behavior.
DEFAULT_RULE_SET: tuple[Rule, ...] = tuple(default_rule_set(REGISTRY))