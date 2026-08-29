"""``spacing-verifier`` VALIDATOR operator (S1B port, WI-PFW-S1B).

Geometric verification of generated row-group structure per the OpenSpec
change ``perception-operator-rule-framework``: same-column alignment within
tolerance, vertical adjacency/containment consistency, and cap/provenance
compliance.  VALIDATOR authority: it only confirms/vetoes — it never generates
rows and cannot be disabled by configuration (the registry rejects any
``enabled`` binding at load time; every parameter is ``tighten_only``, so a
rule may only move it in its declared tightening direction).

**No-new-rejection-surface argument.**  Every check below is a *necessary
condition of the retained candidate's construction guarantees* for the rows
the ``uniform-list-row-grouping`` GENERATOR emits, so the verifier accepts
everything the current pipeline produces — defaults are chosen strictly
looser than those guarantees:

* Generated rows (evidence ``typeInferred`` in the four uniform-list reasons)
  sit on the operator's cadence grid: their center-Y positions are
  ``anchor_center + k*pitch`` with ``pitch >= 2.2*title_height``, and every
  inter-row gap is ``k*pitch`` with ``k in 1..4`` accepted within the 14%
  cadence tolerance.  Consecutive gap values therefore lie in
  ``[0.86*pitch, 4.14*pitch]``, so ``min_gap/median_gap >= 0.86/4.14 ≈ 0.208``
  — the default ``minStepRatio = 0.15`` is strictly below that bound.
* Every generated row's ``x1`` is within ``max(12.0, 0.15*pitch)`` of the
  model's median title column, so the pairwise column spread is at most
  ``2*max(12.0, 0.15*pitch)``.  The derivation-free verifier bound uses the
  observable median gap of the generated rows (``median_gap >= 0.86*pitch``):
  ``max(24.0, 2*0.20*median_gap) >= max(24.0, 0.344*pitch) >= max(24.0, 0.3*pitch)``
  — the default ``columnToleranceFloor = 24.0`` (= 2× the code's 12.0 floor)
  and ``columnToleranceRatio = 0.20`` (the code's 0.15 column ratio widened by
  the 14% cadence-jitter margin) always cover it.
* Structure integrity/containment (numeric bounds, center inside bounds) hold
  for every engine-built candidate by construction (rounding is monotone);
  provenance (``typeInferred`` ∈ the authorized GENERATOR reason set) holds
  because only chevron row composition and the uniform-list generator ever
  mark rows; and the absolute row cap (default 200) is far above any generator
  inventory (confirmed rows at most double).

**C4 scope (FRAME_LOCAL_COMPOSITION_VALIDITY_VETO_REPAIR_GATE).**  The C4
column-spread check is a *uniform-list* shape assumption (all rows on one
column).  It therefore executes ONLY on rows with uniform-list provenance
(``UNIFORM_LIST_ROW_REASONS``) — the set whose construction guarantees the
grid premise.  relation-head bands compose per-band columns and are no longer
wholesale-vetoed on the uniform-list single-column assumption (the located
Frame-Local veto FDP); they remain covered by C1/C2/C3 and C5.

**Structural title-column exemption (WI-PFW-S2fix2), within the C4 scope.**
Real child pages can carry one structural shape the uniform-list model never
produces: a page-TITLE band on the far-left column with the menu content rows
on a single indented dominant column (e.g. x1 69/213/214/208).  For exactly
that shape — evaluated among the uniform-list C4-scope rows — the topmost band
is exempted from the C4 spread computation (it is a title column, not a
misaligned menu row) and C4 is re-computed over the remaining
(dominant-column) bands only.  The exemption is structural and fail-closed:
it fires ONLY when (a) all non-topmost C4-scope bands form ONE modal column
cluster with ≥2 members (within the verifier's per-side column tolerance),
and (b) the topmost band lies LEFT of that cluster by more than the tolerance.
Every other mixed-column shape keeps the C4-scope full-set spread check
(veto).  No threshold is relaxed: the same
``columnToleranceFloor``/``columnToleranceRatio``/``minStepRatio`` bounds and
the full-band vertical-cadence check (C5, over ALL generated rows) remain
exactly as declared.

A rejection is fail-closed: the pipeline executor rolls the generator's output
back and records the veto reason; for the S1 port this is unreachable by
construction (the generator's own checks are at least as strict).
"""
from __future__ import annotations

import math
from dataclasses import dataclass
from typing import Any, Mapping

__all__ = [
    "VERIFIER_PARAM_DEFAULTS",
    "VERIFIER_PARAM_BOUNDS",
    "GENERATED_ROW_REASONS",
    "UNIFORM_LIST_ROW_REASONS",
    "verify",
    "run",
]

# ---------------------------------------------------------------------------
# Parameter surface (all VALIDATOR params are tighten_only in the contract).
# ---------------------------------------------------------------------------

#: Defaults formalize the current geometry envelope, chosen strictly looser
#: than the generator's construction guarantees (see module docstring).
VERIFIER_PARAM_DEFAULTS: dict[str, Any] = {
    "minStepRatio": 0.15,
    "columnToleranceRatio": 0.20,
    "columnToleranceFloor": 24.0,
    "maxMenuItems": 200,
}

VERIFIER_PARAM_BOUNDS: dict[str, tuple[type, tuple[float, float]]] = {
    "minStepRatio": (float, (0.05, 1.0)),
    "columnToleranceRatio": (float, (0.05, 1.0)),
    "columnToleranceFloor": (float, (0.0, 10000.0)),
    "maxMenuItems": (int, (1, 10000)),
}

#: Authorized row-identity provenance of the pipeline's GENERATORs (chevron
#: row composition in heuristics + uniform-list-row-grouping + S2.1
#: row-relation-head).  A menu_item whose typeInferred reason is absent or
#: outside this set was not produced by an authorized GENERATOR — fail-closed
#: (spec: "Navigation/menu identity SHALL be generatable only by GENERATOR
#: operators").  Extending this set only ever ACCEPTS a new GENERATOR's
#: provenance — it adds no rejection surface (G-5); the geometry checks below
#: remain necessary conditions that relation-head's band structure satisfies by
#: construction (same-column heads, adjacency-banded gaps).
GENERATED_ROW_REASONS: frozenset[str] = frozenset({
    "row_composition",                      # apply_chevron_heuristic
    "uniform_list_bracketed_row",           # uniform-list-row-grouping
    "uniform_list_upper_continuation",      # uniform-list-row-grouping
    "uniform_list_lower_continuation",      # uniform-list-row-grouping
    "uniform_list_anchor_duplicate_absorbed",  # uniform-list-row-grouping
    "row_relation_head",                    # row-relation-head (S2.1)
})

#: Uniform-list row-composition provenance — the ONLY set whose construction
#: guarantees the C4 single-column grid premise (rows on the operator's
#: cadence grid at a median title column).  FRAME_LOCAL_COMPOSITION_VALIDITY_
#: VETO_REPAIR_GATE: C4 executes ONLY on this provenance; relation-head bands
#: compose per-band columns and must NOT be wholesale-vetoed by the
#: uniform-list single-column assumption.  C1/C2/C3/C5 continue to cover ALL
#: generated rows (structure/containment/provenance/cap/vertical cadence).
UNIFORM_LIST_ROW_REASONS: frozenset[str] = frozenset({
    "uniform_list_bracketed_row",
    "uniform_list_upper_continuation",
    "uniform_list_lower_continuation",
    "uniform_list_anchor_duplicate_absorbed",
})


@dataclass(frozen=True)
class _VerifierParams:
    min_step_ratio: float = 0.15
    column_tolerance_ratio: float = 0.20
    column_tolerance_floor: float = 24.0
    max_menu_items: int = 200

    @classmethod
    def from_mapping(cls, mapping: Mapping[str, Any] | None) -> "_VerifierParams":
        values = dict(VERIFIER_PARAM_DEFAULTS)
        if mapping is not None:
            values.update(mapping)
        return cls(
            min_step_ratio=float(values["minStepRatio"]),
            column_tolerance_ratio=float(values["columnToleranceRatio"]),
            column_tolerance_floor=float(values["columnToleranceFloor"]),
            max_menu_items=int(values["maxMenuItems"]),
        )


_VERIFIED = "verified"
_REJECTED = "rejected"


def verify(
    candidates: list[dict[str, Any]],
    yolo_detections: list[Any],
    params: Mapping[str, Any] | None = None,
) -> dict[str, Any]:
    """Verify the generated row-group structure of ``candidates``.

    Fail-closed: returns ``{"status": "rejected", "detail": reason}`` on the
    first violated check and never mutates ``candidates``; returns
    ``{"status": "verified", "detail": ...}`` otherwise.  Deterministic and
    pure over (candidates, resolved parameters).
    """
    p = _VerifierParams.from_mapping(params)
    menu_items = [
        candidate for candidate in candidates
        if candidate.get("type") == "menu_item"
    ]

    # --- C1/C2: structure integrity + containment (all rows) ---------------
    for candidate in menu_items:
        violation = _integrity_violation(candidate)
        if violation is not None:
            return _verdict(_REJECTED, violation)
        violation = _containment_violation(candidate)
        if violation is not None:
            return _verdict(_REJECTED, violation)

    # --- C3: provenance / cap compliance (all rows) -------------------------
    if len(menu_items) > p.max_menu_items:
        return _verdict(
            _REJECTED,
            f"fail-closed: menu row inventory {len(menu_items)} exceeds the "
            f"bounded cap {p.max_menu_items}",
        )
    for candidate in menu_items:
        inferred = (candidate.get("evidence") or {}).get("typeInferred")
        if inferred is not None and inferred not in GENERATED_ROW_REASONS:
            return _verdict(
                _REJECTED,
                f"fail-closed: menu_item {candidate.get('id')!r} carries "
                f"unauthorized row identity provenance {inferred!r}; menu "
                "identity is generatable only by GENERATOR operators",
            )

    # --- C4/C5: same-column + vertical adjacency (generated rows only) ------
    generated = [
        candidate for candidate in menu_items
        if (candidate.get("evidence") or {}).get("typeInferred")
        in GENERATED_ROW_REASONS
    ]
    if len(generated) >= 2:
        violation, exemption = _geometry_violation(generated, p)
        if violation is not None:
            return _verdict(_REJECTED, violation)
    else:
        exemption = None

    detail = (
        f"verified {len(generated)} generated row(s) across "
        f"{len(menu_items)} menu row(s)"
    )
    verdict: dict[str, Any] = _verdict(_VERIFIED, detail)
    if exemption is not None:
        record = {
            "band": str(exemption["band"].get("id", "")),
            "x1": exemption["x1"],
            "dominantColumnX1": exemption["dominantColumnX1"],
            "columnTolerance": exemption["columnTolerance"],
        }
        verdict["titleColumnExempted"] = record
        verdict["detail"] = (
            f"{detail}; title_column_exempted: band "
            f"{record['band']!r} (x1={record['x1']:g}px) left of dominant "
            f"column x1={record['dominantColumnX1']:g}px "
            f"(column tolerance {record['columnTolerance']:g}px)"
        )
    return verdict


def run(candidates: list[dict[str, Any]], yolo_detections: list[Any],
        params: Mapping[str, Any]) -> dict[str, Any]:
    """Pipeline runner protocol entry (operator_id ``spacing-verifier``)."""
    return verify(candidates, yolo_detections, params)


def _verdict(status: str, detail: str) -> dict[str, Any]:
    return {"status": status, "detail": detail}


def _integrity_violation(candidate: dict[str, Any]) -> str | None:
    if not str(candidate.get("text", "")).strip():
        return (
            f"fail-closed: menu_item {candidate.get('id')!r} has empty text; "
            "a row group must carry a visible title"
        )
    bounds = candidate.get("boundsPx")
    center = candidate.get("centerPx")
    if not isinstance(bounds, list) or len(bounds) != 4:
        return f"fail-closed: menu_item {candidate.get('id')!r} has malformed boundsPx"
    if not all(_finite_number(v) for v in bounds):
        return f"fail-closed: menu_item {candidate.get('id')!r} has non-finite boundsPx"
    if not isinstance(center, list) or len(center) != 2 or not all(_finite_number(v) for v in center):
        return f"fail-closed: menu_item {candidate.get('id')!r} has malformed centerPx"
    if bounds[2] < bounds[0] or bounds[3] < bounds[1]:
        return f"fail-closed: menu_item {candidate.get('id')!r} has inverted boundsPx"
    return None


def _containment_violation(candidate: dict[str, Any]) -> str | None:
    bounds = candidate["boundsPx"]
    center = candidate["centerPx"]
    if not (bounds[0] <= center[0] <= bounds[2] and bounds[1] <= center[1] <= bounds[3]):
        return (
            f"fail-closed: menu_item {candidate.get('id')!r} center lies "
            "outside its bounds; vertical adjacency containment violated"
        )
    return None


def _geometry_violation(
    generated: list[dict[str, Any]], p: _VerifierParams
) -> tuple[str | None, dict[str, Any] | None]:
    """C4/C5 geometry checks; returns ``(violation, title_column_exemption)``.

    FRAME_LOCAL_COMPOSITION_VALIDITY_VETO_REPAIR_GATE: the C4 same-column
    spread check is scoped to **uniform-list provenance rows only** — its
    derivation premise (rows on the operator's cadence grid at one median
    title column) holds only for ``UNIFORM_LIST_ROW_REASONS``.  relation-head
    bands compose per-band columns and are NOT vetoed on the uniform-list
    single-column premise; they remain fully covered by C1/C2/C3 and C5.
    Genuine uniform-list column misalignment still fails closed (the C4
    premise set).  The structural title-column exemption (WI-PFW-S2fix2) is
    evaluated within the C4 scope: a far-left title band among uniform-list
    rows is exempted when the remaining uniform-list rows form ONE dominant
    column cluster.  C5 vertical constancy keeps covering ALL generated rows
    (never re-scoped).
    """
    ordered = sorted(generated, key=lambda c: _center_y(c))
    gaps = [
        _center_y(lower) - _center_y(upper)
        for upper, lower in zip(ordered, ordered[1:])
    ]
    if any(gap <= 0 for gap in gaps):
        return (
            "fail-closed: generated rows are not strictly vertically ordered",
            None,
        )
    median_gap = float(sorted(gaps)[len(gaps) // 2])

    # C4 (repair gate scope): same-column alignment over uniform-list
    # provenance rows only.  relation-head/chevron rows contribute no spread
    # veto (their columns are per-band; wholesale rollback on the
    # uniform-list grid premise is the located Frame-Local veto FDP).
    uniform_list_rows = [
        candidate for candidate in generated
        if (candidate.get("evidence") or {}).get("typeInferred")
        in UNIFORM_LIST_ROW_REASONS
    ]
    column_bound = max(
        p.column_tolerance_floor, 2.0 * p.column_tolerance_ratio * median_gap
    )
    exemption: dict[str, Any] | None = None
    if len(uniform_list_rows) >= 2:
        exemption = _title_column_exemption(
            sorted(uniform_list_rows, key=lambda c: _center_y(c)), p, median_gap
        )
        spread_bands = uniform_list_rows
        if exemption is not None:
            exempt_band = exemption["band"]
            spread_bands = [
                candidate for candidate in uniform_list_rows
                if candidate is not exempt_band
            ]
        x1_values = [_x1(candidate) for candidate in spread_bands]
        spread = max(x1_values) - min(x1_values)
        if spread > column_bound:
            scope = (
                "non-exempt uniform-list rows"
                if exemption is not None else "uniform-list rows"
            )
            return (
                f"fail-closed: {scope}' column spread {spread:g}px exceeds "
                f"the tolerance bound {column_bound:g}px (median step "
                f"{median_gap:g}px); same-column alignment not verified",
                None,
            )

    # C5: vertical adjacency — every gap at least a bounded fraction of the
    # median step (computed over ALL generated rows; the C4 scope never
    # re-scopes the cadence bound; the exemption only re-scopes the C4 spread).
    if median_gap > 0:
        min_gap = min(gaps)
        if min_gap < p.min_step_ratio * median_gap:
            return (
                f"fail-closed: generated rows' minimum step {min_gap:g}px is "
                f"below {p.min_step_ratio:g}× the median step {median_gap:g}px; "
                "vertical cadence not verified",
                None,
            )
    return None, exemption


def _title_column_exemption(
    ordered: list[dict[str, Any]], p: _VerifierParams, median_gap: float
) -> dict[str, Any] | None:
    """Structural title-column exemption (WI-PFW-S2fix2).

    The one mixed-column page shape the uniform-list model never produces
    (and the C4 ``same-column`` check was not built for): a page-TITLE band
    on the far-left column with the menu content rows forming ONE dominant
    indented column (e.g. x1 69 / 213 / 214 / 208).  For exactly that shape
    the TOPMOST band is exempted from the C4 spread set — it is a title
    column, not a misaligned menu row — and the spread is re-computed over
    the dominant-column bands only.

    Fires iff, over ``ordered`` (top-to-bottom by center-Y):

    * the non-topmost bands are >= 2 AND all lie within the verifier's
      per-side column tolerance of that set's median x1 (ONE dominant column,
      no other band off-cluster) — the median-anchored modal cluster;
    * the topmost band's x1 is LEFT of the dominant column's x1 by MORE than
      the column tolerance.

    Any other mixed shape returns ``None`` and the original full-set spread
    check stands (fail-closed); the C4/C5 thresholds are never relaxed.
    """
    if len(ordered) < 3:
        return None  # need the topmost band + >= 2 dominant-column bands
    per_side_tolerance = max(
        p.column_tolerance_floor / 2.0,
        p.column_tolerance_ratio * median_gap,
    )
    topmost = ordered[0]
    rest = ordered[1:]
    rest_sorted = sorted(rest, key=_x1)
    dominant_x1 = _x1(rest_sorted[len(rest_sorted) // 2])  # deterministic median
    in_cluster = [
        candidate for candidate in rest
        if abs(_x1(candidate) - dominant_x1) <= per_side_tolerance
    ]
    if len(in_cluster) < 2 or len(in_cluster) != len(rest):
        return None  # no dominant column of >= 2 / another band off-cluster
    top_x1 = _x1(topmost)
    if dominant_x1 - top_x1 <= per_side_tolerance:
        return None  # the topmost band is not LEFT of the column beyond tolerance
    return {
        "band": topmost,
        "x1": top_x1,
        "dominantColumnX1": dominant_x1,
        "columnTolerance": per_side_tolerance,
    }


def _center_y(candidate: dict[str, Any]) -> float:
    return float(candidate["centerPx"][1])


def _x1(candidate: dict[str, Any]) -> float:
    return float(candidate["boundsPx"][0])


def _finite_number(value: Any) -> bool:
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(float(value))