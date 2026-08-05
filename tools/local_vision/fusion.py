from __future__ import annotations

import math
from typing import Any, Iterable

from .schema import Detection, OcrToken


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
    # 2026-08-04: text_block 从 Deki-Yolo Text 标签来，是菜单项的主要锚点。
    # _apply_chevron_heuristic 会自动将同行 icon 旁的 text_block 升级为 menu_item。
    # 纯装饰文本保持 text_block，置信度较低，不影响决策。
    "text_block",
}


def fuse_evidence(
    detections: Iterable[Detection],
    ocr_tokens: Iterable[OcrToken],
    *,
    image_width: int,
    image_height: int,
    interactive_labels: set[str] | None = None,
    promote_unmatched_ocr: bool = False,
    max_ocr_distance_ratio: float = 0.055,
) -> dict[str, Any]:
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
            (token, _match_score(detection, token, max_distance))
            for token in ocr
        ]
        matches = [(token, score) for token, score in matches if score > 0]
        matches.sort(key=lambda pair: (-pair[1], pair[0].box.y1, pair[0].box.x1))
        selected = [token for token, _ in matches]
        for token in selected:
            matched_ocr_ids.add(token.id)

        text = _primary_line_text(selected)
        evidence_ids = [detection.id] + [token.id for token in selected]
        risks = _candidate_risks(detection, selected)

        candidates.append(
            {
                "id": f"candidate_{index}",
                "type": detection.label,
                "text": text,
                "confidence": round(_combined_confidence(detection, selected), 6),
                "bounds": detection.box.normalized(image_width, image_height),
                "boundsPx": [
                    round(detection.box.x1),
                    round(detection.box.y1),
                    round(detection.box.x2),
                    round(detection.box.y2),
                ],
                "center": _normalized_center(detection, image_width, image_height),
                "centerPx": [round(v) for v in detection.box.center()],
                "evidence": {
                    "yoloId": detection.id,
                    "ocrIds": [token.id for token in selected],
                    "allIds": evidence_ids,
                },
                "riskFlags": risks,
            }
        )

    if promote_unmatched_ocr:
        next_index = len(candidates) + 1
        for token in ocr:
            if token.id in matched_ocr_ids:
                continue
            candidates.append(
                {
                    "id": f"candidate_{next_index}",
                    "type": "text_block",
                    "text": token.text,
                    "confidence": round(token.confidence * 0.75, 6),
                    "bounds": token.box.normalized(image_width, image_height),
                    "boundsPx": [
                        round(token.box.x1),
                        round(token.box.y1),
                        round(token.box.x2),
                        round(token.box.y2),
                    ],
                    "center": _normalized_center(token, image_width, image_height),
                    "centerPx": [round(v) for v in token.box.center()],
                    "evidence": {
                        "yoloId": None,
                        "ocrIds": [token.id],
                        "allIds": [token.id],
                    },
                    "riskFlags": ["ocr_only"],
                }
            )
            next_index += 1

    # ── search-box pre-labeling ────────────────────────────────────
    # OCR text containing "search" (case-insensitive) is a search input
    # field, not a navigable menu item.  Force type=input before the
    # chevron heuristic runs so it won't be upgraded to menu_item.
    # Fixes: engine clicking search box → stuck in search UI during
    # enumerate_first_level traversal.
    for c in candidates:
        if c.get("type") == "input":
            continue  # already correctly typed
        # button is also wrong for search — YOLO TextButton → button,
        # but search boxes are input fields, not buttons.
        text = c.get("text", "")
        if text and "search" in text.lower():
            c["type"] = "input"
            c["evidence"]["typeInferred"] = "search_text"

    # ── chevron-alignment heuristic ──────────────────────────────
    # OCR text on the same row as a right-side YOLO icon (chevron ">")
    # is a navigable menu item — reclassify text_block → menu_item.
    _apply_chevron_heuristic(candidates, yolo)

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
    """YOLO 框 + 裁剪 OCR 结果直接融合（V15）。

    每个 crop 的 OCR token 已自动关联对应 YOLO 框 → 直接 zip 关联，无空间匹配
    （_match_score 调用删除）。candidates 数量 == detections 数量。

    `promote_unmatched_ocr` 恒为 False（V27 / R-5）：对齐模型下不存在未匹配
    token，OCR-only 提升既不必要也不允许——参数仅保留签名兼容。
    """
    candidates: list[dict[str, Any]] = []
    all_tokens: list[OcrToken] = []

    for detection, tokens in zip(detections, crops_ocr):
        all_tokens.extend(tokens)
        selected = [t for t in tokens if t.text.strip()]

        text = _primary_line_text(selected)
        risks = _candidate_risks(detection, selected)

        candidates.append(
            {
                "id": f"candidate_{len(candidates) + 1}",
                "type": detection.label,
                "text": text,
                "confidence": round(_combined_confidence(detection, selected), 6),
                "confidenceDetail": {
                    "yolo": round(detection.confidence, 6),
                    "ocr": (
                        round(
                            sum(t.confidence for t in selected) / len(selected), 6
                        )
                        if selected
                        else None
                    ),
                },
                "bounds": detection.box.normalized(image_width, image_height),
                "boundsPx": [
                    round(detection.box.x1),
                    round(detection.box.y1),
                    round(detection.box.x2),
                    round(detection.box.y2),
                ],
                "center": _normalized_center(detection, image_width, image_height),
                "centerPx": [round(v) for v in detection.box.center()],
                "evidence": {
                    "yoloId": detection.id,
                    "ocrIds": [t.id for t in selected],
                    "allIds": [detection.id] + [t.id for t in selected],
                },
                "riskFlags": risks,
            }
        )

    # ── search-box pre-labeling ────────────────────────────────────
    for c in candidates:
        if c.get("type") == "input":
            continue
        text = c.get("text", "")
        if text and "search" in text.lower():
            c["type"] = "input"
            c["evidence"]["typeInferred"] = "search_text"

    # chevron-alignment heuristic 保留（同行 text_block → menu_item 重分类）
    _apply_chevron_heuristic(candidates, list(detections))

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


def _match_score(detection: Detection, token: OcrToken, max_distance: float) -> float:
    if detection.box.contains_center(token.box):
        return 1.0

    overlap = detection.box.intersection_area(token.box)
    if overlap > 0:
        denom = max(1.0, min(detection.box.area(), token.box.area()))
        return min(0.95, 0.55 + (overlap / denom) * 0.4)

    dcx, dcy = detection.box.center()
    tcx, tcy = token.box.center()
    distance = math.hypot(dcx - tcx, dcy - tcy)
    if distance <= max_distance:
        return max(0.15, 0.5 * (1.0 - distance / max_distance))

    return 0.0


def _combined_confidence(detection: Detection, tokens: list[OcrToken]) -> float:
    if not tokens:
        return detection.confidence * 0.85
    ocr_conf = sum(t.confidence for t in tokens) / len(tokens)
    return detection.confidence * 0.72 + ocr_conf * 0.28


def _candidate_risks(detection: Detection, tokens: list[OcrToken]) -> list[str]:
    risks: list[str] = []
    if detection.confidence < 0.55:
        risks.append("low_yolo_confidence")
    if not tokens and detection.label not in {"icon", "back", "toolbar", "popup"}:
        risks.append("no_text_evidence")
    if tokens and min(t.confidence for t in tokens) < 0.6:
        risks.append("low_ocr_confidence")
    return risks


def _normalized_center(item: Detection | OcrToken, width: int, height: int) -> dict[str, float]:
    cx, cy = item.box.center()
    return {"x": round(cx / width, 6), "y": round(cy / height, 6)}


# YOLO labels that signal "this is an interactive list row" — these are
# the widgets that sit alongside menu-item text (chevrons, switches, toggles,
# checkboxes).  NOT included: "input" (search bars are self-contained),
# "button" (standalone buttons don't indicate list rows).
_ROW_WIDGET_LABELS = {"icon", "switch", "toggle", "checkbox"}


def _apply_chevron_heuristic(
    candidates: list[dict[str, Any]],
    yolo_detections: list[Detection],
    *,
    max_y_delta_px: float = 40.0,
) -> None:
    """Y-alignment heuristic (system-agnostic).

    Any OCR text on the same row as *any* YOLO interactive widget
    (icon, switch, toggle, checkbox — left or right side) is a navigable
    menu item.  No assumptions about widget position (chevron-on-the-right
    is stock Android; other skins place icons on the left or omit them).

    This runs after primary YOLO→OCR fusion and OCR promotion, so it
    reclassifies both text_block (promoted OCR without a YOLO box) and
    already-fused candidates (e.g. left-icon + OCR "X 蓝牙" → menu_item)."""
    widgets = [
        d
        for d in yolo_detections
        if d.label in _ROW_WIDGET_LABELS
    ]
    if not widgets:
        return

    for c in candidates:
        if not c["text"].strip():
            continue
        # Preserve already-correct top-level types (search=input, button=button).
        if c["type"] in {"menu_item", "input", "button"}:
            continue
        ccy = c["centerPx"][1]

        best_id: str | None = None
        best_dist = float("inf")
        for w in widgets:
            if c["evidence"]["yoloId"] == w.id:
                continue  # self-match — the widget *is* this candidate
            dist = abs(ccy - w.box.center()[1])
            if dist < best_dist:
                best_dist = dist
                best_id = w.id

        if best_id is not None and best_dist <= max_y_delta_px:
            c["type"] = "menu_item"
            if c["evidence"]["yoloId"] is None:
                c["evidence"]["yoloId"] = best_id
            c["evidence"]["allIds"].append(best_id)
            c["evidence"]["typeInferred"] = "row_alignment"
            risks: list[str] = c.get("riskFlags", [])
            for clearable in ("ocr_only", "low_ocr_confidence"):
                if clearable in risks:
                    risks.remove(clearable)


def _dedupe_preserve_order(values: Iterable[str]) -> list[str]:
    seen: set[str] = set()
    result: list[str] = []
    for value in values:
        if value and value not in seen:
            seen.add(value)
            result.append(value)
    return result


def _primary_line_text(tokens: list[OcrToken]) -> str:
    """同一 YOLO 框内多行 token 时取主行作为条目名（D-198 后续修复）。

    实测噪音（RapidOCR 切换后验证，资产截图 settings-home-api35-full）：
    1. 主标题+副标题拼接 — YOLO 行框覆盖主行+副行（"About emulated device
       Android SDK built for x86_64"）→ 按 y 聚类只取最顶部一行；
    2. 行内重叠 token — RapidOCR 把同一行切成两个重叠检测（"Passwords,
       passkeys" + "s&accounts"）→ 水平重叠时保留较长文本；
    3. 单字符噪声（"。"、"X"）→ 长度 < 2 一律过滤。
    """
    meaningful = [t for t in tokens if len(t.text.strip()) >= 2]
    if not meaningful:
        return ""

    ordered = sorted(meaningful, key=lambda t: (t.box.y1, t.box.x1))
    heights = sorted(t.box.y2 - t.box.y1 for t in ordered)
    median_h = heights[len(heights) // 2]
    row_threshold = max(10.0, median_h * 0.6)

    line_y1 = ordered[0].box.y1
    primary = [t for t in ordered if t.box.y1 - line_y1 <= row_threshold]
    primary.sort(key=lambda t: t.box.x1)

    parts: list[str] = []
    last: OcrToken | None = None
    for token in primary:
        text = token.text.strip()
        if last is not None and _tokens_overlap(last, token):
            if len(text) > len(parts[-1]):
                parts[-1] = text
                last = token
            continue
        parts.append(text)
        last = token
    return " ".join(_dedupe_preserve_order(parts))


def _tokens_overlap(a: OcrToken, b: OcrToken) -> bool:
    """水平 + 垂直均有交集（行内重叠检测：同一文字被切成两个 token）。"""
    return (
        a.box.x2 > b.box.x1 and b.box.x2 > a.box.x1
        and min(a.box.y2, b.box.y2) > max(a.box.y1, b.box.y1)
    )
