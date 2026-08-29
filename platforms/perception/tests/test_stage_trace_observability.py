"""Validation-only stage trace must expose decisions without changing fusion."""
from __future__ import annotations

import copy

from uniclaw_perception.fusion.engine import fuse_evidence
from uniclaw_perception.operators.trace import execute_pipeline
from uniclaw_perception.schema import Box, Detection, OcrToken


def _confirmed_candidate(index: int) -> dict:
    # Cadence-valid anchor shape (title height 20, pitch 100 >= 2.2*20, regular
    # gaps) so uniform-list-row-grouping ACTIVATES and the router delegates —
    # the trace exposes the delegation decision on an activated frame.
    y1 = 100 + index * 100
    cy = y1 + 10
    return {
        "id": f"candidate_{index}",
        "type": "menu_item",
        "text": f"Row {index}",
        "confidence": 0.9,
        "bounds": {"x1": 0.1, "y1": (y1) / 600, "x2": 0.8, "y2": (y1 + 20) / 600},
        "boundsPx": [60, y1, 480, y1 + 20],
        "centerPx": [270, cy],
        "evidence": {"yoloId": f"yolo_{index}", "ocrIds": [f"ocr_{index}"], "allIds": []},
        "riskFlags": [],
    }


def test_candidate_views_are_opt_in_and_expose_delegation() -> None:
    candidates = [_confirmed_candidate(i) for i in range(1, 5)]

    _, default_trace = execute_pipeline(copy.deepcopy(candidates), [])
    assert all("beforeCandidates" not in step for step in default_trace.steps)

    output, captured = execute_pipeline(
        copy.deepcopy(candidates), [], capture_candidate_views=True
    )
    relation = next(step for step in captured.steps if step["operator"] == "row-relation-head")
    assert relation["attempted"] is True
    assert relation["outcome"] == "delegated"
    assert relation["beforeCandidates"] == relation["afterCandidates"]
    assert output == candidates


def test_stage_sinks_do_not_change_fusion_output() -> None:
    detections = [Detection("d1", "text_block", 0.9, Box(60, 100, 480, 145))]
    ocr = [OcrToken("o1", "Display", 0.95, Box(75, 108, 260, 136))]

    baseline = fuse_evidence(
        detections, ocr, image_width=600, image_height=600,
        promote_unmatched_ocr=True,
    )
    traces: list[dict] = []
    stages: list[dict] = []
    observed = fuse_evidence(
        detections, ocr, image_width=600, image_height=600,
        promote_unmatched_ocr=True,
        trace_sink=traces.append,
        stage_sink=stages.append,
    )

    assert observed == baseline
    assert len(traces) == 1
    assert [step["operator"] for step in traces[0]["steps"]] == [
        "uniform-list-row-grouping",
        "row-relation-head",
        "spacing-verifier",
        "text-relation-check",
        "structured-corroboration",
    ]
    assert stages[0]["stage"] == "composition-input"
    assert stages[-1]["stage"] == "row-stabilization"
