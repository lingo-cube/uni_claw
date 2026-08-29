"""``row-relation-head`` runner adapter + code-owned engine routing (S2ii, WI-PFW-S2ii).

Bridges the S2i operator's FROZEN raw-input signature
(``row_relation_head.run(detections, ocr_tokens, width, height, params)``) to
the pipeline executor's runner protocol ``(candidates, yolo, resolved_values)``.
Registered in ``RUNNERS`` under the ``row-relation-head`` operator id and
declared in the pipeline AFTER ``uniform-list-row-grouping`` and BEFORE
``spacing-verifier`` (the S2ii append; see ``registry_defaults``).

**Single code-owned routing point** (spec: *"Pipeline topology is code-owned;
rules parameterize only"* — the anchor-availability decision is CODE, not a
rule set):

1. Anchor availability: count confirmed rows the same way the uniform-list
   generator counts them (its ``_confirmed_rows`` helper, evaluated on the
   candidates AFTER uniform-list already ran — i.e. post its own trailing-
   control / clipped-edge mutations).  If the count is at least the uniform-list
   activation floor (``uniform-list-row-grouping.minAnchors`` default, 4), the
   uniform-list generator owns composition: this adapter NOOPS with a
   "delegated" reason and the candidates are byte-untouched — the ≥4-anchor
   path preserves the S1 output exactly (the G-2 hard gate).
2. Below the floor, the adapter runs the relation-head operator on the RAW
   visual regions (uncombined detector boxes + OCR text blocks from the
   engine's ``input_sources`` — G-4: never composed candidates) and MERGES its
   emitted navigation candidates + NonInteractive satellites into the fused
   candidate list.

**Merge policy (deterministic, geometric + text-identity — never text
semantics):**

* a relation-head candidate sharing the SAME VISUAL LINE as an already
  composed navigation row (``menu_item``) is suppressed — one physical line,
  one navigation candidate (fail-closed over emitting a duplicate line that
  the verifier would have to veto; the verifier stays a pure backstop);
* a relation-head candidate whose visible text duplicates an engine-classified
  non-navigation candidate (``input``/``button``/controls/``icon``) at the
  same line is suppressed — the engine's classified line wins (the v1n search
  box is never promoted);
* same text at a DIFFERENT position stays a distinct row (no merge; verified
  by the cross-UI corpus family 1).

The suppressed candidate's satellites are suppressed with it.

The adapter is fail-closed when raw sources are absent: it never falls back to
composing from fused candidates (``noop`` with a recorded reason).
"""
from __future__ import annotations

import math
import statistics
from typing import Any, Mapping, Sequence

from .row_relation_head import run as run_relation_head
from .uniform_list_row_grouping import GROUPING_PARAM_DEFAULTS, _confirmed_rows

__all__ = [
    "ROUTING_MIN_ANCHORS",
    "NON_NAVIGATION_TYPES",
    "run_row_relation_head_routed",
]

#: Routing floor = the uniform-list activation threshold (its ``minAnchors``
#: contract default).  Code-owned: the S2 wiring resolves the root-only default
#: rule set, where ``uniform-list-row-grouping.minAnchors`` IS this default, so
#: resolved minAnchors == 4 == ROUTING_MIN_ANCHORS for every executed frame.
ROUTING_MIN_ANCHORS: int = int(GROUPING_PARAM_DEFAULTS["minAnchors"])

#: Fused candidate types that the engine already classified as NON-navigation:
#: search/input/button and control/widget kinds can never be promoted to rows.
#: A relation-head candidate duplicating one of these at the same visual line
#: is suppressed (the engine's classified line wins; relation-head composes
#: rows, not inputs/controls).
NON_NAVIGATION_TYPES: frozenset[str] = frozenset({
    "input", "button", "toggle", "switch", "checkbox", "slider", "icon",
})

#: Engine-classified NON-navigation types whose same-text same-line occurrence
#: marks the relation-head candidate as a duplicate of one physical line
#: (``menu_item`` line-occupancy is handled separately in ``_existing_duplicate``).
_DUPLICATE_TYPES: frozenset[str] = NON_NAVIGATION_TYPES

#: Same-line tolerance for duplicate detection: a relation-head head and an
#: existing fused candidate describe the same visual line when their vertical
#: centers agree within this geometric band (scale-aware: at least one line
#: height, floored at 24px).
_DUPLICATE_LINE_BAND = 0.75

#: Stable reason strings (deterministic trace content).
#: FRAME_LOCAL_FUSION_ROLE_STABILITY_REPAIR_GATE: delegation now expresses
#: "uniform-list actually composed the frame" (activated), not just
#: "there were enough anchors" — the located FDP was a NOOP uniform-list with
#: a count-only delegated fallback, leaving complete rows as text_block.
_DELEGATED_NOTICE_ACTIVATED = (
    f"delegated: uniform-list-row-grouping activated (composed this frame) — "
    "code-owned routing gate; candidates unchanged"
)
_DELEGATED_NOTICE_COUNT_ONLY = (
    f"delegated: >= {ROUTING_MIN_ANCHORS} confirmed anchors — "
    "uniform-list-row-grouping owns composition (legacy direct-invocation "
    "routing gate; candidates unchanged)"
)
_DELEGATED_NOTICE = _DELEGATED_NOTICE_ACTIVATED
#: FRAME_LOCAL_FUSION_ROLE_STABILITY_REPAIR_GATE — fallback-scope refusals
#: (ambiguity stays fail-closed; the frozen irregular-frame safety is kept):
#: the anchors carry no majority cadence reference, so any fallback
#: composition would be an unproven guess.
_FALLBACK_NO_CADENCE = (
    "fail-closed: fallback scope refused — confirmed anchors carry no "
    "majority-aligned cadence reference (irregular frame ambiguity); "
    "unresolved rows stay uncomposed"
)
#: FRAME_LOCAL_FUSION_ROLE_STABILITY_REPAIR_GATE — invention bound: the
#: fallback composes at most as many rows as the frame directly confirmed
#: (the uniform-list hard invariant ``len(proposals) > len(anchors)`` noops).
_FALLBACK_CAP = (
    "fail-closed: fallback scope refused — envelope-aligned rows exceed the "
    "confirmed-anchor count (inference bound; no rows invented)"
)
_NO_RAW_SOURCES = (
    "fail-closed: raw visual sources not provided to the pipeline runner "
    "(row-relation-head consumes only uncombined detector boxes + OCR text "
    "blocks; never composed candidates)"
)


def run_row_relation_head_routed(
    candidates: list[dict[str, Any]],
    yolo_detections: list[Any],
    values: Mapping[str, Any],
    raw_sources: Mapping[str, Any] | None = None,
    previous_generator_decision: Mapping[str, Any] | None = None,
) -> dict[str, Any]:
    """Pipeline runner entry for ``row-relation-head`` (adapter + routing).

    ``(candidates, yolo_detections, values)`` is the executor's fixed runner
    protocol; ``raw_sources`` is the optional engine-supplied raw visual
    source bundle (``detections``/``ocr`` to_json arrays + ``width``/``height``)
    forwarded only to runners that declare ``handles_raw_sources`` (see
    ``operators/trace.py``).  ``previous_generator_decision`` is the pipeline
    executor's optional forward of the PRECEDING GENERATOR's decision
    (FRAME_LOCAL_FUSION_ROLE_STABILITY_REPAIR_GATE).  Deterministic
    pure-ish orchestration: mutates ``candidates`` only when it appends
    relation-head output.

    **Ownership rule (repair gate) — ownership by ACTUAL composition success,
    not by anchor count alone:**

    * uniform-list ``activated`` (the cadence model was inferable and it
      composed, possibly recovering 0 rows of an already-composed frame) →
      uniform-list owns composition: this adapter NOOPS with a "delegated"
      reason and the candidates are byte-untouched — the activated path
      preserves the S1 output exactly (the G-2 hard gate).
    * uniform-list ``noop``/fail-closed (model not inferable / below minAnchors
      / cap exceeded / disabled) → uniform-list did NOT own the frame:
      the adapter composes the REMAINING unresolved rows from the RAW visual
      regions (uncombined detector boxes + OCR text blocks; G-4) and merges
      its emitted navigation candidates + NonInteractive satellites into the
      fused candidate list.  This is the located FDP fix: previously the
      count-only check ``confirmedAnchors >= 4`` skipped the fallback even
      though uniform-list had just refused composition.
    * Direct invocations without ``previous_generator_decision`` keep the
      legacy count-only delegation (``confirmedAnchors >= ROUTING_MIN_ANCHORS``)
      for behavior compatibility.
    """
    uniform_list_owned = (
        previous_generator_decision is not None
        and previous_generator_decision.get("status") == "activated"
    )
    if uniform_list_owned:
        return {
            "status": "noop",
            "detail": _DELEGATED_NOTICE_ACTIVATED,
            "emitted": 0,
        }
    if previous_generator_decision is None and len(_confirmed_rows(candidates)) >= ROUTING_MIN_ANCHORS:
        # Legacy direct-invocation path (no pipeline context): preserve the
        # pre-repair count-only delegation behavior byte-for-byte.
        return {
            "status": "noop",
            "detail": _DELEGATED_NOTICE_COUNT_ONLY,
            "emitted": 0,
        }

    # (2) Frozen-input path (G-4): raw visual regions only.
    if raw_sources is None:
        return {"status": "noop", "detail": _NO_RAW_SOURCES, "emitted": 0}
    # Unified key is ``detections`` (the shared ``build_raw_sources`` bundle
    # and the trace-document contract); ``yolo`` is a defensive fallback for
    # any stale engine bundle predating the unification (WI-PFW-S2fix).  Since
    # the fix the engine emits only the unified key.
    detections = raw_sources.get("detections") or raw_sources.get("yolo") or []
    ocr_tokens = raw_sources.get("ocr") or []
    width = int(raw_sources["width"])
    height = int(raw_sources["height"])

    # FRAME_LOCAL_FUSION_ROLE_STABILITY_REPAIR_GATE — fallback scope: on a
    # HIGH-anchor frame (>= ROUTING_MIN_ANCHORS confirmed rows) where
    # uniform-list refused composition, the fallback may compose only rows
    # that lie inside the anchor cadence envelope (same pitch/column/
    # tolerance constants; NO tolerance change).  Frames whose anchors carry
    # no majority-aligned cadence (genuinely irregular) stay refused
    # (ambiguity fail-closed), and the fallback never invents more rows than
    # the frame directly confirmed.  LOW-anchor frames (the pre-existing
    # < ROUTING_MIN_ANCHORS path) run the fallback unfiltered, as before.
    anchors = _confirmed_rows(candidates)
    envelope: dict[str, Any] | None = None
    if len(anchors) >= ROUTING_MIN_ANCHORS:
        envelope = _cadence_envelope(anchors)
        if not envelope["valid"]:
            return {"status": "noop", "detail": _FALLBACK_NO_CADENCE, "emitted": 0}

    record = run_relation_head(detections, ocr_tokens, width, height, params=values)
    if record.get("status") != "activated":
        return record

    # (3) Merge per the decision record: append each emitted navigation
    # candidate (and its satellites) that does not duplicate an already fused
    # row-bearing line and — on scoped (high-anchor) frames — lies inside the
    # anchor cadence envelope; deterministic band order.
    satellites_by_band: dict[str, list[dict[str, Any]]] = {}
    for satellite in record.get("satellites", []):
        satellite_id = str(satellite.get("id", ""))
        prefix = satellite_id.rsplit("_sat_", 1)[0] if "_sat_" in satellite_id else satellite_id
        satellites_by_band.setdefault(prefix, []).append(satellite)

    acceptable: list[dict[str, Any]] = []
    suppressed = 0
    for candidate in record.get("candidates", []):
        if _existing_duplicate(candidate, candidates) is not None:
            suppressed += 1
            continue
        if envelope is not None and not _in_cadence_envelope(candidate, envelope):
            # NOT inside the confirmed-anchor cadence envelope: a non-row /
            # off-cadence label must not be promoted into a navigation row
            # (ambiguity / evidence-insufficient fail-closed).
            suppressed += 1
            continue
        acceptable.append(candidate)

    if envelope is not None and len(acceptable) > len(anchors):
        # The hard uniform-list invariant: never invent more rows than were
        # directly confirmed.  Refuse the whole fallback (nothing merged).
        return {"status": "noop", "detail": _FALLBACK_CAP, "emitted": 0}

    merged = 0
    for candidate in acceptable:
        candidates.append(candidate)
        candidates.extend(satellites_by_band.get(str(candidate.get("id", "")), []))
        merged += 1

    return {
        "status": "activated",
        "detail": (
            f"{record['detail']}; merged {merged} band head(s) into the fused "
            f"candidate list, suppressed {suppressed} duplicate/non-navigation/"
            "off-envelope line(s)"
        ),
        "emitted": merged,
    }


def _cadence_envelope(anchors: Sequence[dict[str, Any]]) -> dict[str, Any]:
    """Derive the confirmed-anchor cadence reference for the fallback scope.

    Reuses the uniform-list cadence constants verbatim (median of the lower
    60% of anchor gaps as pitch; 14% relative multiple tolerance; column
    tolerance from ``xToleranceFloor``/``xToleranceRatio``; k = 1..4).  NO
    cadence tolerance is changed — this is a new code-owned READ of the frame,
    not a parameter change.

    ``valid`` is true only when the anchor gaps are STRICTLY-MAJORITY aligned
    to a shared pitch (the real FDP frames keep a majority cadence with one
    out-of-tolerance gap; genuinely irregular frames have no majority
    reference and stay refused).  Same constants as the uniform-list model;
    a strict per-gap requirement here would re-introduce the FDP class.
    """
    gaps: list[float] = []
    prev: float | None = None
    for anchor in anchors:
        cy = _center_y(anchor)
        if prev is not None:
            gap = cy - prev
            if gap > 0:
                gaps.append(gap)
        prev = cy
    if len(gaps) < 3:
        return {"valid": False}
    lower_count = max(2, math.ceil(len(gaps) * 0.60))
    pitch = float(statistics.median(sorted(gaps)[:lower_count]))
    if pitch <= 0:
        return {"valid": False}

    def _aligned(gap: float) -> bool:
        return any(
            abs(gap - k * pitch) <= float(GROUPING_PARAM_DEFAULTS["cadenceTolerance"]) * k * pitch
            for k in range(1, int(GROUPING_PARAM_DEFAULTS["maxCadenceSteps"]) + 1)
        )

    aligned_count = sum(1 for gap in gaps if _aligned(gap))
    if aligned_count * 2 <= len(gaps):
        return {"valid": False}

    x1s = [_x1(a) for a in anchors]
    column = float(statistics.median(x1s))
    x_tolerance = max(
        float(GROUPING_PARAM_DEFAULTS["xToleranceFloor"]),
        float(GROUPING_PARAM_DEFAULTS["xToleranceRatio"]) * pitch,
    )
    return {
        "valid": True,
        "pitch": pitch,
        "column": column,
        "xTolerance": x_tolerance,
        "anchorCenters": [_center_y(a) for a in anchors],
    }


def _in_cadence_envelope(
    candidate: dict[str, Any],
    envelope: Mapping[str, Any],
) -> bool:
    """Fallback-scope test: the emitted row's center lies at k×pitch from a
    confirmed anchor (k = 1..4, same 14% relative tolerance) and its LEFT EDGE
    sits on the anchor text column (same x tolerance; x1, not centerX — row
    boxes vary in width but share the left column).  Purely geometric — never
    text semantics; non-row labels land off-envelope and stay uncomposed
    (ambiguity / evidence-insufficient fail-closed)."""
    center = candidate.get("centerPx")
    bounds_px = candidate.get("boundsPx")
    if not isinstance(center, (list, tuple)) or len(center) < 2:
        return False
    if not isinstance(bounds_px, (list, tuple)) or len(bounds_px) < 4:
        return False
    cy = float(center[1])
    x1 = float(bounds_px[0])
    if abs(x1 - float(envelope["column"])) > float(envelope["xTolerance"]):
        return False
    pitch = float(envelope["pitch"])
    cadence_tolerance = float(GROUPING_PARAM_DEFAULTS["cadenceTolerance"])
    max_steps = int(GROUPING_PARAM_DEFAULTS["maxCadenceSteps"])
    for anchor_cy in envelope["anchorCenters"]:
        distance = abs(cy - float(anchor_cy))
        if any(
            abs(distance - k * pitch) <= cadence_tolerance * k * pitch
            for k in range(1, max_steps + 1)
        ):
            return True
    return False


def _existing_duplicate(
    row: dict[str, Any],
    existing: Sequence[dict[str, Any]],
) -> dict[str, Any] | None:
    """The first existing fused candidate that is the same physical line.

    * ``menu_item`` (row-bearing): candidate sharing its visual line (vertical
      span overlap ≥ half the shorter span) is the same physical row — never a
      second navigation candidate on one line.
    * Non-navigation types (input/button/controls/icon): same visible text
      (identity, never semantics) AND vertical-center agreement within the
      same-line band ⇒ the engine's classified line wins.

    Same text at a different position is a distinct row and is NOT a duplicate.
    """
    for entry in existing:
        entry_type = entry.get("type")
        if entry_type == "menu_item":
            if _shares_line(row, entry):
                return entry
            continue
        if entry_type not in _DUPLICATE_TYPES:
            continue
        if str(entry.get("text", "")).strip() != str(row.get("text", "")).strip():
            continue
        band = _same_line_band(_line_height(row), _line_height(entry))
        if abs(_center_y(row) - _center_y(entry)) <= band:
            return entry
    return None


def _shares_line(row: dict[str, Any], entry: dict[str, Any]) -> bool:
    """Two candidates describe the same visual line when their vertical spans
    overlap by at least half of the shorter span."""
    row_span = _vertical_span(row)
    entry_span = _vertical_span(entry)
    if row_span is None or entry_span is None:
        return False
    row_y1, row_y2 = row_span
    entry_y1, entry_y2 = entry_span
    shorter = min(row_y2 - row_y1, entry_y2 - entry_y1)
    if shorter <= 0:
        return False
    overlap = min(row_y2, entry_y2) - max(row_y1, entry_y1)
    return overlap >= 0.5 * shorter


def _vertical_span(candidate: dict[str, Any]) -> tuple[float, float] | None:
    bounds = candidate.get("boundsPx")
    if isinstance(bounds, (list, tuple)) and len(bounds) >= 4:
        return float(bounds[1]), float(bounds[3])
    return None


def _same_line_band(row_height: float, entry_height: float) -> float:
    return max(24.0, _DUPLICATE_LINE_BAND * max(row_height, entry_height))


def _center_y(candidate: dict[str, Any]) -> float:
    center = candidate.get("centerPx")
    if isinstance(center, (list, tuple)) and len(center) >= 2:
        return float(center[1])
    return 0.0


def _x1(candidate: dict[str, Any]) -> float:
    bounds = candidate.get("boundsPx")
    if isinstance(bounds, (list, tuple)) and len(bounds) >= 4:
        return float(bounds[0])
    return 0.0


def _line_height(candidate: dict[str, Any]) -> float:
    bounds = candidate.get("boundsPx")
    if isinstance(bounds, (list, tuple)) and len(bounds) >= 4:
        return max(0.0, float(bounds[3] - bounds[1]))
    return 0.0


#: Marker consumed by the pipeline executor: this runner accepts the
#: engine-supplied raw visual source bundle (detections/ocr/width/height).
run_row_relation_head_routed.handles_raw_sources = True  # type: ignore[attr-defined]