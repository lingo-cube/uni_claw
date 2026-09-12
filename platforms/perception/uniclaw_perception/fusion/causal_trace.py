"""Fusion causal trace — compact, deterministic, reference-only (fusion-trace gate).

Gate: ``PROJECT_LEADER_PERCEPTION_FUSION_TRACE_COVERAGE_GATE`` (approved trace
coverage; fusion repair NOT authorized).

Purpose: record WHY fusion composed (or refused to compose) every row, so the
first failed predicate that leaves a complete row as ``text_block`` can be read
DIRECTLY from the trace instead of being reverse-engineered offline.

Invariants (hard):

* TRACE != CONTROL — every function here is a pure reader.  Nothing in this
  module (or the step enrichment it feeds) reads back into fusion behavior,
  routing, or candidate output.
* TRACE != EVIDENCE AUTHORITY — the causal verdict is a diagnostic summary of
  the fusion decision chain; it carries no authority over any downstream
  admission, normalization, or completeness decision.
* TRACE != SEMANTIC ADMISSION — the trace never enters the semantic
  capability/admission path; the C# runtime never consumes these fields for a
  decision.

Compactness (per the gate): the trace carries candidate *references* (id/type/
text/rowId) and per-title geometry signatures only — it never embeds the fused
candidate stage views (bounds/evidence/riskFlags) or raw YOLO/OCR payloads.
Heavy stage data stays in the stage artifacts (screenshot, raw detections, the
opt-in ``capture_candidate_views``/``stage_sink`` channels); the trace links to
them via EvidenceRef-style ids.
"""
from __future__ import annotations

from typing import Any, Mapping, Sequence

#: Stable format identity for the fusion causal trace document.
FUSION_TRACE_FORMAT = "perception-fusion-causal-trace"
FUSION_TRACE_FORMAT_VERSION = 1

#: Step kinds mapped from the operator authority (see operators/trace.py).
_STEP_KIND_BY_AUTHORITY = {
    "GENERATOR": "OperatorAttempt",
    "VALIDATOR": "ValidatorDecision",
}

#: Stable fallback/selection reason marker used by the router step.
_DELEGATED_MARKER = "delegated:"


def candidate_ref(candidate: Mapping[str, Any]) -> dict[str, Any]:
    """Compact candidate reference (never bounds/evidence/riskFlags)."""
    return {
        "id": str(candidate.get("id", "")),
        "type": str(candidate.get("type", "")),
        "text": str(candidate.get("text", "")),
        "rowId": candidate.get("row_id"),
    }


def is_composed_row(candidate: Mapping[str, Any]) -> bool:
    """A candidate that represents a composed navigation row (menu_item + text)."""
    return (
        candidate.get("type") == "menu_item"
        and bool(str(candidate.get("text", "")).strip())
    )


def is_uncomposed_title(candidate: Mapping[str, Any]) -> bool:
    """A candidate that LOOKS like a row title but was NOT composed:
    ``text_block`` with non-empty text."""
    return (
        candidate.get("type") == "text_block"
        and bool(str(candidate.get("text", "")).strip())
    )


def confirmed_anchor_count(candidates: Sequence[Mapping[str, Any]]) -> int:
    """Menu_item-with-text rows in ``candidates`` — the router's anchor input."""
    return sum(1 for c in candidates if is_composed_row(c))


def uncomposed_title_refs(candidates: Sequence[Mapping[str, Any]]) -> list[str]:
    """Ids of text_block-with-text candidates (uncomposed row titles)."""
    return [
        str(c["id"]) for c in candidates
        if is_uncomposed_title(c) and c.get("id")
    ]


def composed_row_refs(candidates: Sequence[Mapping[str, Any]]) -> list[str]:
    """Ids of composed menu_item rows."""
    return [
        str(c["id"]) for c in candidates
        if is_composed_row(c) and c.get("id")
    ]


def uncomposed_title_geometry(
    candidate: Mapping[str, Any],
) -> dict[str, Any] | None:
    """Minimal geometry signature of ONE uncomposed title (for the reader to
    re-check the cadence/column slot predicate against the decision)."""
    bounds_px = candidate.get("boundsPx")
    bounds = candidate.get("bounds")
    center = candidate.get("centerPx")
    px = (
        [float(v) for v in bounds_px]
        if isinstance(bounds_px, (list, tuple)) and len(bounds_px) >= 4
        else None
    )
    sig: dict[str, Any] = {"id": str(candidate.get("id", ""))}
    if px is not None:
        sig["x1"], sig["y1"], sig["x2"], sig["y2"] = px
    if isinstance(bounds, dict):
        for key in ("x1", "y1", "x2", "y2"):
            if key in bounds:
                sig[f"n_{key}"] = float(bounds[key])
    if isinstance(center, (list, tuple)) and len(center) >= 2:
        sig["centerY"] = float(center[1])
        sig["centerX"] = float(center[0])
    return sig


def input_refs(
    detections: Sequence[Any],
    ocr_tokens: Sequence[Any],
) -> dict[str, Any]:
    """Compact input refs: yolo detection ids + ocr token ids (EvidenceRefs)."""
    return {
        "yoloIds": [str(d.id) for d in detections if getattr(d, "id", None)],
        "ocrIds": [str(t.id) for t in ocr_tokens if getattr(t, "id", None)],
    }


def strip_stage_views(trace: Mapping[str, Any]) -> dict[str, Any]:
    """Remove the heavy opt-in candidate stage views from a trace document.

    The operator step records may carry ``beforeCandidates``/``afterCandidates``
    (the S1.8 full-candidate capture, opt-in via ``capture_candidate_views``).
    This projection keeps every compact decision/ref field and the
    ``fusionEvents``/``fusionVerdict`` sections, and drops only the stage view
    payloads — the wire/artifact form of the trace (the gate: no large stage
    data copied into the Trace).
    """
    out = dict(trace)
    steps = []
    for step in trace.get("steps", []):
        steps.append(
            {k: v for k, v in step.items()
             if k not in ("beforeCandidates", "afterCandidates")}
        )
    out["steps"] = steps
    return out


def _fallback_summary(
    steps: Sequence[Mapping[str, Any]],
    step_index: int,
) -> dict[str, Any]:
    """Which later generation authority could still have composed the leftover
    titles after the failed decision, and whether it actually ran."""
    later = [
        (i, s) for i, s in enumerate(steps)
        if i > step_index and s.get("authority") == "GENERATOR"
    ]
    if not later:
        return {"checked": True, "available": False,
                "reason": "no later generation operator in the topology"}
    i, step = later[0]
    return {
        "checked": True,
        "available": True,
        "stepIndex": i,
        "operator": step.get("operator"),
        "status": step.get("status"),
        "skipped": step.get("status") == "noop",
        "reason": step.get("detail"),
    }


def first_failed_composition_decision(
    steps: Sequence[Mapping[str, Any]],
) -> dict[str, Any]:
    """Locate the FIRST generator decision that left >= 1 row title uncomposed
    (still ``text_block``) while generation authority refused composition.

    This is the direct answer to the gate question: "which decision first
    caused a complete row to not be composed, leaving text_block?"

    Scoring rule (deterministic, read-only over step outcomes):

    * iterate steps in execution order;
    * the FIRST GENERATOR step whose status is ``noop`` AND whose
      ``outcomeRefs.unresolvedTitleIds`` is non-empty is the first failed
      decision;
    * the fallback block names the next generation authority in the topology
      and whether it was skipped (and why).

    Returns ``{"found": False}`` when no such step exists (fully composed or
    the unresolved residue cannot be attributed to a refusal — the caller then
    labels INSUFFICIENT_TRACE_EVIDENCE where required).
    """
    for index, step in enumerate(steps):
        if step.get("authority") != "GENERATOR":
            continue
        if step.get("status") != "noop":
            continue
        unresolved = step.get("outcomeRefs", {}).get("unresolvedTitleIds", [])
        if not unresolved:
            continue
        detail = str(step.get("detail", ""))
        reason = (
            detail[len(_DELEGATED_MARKER):].strip()
            if detail.startswith(_DELEGATED_MARKER)
            else detail
        )
        return {
            "found": True,
            "stepIndex": index,
            "operator": step.get("operator"),
            "status": "noop",
            "detail": detail,
            "reason": reason,
            "decisionInputs": step.get("decisionInputs"),
            "unresolvedAfter": unresolved,
            "fallback": _fallback_summary(steps, index),
        }
    return {"found": False}


def build_fusion_events(
    *,
    input_refs_doc: dict[str, Any],
    steps: Sequence[Mapping[str, Any]],
    diagnostics: Mapping[str, Any],
    row_map: Mapping[str, Any],
    output: Sequence[Mapping[str, Any]],
) -> list[dict[str, Any]]:
    """Assemble the fusion causal event list in order.

    ``steps`` are the enriched operator steps (RouterDecision / OperatorAttempt
    / OperatorResult / ValidatorDecision).  ``row_map`` maps candidate id ->
    row_id assigned by the stabilizer.  ``output`` is the final candidate list
    snapshot (the FusionOutput refs).  Pure, deterministic, reference-only.
    """
    events: list[dict[str, Any]] = [{"event": "InputRefs", **input_refs_doc}]
    for step in steps:
        kind = _STEP_KIND_BY_AUTHORITY.get(step.get("authority", ""), "OperatorAttempt")
        events.append({
            "event": kind,
            "stepIndex": step.get("stepIndex"),
            "operator": step.get("operator"),
            "status": step.get("status"),
            "detail": step.get("detail"),
            "emitted": step.get("emitted"),
            "decisionInputs": step.get("decisionInputs"),
            "outcomeRefs": step.get("outcomeRefs"),
        })
        if step.get("authority") == "GENERATOR" and (node := step.get("outcomeRefs")):
            events.append({
                "event": "OperatorResult",
                "stepIndex": step.get("stepIndex"),
                "operator": step.get("operator"),
                "composedRowIds": node.get("menuItemIds", []),
                "unresolvedTitleIds": node.get("unresolvedTitleIds", []),
            })
    events.append({"event": "PostPipelineDiagnostics", "diagnostics": diagnostics})
    events.append({
        "event": "RowStabilization",
        "rowIds": row_map,
    })
    events.append({
        "event": "FusionOutput",
        "outputRefs": [candidate_ref(c) for c in output],
        "unresolvedOccurrences": [
            uncomposed_title_geometry(c) or candidate_ref(c)
            for c in output
            if is_uncomposed_title(c)
        ],
    })
    return events