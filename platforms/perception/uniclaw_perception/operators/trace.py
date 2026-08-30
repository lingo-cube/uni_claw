"""Deterministic operator trace and offline replay (S1B, WI-PFW-S1B).

Implements the spec operator-contract trace requirement — *"Operators SHALL
emit a trace (input fingerprint, resolved parameters with rule provenance,
each decision or fail-closed reason) sufficient for offline replay"* — and the
S1.8 offline-replay harness: *same (frame inputs, rule-set hash) ⇒ identical
output and trace*.

The pipeline executor :func:`execute_pipeline` runs the registry-declared
topology over resolved parameters and records one step per operator:
GENERATOR steps carry their decision (``activated``/``noop`` + fail-closed
reason), VALIDATOR steps carry their verdict.  A VALIDATOR rejection is
fail-closed: the executor rolls the last GENERATOR's in-place mutations back
and records the veto reason — for the S1 port this is unreachable by
construction (the verifier's checks are strictly looser than the generator's
own construction guarantees).

Everything is byte-deterministic: fingerprints and hashes are SHA-256 over
canonical JSON (sorted keys, compact separators); the rule-set hash uses the
framework's deterministic serialization.  No writes to disk in the pipeline
path — :func:`replay` and :meth:`TraceRecord.to_bytes` materialize bytes only
when a caller asks.
"""
from __future__ import annotations

import copy
import hashlib
import json
from dataclasses import dataclass, field
from typing import Any, Mapping, Sequence

from .contracts import OperatorAuthority, OperatorRegistry
from .registry_defaults import (
    DEFAULT_CONTEXT,
    DEFAULT_RULE_SET,
    REGISTRY,
    RUNNERS,
)
from .relation_head_router import ROUTING_MIN_ANCHORS
from .resolver import ResolvedParams, resolve
from .ruleset import serialize_rule_set
from .selector import FrameContext, Rule
from .status import FAIL_CLOSED, NOOP, REJECTED
from .uniform_list_row_grouping import _confirmed_rows

__all__ = [
    "TRACE_FORMAT",
    "TRACE_FORMAT_VERSION",
    "TraceRecord",
    "build_raw_sources",
    "execute_pipeline",
    "replay",
    "input_fingerprint",
    "rule_set_hash",
]

TRACE_FORMAT = "perception-operator-trace"
TRACE_FORMAT_VERSION = 1


def build_raw_sources(
    detections: Sequence[Any],
    ocr_tokens: Sequence[Any],
    width: int,
    height: int,
) -> dict[str, Any]:
    """Single-source raw visual region bundle for frozen-input GENERATORs.

    The ONE engine-side construction of the ``raw_sources`` bundle (keys
    ``detections``/``ocr`` = ``to_json`` arrays + ``width``/``height``) that the
    ``row-relation-head`` router consumes.  Both engine paths (full-image and
    per-crop) and every replay route through it, so replay and engine can never
    diverge on bundle shape/keys (WI-PFW-S2fix: the engine/replay construction
    fork defect).  ``detections`` may be ``Detection`` objects or anything
    exposing ``to_json``; callers pass the raw uncombined visual regions.
    """
    return {
        "detections": [d.to_json(width, height) for d in detections],
        "ocr": [t.to_json(width, height) for t in ocr_tokens],
        "width": int(width),
        "height": int(height),
    }


def _canonical_json_bytes(payload: Any) -> bytes:
    return json.dumps(
        payload, sort_keys=True, separators=(",", ":"), ensure_ascii=False
    ).encode("utf-8")


def input_fingerprint(sources: Mapping[str, Any] | None) -> str:
    """SHA-256 of a canonical JSON rendering of the frame input arrays.

    ``sources`` maps a kind (``yolo``/``ocr``) to the deterministic JSON
    rendering of that array (the engine already sorts detections/tokens before
    fusion).  Same inputs ⇒ same fingerprint.
    """
    return hashlib.sha256(_canonical_json_bytes(sources or {})).hexdigest()


def rule_set_hash(rules: Sequence[Rule]) -> str:
    """SHA-256 of the deterministic rule-set serialization (order-independent
    by construction)."""
    return hashlib.sha256(
        serialize_rule_set(rules).encode("utf-8")
    ).hexdigest()


def _provenance_to_json(provenance: Mapping[str, Any]) -> dict[str, Any]:
    out: dict[str, Any] = {}
    for name in sorted(provenance):
        prov = provenance[name]
        out[name] = {
            "ruleId": prov.rule_id,
            "pins": dict(sorted(prov.pins.items())),
            "tagsPins": sorted(prov.tags_pins),
            "specificity": prov.specificity,
        }
    return out


def _resolved_params_to_json(resolved: Sequence[ResolvedParams]) -> list[dict[str, Any]]:
    return [
        {
            "operatorId": entry.operator_id,
            "values": dict(sorted(entry.values.items())),
            "provenance": _provenance_to_json(entry.provenance),
        }
        for entry in resolved
    ]


@dataclass
class TraceRecord:
    """Deterministic execution trace of one pipeline run.

    Serializable to stable bytes via :meth:`to_bytes` (sorted keys, 2-space
    indent, LF, no timestamps/machine paths) — the offline-replay equality
    contract: same (input fingerprint, rule-set hash) ⇒ identical trace bytes.
    """

    input_fingerprint: str
    rule_set_hash: str
    context: Mapping[str, str]
    resolved_params: list[dict[str, Any]] = field(default_factory=list)
    steps: list[dict[str, Any]] = field(default_factory=list)

    def to_dict(self) -> dict[str, Any]:
        return {
            "format": TRACE_FORMAT,
            "formatVersion": TRACE_FORMAT_VERSION,
            "inputFingerprint": self.input_fingerprint,
            "ruleSetHash": self.rule_set_hash,
            "context": dict(sorted(self.context.items())),
            "resolvedParams": self.resolved_params,
            "steps": self.steps,
        }

    def to_text(self) -> str:
        return json.dumps(self.to_dict(), sort_keys=True, indent=2, ensure_ascii=False) + "\n"

    def to_bytes(self) -> bytes:
        return self.to_text().encode("utf-8")


def execute_pipeline(
    candidates: list[dict[str, Any]],
    yolo_detections: list[Any],
    *,
    registry: OperatorRegistry = REGISTRY,
    rules: Sequence[Rule] = DEFAULT_RULE_SET,
    context: FrameContext = DEFAULT_CONTEXT,
    input_sources: Mapping[str, Any] | None = None,
    pipeline: Sequence[str] | None = None,
    raw_sources: Mapping[str, Any] | None = None,
    capture_candidate_views: bool = False,
) -> tuple[list[dict[str, Any]], TraceRecord]:
    """Execute the declared operator topology over the resolved parameters.

    Determinstic pure-ish orchestration: resolution is order-independent,
    every step record is deterministic, and a VALIDATOR veto rolls the last
    GENERATOR's in-place changes back (fail-closed).  Returns
    ``(candidates, trace)``; ``candidates`` is the same list object mutated by
    the operators (the engine continues post-processing it).

    ``raw_sources`` carries the engine's raw visual regions (detections/ocr
    to_json arrays + width/height) for frozen-input GENERATOR runners; it is
    forwarded ONLY to runners marked ``handles_raw_sources`` (the S2ii
    row-relation-head adapter) — every other runner keeps the 3-argument
    protocol byte-unchanged.
    """
    ordered = tuple(pipeline) if pipeline is not None else registry.operators_for_pipeline()
    resolved = resolve(rules, context, registry)
    by_id = {entry.operator_id: entry for entry in resolved}

    trace = TraceRecord(
        input_fingerprint=input_fingerprint(input_sources),
        rule_set_hash=rule_set_hash(rules),
        context=context.to_mapping(),
        resolved_params=_resolved_params_to_json(resolved),
        steps=[],
    )

    def append_step(step: dict[str, Any], before: list[dict[str, Any]] | None = None) -> None:
        if capture_candidate_views:
            detail = str(step.get("detail", ""))
            status = str(step.get("status", ""))
            step["attempted"] = status != "noop" or detail != "disabled by rule configuration"
            step["outcome"] = (
                "delegated" if detail.startswith("delegated:")
                else "matched" if status in {"activated", "verified", "accepted"}
                else "rejected" if status in {"rejected", "fail_closed"}
                else "noop"
            )
            step["beforeCandidates"] = copy.deepcopy(before if before is not None else candidates)
            step["afterCandidates"] = copy.deepcopy(candidates)
        trace.steps.append(step)

    def decision_inputs(operator_id: str, values: Mapping[str, Any]) -> dict[str, Any]:
        """Compact routing/decision inputs for one GENERATOR step — the gate's
        ``confirmed anchor count / relevant routing inputs`` field.  Pure read;
        never mutates candidates (TRACE != CONTROL)."""
        inputs: dict[str, Any] = {
            "confirmedAnchors": len(_confirmed_rows(candidates)),
            "titleTextBlockIds": [
                str(c["id"]) for c in candidates
                if c.get("type") == "text_block"
                and str(c.get("text", "")).strip()
                and c.get("id")
            ],
        }
        if operator_id == "uniform-list-row-grouping":
            inputs["minAnchors"] = int(values.get("minAnchors", 4))
            rows = _confirmed_rows(candidates)
            ys: list[float] = []
            for c in rows:
                cp = c.get("centerPx")
                if isinstance(cp, (list, tuple)) and len(cp) >= 2:
                    ys.append(float(cp[1]))
            gaps = [round(b - a, 1) for a, b in zip(ys, ys[1:]) if b > a]
            heights = [
                round(float(c["boundsPx"][3]) - float(c["boundsPx"][1]), 1)
                for c in rows
                if isinstance(c.get("boundsPx"), (list, tuple)) and len(c["boundsPx"]) >= 4
            ]
            x1s = [
                round(float(c["boundsPx"][0]), 1)
                for c in rows
                if isinstance(c.get("boundsPx"), (list, tuple)) and len(c["boundsPx"]) >= 4
            ]
            inputs["anchorGeometry"] = {
                "gaps": gaps,
                "centerYs": [round(y, 1) for y in ys],
                "titleHeights": heights,
                "x1s": x1s,
            }
        elif operator_id == "row-relation-head":
            inputs["routingFloor"] = ROUTING_MIN_ANCHORS
        return inputs

    def outcome_refs() -> dict[str, Any]:
        """Post-step compact refs: composed rows vs uncomposed row titles."""
        return {
            "menuItemIds": [
                str(c["id"]) for c in candidates
                if c.get("type") == "menu_item"
                and str(c.get("text", "")).strip()
                and c.get("id")
            ],
            "unresolvedTitleIds": [
                str(c["id"]) for c in candidates
                if c.get("type") == "text_block"
                and str(c.get("text", "")).strip()
                and c.get("id")
            ],
        }

    pre_generator_snapshot: list[dict[str, Any]] | None = None
    # FRAME_LOCAL_FUSION_ROLE_STABILITY_REPAIR_GATE: the pipeline forwards each
    # GENERATOR's decision to the next raw-source GENERATOR (the router) so
    # ownership follows actual composition success.  None before the first
    # generator → the router falls back to the legacy count-only behavior on
    # direct invocations.
    previous_generator_decision: dict[str, Any] | None = None
    for step_index, operator_id in enumerate(ordered):
        contract = registry.lookup(operator_id)
        entry = by_id[operator_id]
        runner = RUNNERS[operator_id]

        if contract.authority is OperatorAuthority.GENERATOR:
            operator_input = copy.deepcopy(candidates) if capture_candidate_views else None
            di = decision_inputs(operator_id, entry.values)
            if entry.values.get("enabled", True) is False:
                append_step({
                    "stepIndex": step_index,
                    "operator": operator_id,
                    "authority": "GENERATOR",
                    "status": NOOP,
                    "detail": "disabled by rule configuration",
                    "emitted": 0,
                    "decisionInputs": di,
                    "outcomeRefs": outcome_refs(),
                }, operator_input)
                previous_generator_decision = {"status": NOOP,
                                               "detail": "disabled by rule configuration"}
                continue
            if pre_generator_snapshot is None:
                # Deep copy: operators mutate candidate dicts in place (type,
                # evidence, riskFlags) and may reassign the list — the
                # fail-closed rollback must restore both surfaces exactly.
                pre_generator_snapshot = copy.deepcopy(candidates)
            if getattr(runner, "handles_raw_sources", False):
                # FRAME_LOCAL_FUSION_ROLE_STABILITY_REPAIR_GATE: forward the
                # PREVIOUS generator's decision so the router can decide
                # ownership by actual composition success, not by anchor count
                # alone (a delegated fallback after a uniform-list NOOP is the
                # located FDP).  The keyword is optional — direct runner calls
                # (no pipeline) keep the legacy count-only path.
                decision = runner(
                    candidates, yolo_detections, entry.values, raw_sources,
                    previous_generator_decision=previous_generator_decision,
                )
            else:
                decision = runner(candidates, yolo_detections, entry.values)
            previous_generator_decision = decision
            append_step({
                "stepIndex": step_index,
                "operator": operator_id,
                "authority": "GENERATOR",
                "status": decision["status"],
                "detail": decision["detail"],
                "emitted": decision.get("emitted", 0),
                "decisionInputs": di,
                "outcomeRefs": outcome_refs(),
            }, operator_input)
        else:
            operator_input = copy.deepcopy(candidates) if capture_candidate_views else None
            di = decision_inputs(operator_id, entry.values)
            decision = runner(candidates, yolo_detections, entry.values)
            if decision["status"] == REJECTED:
                if pre_generator_snapshot is not None:
                    candidates[:] = pre_generator_snapshot
                append_step({
                    "stepIndex": step_index,
                    "operator": operator_id,
                    "authority": contract.authority.value,
                    "status": FAIL_CLOSED,
                    "detail": decision["detail"],
                    "emitted": 0,
                    "decisionInputs": di,
                    "outcomeRefs": outcome_refs(),
                }, operator_input)
                break
            append_step({
                "stepIndex": step_index,
                "operator": operator_id,
                "authority": contract.authority.value,
                "status": decision["status"],
                "detail": decision["detail"],
                "emitted": decision.get("emitted", 0),
                "decisionInputs": di,
                "outcomeRefs": outcome_refs(),
            }, operator_input)
    return candidates, trace


def replay(
    case: dict[str, Any],
    *,
    registry: OperatorRegistry = REGISTRY,
    rules: Sequence[Rule] = DEFAULT_RULE_SET,
    context: FrameContext = DEFAULT_CONTEXT,
) -> tuple[list[dict[str, Any]], TraceRecord]:
    """Offline replay of one corpus case through the wired fusion pipeline.

    Reconstructs the frame inputs exactly as the S1 equivalence harness does
    (interpretation mirrors ``tests/test_row_composition_equivalence.py``:
    detections/tokens from the case's ``yolo``/``ocr`` JSON, ``crops``-mode
    via per-crop OCR) and runs ``fuse_evidence`` /
    ``fuse_evidence_from_crops`` with a trace sink so the returned trace is
    the trace of the run that produced the returned candidates.

    The wired engine resolves against the operating root rule set by default;
    pass explicit ``rules``/``context`` to replay an alternate configuration
    directly against :func:`execute_pipeline` semantics used by the engine.
    Deterministic: replaying the same case twice yields byte-identical
    candidates and trace bytes.
    """
    from ..fusion.engine import fuse_evidence, fuse_evidence_from_crops
    from ..schema import Box, Detection, OcrToken

    width = int(case["width"])
    height = int(case["height"])
    detections = [
        Detection(entry["id"], entry["label"], float(entry["confidence"]), Box(*[float(v) for v in entry["bounds"]]))
        for entry in case["yolo"]
    ]
    tokens = [
        OcrToken(entry["id"], entry["text"], float(entry["confidence"]), Box(*[float(v) for v in entry["bounds"]]))
        for entry in case["ocr"]
    ]

    captured: list[dict[str, Any]] = []
    if case.get("mode", "full") == "crops":
        by_id = {token.id: token for token in tokens}
        crops_ocr = [[by_id[i] for i in slot] for slot in case["crops"]]
        evidence = fuse_evidence_from_crops(
            detections, crops_ocr,
            image_width=width, image_height=height,
            registry=registry, rules=rules, context=context,
            trace_sink=captured.append,
        )
    else:
        params = {"promote_unmatched_ocr": False, "max_ocr_distance_ratio": 0.055}
        params.update(case.get("params", {}))
        evidence = fuse_evidence(
            detections, tokens,
            image_width=width, image_height=height,
            promote_unmatched_ocr=bool(params["promote_unmatched_ocr"]),
            max_ocr_distance_ratio=float(params["max_ocr_distance_ratio"]),
            registry=registry, rules=rules, context=context,
            trace_sink=captured.append,
        )
    trace = captured[0] if captured else None
    if trace is None:
        raise RuntimeError("replay: engine did not emit a pipeline trace")
    return evidence["candidates"], TraceRecord(
        input_fingerprint=trace["inputFingerprint"],
        rule_set_hash=trace["ruleSetHash"],
        context=trace["context"],
        resolved_params=trace["resolvedParams"],
        steps=trace["steps"],
    )
