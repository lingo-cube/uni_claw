"""Fusion engine: YOLO + OCR → structured perception candidates.

Primary entry points: fuse_evidence (full-image OCR) and
fuse_evidence_from_crops (per-crop OCR, legacy).

Extracted from fusion.py. Uses heuristics and scoring submodules.
"""
from __future__ import annotations

import math
from typing import Any, Iterable

from ..schema import Detection, OcrToken
from .heuristics import (
    apply_chevron_heuristic,
    apply_search_box_labeling,
    apply_toggle_inference_heuristic,
    primary_line_text,
)
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
) -> dict[str, Any]:
    """Fuse YOLO detections + full-image OCR tokens → structured evidence.

    Primary fusion path for RapidOCR full-image mode.
    """
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

    # Apply heuristics
    apply_search_box_labeling(candidates)
    apply_chevron_heuristic(candidates, yolo)
    apply_toggle_inference_heuristic(candidates, image=image)

    return {
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


def fuse_evidence_from_crops(
    detections: list[Detection],
    crops_ocr: list[list[OcrToken]],
    *,
    image_width: int,
    image_height: int,
    promote_unmatched_ocr: bool = False,
) -> dict[str, Any]:
    """Fuse YOLO detections + per-crop OCR results → structured evidence.

    Legacy path for PaddleOCR per-crop mode. Each crop's OCR tokens are
    already associated with the corresponding YOLO detection.
    promote_unmatched_ocr is always False in this path.
    """
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

    return {
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

# rsi-restart

# rsi-mutation

# rsi-mutation
