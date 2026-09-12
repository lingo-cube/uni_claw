"""``uniform-list-row-grouping`` GENERATOR operator (S1B port, WI-PFW-S1B).

Moved verbatim from ``fusion/row_grouping.py`` (the retained candidate) with
every *tunable* row-composition constant parameterized; parameter defaults are
the retained candidate's constants **exactly** (spec: "root-rule defaults equal
to the current candidate values"; IR-G0 unblock slices, S1 scenario
"behavior-identical to the retained candidate").  With the default rule set the
resolved parameters equal these defaults, so the operator executes the same
algorithm with the same constants the fused pipeline ran before the port — the
S1 zero-difference gate.

Parameter vs constant mapping (default = retained candidate constant):

* ``minAnchors`` = the four-anchor activation (``len(anchors) < 4`` twice).
* ``inferenceCap`` = ``0.50`` bracket-inference ratio cap; ``continuationCap``
  = ``0.30`` continued-chain cap; ``singleSlotCap`` = ``0.25`` single-slot
  bracket cap (``cap = 0.50 if bracket>1 else (0.30 if … else 0.25)``).
* ``maxUpperContinuations`` = ``upper_count < 2``; ``minLowerContinuationTotal``
  = ``len(anchors)+len(proposals) >= 6``; ``continuationFraction`` =
  ``floor((3.0/7.0)*len(anchors))`` upper bound.
* ``minCadenceSteps``/``maxCadenceSteps`` = bracket cadence steps 2..4;
  ``cadenceTolerance`` = ``_near(..., 0.14)``; ``slotYTolerance`` = ``0.48*pitch``;
  ``controlClearance`` = ``0.45*pitch``; ``anchorDuplicateBand`` = ``0.45*pitch``.
* ``xToleranceFloor``/``xToleranceRatio`` = ``max(12.0, 0.15*pitch)`` column
  tolerance; ``upperEdgeDeferY2``/``upperEdgeDeferY1`` = ``0.20``/``0.04``;
  ``lowerEdgeDeferY1``/``lowerEdgeDeferY2`` = ``0.80``/``0.94``; ``edgeSlotY1Min``
  = ``0.80``.

Constants that *characterize the uniform-list visual model* rather than a
row-composition cap stay structural in code (spec freedom: "if a constant is
truly structural, keep it in code"): the pitch/title-height model derivation
(median of the lower 60% of gaps, ``pitch >= 2.2*title_height``, direct/valid
gap tolerances), the title-band height filters (``0.18*pitch``,
``0.45..2.2*title_height``, compact-description 0.75/0.82/0.08..0.32 ratios),
and the pre-model candidate hygiene guards (trailing-control exclusion
``max(12, 0.75*h)``, clipped-edge ``0.72*title_height``).  The hard safety
invariant ``len(proposals) > len(anchors)`` (never invent more rows than were
directly confirmed) is structural.

Implements the spec operator contract: deterministic pure-function semantics
over (inputs, resolved parameters) and an explicit fail-closed outcome with
reason for insufficient inputs (no identity candidate is ever guessed).  The
runner returns a machine-readable decision record consumed by the pipeline
executor (``operators/trace.py``) for deterministic tracing.
"""
from __future__ import annotations

import math
import statistics
from dataclasses import dataclass
from typing import Any, Mapping

from .status import ACTIVATED, NOOP

__all__ = [
    "GROUPING_PARAM_DEFAULTS",
    "GROUPING_PARAM_BOUNDS",
    "apply_uniform_list_grouping_params",
    "run",
]

# ---------------------------------------------------------------------------
# Parameter surface: defaults are the retained candidate's constants verbatim.
# ---------------------------------------------------------------------------

#: Current-constant defaults for every tunable row-composition parameter.
#: Single source of truth: the contract specs (registry_defaults) and the
#: compatibility shim (fusion/row_grouping.py) both derive from this map.
GROUPING_PARAM_DEFAULTS: dict[str, Any] = {
    "minAnchors": 4,
    "inferenceCap": 0.50,
    "continuationCap": 0.30,
    "singleSlotCap": 0.25,
    "maxUpperContinuations": 2,
    "minLowerContinuationTotal": 6,
    "continuationFraction": 3.0 / 7.0,
    "minCadenceSteps": 2,
    "maxCadenceSteps": 4,
    "cadenceTolerance": 0.14,
    "slotYTolerance": 0.48,
    "controlClearance": 0.45,
    "anchorDuplicateBand": 0.45,
    "xToleranceFloor": 12.0,
    "xToleranceRatio": 0.15,
    "upperEdgeDeferY2": 0.20,
    "upperEdgeDeferY1": 0.04,
    "lowerEdgeDeferY1": 0.80,
    "lowerEdgeDeferY2": 0.94,
    "edgeSlotY1Min": 0.80,
}

#: Declared bounds for each parameter (type + inclusive numeric bounds).
#: GENERATOR parameters may move both ways; only VALIDATOR parameters carry a
#: safe direction.  ``inferenceCap`` is documented as (0,1]; NumericBounds is
#: inclusive, so the declared bound is [0.0, 1.0] — a rule binding 0.0 means
#: "never infer", which the algorithm honors fail-closed (any proposal would
#: exceed the ratio cap), so the inclusive lower bound adds no behavior.
GROUPING_PARAM_BOUNDS: dict[str, tuple[type, tuple[float, float]]] = {
    "minAnchors": (int, (1, 64)),
    "inferenceCap": (float, (0.0, 1.0)),
    "continuationCap": (float, (0.0, 1.0)),
    "singleSlotCap": (float, (0.0, 1.0)),
    "maxUpperContinuations": (int, (0, 8)),
    "minLowerContinuationTotal": (int, (2, 128)),
    "continuationFraction": (float, (0.0, 1.0)),
    "minCadenceSteps": (int, (1, 8)),
    "maxCadenceSteps": (int, (1, 8)),
    "cadenceTolerance": (float, (0.0, 1.0)),
    "slotYTolerance": (float, (0.0, 1.0)),
    "controlClearance": (float, (0.0, 1.0)),
    "anchorDuplicateBand": (float, (0.0, 1.0)),
    "xToleranceFloor": (float, (0.0, 10000.0)),
    "xToleranceRatio": (float, (0.0, 1.0)),
    "upperEdgeDeferY2": (float, (0.0, 1.0)),
    "upperEdgeDeferY1": (float, (0.0, 1.0)),
    "lowerEdgeDeferY1": (float, (0.0, 1.0)),
    "lowerEdgeDeferY2": (float, (0.0, 1.0)),
    "edgeSlotY1Min": (float, (0.0, 1.0)),
}


@dataclass(frozen=True)
class _GroupingParams:
    """Resolved row-composition parameters (defaults = retained constants).

    Built from a resolved-parameter mapping (camelCase keys, per the operator
    contract parameter names); missing keys fall back to the current-constant
    defaults so the compatibility shim can invoke the port with no rule set.
    """

    min_anchors: int = 4
    inference_cap: float = 0.50
    continuation_cap: float = 0.30
    single_slot_cap: float = 0.25
    max_upper_continuations: int = 2
    min_lower_continuation_total: int = 6
    continuation_fraction: float = 3.0 / 7.0
    min_cadence_steps: int = 2
    max_cadence_steps: int = 4
    cadence_tolerance: float = 0.14
    slot_y_tolerance: float = 0.48
    control_clearance: float = 0.45
    anchor_duplicate_band: float = 0.45
    x_tolerance_floor: float = 12.0
    x_tolerance_ratio: float = 0.15
    upper_edge_defer_y2: float = 0.20
    upper_edge_defer_y1: float = 0.04
    lower_edge_defer_y1: float = 0.80
    lower_edge_defer_y2: float = 0.94
    edge_slot_y1_min: float = 0.80

    @classmethod
    def from_mapping(cls, mapping: Mapping[str, Any] | None) -> "_GroupingParams":
        values = dict(GROUPING_PARAM_DEFAULTS)
        if mapping is not None:
            values.update(mapping)
        return cls(
            min_anchors=int(values["minAnchors"]),
            inference_cap=float(values["inferenceCap"]),
            continuation_cap=float(values["continuationCap"]),
            single_slot_cap=float(values["singleSlotCap"]),
            max_upper_continuations=int(values["maxUpperContinuations"]),
            min_lower_continuation_total=int(values["minLowerContinuationTotal"]),
            continuation_fraction=float(values["continuationFraction"]),
            min_cadence_steps=int(values["minCadenceSteps"]),
            max_cadence_steps=int(values["maxCadenceSteps"]),
            cadence_tolerance=float(values["cadenceTolerance"]),
            slot_y_tolerance=float(values["slotYTolerance"]),
            control_clearance=float(values["controlClearance"]),
            anchor_duplicate_band=float(values["anchorDuplicateBand"]),
            x_tolerance_floor=float(values["xToleranceFloor"]),
            x_tolerance_ratio=float(values["xToleranceRatio"]),
            upper_edge_defer_y2=float(values["upperEdgeDeferY2"]),
            upper_edge_defer_y1=float(values["upperEdgeDeferY1"]),
            lower_edge_defer_y1=float(values["lowerEdgeDeferY1"]),
            lower_edge_defer_y2=float(values["lowerEdgeDeferY2"]),
            edge_slot_y1_min=float(values["edgeSlotY1Min"]),
        )


# ---------------------------------------------------------------------------
# Operator runner
# ---------------------------------------------------------------------------

#: Structural type sets of the uniform-list visual model (not tunable).
_CONTROL_LABELS = {"switch", "toggle", "checkbox", "slider"}
_TITLE_TYPES = {"text_block", "list_item"}

#: Fail-closed outcome reasons (stable strings for the deterministic trace).
_NOOP_MODEL = "fail-closed: uniform-list cadence model not inferable (insufficient or irregular anchor geometry)"
_NOOP_ANCHORS = "fail-closed: fewer than minAnchors confirmed anchors"
_NOOP_CAP = "fail-closed: inference cap exceeded (would infer more rows than the bounded ratio admits)"


@dataclass(frozen=True)
class _UniformListModel:
    pitch: float
    title_x: float
    title_height: float
    x_tolerance: float


@dataclass(frozen=True)
class _Proposal:
    primary: dict[str, Any]
    absorbed: tuple[dict[str, Any], ...]
    inference_reason: str = "uniform_list_bracketed_row"


def apply_uniform_list_grouping_params(
    candidates: list[dict[str, Any]],
    yolo_detections: list[Any],
    params: Mapping[str, Any] | None = None,
) -> dict[str, Any]:
    """Recover uniquely bracketed rows in a proven uniform list (the ported
    retained candidate, parameterized).

    Behavior is byte-identical to the retained ``apply_uniform_list_grouping``
    when ``params`` equals the current-constant defaults (the resolution of the
    default rule set).  The operation is fail-closed: it activates only from
    ``minAnchors`` or more existing actionable rows, fills at most three
    consecutive cadence slots only when every slot is unambiguous, and never
    extrapolates a bracket beyond its confirmed neighbors.  Raw YOLO/OCR arrays
    are owned by the caller and are not modified.

    Returns a deterministic decision record for the pipeline trace:
    ``{"status": "activated"|"noop", "detail": ..., "emitted": int}``.
    """
    p = _GroupingParams.from_mapping(params)
    reason: str | None = None

    _exclude_rows_with_trailing_controls(candidates)

    anchors = _confirmed_rows(candidates)
    model = _infer_model(anchors, p)
    if model is None:
        return _decision(NOOP, _NOOP_MODEL, 0)

    _exclude_clipped_edge_rows(candidates, anchors, model)
    anchors = _confirmed_rows(candidates)
    if len(anchors) < p.min_anchors:
        return _decision(NOOP, _NOOP_ANCHORS, 0)
    _absorb_anchor_duplicates(candidates, anchors, model, p)
    anchors = _confirmed_rows(candidates)

    proposals: list[_Proposal] = []
    deferred_edge_ids: set[int] = set()
    claimed: set[int] = set()
    controls = [
        candidate for candidate in candidates
        if candidate.get("type") in _CONTROL_LABELS
        and _center_x(candidate) > model.title_x
    ]

    for upper, lower in zip(anchors, anchors[1:]):
        gap = _center_y(lower) - _center_y(upper)
        cadence_steps = int(round(gap / model.pitch))
        if cadence_steps < p.min_cadence_steps or cadence_steps > p.max_cadence_steps:
            continue
        if not _near(gap, cadence_steps * model.pitch, relative=p.cadence_tolerance):
            continue

        bracket: list[_Proposal] = []
        bracket_claimed: set[int] = set()
        for slot_index in range(1, cadence_steps):
            expected_y = _center_y(upper) + slot_index * model.pitch
            if any(abs(_center_y(control) - expected_y) <= p.control_clearance * model.pitch for control in controls):
                bracket = []
                break
            slot = [
                candidate for candidate in candidates
                if id(candidate) not in claimed
                and id(candidate) not in bracket_claimed
                and candidate.get("type") in _TITLE_TYPES
                and candidate.get("text", "").strip()
                and abs(_x1(candidate) - model.title_x) <= model.x_tolerance
                and abs(_center_y(candidate) - expected_y) <= p.slot_y_tolerance * model.pitch
            ]
            proposal = _propose_slot(slot, expected_y, model)
            if proposal is None:
                bracket = []
                break
            bracket.append(proposal)
            bracket_claimed.update(id(item) for item in proposal.absorbed)
        if len(bracket) != cadence_steps - 1:
            continue
        proposals.extend(bracket)
        claimed.update(bracket_claimed)

    bracket_proposal_count = len(proposals)

    # After a downward scroll, one or two prior rows may remain above the first
    # detector-confirmed row.  Recover only complete, unambiguous cadence slots.
    # If the next predicted slot is already clipped at the upper viewport edge,
    # suppress its fused fragments while retaining the caller-owned raw arrays.
    upper_count = 0
    expected_y = _center_y(anchors[0]) - model.pitch
    while upper_count < p.max_upper_continuations and len(proposals) < len(anchors):
        slot = [
            candidate for candidate in candidates
            if id(candidate) not in claimed
            and candidate.get("type") in _TITLE_TYPES
            and candidate.get("text", "").strip()
            and abs(_x1(candidate) - model.title_x) <= model.x_tolerance
            and abs(_center_y(candidate) - expected_y) <= p.slot_y_tolerance * model.pitch
        ]
        proposal = _propose_slot(slot, expected_y, model)
        if proposal is None:
            edge_components = [
                candidate for candidate in candidates
                if id(candidate) not in claimed
                and candidate.get("type") not in _CONTROL_LABELS | {"input"}
                and candidate.get("text", "").strip()
                and _x1(candidate) <= model.title_x + model.x_tolerance
                and abs(_center_y(candidate) - expected_y) <= p.slot_y_tolerance * model.pitch
            ]
            if edge_components and max(_normalized_y2(item) for item in edge_components) <= p.upper_edge_defer_y2:
                deferred_edge_ids.update(id(item) for item in edge_components)
            break
        if _normalized_y1(proposal.primary) <= p.upper_edge_defer_y1:
            deferred_edge_ids.update(id(item) for item in proposal.absorbed)
            break
        proposal = _Proposal(
            proposal.primary,
            proposal.absorbed,
            "uniform_list_upper_continuation",
        )
        proposals.append(proposal)
        claimed.update(id(item) for item in proposal.absorbed)
        upper_count += 1
        expected_y -= model.pitch

    # A strongly proven list may continue into the lower viewport without a
    # detector anchor.  This is bounded by the continuation/multi-bracket caps
    # and to complete, consecutive title slots.  The first missing/ambiguous/clipped
    # slot stops the chain; there is no arbitrary extrapolation or recursion
    # beyond the current frame's visible evidence.
    if len(anchors) + len(proposals) >= p.min_lower_continuation_total:
        # At most 30% of the final logical row inventory may be inferred.  This
        # admits the real five-anchor + one bracketed-row + one continuation
        # shape while still bounding a six-anchor list to two inferred rows.
        maximum_inferred = (
            len(anchors)
            if bracket_proposal_count > 1
            else math.floor(p.continuation_fraction * len(anchors))
        )
        expected_y = _center_y(anchors[-1]) + model.pitch
        while len(proposals) < maximum_inferred:
            if any(abs(_center_y(control) - expected_y) <= p.control_clearance * model.pitch for control in controls):
                break
            slot = [
                candidate for candidate in candidates
                if id(candidate) not in claimed
                and candidate.get("type") in _TITLE_TYPES
                and candidate.get("text", "").strip()
                and abs(_x1(candidate) - model.title_x) <= model.x_tolerance
                and abs(_center_y(candidate) - expected_y) <= p.slot_y_tolerance * model.pitch
            ]
            proposal = _propose_slot(slot, expected_y, model)
            if proposal is None:
                edge_components = [
                    candidate for candidate in candidates
                    if id(candidate) not in claimed
                    and candidate.get("type") not in _CONTROL_LABELS | {"input"}
                    and candidate.get("text", "").strip()
                    and _x1(candidate) <= model.title_x + model.x_tolerance
                    and abs(_center_y(candidate) - expected_y) <= p.slot_y_tolerance * model.pitch
                ]
                if edge_components and min(_normalized_y1(item) for item in edge_components) >= p.lower_edge_defer_y1:
                    deferred_edge_ids.update(id(item) for item in edge_components)
                break
            if _normalized_y2(proposal.primary) > p.lower_edge_defer_y2:
                deferred_edge_ids.update(id(item) for item in proposal.absorbed)
                break
            proposal = _Proposal(
                proposal.primary,
                proposal.absorbed,
                "uniform_list_lower_continuation",
            )
            proposals.append(proposal)
            claimed.update(id(item) for item in proposal.absorbed)
            expected_y += model.pitch

        # If the bounded inference budget is exhausted while one more exact
        # cadence slot begins at the lower viewport edge, defer that incomplete
        # logical row from fused occurrences.  Its raw YOLO/OCR stays intact and
        # the newly admitted continuation forces the next bounded scroll, where
        # the row can be observed away from the edge and proven normally.
        if len(proposals) >= maximum_inferred and proposals:
            edge_slot = [
                candidate for candidate in candidates
                if id(candidate) not in claimed
                and candidate.get("type") in _TITLE_TYPES
                and candidate.get("text", "").strip()
                and abs(_x1(candidate) - model.title_x) <= model.x_tolerance
                and abs(_center_y(candidate) - expected_y) <= p.slot_y_tolerance * model.pitch
            ]
            edge_proposal = _propose_slot(edge_slot, expected_y, model)
            if edge_proposal is not None and _normalized_y1(edge_proposal.primary) >= p.edge_slot_y1_min:
                deferred_edge_ids.update(id(item) for item in edge_proposal.absorbed)

    # A layout rule must not invent more rows than were directly confirmed.
    # Multi-slot bracket recovery may account for up to half of the final row
    # inventory, but every such row remains bounded by confirmed neighbors.
    # Single-slot and extrapolated continuation retain tighter caps.
    if proposals:
        inference_ratio = len(proposals) / (len(anchors) + len(proposals))
        cap = p.inference_cap if bracket_proposal_count > 1 else (
            p.continuation_cap if len(proposals) > bracket_proposal_count else p.single_slot_cap
        )
        if len(proposals) > len(anchors) or inference_ratio > cap:
            return _decision(NOOP, _NOOP_CAP, 0)

    absorbed_ids: set[int] = set()
    for proposal in proposals:
        primary = proposal.primary
        primary["type"] = "menu_item"
        evidence = primary.setdefault("evidence", {})
        evidence["ocrIds"] = _stable_union(
            item
            for component in proposal.absorbed
            for item in component.get("evidence", {}).get("ocrIds", [])
        )
        evidence["allIds"] = _stable_union(
            item
            for component in proposal.absorbed
            for item in component.get("evidence", {}).get("allIds", [])
        )
        evidence["typeInferred"] = proposal.inference_reason
        for clearable in ("ocr_only", "low_ocr_confidence"):
            if clearable in primary.get("riskFlags", []):
                primary["riskFlags"].remove(clearable)
        absorbed_ids.update(id(item) for item in proposal.absorbed if item is not primary)

    removed_ids = absorbed_ids | deferred_edge_ids
    if removed_ids:
        candidates[:] = [candidate for candidate in candidates if id(candidate) not in removed_ids]

    return _decision(
        ACTIVATED,
        f"recovered {len(proposals)} inferred row(s) within the bounded cadence envelope",
        len(proposals),
    )


def run(candidates: list[dict[str, Any]], yolo_detections: list[Any],
        params: Mapping[str, Any]) -> dict[str, Any]:
    """Pipeline runner protocol entry (operator_id ``uniform-list-row-grouping``)."""
    return apply_uniform_list_grouping_params(candidates, yolo_detections, params)


def _decision(status: str, detail: str, emitted: int) -> dict[str, Any]:
    return {"status": status, "detail": detail, "emitted": emitted}


def _confirmed_rows(candidates: list[dict[str, Any]]) -> list[dict[str, Any]]:
    return sorted(
        [
            candidate for candidate in candidates
            if candidate.get("type") == "menu_item" and candidate.get("text", "").strip()
        ],
        key=lambda candidate: (_center_y(candidate), _x1(candidate), candidate.get("id", "")),
    )


def _absorb_anchor_duplicates(
    candidates: list[dict[str, Any]],
    anchors: list[dict[str, Any]],
    model: _UniformListModel,
    p: _GroupingParams,
) -> None:
    """Collapse alternate detector boxes for an already confirmed title."""
    absorbed_ids: set[int] = set()
    for anchor in anchors:
        duplicates = [
            candidate for candidate in candidates
            if candidate is not anchor
            and candidate.get("type") in _TITLE_TYPES
            and candidate.get("text", "").strip() == anchor.get("text", "").strip()
            and abs(_center_y(candidate) - _center_y(anchor)) <= p.anchor_duplicate_band * model.pitch
        ]
        if not duplicates:
            continue
        components = [anchor, *duplicates]
        evidence = anchor.setdefault("evidence", {})
        evidence["ocrIds"] = _stable_union(
            item
            for component in components
            for item in component.get("evidence", {}).get("ocrIds", [])
        )
        evidence["allIds"] = _stable_union(
            item
            for component in components
            for item in component.get("evidence", {}).get("allIds", [])
        )
        evidence["typeInferred"] = "uniform_list_anchor_duplicate_absorbed"
        absorbed_ids.update(id(item) for item in duplicates)
    if absorbed_ids:
        candidates[:] = [candidate for candidate in candidates if id(candidate) not in absorbed_ids]


def _infer_model(anchors: list[dict[str, Any]], p: _GroupingParams) -> _UniformListModel | None:
    if len(anchors) < p.min_anchors:
        return None
    gaps = [
        _center_y(right) - _center_y(left)
        for left, right in zip(anchors, anchors[1:])
        if _center_y(right) > _center_y(left)
    ]
    if len(gaps) < 3:
        return None

    lower_count = max(2, math.ceil(len(gaps) * 0.60))
    pitch = float(statistics.median(sorted(gaps)[:lower_count]))
    heights = [_height(anchor) for anchor in anchors if _height(anchor) > 0]
    if not heights:
        return None
    title_height = float(statistics.median(heights))
    if pitch <= 0:
        return None
    if pitch < 2.2 * title_height:
        return None

    direct = [gap for gap in gaps if _near(gap, pitch, relative=0.15)]
    valid = [
        gap for gap in gaps
        if any(_near(gap, multiple * pitch, relative=0.14) for multiple in range(1, 5))
    ]
    # WI-P26-ROWFIX-A (fix #3, cadence consensus): replace the all-gates rule
    # (``valid == all``) with a majority-consensus gate — outlier gaps simply
    # don't participate as cadence anchors (they hit the ``continue`` branches
    # in the row-recovery loops, so no rows are ever fabricated for outlier
    # regions) instead of vetoing the whole cadence model.  Pitch inference
    # (median of the lower 60% of gaps) and every validator rule are unchanged.
    if len(direct) < 2 or len(valid) < max(3, math.ceil(0.6 * len(gaps))):
        return None

    title_x = float(statistics.median(_x1(anchor) for anchor in anchors))
    x_tolerance = max(p.x_tolerance_floor, p.x_tolerance_ratio * pitch)
    if any(abs(_x1(anchor) - title_x) > x_tolerance for anchor in anchors):
        return None
    return _UniformListModel(pitch, title_x, title_height, x_tolerance)


def _propose_slot(
    slot: list[dict[str, Any]],
    expected_y: float,
    model: _UniformListModel,
) -> _Proposal | None:
    if not slot:
        return None
    title_band = [
        candidate for candidate in slot
        if abs(_center_y(candidate) - expected_y) <= 0.18 * model.pitch
        and 0.45 * model.title_height <= _height(candidate) <= 2.2 * model.title_height
    ]
    if not title_band:
        return None

    # Overlapping detector interpretations with the same visible title are one
    # logical choice.  Distinct title strings at the expected baseline are a
    # genuine ambiguity and stay non-actionable.
    texts = {candidate.get("text", "").strip() for candidate in title_band}
    if len(texts) == 1:
        primary = min(
            title_band,
            key=lambda candidate: (
                abs(_center_y(candidate) - expected_y),
                _height(candidate),
                -float(candidate.get("confidence", 0.0)),
                candidate.get("id", ""),
            ),
        )
    else:
        primary = _unique_title_above_compact_description(title_band, expected_y, model)
        if primary is None:
            return None
    primary_text = primary.get("text", "").strip()
    primary_ocr = set(primary.get("evidence", {}).get("ocrIds", []))
    absorbed: list[dict[str, Any]] = []
    for candidate in slot:
        candidate_text = candidate.get("text", "").strip()
        candidate_ocr = set(candidate.get("evidence", {}).get("ocrIds", []))
        same_title = candidate_text == primary_text
        shares_ocr = bool(primary_ocr & candidate_ocr)
        subordinate = (
            _center_y(candidate) > _center_y(primary)
            and _center_y(candidate) <= expected_y + 0.48 * model.pitch
            and abs(_x1(candidate) - _x1(primary)) <= model.x_tolerance
        )
        if candidate is primary or same_title or shares_ocr or subordinate:
            absorbed.append(candidate)
    return _Proposal(primary, tuple(absorbed))


def _unique_title_above_compact_description(
    title_band: list[dict[str, Any]],
    expected_y: float,
    model: _UniformListModel,
) -> dict[str, Any] | None:
    """Distinguish a compact subtitle from a row title by geometry alone.

    Some Settings rows place a short description close enough to the expected
    row center that both OCR lines enter the title band.  Accept that shape
    only when every alternate line is strictly below one unique, title-sized
    line and is visibly more compact.  Peer-sized or same-baseline alternatives
    remain ambiguous and fail closed.
    """
    possible: list[dict[str, Any]] = []
    for candidate in title_band:
        if _center_y(candidate) > expected_y:
            continue
        if _height(candidate) < 0.75 * model.title_height:
            continue
        others = [item for item in title_band if item is not candidate]
        if not others:
            continue
        if all(
            0.08 * model.pitch <= _center_y(item) - _center_y(candidate) <= 0.32 * model.pitch
            and _height(item) <= 0.82 * model.title_height
            and abs(_x1(item) - _x1(candidate)) <= model.x_tolerance
            for item in others
        ):
            possible.append(candidate)
    return possible[0] if len(possible) == 1 else None


def _exclude_rows_with_trailing_controls(
    candidates: list[dict[str, Any]],
) -> None:
    controls = [
        candidate for candidate in candidates
        if candidate.get("type") in _CONTROL_LABELS
    ]
    for row in _confirmed_rows(candidates):
        if not any(_center_x(control) > _x1(row)
                   and abs(_center_y(control) - _center_y(row)) <= max(12.0, 0.75 * max(_height(row), _height(control)))
                   for control in controls):
            continue
        row["type"] = "text_block"
        row.setdefault("evidence", {})["typeInferred"] = "local_control_row_excluded"


def _exclude_clipped_edge_rows(
    candidates: list[dict[str, Any]],
    anchors: list[dict[str, Any]],
    model: _UniformListModel,
) -> None:
    if len(anchors) < 5:
        return
    clipped_ids: set[int] = set()
    for edge in (anchors[0], anchors[-1]):
        if _height(edge) <= 0.72 * model.title_height:
            clipped_ids.add(id(edge))
    if clipped_ids:
        # Keep raw detector/OCR evidence untouched, but do not expose a visibly
        # clipped interpretation as a logical fused occurrence.
        candidates[:] = [candidate for candidate in candidates if id(candidate) not in clipped_ids]


def _x1(candidate: dict[str, Any]) -> float:
    return float(candidate.get("boundsPx", [0, 0, 0, 0])[0])


def _center_y(candidate: dict[str, Any]) -> float:
    return float(candidate.get("centerPx", [0, 0])[1])


def _center_x(candidate: dict[str, Any]) -> float:
    return float(candidate.get("centerPx", [0, 0])[0])


def _height(candidate: dict[str, Any]) -> float:
    bounds = candidate.get("boundsPx", [0, 0, 0, 0])
    return max(0.0, float(bounds[3] - bounds[1]))


def _normalized_y2(candidate: dict[str, Any]) -> float:
    return float(candidate.get("bounds", {}).get("y2", 1.0))


def _normalized_y1(candidate: dict[str, Any]) -> float:
    return float(candidate.get("bounds", {}).get("y1", 0.0))


def _near(value: float, target: float, *, relative: float) -> bool:
    return target > 0 and abs(value - target) <= relative * target


def _stable_union(items: Any) -> list[Any]:
    result: list[Any] = []
    seen: set[Any] = set()
    for item in items:
        if item in seen:
            continue
        seen.add(item)
        result.append(item)
    return result