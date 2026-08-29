"""Fusion engine: YOLO + OCR → structured perception candidates.

Primary entry points: fuse_evidence (full-image OCR) and
fuse_evidence_from_crops (per-crop OCR, legacy).

Extracted from fusion.py. Uses heuristics and scoring submodules.
"""
from __future__ import annotations

import copy
import math
from collections import defaultdict
from typing import Any, Iterable, Sequence

from ..schema import Detection, OcrToken
from ..operators.registry_defaults import (
    DEFAULT_CONTEXT,
    DEFAULT_RULE_SET,
    REGISTRY,
)
from ..operators.trace import build_raw_sources, execute_pipeline
from .heuristics import (
    apply_chevron_heuristic,
    apply_search_box_labeling,
    apply_toggle_inference_heuristic,
    prune_empty_text_artifacts,
    primary_line_text,
)
from .row_stabilizer import stabilize_with_context
from .scoring import (
    candidate_risks,
    combined_confidence,
    match_score,
    normalized_center,
)


DEFAULT_INTERACTIVE_LABELS = {
    "button",
    "list_item",
    "toggle",
    "switch",
    "input",
    "tab",
    "icon",
    "popup",
    "toolbar",
    "back",
    "checkbox",
    "slider",
    "text_block",
}

#: Candidate types EXEMPT from the same-line same-text dedup pass.  Only
#: NON-navigation candidates participate: composed navigation rows
#: (``menu_item``) own their absorption logic (apply_chevron_heuristic /
#: uniform-list anchor absorption) and must never be touched here, and
#: ``NonInteractive`` satellites are operator-composed, band-bound outputs of
#: ``row-relation-head`` (never fusion candidates; the G-1 satellite-absorption
#: gate locks their emission).  Icon candidates carry no text, so the
#: non-empty text guard excludes them naturally.
_DEDUP_EXEMPT_TYPES: frozenset[str] = frozenset({"menu_item", "NonInteractive"})


#: Column-alignment tolerance (normalized x1) for promoting an uncomposed
#: ``text_block`` to ``menu_item`` when it sits on the modal menu_item column
#: (WI-S2fix6 — perception type-consistency repair).
_PROMOTE_COLUMN_TOLERANCE: float = 0.03

#: Minimum box-width fraction of the average menu_item width for a text_block
#: to be treated as a row TITLE (a narrow caption/subtitle is not a title).
_PROMOTE_MIN_WIDTH_FRACTION: float = 0.6

#: Minimum number of ``menu_item`` candidates required to establish a reliable
#: modal column reference for promotion.
_PROMOTE_MIN_MENU_ITEMS: int = 2


#: RVLM-1 overlap thresholds for text-to-box misattribution detection.
#: A misattributed text candidate and the real ``menu_item`` row share
#: (nearly) the same box — frozen corpus (occ_23/occ_24) has identical bounds.
#: Vertical overlap >= 80% of the shorter span AND horizontal overlap >= 50%
#: of the shorter span qualifies as "same position".
_MISATTRIB_V_OVERLAP_FRAC: float = 0.8
_MISATTRIB_H_OVERLAP_FRAC: float = 0.5


def _emit_candidate_stage(
    stage_sink: Any | None,
    stage: str,
    candidates: list[dict[str, Any]],
    **decision: Any,
) -> None:
    """Emit an opt-in immutable validation view of the actual single pass."""
    if stage_sink is not None:
        stage_sink({
            "stage": stage,
            "coordinateSpace": "PREPROCESSED",
            "candidates": copy.deepcopy(candidates),
            **decision,
        })


def _attach_fusion_trace(
    pipeline_trace: dict[str, Any],
    *,
    detections: list[Any],
    ocr_tokens: list[Any],
    diagnostics: Mapping[str, Any],
    row_map: Mapping[str, Any],
    output: list[dict[str, Any]],
    trace_sink: Any | None,
) -> None:
    """Attach the fusion causal document (TRACE != CONTROL) and sink it.

    Pure recording of the fusion decision chain on top of the deterministic
    operator pipeline trace: input refs, per-step router/operator/validator
    decisions (enriched in ``operators/trace.py``), post-pipeline diagnostics,
    row stabilization rowId map, final output refs, and the causal verdict
    (first failed composition decision).  Nothing here reads back into fusion
    or routing behavior; the trace is reference/evidence-only.
    """
    if trace_sink is None:
        return
    from .causal_trace import (
        FUSION_TRACE_FORMAT,
        FUSION_TRACE_FORMAT_VERSION,
        build_fusion_events,
        first_failed_composition_decision,
        input_refs,
    )
    steps = pipeline_trace.get("steps", [])
    events = build_fusion_events(
        input_refs_doc=input_refs(detections, ocr_tokens),
        steps=steps,
        diagnostics=diagnostics,
        row_map=row_map,
        output=output,
    )
    pipeline_trace["fusion"] = {
        "format": FUSION_TRACE_FORMAT,
        "formatVersion": FUSION_TRACE_FORMAT_VERSION,
        "events": events,
        "verdict": first_failed_composition_decision(steps),
    }
    trace_sink(pipeline_trace)


def fuse_evidence(
    detections: Iterable[Detection],
    ocr_tokens: Iterable[OcrToken],
    *,
    image: Any | None = None,
    image_width: int,
    image_height: int,
    interactive_labels: set[str] | None = None,
    promote_unmatched_ocr: bool = False,
    max_ocr_distance_ratio: float = 0.055,
    stabilize: bool = False,
    stabilize_context: list[dict[str, Any]] | None = None,
    trace_sink: Any | None = None,
    stage_sink: Any | None = None,
    registry: Any | None = None,
    rules: Any | None = None,
    context: Any | None = None,
) -> dict[str, Any]:
    """Fuse YOLO detections + full-image OCR tokens → structured evidence.

    Primary fusion path for RapidOCR full-image mode.

    The row-composition step executes the declared operator pipeline
    (``uniform-list-row-grouping`` GENERATOR then ``spacing-verifier``
    VALIDATOR) through the operator framework: parameters resolve from the
    root-rule defaults (= the retained candidate's current constants), so this
    path is the S1 zero-diff port.  ``trace_sink``, if given, receives the
    deterministic pipeline trace (S1.8 offline-replay support).
    """
    pipeline_registry = REGISTRY if registry is None else registry
    pipeline_rules = DEFAULT_RULE_SET if rules is None else rules
    pipeline_context = DEFAULT_CONTEXT if context is None else context
    labels = interactive_labels or DEFAULT_INTERACTIVE_LABELS
    yolo = sorted(
        [d for d in detections if d.label in labels],
        key=lambda d: (d.box.y1, d.box.x1, d.box.y2, d.box.x2),
    )
    ocr = sorted(
        [t for t in ocr_tokens if t.text.strip()],
        key=lambda t: (t.box.y1, t.box.x1, t.box.y2, t.box.x2),
    )

    candidates: list[dict[str, Any]] = []
    matched_ocr_ids: set[str] = set()
    screen_diag = math.hypot(image_width, image_height)
    max_distance = screen_diag * max_ocr_distance_ratio

    for index, detection in enumerate(yolo, start=1):
        matches = [
            (token, match_score(detection, token, max_distance))
            for token in ocr
        ]
        matches = [(token, score) for token, score in matches if score > 0]
        matches.sort(key=lambda pair: (-pair[1], pair[0].box.y1, pair[0].box.x1))
        selected = [token for token, _ in matches]
        for token in selected:
            matched_ocr_ids.add(token.id)

        text = primary_line_text(selected)
        evidence_ids = [detection.id] + [token.id for token in selected]
        risks = candidate_risks(detection, selected)

        candidates.append({
            "id": f"candidate_{index}",
            "type": detection.label,
            "text": text,
            "confidence": round(combined_confidence(detection, selected), 6),
            "bounds": detection.box.normalized(image_width, image_height),
            "boundsPx": [
                round(detection.box.x1), round(detection.box.y1),
                round(detection.box.x2), round(detection.box.y2),
            ],
            "center": normalized_center(detection, image_width, image_height),
            "centerPx": [round(v) for v in detection.box.center()],
            "evidence": {
                "yoloId": detection.id,
                "ocrIds": [token.id for token in selected],
                "allIds": evidence_ids,
            },
            "riskFlags": risks,
        })

    if promote_unmatched_ocr:
        next_index = len(candidates) + 1
        for token in ocr:
            if token.id in matched_ocr_ids:
                continue
            candidates.append({
                "id": f"candidate_{next_index}",
                "type": "text_block",
                "text": token.text,
                "confidence": round(token.confidence * 0.75, 6),
                "bounds": token.box.normalized(image_width, image_height),
                "boundsPx": [
                    round(token.box.x1), round(token.box.y1),
                    round(token.box.x2), round(token.box.y2),
                ],
                "center": normalized_center(token, image_width, image_height),
                "centerPx": [round(v) for v in token.box.center()],
                "evidence": {
                    "yoloId": None,
                    "ocrIds": [token.id],
                    "allIds": [token.id],
                },
                "riskFlags": ["ocr_only"],
            })
            next_index += 1

    # Apply heuristics; the row-composition step runs through the operator
    # framework (registry-declared topology + resolved root-rule defaults).
    apply_search_box_labeling(candidates)
    apply_chevron_heuristic(candidates, yolo)
    _emit_candidate_stage(stage_sink, "composition-input", candidates)
    pipeline_trace = _run_operator_pipeline(
        candidates, yolo,
        image_width=image_width, image_height=image_height,
        ocr=ocr,
        registry=pipeline_registry, rules=pipeline_rules, context=pipeline_context,
        trace_sink=trace_sink,
    )
    _emit_candidate_stage(stage_sink, "composition-output", candidates)
    apply_toggle_inference_heuristic(candidates, image=image)
    _emit_candidate_stage(stage_sink, "toggle-inference", candidates)
    prune_empty_text_artifacts(candidates)
    _emit_candidate_stage(stage_sink, "prune-empty-text", candidates)
    # OCR noise floor: drop non-interactive text_blocks shorter than 3 chars
    # (e.g. "ED" — garbled partial detections that downstream would classify
    # as Unknown and block completeness). Real menu titles are ≥3 chars.
    candidates[:] = [c for c in candidates if not (
        c.get("type") == "text_block" and len((c.get("text") or "").strip()) < 3
    )]
    _emit_candidate_stage(stage_sink, "short-text-noise-floor", candidates)
    line_dup_suppressed = dedupe_same_line_nonnav_candidates(candidates)
    _emit_candidate_stage(
        stage_sink, "same-line-nonnav-dedup", candidates,
        status="matched" if line_dup_suppressed else "noop",
        detail=line_dup_suppressed,
    )
    type_promotions = _promote_column_aligned_text_blocks(candidates)
    _emit_candidate_stage(
        stage_sink, "column-aligned-type-promotion", candidates,
        status="matched" if type_promotions else "noop",
        detail=type_promotions,
    )
    misattribution_removed = _detect_text_box_misattribution(candidates)
    _emit_candidate_stage(
        stage_sink, "text-box-misattribution", candidates,
        status="matched" if misattribution_removed else "noop",
        detail=misattribution_removed,
    )

    # Cross-frame row stabilization is the final assembly step (after type
    # promotion).  It is opt-in and **stateless** (WI-CTX): the caller supplies
    # the known-row context (``stabilize_context``) and the stabilizer tags
    # each candidate with a ``row_id`` (matched id, or ``None`` for a new row)
    # without retaining any state.  The default single-frame path
    # (``stabilize=False``) leaves the candidates untouched so the S1
    # equivalence baseline stays byte-identical.
    if stabilize:
        stabilize_with_context(candidates, stabilize_context)
    _emit_candidate_stage(stage_sink, "row-stabilization", candidates)

    result = {
        "image": {"width": image_width, "height": image_height},
        "yolo": [d.to_json(image_width, image_height) for d in yolo],
        "ocr": [t.to_json(image_width, image_height) for t in ocr],
        "candidates": candidates,
        "summary": {
            "yoloCount": len(yolo),
            "ocrCount": len(ocr),
            "candidateCount": len(candidates),
            "unmatchedOcrCount": len([t for t in ocr if t.id not in matched_ocr_ids]),
        },
    }
    diagnostics: dict[str, Any] = {}
    if line_dup_suppressed:
        diagnostics["lineDupSuppressed"] = line_dup_suppressed
    if type_promotions:
        diagnostics["typePromotions"] = type_promotions
    if misattribution_removed:
        diagnostics["misattributionRemoved"] = misattribution_removed
    if diagnostics:
        result["_diagnostics"] = diagnostics
    _attach_fusion_trace(
        pipeline_trace,
        detections=yolo,
        ocr_tokens=ocr,
        diagnostics=result.get("_diagnostics", {}),
        row_map={
            str(c.get("id", "")): c.get("row_id") for c in candidates if c.get("id")
        },
        output=candidates,
        trace_sink=trace_sink,
    )
    return result


def fuse_evidence_from_crops(
    detections: list[Detection],
    crops_ocr: list[list[OcrToken]],
    *,
    image_width: int,
    image_height: int,
    promote_unmatched_ocr: bool = False,
    stabilize: bool = False,
    stabilize_context: list[dict[str, Any]] | None = None,
    trace_sink: Any | None = None,
    stage_sink: Any | None = None,
    registry: Any | None = None,
    rules: Any | None = None,
    context: Any | None = None,
) -> dict[str, Any]:
    """Fuse YOLO detections + per-crop OCR results → structured evidence.

    Legacy path for PaddleOCR per-crop mode. Each crop's OCR tokens are
    already associated with the corresponding YOLO detection.
    promote_unmatched_ocr is always False in this path.
    """
    pipeline_registry = REGISTRY if registry is None else registry
    pipeline_rules = DEFAULT_RULE_SET if rules is None else rules
    pipeline_context = DEFAULT_CONTEXT if context is None else context
    candidates: list[dict[str, Any]] = []
    all_tokens: list[OcrToken] = []

    for detection, tokens in zip(detections, crops_ocr):
        all_tokens.extend(tokens)
        selected = [t for t in tokens if t.text.strip()]

        text = primary_line_text(selected)
        risks = candidate_risks(detection, selected)

        candidates.append({
            "id": f"candidate_{len(candidates) + 1}",
            "type": detection.label,
            "text": text,
            "confidence": round(combined_confidence(detection, selected), 6),
            "confidenceDetail": {
                "yolo": round(detection.confidence, 6),
                "ocr": (
                    round(sum(t.confidence for t in selected) / len(selected), 6)
                    if selected else None
                ),
            },
            "bounds": detection.box.normalized(image_width, image_height),
            "boundsPx": [
                round(detection.box.x1), round(detection.box.y1),
                round(detection.box.x2), round(detection.box.y2),
            ],
            "center": normalized_center(detection, image_width, image_height),
            "centerPx": [round(v) for v in detection.box.center()],
            "evidence": {
                "yoloId": detection.id,
                "ocrIds": [t.id for t in selected],
                "allIds": [detection.id] + [t.id for t in selected],
            },
            "riskFlags": risks,
        })

    apply_search_box_labeling(candidates)
    apply_chevron_heuristic(candidates, list(detections))
    _emit_candidate_stage(stage_sink, "composition-input", candidates)
    pipeline_trace = _run_operator_pipeline(
        candidates, list(detections),
        image_width=image_width, image_height=image_height,
        ocr=all_tokens,
        registry=pipeline_registry, rules=pipeline_rules, context=pipeline_context,
        trace_sink=trace_sink,
    )
    _emit_candidate_stage(stage_sink, "composition-output", candidates)
    prune_empty_text_artifacts(candidates)
    _emit_candidate_stage(stage_sink, "prune-empty-text", candidates)
    line_dup_suppressed = dedupe_same_line_nonnav_candidates(candidates)
    _emit_candidate_stage(
        stage_sink, "same-line-nonnav-dedup", candidates,
        status="matched" if line_dup_suppressed else "noop",
        detail=line_dup_suppressed,
    )
    type_promotions = _promote_column_aligned_text_blocks(candidates)
    _emit_candidate_stage(
        stage_sink, "column-aligned-type-promotion", candidates,
        status="matched" if type_promotions else "noop",
        detail=type_promotions,
    )
    misattribution_removed = _detect_text_box_misattribution(candidates)
    _emit_candidate_stage(
        stage_sink, "text-box-misattribution", candidates,
        status="matched" if misattribution_removed else "noop",
        detail=misattribution_removed,
    )

    # Cross-frame row stabilization — final assembly step, opt-in and
    # stateless (WI-CTX): the caller's known-row context (``stabilize_context``)
    # is matched and each candidate is tagged with a ``row_id`` (matched id, or
    # ``None`` for a new row).  The default single-frame path is a no-op and
    # preserves the S1 equivalence baseline.
    if stabilize:
        stabilize_with_context(candidates, stabilize_context)
    _emit_candidate_stage(stage_sink, "row-stabilization", candidates)

    result = {
        "image": {"width": image_width, "height": image_height},
        "yolo": [d.to_json(image_width, image_height) for d in detections],
        "ocr": [t.to_json(image_width, image_height) for t in all_tokens],
        "candidates": candidates,
        "summary": {
            "yoloCount": len(detections),
            "ocrCount": len(all_tokens),
            "candidateCount": len(candidates),
            "unmatchedOcrCount": 0,
        },
    }
    diagnostics: dict[str, Any] = {}
    if line_dup_suppressed:
        diagnostics["lineDupSuppressed"] = line_dup_suppressed
    if type_promotions:
        diagnostics["typePromotions"] = type_promotions
    if misattribution_removed:
        diagnostics["misattributionRemoved"] = misattribution_removed
    if diagnostics:
        result["_diagnostics"] = diagnostics
    _attach_fusion_trace(
        pipeline_trace,
        detections=detections,
        ocr_tokens=all_tokens,
        diagnostics=result.get("_diagnostics", {}),
        row_map={
            str(c.get("id", "")): c.get("row_id") for c in candidates if c.get("id")
        },
        output=candidates,
        trace_sink=trace_sink,
    )
    return result


def dedupe_same_line_nonnav_candidates(
    candidates: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    """Collapse same-line same-text NON-navigation candidates deterministically.

    Real-frame defect (run 4, seq 17): one visual search line emits 2-4
    ``input`` candidates with the identical text ``Q Search settings`` — OCR
    returns horizontally-offset boxes (magnifier glyph + text) around the same
    search bar.  The existing IoU-based dedup (IoU >= 0.6) cannot merge
    horizontally-offset same-line boxes, and without a final defensive pass
    every duplicate becomes an evidence-less Unknown element in the frozen
    downstream stack.

    Rule (deterministic, minimal surface):
    * Only candidates whose type is NOT in ``_DEDUP_EXEMPT_TYPES``
      (``menu_item``, ``NonInteractive``) participate — composed navigation
      rows own their own absorption logic and operator-composed satellites are
      band-bound outputs of ``row-relation-head``; neither is ever touched.
      Non-empty ``text.strip()`` is required, so icons (no text) are naturally
      unaffected.
    * Same visual row: vertical span overlap >= half the shorter candidate's
      height (mirrors the relation-head router line-occupancy precedent,
      ``relation_head_router._shares_line``), OR the vertical gap between the
      two boxes is <= the shorter candidate's height (adjacent-line case — a
      title and its immediately-following shadow text_block, e.g.
      ``Sound & vibration`` at y=[0.401,0.421] and a duplicate shadow at
      y=[0.430,0.446] have gap=0.009 with no overlap but are one visual row).
      Same text on truly DIFFERENT lines (gap >> height, e.g. Settings row
      spacing of 0.06-0.10) is preserved.
    * Same text: ``text.strip()`` exact equality.
    * Group members collapse to exactly one survivor: highest confidence;
      tie -> larger bounds area; tie -> lexicographically smallest id.

    Suppressed candidates are REMOVED in place; the returned list records
    ``[{id, text, keptId}]`` for the engine's ``_diagnostics`` surface.  The
    survivor's serialization and the ``candidates`` list order of everything
    else are untouched.
    """
    participants = [
        candidate for candidate in candidates
        if candidate.get("type", "") not in _DEDUP_EXEMPT_TYPES
        and candidate.get("text", "").strip()
        and isinstance(candidate.get("boundsPx"), list)
        and len(candidate["boundsPx"]) >= 4
    ]
    if len(participants) < 2:
        return []

    def _same_line(a: dict[str, Any], b: dict[str, Any]) -> bool:
        ay1, ay2 = float(a["boundsPx"][1]), float(a["boundsPx"][3])
        by1, by2 = float(b["boundsPx"][1]), float(b["boundsPx"][3])
        shorter = min(ay2 - ay1, by2 - by1)
        if shorter <= 0:
            return False
        overlap = min(ay2, by2) - max(ay1, by1)
        if overlap >= 0.5 * shorter:
            return True
        # Adjacent-line case: no overlap, but the vertical gap between the two
        # boxes is <= the shorter candidate's height. Covers a title immediately
        # followed by its shadow text_block (gap ~0.009, shorter height ~0.016)
        # while preserving same-text truly-different-line pairs whose gap is
        # far larger than a row height (Settings row spacing 0.06-0.10).
        gap = max(0.0, max(ay1, by1) - min(ay2, by2))
        return gap <= shorter

    # Union-find over the pairwise same-line relation; text equality gates the
    # union so same-text DIFFERENT-line duplicates stay in separate groups.
    parent = list(range(len(participants)))

    def _find(i: int) -> int:
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i

    def _union(i: int, j: int) -> None:
        ri, rj = _find(i), _find(j)
        if ri != rj:
            parent[rj] = ri

    for i in range(len(participants)):
        for j in range(i + 1, len(participants)):
            if (
                participants[i]["text"].strip() == participants[j]["text"].strip()
                and _same_line(participants[i], participants[j])
            ):
                _union(i, j)

    groups: dict[int, list[dict[str, Any]]] = {}
    for i, candidate in enumerate(participants):
        groups.setdefault(_find(i), []).append(candidate)

    drop_ids: set[int] = set()
    suppressed: list[dict[str, Any]] = []
    for members in groups.values():
        if len(members) < 2:
            continue
        kept = min(members, key=_dedup_survivor_key)
        for member in members:
            if member is kept:
                continue
            drop_ids.add(id(member))
            suppressed.append({
                "id": member.get("id", ""),
                "text": member.get("text", ""),
                "keptId": kept.get("id", ""),
            })

    if drop_ids:
        candidates[:] = [
            candidate for candidate in candidates
            if id(candidate) not in drop_ids
        ]
    return suppressed


def _dedup_survivor_key(candidate: dict[str, Any]) -> tuple[Any, ...]:
    """Deterministic survivor order: highest confidence, tie -> larger bounds
    area, tie -> lexicographically smallest id."""
    x1, y1, x2, y2 = (float(v) for v in candidate["boundsPx"])
    return (
        -float(candidate.get("confidence", 0.0)),
        -max(0.0, (x2 - x1) * (y2 - y1)),
        candidate.get("id", ""),
    )


def _shares_visual_line(a_px: Sequence[float], b_px: Sequence[float]) -> bool:
    """Two pixel boxes describe the same visual line when their vertical spans
    overlap by at least half the shorter span OR the gap between them is no
    larger than the shorter height (mirrors ``dedupe_same_line_nonnav_candidates``
    so a caption/shadow bound to a composed row is never retyped into a second
    navigation candidate on one physical line)."""
    ay1, ay2 = float(a_px[1]), float(a_px[3])
    by1, by2 = float(b_px[1]), float(b_px[3])
    shorter = min(ay2 - ay1, by2 - by1)
    if shorter <= 0:
        return False
    overlap = min(ay2, by2) - max(ay1, by1)
    if overlap >= 0.5 * shorter:
        return True
    gap = max(0.0, max(ay1, by1) - min(ay2, by2))
    return gap <= shorter


def _modal_column_x1(values: Sequence[float], tolerance: float) -> float:
    """Deterministic modal column x1 with tolerance clustering.

    Real composed menu_items sit at a common column but their normalized x1
    varies slightly across detections, so an exact-value mode would fragment
    the column.  Each value's weight is the count of values within
    ``tolerance`` of it; the winner is the highest weight, tie -> smallest
    value (deterministic).
    """
    best_value = float(values[0])
    best_count = -1
    for value in values:
        count = sum(1 for other in values if abs(value - other) <= tolerance)
        if count > best_count or (count == best_count and value < best_value):
            best_count = count
            best_value = value
    return best_value


def _promote_column_aligned_text_blocks(
    candidates: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    """Promote uncomposed ``text_block`` candidates to ``menu_item`` when they
    sit on the composed menu_items' modal x1 column (WI-S2fix6).

    E4 diagnosis: the same Settings row (e.g. ``Sound & vibration``) is sensed
    as ``menu_item`` when ``row-relation-head`` composes it but as
    ``text_block`` when composition fails, so the downstream ``Text|Type``
    signature is unstable and the normalizer overlap chain breaks.  This final
    assembly step makes the type stable across frames: only the ``type`` field
    of a qualifying ``text_block`` is changed to ``menu_item``; text, bounds,
    confidence and evidence are untouched.

    Guards (deterministic, geometric + provenance — never text semantics):

    * Scoped to the relation-head composition path — the E4 defect is a
      relation-head composition inconsistency, so promotion fires ONLY when a
      ``row_relation_head`` menu_item exists (relation-head ran and composed at
      least one row).  When relation-head delegated to uniform-list
      (``>= ROUTING_MIN_ANCHORS`` confirmed anchors), uniform-list owns
      composition and its fail-closed rejections are NOT overridden here
      (preserves the S1 equivalence baseline byte-for-byte).
    * ``>= _PROMOTE_MIN_MENU_ITEMS`` menu_items establish a reliable modal
      column.
    * Column alignment: ``|text_block.x1 - modal_x1| <= column_tolerance``
      (normalized ``bounds.x1``).
    * NOT on the same visual line as any menu_item — a caption/shadow bound to
      a composed row never becomes a second navigation candidate on one line.
    * NOT a relation-head satellite duplicate: satellites are emitted as
      ``NonInteractive`` (excluded by the type filter), and a text_block whose
      text exactly matches an emitted satellite's text is a caption duplicate
      and is left alone.
    * Box width ``>= _PROMOTE_MIN_WIDTH_FRACTION`` of the average menu_item
      width (a row title is as wide as its row; a narrow subtitle is not).
    * Non-empty text.

    Returns ``[{id, text}]`` for the engine's ``_diagnostics`` surface; mutates
    only the ``type`` field of promoted candidates in place.
    """
    menu_items = [
        candidate for candidate in candidates
        if candidate.get("type") == "menu_item"
    ]
    if len(menu_items) < _PROMOTE_MIN_MENU_ITEMS:
        return []
    # Scope to the relation-head composition path (the E4 defect class).
    if not any(
        candidate.get("evidence", {}).get("typeInferred") == "row_relation_head"
        for candidate in menu_items
    ):
        return []

    column_x1s = [
        float(candidate["bounds"]["x1"]) for candidate in menu_items
        if isinstance(candidate.get("bounds"), dict) and "x1" in candidate["bounds"]
    ]
    if len(column_x1s) < _PROMOTE_MIN_MENU_ITEMS:
        return []
    modal_x1 = _modal_column_x1(column_x1s, _PROMOTE_COLUMN_TOLERANCE)

    width_bearers = [
        candidate for candidate in menu_items
        if isinstance(candidate.get("boundsPx"), list) and len(candidate["boundsPx"]) >= 4
    ]
    if not width_bearers:
        return []
    avg_menu_width = sum(
        candidate["boundsPx"][2] - candidate["boundsPx"][0]
        for candidate in width_bearers
    ) / len(width_bearers)

    satellite_texts = {
        str(candidate.get("text", "")).strip()
        for candidate in candidates
        if candidate.get("type") == "NonInteractive"
        and str(candidate.get("text", "")).strip()
    }

    promoted: list[dict[str, Any]] = []
    for candidate in candidates:
        if candidate.get("type") != "text_block":
            continue
        text = str(candidate.get("text", "")).strip()
        if not text:
            continue
        bounds = candidate.get("bounds")
        if not isinstance(bounds, dict) or "x1" not in bounds:
            continue
        if abs(float(bounds["x1"]) - modal_x1) > _PROMOTE_COLUMN_TOLERANCE:
            continue
        bounds_px = candidate.get("boundsPx")
        if not isinstance(bounds_px, list) or len(bounds_px) < 4:
            continue
        if any(
            isinstance(menu_item.get("boundsPx"), list)
            and len(menu_item["boundsPx"]) >= 4
            and _shares_visual_line(bounds_px, menu_item["boundsPx"])
            for menu_item in menu_items
        ):
            continue
        if text in satellite_texts:
            continue
        if bounds_px[2] - bounds_px[0] < _PROMOTE_MIN_WIDTH_FRACTION * avg_menu_width:
            continue
        candidate["type"] = "menu_item"
        promoted.append({"id": candidate.get("id", ""), "text": text})
    return promoted


# rsi-restart

# rsi-mutation

# rsi-mutation

# rsi-restart


def _bounds_overlap_same_position(a_bounds: dict, b_bounds: dict) -> bool:
    """Two normalized bounds dicts describe the same position when their
    vertical spans overlap >= ``_MISATTRIB_V_OVERLAP_FRAC`` of the shorter
    span AND horizontal spans overlap >= ``_MISATTRIB_H_OVERLAP_FRAC`` of the
    shorter span (mirrors the frozen-corpus identical-bounds pattern)."""
    ab = a_bounds or {}
    bb = b_bounds or {}
    ay1, ay2 = float(ab.get("y1", 0)), float(ab.get("y2", 0))
    by1, by2 = float(bb.get("y1", 0)), float(bb.get("y2", 0))
    ax1, ax2 = float(ab.get("x1", 0)), float(ab.get("x2", 0))
    bx1, bx2 = float(bb.get("x1", 0)), float(bb.get("x2", 0))

    v_shorter = min(ay2 - ay1, by2 - by1)
    if v_shorter <= 0:
        return False
    v_overlap = min(ay2, by2) - max(ay1, by1)
    if v_overlap < v_shorter * _MISATTRIB_V_OVERLAP_FRAC:
        return False

    h_shorter = min(ax2 - ax1, bx2 - bx1)
    if h_shorter <= 0:
        return False
    h_overlap = min(ax2, bx2) - max(ax1, bx1)
    return h_overlap >= h_shorter * _MISATTRIB_H_OVERLAP_FRAC


def _detect_text_box_misattribution(
    candidates: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    """RVLM-1: remove OCR-to-box misattributions (purely structural).

    Pattern (frozen Display V1 corpus, occurrence-provenance.md): text T
    appears at position A (original) and position B (misattributed), where B
    has a different ``menu_item`` text T' (T ≠ T') and T@B overlaps T'@B →
    T@B is a misattribution → remove.

    Guards (deterministic, geometric + exact-text — never text semantics):

    * Exact text equality (``text.strip()``) groups candidates — fuzzy
      matching is forbidden ('Color' ≠ 'Colors').
    * The "real" row at position B must be a ``menu_item`` with DIFFERENT
      text (the misattributed text was placed on a real menu row's box).
    * There must be at least one other occurrence of T at a DIFFERENT
      position (provenance / original).  No original → keep (don't guess).
    * The other occurrences must cluster to EXACTLY ONE distinct position
      (unambiguous original).  Two or more distinct original positions →
      ambiguous → keep (don't guess).
    * Candidates without a ``bounds`` dict are skipped (defensive).

    Mutates ``candidates`` in place (removes misattributed entries) and
    returns ``[{id, text, removedBounds, originalBounds,
    overlappingMenuText, reason}]`` for the engine's ``_diagnostics`` surface.
    """
    by_text: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for candidate in candidates:
        text = (candidate.get("text") or "").strip()
        if text:
            by_text[text].append(candidate)

    to_remove: set[int] = set()
    removed: list[dict[str, Any]] = []

    for text, occurrences in by_text.items():
        if len(occurrences) < 2:
            continue  # unique text → no misattribution possible

        for occ in occurrences:
            occ_bounds = occ.get("bounds")
            if not isinstance(occ_bounds, dict) or not occ_bounds:
                continue

            # Find a menu_item with DIFFERENT text at the same position as occ.
            overlapping_menu: dict[str, Any] | None = None
            for other in candidates:
                if other is occ:
                    continue
                other_text = (other.get("text") or "").strip()
                if not other_text or other_text == text:
                    continue  # same text or no text → not the "real" row
                if other.get("type") != "menu_item":
                    continue  # only a menu_item proves the real row
                other_bounds = other.get("bounds")
                if not isinstance(other_bounds, dict) or not other_bounds:
                    continue
                if _bounds_overlap_same_position(occ_bounds, other_bounds):
                    overlapping_menu = other
                    break

            if overlapping_menu is None:
                continue  # no different-text menu at same position → not misattributed

            # Other occurrences of the same text at a DIFFERENT position
            # (potential originals).  Same-position duplicates (e.g. a
            # menu_item echo of the section header) are excluded — they are
            # co-located, not a separate original.
            others = [
                o for o in occurrences
                if o is not occ
                and isinstance(o.get("bounds"), dict)
                and not _bounds_overlap_same_position(o["bounds"], occ_bounds)
            ]
            if not others:
                continue  # no original at a different position → don't guess

            # Cluster the others by position → count distinct original
            # positions.  One cluster = unambiguous original; 2+ = ambiguous.
            clusters: list[list[dict[str, Any]]] = []
            for other in others:
                placed = False
                for cluster in clusters:
                    if _bounds_overlap_same_position(
                        other["bounds"], cluster[0]["bounds"]
                    ):
                        cluster.append(other)
                        placed = True
                        break
                if not placed:
                    clusters.append([other])

            if len(clusters) != 1:
                continue  # ambiguous (0 shouldn't happen; 2+ = multiple originals) → keep

            original = clusters[0][0]
            to_remove.add(id(occ))
            removed.append({
                "id": occ.get("id", ""),
                "text": text,
                "removedBounds": occ_bounds,
                "originalBounds": original.get("bounds"),
                "overlappingMenuText": (overlapping_menu.get("text") or "").strip(),
                "reason": "text-to-box misattribution (RVLM-1)",
            })

    if to_remove:
        candidates[:] = [
            candidate for candidate in candidates
            if id(candidate) not in to_remove
        ]
    return removed


def _run_operator_pipeline(
    candidates: list[dict[str, Any]],
    yolo_detections: list[Any],
    *,
    image_width: int,
    image_height: int,
    ocr: Iterable[OcrToken],
    registry: Any,
    rules: Any,
    context: Any,
    trace_sink: Any | None,
) -> dict[str, Any]:
    """Execute the declared operator pipeline over the fused candidates.

    Resolves parameters against the given rule set (the S1 wiring uses the
    root-only default rule set, whose values equal the retained candidate's
    current constants — the S1 zero-diff contract) and runs the code-owned
    topology (generator then validator) via the framework.  The deterministic
    trace record is RETURNED (the engine attaches the fusion causal document
    and forwards it to ``trace_sink`` when provided — this gate's trace
    coverage); nothing is written to disk in the pipeline path.
    """
    _candidates, _trace = execute_pipeline(
        candidates,
        yolo_detections,
        registry=registry,
        rules=rules,
        context=context,
        input_sources={
            "yolo": [d.to_json(image_width, image_height) for d in yolo_detections],
            "ocr": [t.to_json(image_width, image_height) for t in ocr],
        },
        # Frozen-input GENERATOR runners (the row-relation-head adapter)
        # consume the RAW visual regions — uncombined detector boxes + OCR
        # text blocks plus the source dimensions — never fusion-composed
        # candidates (input freeze G-4).  Built by the single shared
        # constructor (WI-PFW-S2fix): engine and replay can never diverge on
        # bundle keys/shape (unified ``detections``/``ocr`` keys).
        raw_sources=build_raw_sources(
            yolo_detections, ocr, image_width, image_height
        ),
        capture_candidate_views=trace_sink is not None,
    )
    return _trace.to_dict()
