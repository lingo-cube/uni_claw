"""Fusion causal trace tests (PROJECT_LEADER_PERCEPTION_FUSION_TRACE_COVERAGE_GATE).

Trace-only coverage: records WHY fusion composed (or refused to compose) each
row — RouterDecision / OperatorAttempt / OperatorResult / ValidatorDecision /
FusionOutput — in compact reference form, WITHOUT changing fusion behavior.

Invariants under test:
  * TRACE != CONTROL — fuse with/without trace_sink produces identical output.
  * the verdict reads the FIRST failed composition decision directly from the
    trace (no fusion re-execution), incl. fallback (relation-head skipped).
  * the trace stays compact/ref-based: strip_stage_views drops candidate views
    but keeps decisions + refs + the verdict.
"""
from __future__ import annotations

import copy

from uniclaw_perception.fusion.causal_trace import (
    first_failed_composition_decision,
    strip_stage_views,
)
from uniclaw_perception.fusion.engine import fuse_evidence
from uniclaw_perception.operators.trace import execute_pipeline
from uniclaw_perception.schema import Box, Detection, OcrToken


def _wave(y1: int, text: str, index: int, kind: str = "menu_item") -> dict:
    """One candidate with a clean preprocessed-space (720x~1020) signature."""
    return {
        "id": f"candidate_{index}",
        "type": kind,
        "text": text,
        "confidence": 0.9,
        "bounds": {
            "x1": 60 / 720, "y1": y1 / 1020,
            "x2": 500 / 720, "y2": (y1 + 45) / 1020,
        },
        "boundsPx": [60, y1, 500, y1 + 45],
        "centerPx": [280, y1 + 22],
        "evidence": {"yoloId": f"yolo_{index}", "ocrIds": [f"ocr_{index}"], "allIds": []},
        "riskFlags": [],
    }


def _run_captured(candidates: list[dict], detections=None, ocr=None):
    """Run the operator pipeline with a trace captured (views on)."""
    traces: list[dict] = []
    output, trace = execute_pipeline(
        copy.deepcopy(candidates), detections or [],
        capture_candidate_views=True,
    )
    traces.append(trace.to_dict())
    return output, traces[0]


def test_trace_does_not_change_fusion_output() -> None:
    detections = [
        Detection("d1", "text_block", 0.9, Box(60, 100, 500, 145)),
        Detection("d2", "text_block", 0.9, Box(60, 250, 500, 295)),
        Detection("d3", "text_block", 0.9, Box(60, 400, 500, 445)),
        Detection("d4", "text_block", 0.9, Box(60, 550, 500, 595)),
    ]
    ocr = [
        OcrToken(f"o{i}", f"Row {i}", 0.95, Box(70, y + 8, 300, y + 36))
        for i, y in enumerate((100, 250, 400, 550), start=1)
    ]
    baseline = fuse_evidence(detections, ocr, image_width=720, image_height=1020)
    traced: list[dict] = []
    observed = fuse_evidence(
        detections, ocr, image_width=720, image_height=1020,
        trace_sink=traced.append,
    )
    assert observed == baseline
    assert len(traced) == 1


def test_verdict_anchor_cadence_fail_uniform_list_first() -> None:
    """The FDP frame: >= 4 confirmed anchors, irregular cadence ->
    uniform-list noop (model not inferable) -> row-relation-head NOT skipped
    (repair gate: ownership by actual success) — in a bare pipeline call it
    fails closed on missing raw sources; the VERDICT still names uniform-list
    as the FIRST failed composition decision."""
    candidates = [
        _wave(100, "Row 1", 1),
        _wave(250, "Row 2", 2),
        _wave(430, "Row 3", 3),
        _wave(630, "Row 4", 4),
        _wave(175, "Uncomposed subtitle row", 5, kind="text_block"),
    ]
    output, trace = _run_captured(candidates)
    steps = trace["steps"]

    # uniform-list refused; the fallback is attempted (not count-delegated);
    # one title remains unresolved.
    assert steps[0]["operator"] == "uniform-list-row-grouping"
    assert steps[0]["status"] == "noop"
    assert "cadence model not inferable" in steps[0]["detail"]
    assert steps[0]["decisionInputs"]["confirmedAnchors"] == 4
    assert "candidate_5" in steps[0]["outcomeRefs"]["unresolvedTitleIds"]

    relation = next(s for s in steps if s["operator"] == "row-relation-head")
    assert relation["status"] == "noop"
    assert "delegated" not in relation["detail"]          # repair: not skipped-by-count
    assert "fail-closed" in relation["detail"]            # bare call: no raw sources
    assert relation["decisionInputs"]["confirmedAnchors"] == 4

    verdict = first_failed_composition_decision(steps)
    assert verdict["found"] is True
    assert verdict["operator"] == "uniform-list-row-grouping"
    assert "cadence model not inferable" in verdict["reason"]
    assert "candidate_5" in verdict["unresolvedAfter"]
    # fallback: relation-head attempted, skipped with a FAIL-CLOSED (not
    # delegated) reason.
    assert verdict["fallback"]["available"] is True
    assert verdict["fallback"]["operator"] == "row-relation-head"
    assert verdict["fallback"]["skipped"] is True
    assert "delegated:" not in verdict["fallback"]["reason"]

    # The stripped trace keeps the verdict + refs (no candidate views).
    stripped = strip_stage_views(trace)
    assert all("beforeCandidates" not in s for s in stripped["steps"])
    assert stripped["steps"][0]["outcomeRefs"]["unresolvedTitleIds"]
    assert stripped["steps"][1]["decisionInputs"]["confirmedAnchors"] == 4


def test_verdict_anchors_below_floor_relation_head_runs() -> None:
    """< 4 anchors -> uniform-list noop (model not inferable below the anchor
    floor) -> relation-head RUNS (not skipped).  Verdict still names
    uniform-list as the first refusal, but the fallback records that the
    relation-head actually attempted."""
    candidates = [
        _wave(100, "Row 1", 1),
        _wave(250, "Row 2", 2),
        _wave(175, "Leftover title", 3, kind="text_block"),
    ]
    _, trace = _run_captured(candidates)
    steps = trace["steps"]
    assert steps[0]["status"] == "noop"
    assert "fail-closed" in steps[0]["detail"]
    relation = next(s for s in steps if s["operator"] == "row-relation-head")
    # below the anchor floor -> the relation-head adapter runs (no raw sources
    # in a bare pipeline call -> fail-closed noop is expected; the KEY is that
    # it was NOT "delegated").
    assert "delegated" not in relation["detail"]

    verdict = first_failed_composition_decision(steps)
    assert verdict["found"] is True
    assert verdict["fallback"]["available"] is True
    assert verdict["fallback"]["operator"] == "row-relation-head"


def test_verdict_partial_activation_router_skipped() -> None:
    """uniform-list ACTIVATES (regular cadence, 4 anchors) but an off-cadence
    text_block stays; the router then DELEGATES (activated ownership — the
    repair gate keeps the activated path byte-identical).  The first FAILED
    decision for the leftover title is the delegation — the
    'partial-activated -> relation-head skipped' shape (unchanged ownership)."""
    candidates = [
        _wave(100, "Row 1", 1),
        _wave(250, "Row 2", 2),
        _wave(400, "Row 3", 3),
        _wave(550, "Row 4", 4),
        _wave(325, "Half-pitch leftover", 5, kind="text_block"),
    ]
    _, trace = _run_captured(candidates)
    steps = trace["steps"]
    assert steps[0]["status"] == "activated"
    assert steps[0]["decisionInputs"]["confirmedAnchors"] == 4
    # uniform-list activated, but the off-cadence title was not in its envelope.
    assert "candidate_5" in steps[0]["outcomeRefs"]["unresolvedTitleIds"]

    relation = next(s for s in steps if s["operator"] == "row-relation-head")
    assert relation["status"] == "noop"
    assert relation["detail"].startswith("delegated:")
    assert "activated" in relation["detail"]

    verdict = first_failed_composition_decision(steps)
    assert verdict["found"] is True
    assert verdict["operator"] == "row-relation-head"
    assert verdict["reason"].startswith("uniform-list-row-grouping activated")
    assert verdict["fallback"]["available"] is False


def test_verdict_resolved_frame_not_found() -> None:
    """Regular cadence, all rows composed as menu_items -> no failed decision."""
    candidates = [
        _wave(100, "Row 1", 1),
        _wave(250, "Row 2", 2),
        _wave(400, "Row 3", 3),
        _wave(550, "Row 4", 4),
    ]
    _, trace = _run_captured(candidates)
    steps = trace["steps"]
    assert steps[0]["status"] == "activated"
    verdict = first_failed_composition_decision(steps)
    assert verdict["found"] is False


def test_fusion_events_include_output_refs_and_row_ids() -> None:
    """fuse_evidence attaches the fusion causal document: events (RouterDecision/
    OperatorAttempt/ValidatorDecision/FusionOutput) + verdict; FusionOutput
    carries per-candidate refs incl. stabilizer row ids."""
    detections = [
        Detection("d1", "text_block", 0.9, Box(60, 100, 500, 145)),
        Detection("d2", "text_block", 0.9, Box(60, 250, 500, 295)),
        Detection("d3", "text_block", 0.9, Box(60, 400, 500, 445)),
        Detection("d4", "text_block", 0.9, Box(60, 550, 500, 595)),
    ]
    ocr = [
        OcrToken(f"o{i}", f"Row {i}", 0.95, Box(70, y + 8, 300, y + 36))
        for i, y in enumerate((100, 250, 400, 550), start=1)
    ]
    traced: list[dict] = []
    fuse_evidence(
        detections, ocr, image_width=720, image_height=1020,
        trace_sink=traced.append,
        stabilize=True,
        stabilize_context=[{"id": "row_001", "text": "Row 1"},
                           {"id": "row_002", "text": "Row 2"}],
    )
    doc = traced[0]
    fusion = doc["fusion"]
    assert fusion["format"] == "perception-fusion-causal-trace"
    events = fusion["events"]
    kinds = [e["event"] for e in events]
    assert kinds[0] == "InputRefs"
    assert "OperatorAttempt" in kinds
    assert "ValidatorDecision" in kinds
    assert kinds[-1] == "FusionOutput"

    output_event = events[-1]
    row_ids = {r["id"]: r.get("rowId") for r in output_event["outputRefs"]}
    # stabilizer tagged the known rows with row_001/row_002.
    assert any(rid == "row_002" for rid in row_ids.values())
    verdict = fusion["verdict"]
    assert verdict["found"] in (True, False)