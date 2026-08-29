"""FRAME_LOCAL_COMPOSITION_VALIDITY_VETO_REPAIR_GATE — RED→GREEN falsifier.

FDP (trace-proven on the real Display-subpage run): relation-head bands with
per-band columns were ROLLED BACK by the spacing-verifier's C4 column-spread
check — a uniform-list single-column grid premise — collapsing the navigation
projection 11->1 (seq 9: "column spread 95px exceeds the tolerance bound
42.4px") and exhausting the quiescence budget.

Authorized repair (spacing_verifier only; thresholds untouched):

* C1/C2/C3/C5 keep covering ALL generated rows (structure / containment /
  provenance / cap / vertical cadence).
* C4 column-spread executes ONLY on rows with uniform-list provenance
  (``UNIFORM_LIST_ROW_REASONS``) — the set whose construction guarantees the
  grid premise.
* relation-head bands are no longer wholesale-vetoed on the uniform-list
  single-column assumption; genuine uniform-list column misalignment still
  fails closed.

RED premise: the seq-9 captured geometry (12 relation-head rows, x1 spread 95,
median step 106 -> bound 42.4) is vetoed wholesale by the pre-repair rule.
"""
from __future__ import annotations

from uniclaw_perception.operators.spacing_verifier import (
    UNIFORM_LIST_ROW_REASONS,
    VERIFIER_PARAM_DEFAULTS,
    verify,
)

_PARAMS = dict(VERIFIER_PARAM_DEFAULTS)


def _menu(
    identifier: str,
    text: str,
    x1: float,
    y1: float,
    x2: float,
    y2: float,
    inferred: str,
) -> dict:
    return {
        "id": identifier,
        "type": "menu_item",
        "text": text,
        "confidence": 0.9,
        "bounds": {
            "x1": round(x1 / 1080.0, 6),
            "y1": round(y1 / 2400.0, 6),
            "x2": round(x2 / 1080.0, 6),
            "y2": round(y2 / 2400.0, 6),
        },
        "boundsPx": [x1, y1, x2, y2],
        "center": {
            "x": round((x1 + x2) / 2.0 / 1080.0, 6),
            "y": round((y1 + y2) / 2.0 / 2400.0, 6),
        },
        "centerPx": [round((x1 + x2) / 2.0), round((y1 + y2) / 2.0)],
        "evidence": {"typeInferred": inferred, "ocrIds": [], "allIds": [identifier]},
        "riskFlags": [],
    }


def _seq9_row_set() -> list[dict]:
    """The captured seq-9 Display-subpage composition: 12 generated rows
    (relation-head bands + the top garbage band) with x1 spread 95px and a
    median step ~106px (C4 bound = max(24, 2*0.20*106) = 42.4px)."""
    labels = [
        ("band_1", "ispiay", 127),
        ("band_2", "Brightness level", 127),
        ("band_3", "Lock display", 128),
        ("band_4", "Lock screen", 130),
        ("band_5", "Screen timeout", 125),
        ("band_6", "Not set", 126),
        ("band_7", "Appearance", 127),
        ("band_8", "Will never turn on automatically", 128),
        ("band_9", "Display size and text", 222),
        ("band_10", "Color", 208),
        ("band_11", "Colors", 127),
        ("band_12", "Other display controls", 222),
    ]
    rows = []
    for index, (identifier, text, x1) in enumerate(labels):
        y1 = 300 + index * 106
        rows.append(_menu(identifier, text, x1, y1, x1 + 240, y1 + 45, "row_relation_head"))
    return rows


def _uniform_misaligned() -> list[dict]:
    """Genuine uniform-list column misalignment (spread 95 > bound)."""
    rows = []
    centers = [300, 420, 540, 660, 780, 900, 1020, 1140, 1260, 1380, 1500, 1620]
    x1s = [127, 127, 127, 127, 127, 127, 127, 127, 127, 222, 127, 127]
    for index, (cy, x1) in enumerate(zip(centers, x1s)):
        rows.append(_menu(f"u_{index}", f"Row {index}", x1, cy, x1 + 240, cy + 40,
                          "uniform_list_bracketed_row"))
    return rows


def test_red_premise_seq9_spread_exceeds_bound() -> None:
    """RED premise: the seq-9 geometry IS a C4 violation under the old rule
    (95px > 42.4px) — the wholesale-rollback trigger."""
    rows = _seq9_row_set()
    xs = [r["boundsPx"][0] for r in rows]
    ys = [r["boundsPx"][1] for r in rows]
    gaps = sorted(b - a for a, b in zip(ys, ys[1:]))
    median_gap = gaps[len(gaps) // 2]
    bound = max(_PARAMS["columnToleranceFloor"], 2 * _PARAMS["columnToleranceRatio"] * median_gap)
    assert max(xs) - min(xs) > bound


def test_seq9_relation_head_wide_spread_verifies_green() -> None:
    """GREEN: relation-head bands with a wide per-band column spread are NOT
    vetoed by the uniform-list single-column premise — C4 is scoped to
    uniform-list provenance; C1/C2/C3/C5 still pass -> verified."""
    verdict = verify(_seq9_row_set(), [], _PARAMS)
    assert verdict["status"] == "verified", verdict["detail"]
    assert "column spread" not in verdict["detail"]


def test_genuine_uniform_list_column_misalignment_still_rejects() -> None:
    """Counterexample: TRUE uniform-list column misalignment still fails
    closed (C4 retains its veto on uniform-list provenance rows)."""
    verdict = verify(_uniform_misaligned(), [], _PARAMS)
    assert verdict["status"] == "rejected"
    assert "column spread" in verdict["detail"]


def test_aligned_uniform_list_rows_verify() -> None:
    """Aligned uniform-list rows verify (C4 bound respected on the grid)."""
    rows = []
    centers = [300, 420, 540, 660, 780, 900, 1020]
    for index, cy in enumerate(centers):
        rows.append(_menu(f"u_{index}", f"Row {index}", 127, cy, 367, cy + 40,
                          "uniform_list_bracketed_row"))
    verdict = verify(rows, [], _PARAMS)
    assert verdict["status"] == "verified", verdict["detail"]


def test_relation_head_malformed_bounds_still_rejected() -> None:
    """Counterexample: relation-head malformed structure (C1) still rejects."""
    row = _menu("band_1", "Display", 127, 300, 367, 345, "row_relation_head")
    row["boundsPx"] = [127, 345, 60, 300]  # inverted
    verdict = verify([row], [], _PARAMS)
    assert verdict["status"] == "rejected"
    assert "inverted boundsPx" in verdict["detail"]


def test_relation_head_unauthorized_provenance_still_rejected() -> None:
    """Counterexample: unauthorized provenance (C3) still rejects."""
    row = _menu("band_1", "Display", 127, 300, 367, 345, "not_a_generator")
    verdict = verify([row], [], _PARAMS)
    assert verdict["status"] == "rejected"
    assert "unauthorized row identity provenance" in verdict["detail"]


def test_relation_head_cap_exceeded_still_rejected() -> None:
    """Counterexample: the row cap (C3) still rejects relation-head rows."""
    params = dict(_PARAMS)
    params["maxMenuItems"] = 1
    verdict = verify(_seq9_row_set(), [], params)
    assert verdict["status"] == "rejected"
    assert "exceeds the bounded cap" in verdict["detail"]


def test_relation_head_vertical_cadence_still_rejected() -> None:
    """Counterexample: C5 vertical cadence still rejects relation-head rows
    (two bands nearly overlapping -> min step below the ratio bound)."""
    rows = [
        _menu("band_1", "Display", 127, 300, 367, 345, "row_relation_head"),
        _menu("band_2", "Display size and text", 127, 303, 367, 348, "row_relation_head"),
        _menu("band_3", "Color", 128, 420, 368, 465, "row_relation_head"),
    ]
    verdict = verify(rows, [], _PARAMS)
    assert verdict["status"] == "rejected"
    assert "minimum step" in verdict["detail"]


def test_mixed_frame_uniform_aligned_relation_head_wide_verifies() -> None:
    """The repaired frame shape: aligned uniform-list rows + wide-column
    relation-head bands coexist — C4 checks the uniform-list set only;
    relation-head bands pass C1/C2/C3/C5 -> verified (the seq-9 fix)."""
    rows = []
    centers = [300, 420, 540, 660, 780, 900, 1020]
    for index, cy in enumerate(centers):
        rows.append(_menu(f"u_{index}", f"Uniform row {index}", 127, cy, 367, cy + 40,
                          "uniform_list_bracketed_row"))
    for band_index, cy in enumerate([1140, 1260, 1380]):
        rows.append(_menu(f"relation_head_band_{band_index}", "Sub row", 222 if band_index == 1 else 208,
                          cy, 462 if band_index == 1 else 448, cy + 45, "row_relation_head"))
    verdict = verify(rows, [], _PARAMS)
    assert verdict["status"] == "verified", verdict["detail"]