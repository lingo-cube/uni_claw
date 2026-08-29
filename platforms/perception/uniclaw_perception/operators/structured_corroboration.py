"""``structured-corroboration`` VALIDATOR operator (S4, WI-PFW-S4).

Structured-hierarchy (uiautomator-style XML) auxiliary corroboration per
OpenSpec change ``perception-operator-rule-framework`` (spec *"Authority
classes constrain generation"*: XML is *auxiliary corroboration only* — it
NEVER creates or vetoes identity outright in general).  VALIDATOR authority:
only downgrades confidence or (in one maximally conservative case) vetoes;
never generates candidates; cannot be disabled by configuration.

**Optional structured input channel (adapter-side, additive).**  The operator
consumes an OPTIONAL ``structured`` tier — uiautomator-style rows, e.g.
``{"text": …, "clickable": …, "focusable": …, "boundsPx": [x1, y1, x2, y2]}`` —
carried in the runner's ``raw_sources`` bundle under the ``structured`` key
(the engine's current raw-source bundle does not include it, so the executed
pipeline ALWAYS sees an absent channel).  Absent channel (``None`` or empty)
⇒ the verifier passes trivially with the note *"no structured evidence"* —
XML never adds a rejection surface when it is unavailable.  The channel is
exercised by direct calls (unit + corpus tests) and by any future engine
opt-in; the engine/executor are NOT modified (prefer adapter side).

**Cross-checks** (each composed navigation row = a ``menu_item`` candidate,
vs the structured nodes intersecting its bounds):

1. *Static-label mismatch*: a NON-CLICKABLE text node at overlapping bounds
   carries the head text ⇒ the row's interactivity is not corroborated ⇒
   DOWNGRADE confidence (annotate negative ``confidenceDelta``; never veto —
   corroboration is auxiliary).
2. *Corroboration*: a clickable AND focusable node at overlapping bounds
   carries the head text ⇒ annotate positive corroboration (no confidence
   change; identity is never moved by XML).
3. *In doubt*: the region is intersected only by text-less nodes and the head
   text matches nothing ⇒ downgrade only (never veto).
4. *Strong contradiction (the ONLY veto)*: the structured tier is available for
   the region (at least one TEXT-BEARING node intersects the row's bounds) AND
   the head text appears NOWHERE among those intersecting nodes ⇒ the composed
   row has no backing in a fully-available hierarchy region ⇒ fail-closed
   veto with reason.  Kept maximally conservative: absence of a text-bearing
   match is the contradiction, never mere absence of the tier.

**Annotate-only byte contract (this slice).**  Like ``text-relation-check``,
this operator NEVER mutates candidates (the equivalence gate serializes the
full candidate dicts byte-for-byte): downgrades/corroborations live in the
decision record's ``annotations``, never on the candidates, and XML is never
an identity source (no candidate creation, no text/identity mutation).

The runner protocol is the executor's fixed
``(candidates, yolo_detections, resolved_values)`` triple; the optional
``raw_sources`` bundle is accepted for the structured channel exactly like the
frozen-input adapter pattern (``relation_head_router``), but the executor only
ever calls VALIDATOR runners with three arguments.  Deterministic and pure.
"""
from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Mapping, Sequence

__all__ = [
    "CORROBORATION_PARAM_DEFAULTS",
    "CORROBORATION_PARAM_BOUNDS",
    "STATIC_LABEL_DELTA",
    "IN_DOUBT_DELTA",
    "corroborate",
    "run",
]

# ---------------------------------------------------------------------------
# Parameter surface (bounded; VALIDATOR params are tighten_only in the
# contract — a rule may only move them toward stricter values).
# ---------------------------------------------------------------------------

#: Default: a structured node "overlaps" a row's bounds when the intersection
#: covers at least 30% of the SMALLER region (bounds_overlap_min).
CORROBORATION_PARAM_DEFAULTS: dict[str, Any] = {
    "bounds_overlap_min": 0.3,
}

#: Declared bounds (type + inclusive numeric bounds); the (0, X] open lower
#: bound is declared as 0.0 per the framework convention.
CORROBORATION_PARAM_BOUNDS: dict[str, tuple[type, tuple[float, float]]] = {
    "bounds_overlap_min": (float, (0.0, 1.0)),
}

#: Suggested confidence deltas (annotate-only; never applied this slice):
#: static-label mismatch (−0.05), in-doubt region (−0.02).  Corroboration
#: carries no delta.
STATIC_LABEL_DELTA: float = -0.05
IN_DOUBT_DELTA: float = -0.02

_VERIFIED = "verified"
_REJECTED = "rejected"

#: Note recorded when the structured tier is absent (the executed pipeline's
#: constant state — the engine's raw-source bundle has no ``structured`` key).
_NO_STRUCTURED_EVIDENCE = (
    "no structured evidence: the optional structured (uiautomator-style) tier "
    "was not provided for this run; structured corroboration passes trivially "
    "(XML never adds a rejection surface when unavailable)"
)


@dataclass(frozen=True)
class _CorroborationParams:
    bounds_overlap_min: float = 0.3

    @classmethod
    def from_mapping(cls, mapping: Mapping[str, Any] | None) -> "_CorroborationParams":
        values = dict(CORROBORATION_PARAM_DEFAULTS)
        if mapping is not None:
            values.update(mapping)
        return cls(bounds_overlap_min=float(values["bounds_overlap_min"]))


def corroborate(
    candidates: Sequence[dict[str, Any]],
    structured: Sequence[Mapping[str, Any]] | None,
    params: Mapping[str, Any] | None = None,
) -> dict[str, Any]:
    """Corroborate the composed navigation rows against the optional
    structured tier (see module docstring for the cross-check semantics).

    Fail-closed on the one strong-contradiction case; otherwise verifies with
    annotations.  NEVER mutates ``candidates`` and never emits candidates.
    Deterministic and pure over (candidates, structured tier, params).
    """
    p = _CorroborationParams.from_mapping(params)
    nodes = [_StructuredNode(node) for node in (structured or [])]
    if not nodes:
        return _verdict(
            _VERIFIED,
            _NO_STRUCTURED_EVIDENCE,
            annotations=[],
        )

    rows = [
        candidate for candidate in candidates
        if candidate.get("type") == "menu_item"
    ]
    annotations: list[dict[str, Any]] = []
    for row in rows:
        row_bounds = _bounds(row)
        if row_bounds is None:
            continue
        head_text = _head_text(row)
        intersecting = [
            node for node in nodes
            if _overlap_ratio(node.bounds, row_bounds) >= p.bounds_overlap_min
        ]
        if not intersecting:
            # No hierarchy node covers this row's region at all — the tier is
            # not authoritative for it; nothing to corroborate or contradict.
            continue
        matching = [
            node for node in intersecting
            if node.text and node.text == head_text
        ]
        if matching:
            interactive = [
                node for node in matching
                if node.clickable and node.focusable
            ]
            if interactive:
                annotations.append({
                    "kind": "corroborated",
                    "candidateId": row.get("id"),
                    "confidenceDelta": 0.0,
                    "structuredNodeText": interactive[0].text,
                    "reason": (
                        "head text corroborated by a clickable+focusable "
                        "structured node at overlapping bounds"
                    ),
                })
            else:
                annotations.append({
                    "kind": "confidence_delta",
                    "candidateId": row.get("id"),
                    "confidenceDelta": STATIC_LABEL_DELTA,
                    "structuredNodeText": matching[0].text,
                    "reason": (
                        "head text appears only as a NON-CLICKABLE structured "
                        "text node at overlapping bounds; the row's "
                        "navigability is not corroborated by the hierarchy"
                    ),
                })
            continue
        text_bearing = [node for node in intersecting if node.text]
        if text_bearing:
            # Strong contradiction: the region IS represented by text-bearing
            # hierarchy nodes and the head text appears nowhere among them.
            return _verdict(
                _REJECTED,
                f"fail-closed: composed head {row.get('id')!r} text "
                f"{head_text!r} exists nowhere in the structured tier at its "
                "bounds although the region is fully represented by "
                "text-bearing nodes; XML hierarchy contradicts the composed "
                "row (maximally conservative strong-contradiction veto)",
                annotations=sorted(annotations, key=lambda a: str(a.get("candidateId", ""))),
            )
        # In doubt: only text-less nodes (frames/containers) intersect and the
        # head text matches nothing — downgrade only, never veto.
        annotations.append({
            "kind": "confidence_delta",
            "candidateId": row.get("id"),
            "confidenceDelta": IN_DOUBT_DELTA,
            "reason": (
                "no text-bearing structured node corroborates the head text in "
                "this region (only text-less nodes intersect); in doubt — "
                "downgrade only"
            ),
        })

    return _verdict(
        _VERIFIED,
        f"verified {len(rows)} composed row(s) against "
        f"{len(nodes)} structured node(s) ({len(annotations)} annotation(s))",
        annotations=sorted(annotations, key=lambda a: str(a.get("candidateId", ""))),
    )


def run(
    candidates: list[dict[str, Any]],
    yolo_detections: list[Any],
    params: Mapping[str, Any] | None = None,
    raw_sources: Mapping[str, Any] | None = None,
) -> dict[str, Any]:
    """Pipeline runner protocol entry (operator_id ``structured-corroboration``).

    The optional structured tier is read from ``raw_sources["structured"]``
    (adapter-side additive channel; see module docstring).  The executor calls
    VALIDATOR runners with three arguments, so in the executed pipeline
    ``raw_sources`` is always ``None`` and the verifier passes trivially.
    """
    structured = None
    if isinstance(raw_sources, Mapping):
        structured = raw_sources.get("structured")
    return corroborate(candidates, structured, params)


def _verdict(status: str, detail: str, *, annotations: list[dict[str, Any]]) -> dict[str, Any]:
    return {"status": status, "detail": detail, "annotations": annotations}


def _head_text(candidate: dict[str, Any]) -> str:
    return str(candidate.get("text", "") or "").strip()


def _bounds(candidate: dict[str, Any]) -> tuple[float, float, float, float] | None:
    bounds = candidate.get("boundsPx")
    if not isinstance(bounds, (list, tuple)) or len(bounds) != 4:
        return None
    try:
        return tuple(float(value) for value in bounds)  # type: ignore[return-value]
    except (TypeError, ValueError):
        return None


def _area(bounds: tuple[float, float, float, float]) -> float:
    return max(0.0, bounds[2] - bounds[0]) * max(0.0, bounds[3] - bounds[1])


def _overlap_ratio(a: tuple[float, float, float, float], b: tuple[float, float, float, float]) -> float:
    """Intersection area as a fraction of the smaller region (0.0..1.0)."""
    inter_w = max(0.0, min(a[2], b[2]) - max(a[0], b[0]))
    inter_h = max(0.0, min(a[3], b[3]) - max(a[1], b[1]))
    min_area = min(_area(a), _area(b))
    if min_area <= 0:
        return 0.0
    return (inter_w * inter_h) / min_area


class _StructuredNode:
    """Read-only view of one uiautomator-style structured row."""

    __slots__ = ("text", "clickable", "focusable", "bounds")

    def __init__(self, raw: Mapping[str, Any]) -> None:
        self.text = str(raw.get("text", "") or "").strip()
        self.clickable = bool(raw.get("clickable", False))
        self.focusable = bool(raw.get("focusable", False))
        bounds = raw.get("boundsPx")
        if not isinstance(bounds, (list, tuple)) or len(bounds) != 4:
            raise ValueError(
                f"INVALID_STRUCTURED: structured node {raw.get('id', '')!r} "
                "must carry a 4-number 'boundsPx'"
            )
        try:
            self.bounds = tuple(float(value) for value in bounds)  # type: ignore[assignment]
        except (TypeError, ValueError):
            raise ValueError(
                f"INVALID_STRUCTURED: structured node {raw.get('id', '')!r} "
                "has non-numeric boundsPx"
            ) from None