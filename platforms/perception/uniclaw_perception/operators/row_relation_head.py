"""``row-relation-head`` GENERATOR operator (S2.1, WI-PFW-S2i).

Implements the OpenSpec change ``perception-operator-rule-framework`` S2.1
requirement (spec *"IR-G0 unblock slices with equivalence gates"*): a
deterministic GENERATOR whose inputs are FROZEN to raw visual regions —
uncombined detector boxes and OCR text blocks — plus the pairwise geometric
relations derivable from them (same-column, vertical adjacency, containment,
overlap).  It SHALL NOT consume already-established row groups (no
identify-rows-to-identify-rows circularity).  Text semantics, XML, and VLM
are FORBIDDEN as row-identity sources here (spec S2.1 / *"Authority classes
constrain generation"*: only GENERATOR operators produce navigation identity,
and this operator's identity is geometric only).  Its output is a row-group
PROPOSAL (head/satellite election) that must pass ``spacing-verifier`` once
routed (S2ii engine wiring), activating structured composition for low-anchor
viewports without text-role guessing.

Frozen-input signature (input-freeze gate G-4 of ``evidence/S2-acceptance-protocol.md``):

    run(detections, ocr_tokens, width, height, params=None)

``detections`` / ``ocr_tokens`` are raw to_json-shaped dicts (pixel
``boundsPx``, like ``schema.Detection.to_json`` / ``schema.OcrToken.to_json``)
— never fusion-composed candidates.  The entry is callable with only raw
inputs: ``params`` is optional and defaults to the bounded parameter defaults
below.

Algorithm (deterministic, geometric only):

1. Cluster all raw boxes (detections + OCR tokens) into vertical bands by
   union-find.  Two boxes relate iff one 2-D-contains the other (in either
   direction) OR they share a column (``|x1a-x1b| <= column_tolerance*width``
   or horizontal overlap) AND are vertically related (vertical gap ``<=
   adjacency_gap_ratio * min(height)`` or vertical overlap).  Bands are
   ordered top-to-bottom by their top edge.
2. Per band, head election: the band's leftmost TEXT column is the minimum
   ``x1`` over non-empty OCR text boxes; head candidates are raw DETECTION
   boxes that (a) start at/near that column, (b) geometrically bear a text box
   (contain/overlap at least one non-empty OCR token), and (c) reach
   ``min_head_width_ratio`` of the band width.  The head is the TOPMOST
   candidate at the title column — a row's title always sits ABOVE its
   caption, so the topmost box is the head even when a lower caption line is
   wider (S2fix4: the real mid-viewport shape has longer captions than
   titles, and the former widest-first rule elected those captions as heads —
   the v1n-class misclassification regression).  Only candidates on the SAME
   line as the topmost (their vertical spans overlap >= 80% of the shorter
   box's height — two detector boxes interpreting one wider title line)
   compete: among them the WIDEST wins.  Equal-width same-line ties resolve
   deterministically — duplicate texts (same detector interpretation of one
   title) merge, a vertically stacked pair elects the topmost and absorbs the
   remainder as caption satellites, and any other equal-width tie with
   distinct texts is an ambiguity that rejects the band (fail-closed).
   Icon/toggle boxes that do not start at the text column never become heads;
   OCR-only bands (no detector box at the column) never produce a head either
   — a subtitle line with no detector anchor can never be promoted.
3. Subtitle guard (geometric, never text-semantic): if a band's head would sit
   at the previous band's caption offset — same column as the previous band's
   head, vertical offset matching the previous band's title→caption offset
   within a geometric tolerance — the head is a caption continuation
   (subtitle/wrapped line) and the band is rejected (fail-closed), with the
   reason recorded in the trace decision.  Note on reachability: any text at
   an in-band caption's own offset is itself vertically adjacent to that
   caption, so banding absorbs such lines in-band (as caption satellites)
   before a separate band can form; the operative subtitle protection is
   therefore (a) in-band absorption and (b) the detector-anchor fail-closed
   rule (an OCR-only subtitle line has no detection box at its column), while
   (c) this continuation predicate remains as the spec-named defense for the
   geometries where a separate band does form.  Verified on the real
   ``v1n_low_anchor_viewport_subtitle_fail_closed`` corpus frame: the subtitle
   is adjacent and absorbed; it never becomes a menu_item.
4. Satellites (caption/icon/toggle/control boxes) are absorbed with their
   roles and provenance (``allIds``/``ocrIds``/``headId``) and emitted as
   NonInteractive.  At most ``max_satellites_per_row`` per band (bounded;
   truncation is deterministic and the band record carries the count).
5. One navigation candidate is emitted per accepted band (head's text, head's
   bounds, provenance reason ``row_relation_head``).  No confident head
   (no text column / no text-bearing detection at the column / too narrow /
   tie / subtitle continuation) ⇒ no candidate, with a recorded fail-closed
   reason — never a guess.

Determinism (protocol gate G-7): same inputs + same resolved parameters ⇒ the
same decision record byte-for-byte (stable ordering everywhere;
``record_trace_bytes`` renders the canonical form used by the determinism
tests).
"""
from __future__ import annotations

import json
import math
from dataclasses import dataclass
from typing import Any, Mapping, Sequence

from .status import ACTIVATED, COMPOSED, NOOP, REJECTED

__all__ = [
    "ROW_RELATION_HEAD_PARAM_DEFAULTS",
    "ROW_RELATION_HEAD_PARAM_BOUNDS",
    "run",
    "record_trace_bytes",
]

# ---------------------------------------------------------------------------
# Parameter surface (bounded; GENERATOR params move both ways, no safe
# direction — only VALIDATOR parameters carry one).
# ---------------------------------------------------------------------------

#: Defaults for the bounded S2.1 parameters.  ``column_tolerance`` is declared
#: as (0, 0.5], ``adjacency_gap_ratio`` as (0, 3.0], ``min_head_width_ratio``
#: as (0, 1.0].  ``NumericBounds`` is inclusive, so the declared lower bound
#: is 0.0 — the reading is that a 0.0 value (never useful in practice) adds no
#: behavior beyond the fail-closed direction of the algorithm, mirroring the
#: ``inferenceCap`` precedent in ``uniform-list-row-grouping``.
ROW_RELATION_HEAD_PARAM_DEFAULTS: dict[str, Any] = {
    "column_tolerance": 0.05,
    "adjacency_gap_ratio": 1.0,
    "min_head_width_ratio": 0.15,
    "max_satellites_per_row": 6,
}

#: Declared bounds for each parameter (type + inclusive numeric bounds).
ROW_RELATION_HEAD_PARAM_BOUNDS: dict[str, tuple[type, tuple[float, float]]] = {
    "column_tolerance": (float, (0.0, 0.5)),
    "adjacency_gap_ratio": (float, (0.0, 3.0)),
    "min_head_width_ratio": (float, (0.0, 1.0)),
    "max_satellites_per_row": (int, (1, 12)),
}

#: Structural geometry constants (characterize the row visual model, not
#: tunable — the same freedom the uniform-list operator documents for its own
#: structural constants).
#: Vertical stacking tolerance for a title-above-caption equal-width pair: the
#: lower box must start at least half a line below the upper box.
_STACK_MIN_FRACTION = 0.5
#: Same-line tolerance (S2fix4 head election): candidates whose vertical spans
#: overlap by at least this fraction of the shorter box's height are judged to
#: be on the SAME line (two detector boxes interpreting one row title) and
#: compete by width; below it the lower box is a different line
#: (title-above-caption) and never competes for the head.
_SAME_LINE_OVERLAP = 0.8
#: Subtitle-guard continuation tolerance (fraction of the smaller head
#: height): the candidate band's head must reproduce the previous band's
#: caption offset within this tolerance to be judged a caption continuation.
_CONTINUATION_TOLERANCE = 0.5

#: Stable fail-closed reason strings (deterministic trace content).
_REASON_NO_TEXT = (
    "fail-closed: band carries no OCR text (no leftmost text column to anchor a head)"
)
_REASON_NO_DET = (
    "fail-closed: no text-bearing detection starts at the band's leftmost text "
    "column (no confident head; column not anchored)"
)
_REASON_TOO_NARROW = (
    "fail-closed: every text-bearing detection at the leftmost column is narrower "
    "than min_head_width_ratio of the band width (no confident head)"
)
_REASON_TIE = (
    "fail-closed: two equal-width text-bearing detections at the same column and "
    "line tie for the head (ambiguous; no confident head)"
)
_REASON_SUBTITLE = (
    "fail-closed: head continues the previous band's caption geometry (same column "
    "at the previous band's caption offset) — subtitle continuation, not a row"
)

#: Detection label → NonInteractive satellite role (provenance metadata only;
#: never used for row identity).
_SATELLITE_ROLE_BY_LABEL = {
    "toggle": "toggle",
    "switch": "toggle",
    "checkbox": "toggle",
    "slider": "toggle",
    "icon": "icon",
    "popup": "icon",
    "back": "icon",
    "toolbar": "icon",
}


@dataclass(frozen=True)
class _RelationHeadParams:
    """Resolved S2.1 parameters (defaults = the bounded declared defaults)."""

    column_tolerance: float = 0.05
    adjacency_gap_ratio: float = 1.0
    min_head_width_ratio: float = 0.15
    max_satellites_per_row: int = 6

    @classmethod
    def from_mapping(cls, mapping: Mapping[str, Any] | None) -> "_RelationHeadParams":
        values = dict(ROW_RELATION_HEAD_PARAM_DEFAULTS)
        if mapping is not None:
            values.update(mapping)
        return cls(
            column_tolerance=float(values["column_tolerance"]),
            adjacency_gap_ratio=float(values["adjacency_gap_ratio"]),
            min_head_width_ratio=float(values["min_head_width_ratio"]),
            max_satellites_per_row=int(values["max_satellites_per_row"]),
        )


# ---------------------------------------------------------------------------
# Operator runner
# ---------------------------------------------------------------------------


def run(
    detections: Sequence[dict[str, Any]],
    ocr_tokens: Sequence[dict[str, Any]],
    width: int,
    height: int,
    params: Mapping[str, Any] | None = None,
) -> dict[str, Any]:
    """Compose navigation candidates from raw visual regions (S2.1 entry).

    Frozen-input GENERATOR: consumes only uncombined detector boxes + OCR text
    blocks (never fusion-composed candidates) and their derived pairwise
    geometry.  Deterministic pure function — the input arrays are not
    modified.  Returns a machine-readable decision record for the pipeline
    trace: ``status`` (``activated``/``noop``), ``detail``, ``emitted``,
    per-band decisions with fail-closed reasons, and the emitted
    ``candidates`` (one per accepted band) and NonInteractive ``satellites``.
    """
    if width <= 0 or height <= 0:
        raise ValueError("INVALID_GEOMETRY: source dimensions must be positive")
    p = _RelationHeadParams.from_mapping(params)

    boxes: list[_Box] = [
        _Box(detection, is_ocr=False) for detection in detections
    ]
    boxes.extend(_Box(token, is_ocr=True) for token in ocr_tokens)
    if not boxes:
        return _record(NOOP, "fail-closed: no raw visual regions provided", 0, [])

    bands = _cluster_bands(boxes, p, float(width))
    band_records: list[dict[str, Any]] = []
    candidates: list[dict[str, Any]] = []
    satellites: list[dict[str, Any]] = []
    previous: _ElectedBand | None = None

    for band_index, band in enumerate(bands):
        elected = _elect_band_head(band, band_index, p, float(width))
        if elected is None:
            band_records.append(_band_record(band, band_index, REJECTED, _REASON_NO_TEXT))
            continue
        if elected.reason is not None:
            band_records.append(_band_record(band, band_index, REJECTED, elected.reason))
            continue
        if _is_subtitle_continuation(elected, previous, p, float(width)):
            band_records.append(
                _band_record(band, band_index, REJECTED, _REASON_SUBTITLE)
            )
            continue
        candidate, band_satellites = _emit(
            elected, band, band_index, p, width, height
        )
        band_records.append(
            _band_record(
                band, band_index, COMPOSED, None,
                head_text=candidate["text"], head_id=candidate.get("id"),
                satellites=band_satellites,
            )
        )
        candidates.append(candidate)
        satellites.extend(band_satellites)
        previous = elected

    if candidates:
        detail = (
            f"composed {len(candidates)} navigation candidate(s) across "
            f"{len(bands)} band(s); "
            f"{len([b for b in band_records if b['status'] == REJECTED])} "
            "band(s) rejected fail-closed"
        )
        status = ACTIVATED
    else:
        detail = (
            "fail-closed: no confident band head — no navigation candidate emitted"
        )
        status = NOOP
    return _record(status, detail, len(candidates), band_records, candidates, satellites)


def record_trace_bytes(record: dict[str, Any]) -> bytes:
    """Canonical byte rendering of a decision record (determinism gate G-7).

    Sorted keys, compact separators, UTF-8 — byte-identical across replays of
    the same (inputs, parameters), matching the offline-replay contract of the
    operator trace (spec: *"Operators SHALL emit a trace … sufficient for
    offline replay"*).
    """
    return json.dumps(
        record, sort_keys=True, separators=(",", ":"), ensure_ascii=False
    ).encode("utf-8")


# ---------------------------------------------------------------------------
# Bands
# ---------------------------------------------------------------------------


def _cluster_bands(
    boxes: list["_Box"], p: _RelationHeadParams, width: float
) -> list[list["_Box"]]:
    """Union-find clustering of raw boxes into vertical bands (deterministic).

    Two boxes relate iff one 2-D-contains the other, or they share a column
    (``|x1a-x1b| <= column_tolerance*width`` or horizontal overlap) AND are
    vertically related (gap ``<= adjacency_gap_ratio * min(height)`` or
    vertical overlap).  Bands are returned top-to-bottom by top edge then left
    edge.
    """
    parent = list(range(len(boxes)))

    def find(i: int) -> int:
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i

    def union(i: int, j: int) -> None:
        ri, rj = find(i), find(j)
        if ri != rj:
            parent[max(ri, rj)] = min(ri, rj)

    for i, a in enumerate(boxes):
        for j in range(i + 1, len(boxes)):
            if _related(a, boxes[j], p, width):
                union(i, j)

    groups: dict[int, list[_Box]] = {}
    for index, box in enumerate(boxes):
        groups.setdefault(find(index), []).append(box)
    bands = [
        sorted(group, key=lambda box: (box.y1, box.x1, box.item_id))
        for group in groups.values()
    ]
    return sorted(bands, key=lambda band: (min(box.y1 for box in band), min(box.x1 for box in band)))


def _related(a: "_Box", b: "_Box", p: _RelationHeadParams, width: float) -> bool:
    if _contains(a, b) or _contains(b, a):
        return True
    columns_shared = abs(a.x1 - b.x1) <= p.column_tolerance * width
    h_overlap = max(a.x1, b.x1) < min(a.x2, b.x2)
    if not (columns_shared or h_overlap):
        return False
    gap = max(a.y1, b.y1) - min(a.y2, b.y2)
    if gap > 0:
        return gap <= p.adjacency_gap_ratio * min(a.height, b.height)
    return True  # vertical overlap


def _contains(outer: "_Box", inner: "_Box") -> bool:
    return (
        outer.x1 <= inner.x1
        and inner.x2 <= outer.x2
        and outer.y1 <= inner.y1
        and inner.y2 <= outer.y2
    )


# ---------------------------------------------------------------------------
# Head election
# ---------------------------------------------------------------------------


@dataclass(frozen=True)
class _ElectedBand:
    """Outcome of head election for one band (or a fail-closed reason)."""

    band_index: int
    head: "_Box | None" = None
    head_text: str = ""
    head_ocr_ids: tuple[str, ...] = ()
    caption_boxes: tuple["_Box", ...] = ()
    reason: str | None = None


def _elect_band_head(
    band: Sequence["_Box"],
    band_index: int,
    p: _RelationHeadParams,
    width: float,
) -> _ElectedBand | None:
    """Elect the band head from raw geometry (see module docstring §2)."""
    text_boxes = [box for box in band if box.is_ocr and box.text and box.text.strip()]
    if not text_boxes:
        return None  # _REASON_NO_TEXT
    leftmost_text_x1 = min(box.x1 for box in text_boxes)
    text_column_tolerance = p.column_tolerance * width

    band_width = max(box.x2 for box in band) - min(box.x1 for box in band)
    if band_width <= 0:
        return _ElectedBand(band_index, reason=_REASON_NO_DET)
    min_head_width = p.min_head_width_ratio * band_width

    # Head candidates: raw DETECTIONS (uncombined detector boxes) that start
    # at/near the band's leftmost text column, geometrically bear a text box,
    # and reach min_head_width_ratio of the band width.  Icons/toggles on the
    # row's right never start at the text column and never become heads;
    # OCR-only bands (no detector box at the column) never produce a head —
    # a subtitle line with no detector anchor can never be promoted.
    candidates: list[_Box] = []
    for box in band:
        if box.is_ocr:
            continue
        if abs(box.x1 - leftmost_text_x1) > text_column_tolerance:
            continue
        if box.width < min_head_width:
            continue
        if not _bears_text(box, text_boxes):
            continue
        candidates.append(box)
    if not candidates:
        reason = (
            _REASON_TOO_NARROW
            if any(
                not box.is_ocr
                and abs(box.x1 - leftmost_text_x1) <= text_column_tolerance
                and _bears_text(box, text_boxes)
                for box in band
            )
            else _REASON_NO_DET
        )
        return _ElectedBand(band_index, reason=reason)

    # --- S2fix4 head election: TOPMOST primary, width as a same-line tiebreak. ---
    # A row's title always sits ABOVE its caption, so the band's topmost
    # text-bearing detection at the text column is the head — even when a lower
    # caption line is wider (the real run-5 frame 9 mid-viewport shape; the
    # former widest-first rule elected those wider captions as heads — v1n-class
    # regression).  Only candidates on the SAME line as the topmost (vertical
    # overlap >= _SAME_LINE_OVERLAP of the shorter box — two detector boxes
    # interpreting one wider title line) compete by width; the widest of that
    # group wins.  Equal-width same-line ties resolve exactly as before
    # (same-text duplicates merge, a stacked pair elects the topmost, any other
    # distinct-text tie fails closed).
    topmost = min(candidates, key=lambda box: (box.y1, box.x1, box.item_id))
    same_line = [box for box in candidates if _same_line(topmost, box)]
    max_width = max(box.width for box in same_line)
    tied = [box for box in same_line if box.width == max_width]
    ordered = sorted(tied, key=lambda box: (box.y1, box.x1, box.item_id))
    head = ordered[0]
    head_text, head_ocr_ids = _head_text(head, text_boxes)

    if len(ordered) > 1:
        texts = {box.text for box in ordered if box.text}
        if len(texts) == 1:
            # Duplicate detector interpretations of one visible title (same
            # text) — deterministic merge to the topmost box.
            pass
        elif all(
            lower.y1 - ordered[0].y1
            >= _STACK_MIN_FRACTION * min(ordered[0].height, lower.height)
            for lower in ordered[1:]
        ):
            # Vertically stacked equal-width pair at the same column =
            # title-above-caption: elect the topmost; the caption line is
            # absorbed as a satellite, never a head.
            pass
        else:
            # Equal-width, distinct texts on the same line: a genuine
            # ambiguity (two interpretations of one slot) — fail closed.
            return _ElectedBand(band_index, reason=_REASON_TIE)

    return _ElectedBand(
        band_index,
        head=head,
        head_text=head_text,
        head_ocr_ids=head_ocr_ids,
        caption_boxes=_caption_boxes(band, head, text_boxes),
    )


def _bears_text(box: "_Box", text_boxes: Sequence["_Box"]) -> bool:
    return any(
        box.x1 < other.x2 and other.x1 < box.x2
        and box.y1 < other.y2 and other.y1 < box.y2
        for other in text_boxes
    )


def _same_line(a: "_Box", b: "_Box") -> bool:
    """True when two boxes sit on the same visual line (S2fix4 tiebreak).

    Their vertical spans overlap by at least ``_SAME_LINE_OVERLAP`` of the
    shorter box's height — the signature of two detector boxes interpreting
    one row line.  A title-above-caption pair never overlaps enough (if at
    all), so the caption stays a different line and never competes for the
    head.
    """
    overlap = min(a.y2, b.y2) - max(a.y1, b.y1)
    if overlap <= 0:
        return False
    return overlap >= _SAME_LINE_OVERLAP * min(a.height, b.height)


def _head_text(head: "_Box", text_boxes: Sequence["_Box"]) -> tuple[str, tuple[str, ...]]:
    """The head's text = the topmost non-empty OCR token it bears (geometry
    only; no text-role semantics)."""
    bearing = [box for box in text_boxes if _overlaps(head, box)]
    if not bearing:
        return "", ()
    primary = sorted(bearing, key=lambda box: (box.y1, box.x1, box.item_id))[0]
    ocr_ids = tuple(sorted(box.item_id for box in bearing if box.item_id))
    return primary.text, ocr_ids


def _overlaps(a: "_Box", b: "_Box") -> bool:
    return (
        a.x1 < b.x2 and b.x1 < a.x2 and a.y1 < b.y2 and b.y1 < a.y2
    )


def _caption_boxes(
    band: Sequence["_Box"], head: "_Box", text_boxes: Sequence["_Box"]
) -> tuple["_Box", ...]:
    """OCR text boxes in the band other than the head's own text sources —
    caption/description lines (satellites, never heads)."""
    bearing_ids = {
        box.item_id for box in text_boxes if _overlaps(head, box)
    }
    captions = [
        box for box in text_boxes
        if box.item_id not in bearing_ids
        and box.item_id != head.item_id
        and not _contains(head, box)
    ]
    return tuple(sorted(captions, key=lambda box: (box.y1, box.x1, box.item_id)))


# ---------------------------------------------------------------------------
# Subtitle guard (geometric continuation detection)
# ---------------------------------------------------------------------------


def _is_subtitle_continuation(
    elected: _ElectedBand,
    previous: _ElectedBand | None,
    p: _RelationHeadParams,
    width: float,
) -> bool:
    """Geometric (never text-semantic) subtitle guard: reject a band whose
    head continues the previous band's caption geometry.

    Fires iff the previous band elected a head, its caption offset is known
    (a caption satellite exists below its head), and the current head shares
    the previous head's column while sitting at that caption offset (within a
    geometric tolerance).  A wrapped/stacked caption line therefore can never
    be elected a navigation candidate.

    Reachability: by construction any line at an in-band caption's offset is
    itself within the band-adjacency window of that caption, so end-to-end
    these lines are absorbed in-band before a separate band head exists; this
    predicate is the fail-closed defense for the separate-band geometries the
    spec names (see the module docstring §3).
    """
    if previous is None or previous.head is None or elected.head is None:
        return False
    previous_head = previous.head
    if not previous.caption_boxes:
        return False
    caption_top = previous.caption_boxes[0]
    caption_offset = caption_top.y1 - previous_head.y1
    if caption_offset <= 0:
        return False
    if abs(elected.head.x1 - previous_head.x1) > p.column_tolerance * width:
        return False
    if elected.head.y1 <= previous_head.y1:
        return False
    offset_match = abs(
        (elected.head.y1 - previous_head.y1) - caption_offset
    ) <= _CONTINUATION_TOLERANCE * min(elected.head.height, previous_head.height)
    return offset_match


# ---------------------------------------------------------------------------
# Emission
# ---------------------------------------------------------------------------


def _emit(
    elected: _ElectedBand,
    band: Sequence["_Box"],
    band_index: int,
    p: _RelationHeadParams,
    width: int,
    height: int,
) -> tuple[dict[str, Any], list[dict[str, Any]]]:
    """Emit the ONE navigation candidate for the band plus NonInteractive
    satellites (bounded by ``max_satellites_per_row``; deterministic order,
    first-N kept, the band record carries the count)."""
    head = elected.head
    assert head is not None
    candidate_id = f"relation_head_band_{band_index}"
    candidate: dict[str, Any] = {
        "id": candidate_id,
        "type": "menu_item",
        "text": elected.head_text,
        "confidence": round(head.confidence, 6),
        "bounds": _normalized_bounds(head, width, height),
        "boundsPx": [head.x1, head.y1, head.x2, head.y2],
        "center": {
            "x": round((head.x1 + head.x2) / 2.0 / width, 6),
            "y": round((head.y1 + head.y2) / 2.0 / height, 6),
        },
        "centerPx": [round((head.x1 + head.x2) / 2.0), round((head.y1 + head.y2) / 2.0)],
        "evidence": {
            "yoloId": head.item_id,
            "ocrIds": list(elected.head_ocr_ids),
            "allIds": _stable_ids([head, *elected.caption_boxes], band),
            "typeInferred": "row_relation_head",
        },
        "riskFlags": [],
    }

    caption_items = [box for box in elected.caption_boxes if box.item_id != head.item_id]
    detections = [box for box in band if not box.is_ocr and box.item_id != head.item_id]
    members = sorted(
        [*caption_items, *detections],
        key=lambda box: (box.y1, box.x1, box.item_id),
    )
    satellites: list[dict[str, Any]] = []
    for offset, box in enumerate(members[: p.max_satellites_per_row]):
        role = "caption" if box.is_ocr else _SATELLITE_ROLE_BY_LABEL.get(
            box.label, "control"
        )
        satellites.append({
            "id": f"{candidate_id}_sat_{offset}",
            "type": "NonInteractive",
            "role": role,
            "text": box.text if box.is_ocr else "",
            "bounds": _normalized_bounds(box, width, height),
            "boundsPx": [box.x1, box.y1, box.x2, box.y2],
            "center": {
                "x": round((box.x1 + box.x2) / 2.0 / width, 6),
                "y": round((box.y1 + box.y2) / 2.0 / height, 6),
            },
            "centerPx": [round((box.x1 + box.x2) / 2.0), round((box.y1 + box.y2) / 2.0)],
            "evidence": {
                "yoloId": box.item_id if not box.is_ocr else None,
                "ocrIds": [box.item_id] if box.is_ocr else [],
                "allIds": [box.item_id],
                "typeInferred": "row_relation_head_satellite",
                "headId": candidate_id,
            },
            "riskFlags": [],
        })
    return candidate, satellites


def _normalized_bounds(box: "_Box", width: int, height: int) -> dict[str, float]:
    return {
        "x1": round(box.x1 / width, 6),
        "y1": round(box.y1 / height, 6),
        "x2": round(box.x2 / width, 6),
        "y2": round(box.y2 / height, 6),
    }


def _stable_ids(head_and_captions: Sequence["_Box"], band: Sequence["_Box"]) -> list[str]:
    seen: list[str] = []
    for box in [*head_and_captions, *band]:
        if box.item_id not in seen:
            seen.append(box.item_id)
    return seen


def _band_record(
    band: Sequence["_Box"],
    band_index: int,
    status: str,
    reason: str | None,
    *,
    head_text: str = "",
    head_id: str = "",
    satellites: list[dict[str, Any]] | None = None,
) -> dict[str, Any]:
    record: dict[str, Any] = {
        "bandIndex": band_index,
        "yTop": round(min(box.y1 for box in band), 6),
        "status": status,
    }
    if reason is not None:
        record["reason"] = reason
    else:
        record["headText"] = head_text
        record["headId"] = head_id
        record["satelliteCount"] = len(satellites or [])
    return record


def _record(
    status: str,
    detail: str,
    emitted: int,
    bands: list[dict[str, Any]],
    candidates: list[dict[str, Any]] | None = None,
    satellites: list[dict[str, Any]] | None = None,
) -> dict[str, Any]:
    return {
        "status": status,
        "detail": detail,
        "emitted": emitted,
        "bands": bands,
        "candidates": candidates or [],
        "satellites": satellites or [],
    }


# ---------------------------------------------------------------------------
# Raw box view (uncombined detector boxes + OCR text blocks)
# ---------------------------------------------------------------------------


class _Box:
    """Read-only view of one raw input dict (detector box or OCR text block).

    ``is_ocr=False`` marks a raw YOLO detection dict, ``is_ocr=True`` an OCR
    token dict — the caller's two raw arrays, never composed candidates.
    """

    __slots__ = (
        "item_id", "label", "confidence", "x1", "y1", "x2", "y2",
        "is_ocr", "text",
    )

    def __init__(self, raw: dict[str, Any], *, is_ocr: bool) -> None:
        self.x1, self.y1, self.x2, self.y2 = _finite_box(raw)
        if self.x2 < self.x1 or self.y2 < self.y1:
            raise ValueError(
                f"INVALID_GEOMETRY: raw input {raw.get('id')!r} has inverted bounds"
            )
        self.item_id = str(raw.get("id", ""))
        self.label = str(raw.get("label", "text_block"))
        self.confidence = float(raw.get("confidence", 0.0))
        self.is_ocr = is_ocr
        self.text = str(raw.get("text", "") or "")

    @property
    def width(self) -> float:
        return self.x2 - self.x1

    @property
    def height(self) -> float:
        return self.y2 - self.y1

    def __repr__(self) -> str:  # pragma: no cover - debugging aid
        kind = "ocr" if self.is_ocr else "det"
        return (
            f"_Box({kind} {self.item_id!r} "
            f"[{self.x1:g},{self.y1:g},{self.x2:g},{self.y2:g}] {self.text!r})"
        )


def _finite_box(raw: dict[str, Any]) -> tuple[float, float, float, float]:
    bounds = raw["boundsPx"]
    if not isinstance(bounds, (list, tuple)) or len(bounds) != 4:
        raise ValueError(
            f"INVALID_GEOMETRY: raw input {raw.get('id')!r} must carry a "
            "4-number 'boundsPx'"
        )
    values: list[float] = []
    for value in bounds:
        if isinstance(value, bool) or not isinstance(value, (int, float)):
            raise ValueError(f"INVALID_GEOMETRY: non-numeric bounds component {value!r}")
        number = float(value)
        if not math.isfinite(number):
            raise ValueError(f"INVALID_GEOMETRY: non-finite bounds component {value!r}")
        values.append(number)
    return values[0], values[1], values[2], values[3]