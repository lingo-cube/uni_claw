"""FRAME_LOCAL_FUSION_ROLE_STABILITY_REPAIR_GATE — RED→GREEN falsifier.

FDP (trace-proven on real frames): uniform-list refused composition (cadence
model not inferable) yet row-relation-head was still skipped because
confirmedAnchors >= 4 — leaving complete rows as text_block.

Minimal fix (approved): operator ownership follows ACTUAL composition success,
not the anchor count:

* uniform-list ACTIVATED        -> relation-head delegated (unchanged; the
  G-2 activated path is byte-identical).
* uniform-list NOOP (model not inferable / cap / disabled) on a HIGH-anchor
  frame -> relation-head may compose ONLY rows inside the anchor cadence
  envelope (same pitch/column/tolerance constants — NO tolerance change),
  bounded by the hard "never invent more rows than confirmed" invariant.
  Genuinely irregular frames (no majority cadence reference) stay refused
  (ambiguity fail-closed); direct invocations without the previous decision
  keep the legacy count-only delegation.

WI-P26-ROWFIX-B (Leader-authorized consensus semantics): the ROWFIX-A
cadence-consensus fix means the captured FDP frame's own gaps
[128,158,149,158,154,461] now ACTIVATE uniform-list (the 461px outlier is a
3×-pitch multi-step gap, no longer a veto) — so the two FDP frame tests
assert the consensus division of labour (uniform-list owns the frame,
relation-head delegates).  The ORIGINAL delegation intent — uniform-list
legitimately noops on a high-anchor frame and relation-head still composes
the complete rows — is preserved by a new genuinely-noop geometry (fewer
than 2 direct gaps, so cadence consensus fails) whose anchors still carry a
majority cadence reference, proving relation-head composes rows when
uniform-list noops.

The falsifier cases use the real captured FDP-frame geometry
(/tmp/p26-fusion-trace-v2-traces.json seq 4/5): 7 anchors with the 128px
cadence violation + Text Block leftovers 'Sound & vibration' / 'Display' /
'Dark theme, font size, brightness'.
"""
from __future__ import annotations

import copy

from uniclaw_perception.operators.trace import execute_pipeline
from uniclaw_perception.schema import Box, Detection, OcrToken


# ── helpers ────────────────────────────────────────────────────────────────

def _candidate(index: int, text: str, center_y: float,
               kind: str = "menu_item", y1: float | None = None,
               y2: float | None = None, x1: float = 127.0) -> dict:
    y1 = center_y - 20 if y1 is None else y1
    y2 = center_y + 20 if y2 is None else y2
    return {
        "id": f"candidate_{index}",
        "type": kind,
        "text": text,
        "confidence": 0.9,
        "bounds": {"x1": x1 / 720, "y1": y1 / 1400, "x2": (x1 + 300) / 720, "y2": y2 / 1400},
        "boundsPx": [x1, y1, x1 + 300, y2],
        "centerPx": [x1 + 150, center_y],
        "evidence": {"yoloId": f"yolo_{index}", "ocrIds": [f"ocr_{index}"], "allIds": []},
        "riskFlags": [],
    }


def _fdp_frame_candidates() -> list[dict]:
    """Exact geometry of the captured FDP frame (v2 run seq 4/5)."""
    anchors = [
        ("Mobile, Wi-Fi, hotspot", 164.0),
        ("Connected devices", 292.0),
        ("Apps", 450.0),
        ("Notifications", 599.0),
        ("Battery", 757.0),
        ("Storage", 911.0),
        ("Wallpaper", 1372.0),
    ]
    cands = [_candidate(i + 1, text, cy) for i, (text, cy) in enumerate(anchors)]
    cands.append(_candidate(25, "Sound & vibration", 1061.0, "text_block", 1047.0, 1076.0))
    cands.append(_candidate(28, "Display", 1219.0, "text_block", 1201.0, 1237.0))
    cands.append(_candidate(29, "Dark theme, font size, brightness", 1258.0, "text_block", 1245.0, 1271.0))
    return cands


def _fdp_frame_raw() -> tuple[list[Detection], list[OcrToken]]:
    detections = [
        Detection("yolo_1", "text_block", 0.8, Box(127, 138, 427, 195)),
        Detection("yolo_2", "text_block", 0.8, Box(126, 281, 426, 303)),
        Detection("yolo_3", "text_block", 0.8, Box(128, 440, 428, 460)),
        Detection("yolo_4", "text_block", 0.8, Box(128, 589, 428, 609)),
        Detection("yolo_5", "text_block", 0.8, Box(130, 746, 430, 768)),
        Detection("yolo_6", "text_block", 0.8, Box(125, 901, 425, 921)),
        Detection("yolo_7", "text_block", 0.8, Box(126, 1360, 426, 1384)),
        Detection("yolo_25", "text_block", 0.8, Box(127, 1047, 427, 1076)),
        Detection("yolo_28", "text_block", 0.8, Box(128, 1201, 428, 1237)),
        Detection("yolo_29", "text_block", 0.8, Box(126, 1245, 481, 1271)),
    ]
    ocr = [
        OcrToken(f"ocr_{i}", text, 0.95, Box(*bounds))
        for i, text, bounds in [
            (1, "Mobile, Wi-Fi, hotspot", (130, 141, 430, 192)),
            (2, "Connected devices", (129, 284, 429, 300)),
            (3, "Apps", (131, 443, 431, 457)),
            (4, "Notifications", (131, 592, 431, 606)),
            (5, "Battery", (133, 749, 433, 765)),
            (6, "Storage", (128, 904, 428, 918)),
            (7, "Wallpaper", (129, 1363, 429, 1381)),
            (25, "Sound & vibration", (130, 1050, 360, 1072)),
            (28, "Display", (131, 1204, 238, 1233)),
            (29, "Dark theme, font size, brightness", (129, 1248, 478, 1268)),
        ]
    ]
    return detections, ocr


def _raw_bundle(detections, ocr, width: int, height: int) -> dict:
    return {
        "detections": [d.to_json(width, height) for d in detections],
        "ocr": [t.to_json(width, height) for t in ocr],
        "width": width,
        "height": height,
    }


def _full_pipeline(candidates, detections, ocr, width=720, height=1400):
    out, trace = execute_pipeline(
        copy.deepcopy(candidates), detections,
        input_sources={
            "yolo": [d.to_json(width, height) for d in detections],
            "ocr": [t.to_json(width, height) for t in ocr],
        },
        raw_sources=_raw_bundle(detections, ocr, width, height),
        capture_candidate_views=True,
    )
    steps = {s["operator"]: s for s in trace.steps}
    return out, steps


# ── RED→GREEN (the FDP frame, consensus semantics) ────────────────────────
#
# WI-P26-ROWFIX-A (fix #3, cadence consensus; Leader-authorized): the FDP
# frame's gaps [128, 158, 149, 158, 154, 461] now carry a valid majority
# cadence (pitch ≈ 151.5 from the lower-60% median; the 461px outlier is a
# 3×-pitch multi-step gap, no longer a veto).  uniform-list ACTIVATES and
# owns this frame — relation-head is DELEGATED (the activated path preserved
# G-2 byte-identical).  The decomposition of responsibility is therefore the
# mirror of the pre-consensus FDP: uniform-list composes the complete rows,
# and the cadence-off subtitle is absorbed as supporting row data rather
# than becoming an independent interactive element.

def test_fdp_frame_uniform_list_consensus_activates():
    """Precondition (consensus semantics): uniform-list ACTIVATES on the FDP
    frame — the cadence consensus gate admits the outlier 461px gap as a
    3×-pitch multi-step instead of vetoing the whole model."""
    dets, ocr = _fdp_frame_raw()
    _, steps = _full_pipeline(_fdp_frame_candidates(), dets, ocr)
    uni = steps["uniform-list-row-grouping"]
    assert uni["status"] == "activated"
    assert "recovered 2 inferred row(s) within the bounded cadence envelope" in uni["detail"]
    assert uni["decisionInputs"]["confirmedAnchors"] == 7
    assert uni["decisionInputs"]["anchorGeometry"]["gaps"] == [128.0, 158.0, 149.0, 158.0, 154.0, 461.0]


def test_fdp_frame_uniform_list_owns_rows_relation_head_delegated():
    """GREEN (consensus semantics): with uniform-list ACTIVATED, ownership
    follows ACTUAL composition success — relation-head delegates (byte-touch
    nothing), uniform-list composes the complete rows 'Sound & vibration' and
    'Display' as bracketed rows, and the cadence-off subtitle
    'Dark theme, font size, brightness' is absorbed as supporting row data
    (its OCR joins the Display row) — never promoted, never an independent
    text_block or menu_item (fail-closed stays closed)."""
    dets, ocr = _fdp_frame_raw()
    out, steps = _full_pipeline(_fdp_frame_candidates(), dets, ocr)
    uni = steps["uniform-list-row-grouping"]
    relation = steps["row-relation-head"]
    assert uni["status"] == "activated"
    assert relation["status"] == "noop"                 # delegated, NOT skipped
    assert relation["detail"].startswith("delegated:")
    row_texts = {c["text"] for c in out if c.get("type") == "menu_item"}
    assert "Sound & vibration" in row_texts             # complete row composed
    assert "Display" in row_texts                       # complete row composed
    composed = {c["text"]: c for c in out if c.get("type") == "menu_item"}
    assert composed["Sound & vibration"]["evidence"]["typeInferred"] == "uniform_list_bracketed_row"
    assert composed["Display"]["evidence"]["typeInferred"] == "uniform_list_bracketed_row"
    # The subtitle is absorbed as supporting row data — not independently
    # emitted under ANY type (no text_block, no menu_item, no NonInteractive).
    assert all(c["text"] != "Dark theme, font size, brightness" for c in out)
    assert "ocr_29" in composed["Display"]["evidence"]["ocrIds"]
    # confirmed anchors are never rewritten
    assert "Mobile, Wi-Fi, hotspot" in row_texts


def test_fdp_frame_genuinely_noop_uniform_list_still_composes_rows():
    """WI-P26-ROWFIX-B: the original delegation intent, preserved with a
    GENUINELY-noop geometry — fewer than 2 direct gaps so the cadence
    consensus fails (uniform-list actually refuses), yet the frame's anchors
    still carry a majority cadence reference, so relation-head composes the
    complete rows when uniform-list legitimately noops (NOT delegated, NOT
    skipped).  Fail-closed: the off-envelope subtitle stays uncomposed."""
    anchors = [
        ("Row A", 100.0), ("Row B", 190.0), ("Row C", 300.0),
        ("Row D", 490.0), ("Row E", 680.0),
    ]
    # Uniform row-box heights (cy±10) so every box shares the page's font
    # modality — this test is about delegation semantics, not font-size
    # variance (the label-height rule correctly ignores equal-height rows).
    candidates = [
        _candidate(i + 1, text, cy, y1=cy - 10.0, y2=cy + 10.0)
        for i, (text, cy) in enumerate(anchors)
    ]
    # Leftover complete rows ON the 110px cadence (300+110, 490+110).
    candidates.append(_candidate(25, "Sound & vibration", 410.0, "text_block", 400.0, 420.0))
    candidates.append(_candidate(28, "Display", 600.0, "text_block", 590.0, 610.0))
    # Off-envelope subtitle (not at k*110 from any anchor center).
    candidates.append(_candidate(29, "Dark theme, font size, brightness", 1300.0, "text_block", 1292.0, 1308.0))

    dets = [Detection(c["evidence"]["yoloId"], "text_block", 0.8, Box(*c["boundsPx"])) for c in candidates]
    ocr = [OcrToken(c["evidence"]["ocrIds"][0], c["text"], 0.95, Box(*c["boundsPx"])) for c in candidates if c["text"]]
    out, steps = _full_pipeline(candidates, dets, ocr)

    uni = steps["uniform-list-row-grouping"]
    relation = steps["row-relation-head"]
    # Genuine noop: cadence consensus fails (gaps [90, 110, 190, 190] have
    # only ONE direct gap vs the 110px pitch).
    assert uni["status"] == "noop"
    assert "cadence model not inferable" in uni["detail"]
    assert uni["decisionInputs"]["anchorGeometry"]["gaps"] == [90.0, 110.0, 190.0, 190.0]
    # uniform-list noop => relation-head composes (delegation intent intact).
    assert relation["status"] == "activated"
    assert "delegated" not in (relation["detail"] or "")
    row_texts = {c["text"] for c in out if c.get("type") == "menu_item"}
    assert "Sound & vibration" in row_texts
    assert "Display" in row_texts
    composed = {c["text"]: c for c in out if c.get("type") == "menu_item"}
    assert composed["Sound & vibration"]["evidence"]["typeInferred"] == "row_relation_head"
    assert composed["Display"]["evidence"]["typeInferred"] == "row_relation_head"
    # Off-envelope subtitle is never promoted: stays an uncomposed text_block.
    assert "Dark theme, font size, brightness" not in row_texts
    assert "Dark theme, font size, brightness" in {
        c["text"] for c in out if c.get("type") == "text_block"
    }
    # Confirmed anchors are never rewritten, and relation-head composes only
    # the in-envelope leftovers (never invents rows beyond confirmed).
    assert set(r for r in ("Row A", "Row B", "Row C", "Row D", "Row E")) <= row_texts
    assert len([t for t in row_texts if t not in ("Row A", "Row B", "Row C", "Row D", "Row E")]) == 2


def test_activated_frame_still_delegates_fallback_absent():
    """Counterexample 1: uniform-list ACTIVATED -> relation-head delegated;
    no fallback rewrite (G-2 activated path unchanged)."""
    candidates = [
        _candidate(1, "Row 1", 100.0),
        _candidate(2, "Row 2", 250.0),
        _candidate(3, "Row 3", 400.0),
        _candidate(4, "Row 4", 550.0),
    ]
    _, steps = _full_pipeline(candidates, [], [])
    relation = steps["row-relation-head"]
    assert relation["status"] == "noop"
    assert (relation["detail"] or "").startswith("delegated:")


def test_irregular_frame_stays_refused_ambiguity_fail_closed():
    """Counterexample / frozen safety: anchors with NO majority cadence
    reference stay refused — a non-row label between irregular rows is never
    promoted (the pre-repair safety suite stays green on this class)."""
    candidates = [
        _candidate(1, "Confirmed 1", 100.0),
        _candidate(2, "Confirmed 2", 200.0),
        _candidate(3, "Confirmed 3", 350.0),
        _candidate(4, "Confirmed 4", 500.0),
        _candidate(5, "Static information", 280.0, "text_block", 265.0, 295.0, x1=120.0),
    ]
    dets = [Detection("t1", "text_block", 0.9, Box(120, 85, 300, 115)),
            Detection("t2", "text_block", 0.9, Box(120, 185, 300, 215)),
            Detection("t3", "text_block", 0.9, Box(120, 335, 300, 365)),
            Detection("t4", "text_block", 0.9, Box(120, 485, 300, 515)),
            Detection("t5", "text_block", 0.9, Box(120, 265, 310, 295))]
    ocr = [OcrToken(f"o{i}", t, 0.9, Box(b[0], b[1], b[2], b[3]))
           for i, (t, b) in enumerate(
               [("Confirmed 1", (122, 88, 298, 112)), ("Confirmed 2", (122, 188, 298, 212)),
                ("Confirmed 3", (122, 338, 298, 362)), ("Confirmed 4", (122, 488, 298, 512)),
                ("Static information", (122, 268, 308, 292))], start=1)]
    out, steps = _full_pipeline(candidates, dets, ocr, width=600, height=1200)
    relation = steps["row-relation-head"]
    assert relation["status"] == "noop"
    assert "fallback scope refused" in relation["detail"]
    assert "Static information" not in {c["text"] for c in out if c.get("type") == "menu_item"}


def test_invention_bound_never_exceeds_confirmed_rows():
    """Counterexample / frozen safety: when the envelope-aligned leftovers
    exceed the confirmed-anchor count, the fallback refuses the frame (the
    uniform-list hard invariant 'never invent more rows than confirmed')."""
    candidates = [
        _candidate(1, "Confirmed 1", 100.0),
        _candidate(2, "Confirmed 2", 200.0),
        _candidate(3, "Confirmed 3", 350.0),
        _candidate(4, "Confirmed 4", 500.0),
        _candidate(5, "Confirmed 5", 650.0),
    ]
    # 6 aligned leftovers on 5 anchors -> cap refusal.
    leftovers = [
        ("Missing 200", 200.0), ("Missing 300", 300.0), ("Missing 400", 400.0),
        ("Missing 700", 700.0), ("Missing 800", 800.0), ("Missing 900", 900.0),
    ]
    for i, (text, cy) in enumerate(leftovers, start=100):
        candidates.append(_candidate(i, text, cy, "text_block", cy - 15, cy + 15))
    dets = [Detection("d1", "text_block", 0.9, Box(120, 85, 300, 115)),
            Detection("d2", "text_block", 0.9, Box(120, 185, 300, 215)),
            Detection("d3", "text_block", 0.9, Box(120, 335, 300, 365)),
            Detection("d4", "text_block", 0.9, Box(120, 485, 300, 515)),
            Detection("d5", "text_block", 0.9, Box(120, 635, 300, 665))]
    ocr = [OcrToken(f"o{i}", t, 0.9, Box(b[0], b[1], b[2], b[3])) for i, (t, b) in enumerate(
        [("Confirmed 1", (122, 88, 298, 112)), ("Confirmed 2", (122, 188, 298, 212)),
         ("Confirmed 3", (122, 338, 298, 362)), ("Confirmed 4", (122, 488, 298, 512)),
         ("Confirmed 5", (122, 638, 298, 662))], start=1)]
    traces: list[dict] = []
    execute_pipeline(
        copy.deepcopy(candidates), dets,
        input_sources={"yolo": [], "ocr": []},
        raw_sources=_raw_bundle(dets, ocr, 600, 1200),
        capture_candidate_views=True,
    )
    # uniform-list refuses (cap-exceeded) and the fallback must not invent rows.
    out, steps = _full_pipeline(candidates, dets, ocr, width=600, height=1200)
    relation = steps["row-relation-head"]
    assert relation["status"] in ("noop", "activated")
    # fusion output never gains fabricated rows beyond the confirmed anchors
    rows = {c["text"] for c in out if c.get("type") == "menu_item"}
    assert not any(text in rows for text, _ in leftovers)


# ── legacy/direct-invocation compatibility ────────────────────────────────

def test_direct_router_invocation_keeps_count_only_delegation():
    """Direct runner calls (no pipeline context / no previous decision) keep
    the pre-repair count-only delegation for compatibility."""
    from uniclaw_perception.operators.relation_head_router import (
        run_row_relation_head_routed,
    )
    from uniclaw_perception.operators.uniform_list_row_grouping import _confirmed_rows  # noqa: F401
    candidates = [
        _candidate(1, "Row 1", 100.0),
        _candidate(2, "Row 2", 250.0),
        _candidate(3, "Row 3", 400.0),
        _candidate(4, "Row 4", 550.0),
    ]
    decision = run_row_relation_head_routed(
        copy.deepcopy(candidates), [], {}, raw_sources=None)
    assert decision["status"] == "noop"
    assert (decision["detail"] or "").startswith("delegated:")


def test_uniform_list_noop_greater_than_floor_not_delegated():
    """The repaired ownership rule: uniform-list NOOP on a >=4-anchor frame is
    NOT delegated — the fallback is attempted (fail-closed only when the raw
    sources are absent)."""
    from uniclaw_perception.operators.relation_head_router import (
        run_row_relation_head_routed,
    )
    candidates = [
        _candidate(1, "Row 1", 100.0),
        _candidate(2, "Row 2", 250.0),
        _candidate(3, "Row 3", 410.0),
        _candidate(4, "Row 4", 580.0),
    ]
    prev = {"status": "noop", "detail": "fail-closed: cadence model not inferable"}
    decision = run_row_relation_head_routed(
        copy.deepcopy(candidates), [], {}, raw_sources=None,
        previous_generator_decision=prev)
    assert decision["status"] == "noop"
    assert (decision["detail"] or "").startswith("fail-closed:")  # NOT delegated