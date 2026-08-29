"""``text-relation-check`` VALIDATOR operator (S4, WI-PFW-S4).

Text-semantics conflict verifier per OpenSpec change
``perception-operator-rule-framework`` (spec *"Authority classes constrain
generation"*: *"Text semantics may only veto or downgrade confidence … never
emits a navigation candidate"*).  VALIDATOR authority: it NEVER generates
candidates and cannot be disabled by configuration (the registry rejects any
``enabled`` binding at load time; every parameter is ``tighten_only``).

**Deterministic string/geometry CONFLICT checks only** — no semantic
similarity models.  The checks are explicit structural text anomalies on the
pipeline's COMPOSED navigation rows (``menu_item`` candidates — the heads):

1. *Empty / too-short head text*: a head whose stripped text is shorter than
   ``min_head_text_length`` (default 1 ⇒ empty/whitespace head) is a
   structural anomaly — the row carries no visible title.  Fail-closed veto.
2. *Verbatim duplicate head text at the SAME position*: two heads whose
   stripped texts are identical AND whose bounds overlap by at least
   ``1 - duplicate_position_tolerance`` of the smaller box are one physical
   slot carrying two identical titles — a merge anomaly.  Fail-closed veto
   (the router's same-line suppression makes this unreachable on current
   corpus output, but the anomaly is the spec-named one and stays fail-closed).
3. *Verbatim duplicate head text at DIFFERENT positions*: same text on
   distinct rows (the cross-UI corpus's same-text rows) is a LOW-severity
   text-relation conflict — the rows stay distinct (never vetoed) but each
   duplicate is annotated with a small negative ``confidenceDelta``
   suggestion (annotate-only).

**Annotate-only byte contract (this slice).**  The S1/S2 equivalence gate
compares the FULL serialized candidate dicts byte-for-byte, so this operator
NEVER mutates candidates: no confidence mutation, no identity/text change, no
new candidate fields.  Downgrade semantics are expressed in the decision
record's ``annotations`` (``{"kind": "confidence_delta", "confidenceDelta":
-0.05, …}``) which the pipeline executor ignores for serialization; a future
confidence layer may honor them.  Vetoes, when they fire, are reported through
the executor's fail-closed rollback but are unreachable on the current 34-case
corpus (zero-veto gate).

The runner protocol is the executor's fixed
``(candidates, yolo_detections, resolved_values)`` triple (see
``operators/trace.py``); ``yolo_detections`` is unused (text is read from the
composed candidates only).  Deterministic and pure over
``(candidates, resolved parameters)``.
"""
from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Mapping, Sequence

__all__ = [
    "TEXT_RELATION_PARAM_DEFAULTS",
    "TEXT_RELATION_PARAM_BOUNDS",
    "CONFLICT_DELTA",
    "check",
    "run",
]

# ---------------------------------------------------------------------------
# Parameter surface (bounded; VALIDATOR params are tighten_only in the
# contract — a rule may only move them toward stricter values).
# ---------------------------------------------------------------------------

#: Defaults for the bounded S4 parameters.  ``min_head_text_length`` 1 =
#: reject empty/whitespace heads; ``duplicate_position_tolerance`` 0.01 =
#: two heads are "at the same position" when their bounds overlap by ≥ 0.99
#: of the smaller box (only genuine duplicate slots qualify).
TEXT_RELATION_PARAM_DEFAULTS: dict[str, Any] = {
    "min_head_text_length": 1,
    "duplicate_position_tolerance": 0.01,
}

#: Declared bounds (type + inclusive numeric bounds).  Following the
#: framework's (0, X] convention, the open lower bound is declared as 0.0
#: (a 0.0 value adds no behavior beyond the fail-closed direction).
TEXT_RELATION_PARAM_BOUNDS: dict[str, tuple[type, tuple[float, float]]] = {
    "min_head_text_length": (int, (1, 20)),
    "duplicate_position_tolerance": (float, (0.0, 0.1)),
}

#: Suggested confidence deltas (annotate-only; never applied this slice).
#: CONFLICT_DELTA for same-text-different-position duplicates (low-severity
#: text-relation conflict).
CONFLICT_DELTA: float = -0.05

_VERIFIED = "verified"
_REJECTED = "rejected"


@dataclass(frozen=True)
class _TextRelationParams:
    min_head_text_length: int = 1
    duplicate_position_tolerance: float = 0.01

    @classmethod
    def from_mapping(cls, mapping: Mapping[str, Any] | None) -> "_TextRelationParams":
        values = dict(TEXT_RELATION_PARAM_DEFAULTS)
        if mapping is not None:
            values.update(mapping)
        return cls(
            min_head_text_length=int(values["min_head_text_length"]),
            duplicate_position_tolerance=float(values["duplicate_position_tolerance"]),
        )


def check(
    candidates: Sequence[dict[str, Any]],
    params: Mapping[str, Any] | None = None,
) -> dict[str, Any]:
    """Conflict-check the head texts of the composed navigation rows.

    Fail-closed: returns ``{"status": "rejected", "detail": reason}`` on the
    first structural text anomaly and NEVER mutates ``candidates``; returns
    ``{"status": "verified", "detail": …, "annotations": […]}`` otherwise.
    Deterministic and pure over (candidates, resolved parameters).
    """
    p = _TextRelationParams.from_mapping(params)
    heads = [
        candidate for candidate in candidates
        if candidate.get("type") == "menu_item"
    ]

    # --- C1: empty / too-short head text (structural anomaly) ---------------
    for head in heads:
        text = _head_text(head)
        if len(text) < p.min_head_text_length:
            return _verdict(
                _REJECTED,
                f"fail-closed: composed head {head.get('id')!r} carries text "
                f"{text!r} shorter than min_head_text_length "
                f"{p.min_head_text_length}; a navigation row must carry a "
                "visible title (structural text anomaly)",
                annotations=[],
            )

    # --- C2: verbatim duplicate head text at the SAME position (merge
    # anomaly, spec-named conflict) ------------------------------------------
    for index, head in enumerate(heads):
        for other in heads[index + 1:]:
            if (
                _head_text(head) == _head_text(other)
                and _same_position(head, other, p.duplicate_position_tolerance)
            ):
                return _verdict(
                    _REJECTED,
                    f"fail-closed: composed heads {head.get('id')!r} and "
                    f"{other.get('id')!r} carry verbatim duplicate text "
                    f"{_head_text(head)!r} at the same position; one physical "
                    "slot must not host two identical titles (structural text "
                    "anomaly after merge)",
                    annotations=[],
                )

    # --- C3: verbatim duplicate head text at DIFFERENT positions (low-
    # severity conflict; distinct rows stay, each duplicate annotated) ------
    by_text: dict[str, list[dict[str, Any]]] = {}
    for head in heads:
        by_text.setdefault(_head_text(head), []).append(head)
    annotations: list[dict[str, Any]] = []
    for text, group in by_text.items():
        if len(group) > 1:
            for head in group:
                annotations.append({
                    "kind": "confidence_delta",
                    "candidateId": head.get("id"),
                    "confidenceDelta": CONFLICT_DELTA,
                    "reason": (
                        f"head text {text!r} duplicated verbatim at a "
                        "different position; text alone cannot disambiguate "
                        "the rows"
                    ),
                })

    return _verdict(
        _VERIFIED,
        f"verified {len(heads)} composed head(s) ({len(annotations)} "
        "text-relation conflict annotation(s))",
        annotations=sorted(annotations, key=lambda a: str(a.get("candidateId", ""))),
    )


def run(
    candidates: list[dict[str, Any]],
    yolo_detections: list[Any],
    params: Mapping[str, Any] | None = None,
) -> dict[str, Any]:
    """Pipeline runner protocol entry (operator_id ``text-relation-check``)."""
    return check(candidates, params)


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


def _same_position(
    head: dict[str, Any],
    other: dict[str, Any],
    tolerance: float,
) -> bool:
    """Two heads occupy the same physical slot when their bounds overlap by at
    least ``1 - tolerance`` of the smaller box (only genuine duplicate slots
    qualify; deterministic geometry only)."""
    a = _bounds(head)
    b = _bounds(other)
    if a is None or b is None:
        return False
    inter_w = max(0.0, min(a[2], b[2]) - max(a[0], b[0]))
    inter_h = max(0.0, min(a[3], b[3]) - max(a[1], b[1]))
    min_area = min(_area(a), _area(b))
    if min_area <= 0:
        return False
    return (inter_w * inter_h) >= (1.0 - tolerance) * min_area