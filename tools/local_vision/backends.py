from __future__ import annotations

from pathlib import Path
from typing import Any

from .schema import Box, Detection, OcrToken

YOLO_LABEL_ALIASES = {
    "backgroundimage": "image",
    "bottom_navigation": "tab",
    "card": "list_item",
    "checkbox": "checkbox",
    "checkedtextview": "checkbox",
    "drawer": "toolbar",
    "edittext": "input",
    "icon": "icon",
    "image": "image",
    "map": "image",
    "modal": "popup",
    "multi_tab": "tab",
    "pageindicator": "icon",
    "remember": "checkbox",
    "spinner": "input",
    "switch": "switch",
    "text": "text_block",
    "textbutton": "button",
    "toolbar": "toolbar",
    "uppertaskbar": "toolbar",
}


def run_yolo(
    image_path: Path,
    *,
    model_path: str,
    image_size: int,
    confidence: float,
    device: str,
) -> list[Detection]:
    try:
        from ultralytics import YOLO
    except ImportError as exc:
        raise RuntimeError(
            "ultralytics is not installed. Install tools/local_vision/requirements.txt "
            "or pass --yolo-json with precomputed detections."
        ) from exc

    model = YOLO(model_path)
    results = model.predict(
        source=str(image_path),
        imgsz=image_size,
        conf=confidence,
        device=device,
        verbose=False,
    )
    detections: list[Detection] = []
    for result in results:
        names = result.names
        boxes = result.boxes
        if boxes is None:
            continue
        for index, box in enumerate(boxes):
            xyxy = [float(v) for v in box.xyxy[0].tolist()]
            cls = int(box.cls[0].item())
            conf = float(box.conf[0].item())
            raw_label = str(names.get(cls, cls))
            detections.append(
                Detection(
                    id=f"det_{len(detections) + 1}",
                    label=normalize_yolo_label(raw_label),
                    confidence=conf,
                    box=Box.from_list(xyxy),
                )
            )
    return detections


def normalize_yolo_label(label: str) -> str:
    key = label.strip().replace("-", "_").replace(" ", "_").lower()
    return YOLO_LABEL_ALIASES.get(key, key)


def run_paddle_ocr(image_path: Path, *, language: str) -> list[OcrToken]:
    try:
        from paddleocr import PaddleOCR
    except ImportError as exc:
        raise RuntimeError(
            "paddleocr is not installed. Install tools/local_vision/requirements.txt "
            "or pass --ocr-json with precomputed OCR tokens."
        ) from exc

    ocr = _create_paddle_ocr(PaddleOCR, language)
    raw = _call_paddle_ocr(ocr, image_path)
    return _normalize_paddle_result(raw)


def load_detections_json(path: Path) -> list[Detection]:
    import json

    data = json.loads(path.read_text(encoding="utf-8"))
    records = data.get("detections", data if isinstance(data, list) else [])
    detections: list[Detection] = []
    for index, record in enumerate(records, start=1):
        bounds = record.get("boundsPx") or record.get("box") or record.get("bounds")
        if bounds is None:
            raise ValueError(f"detection record missing bounds: {record!r}")
        detections.append(
            Detection(
                id=str(record.get("id") or f"det_{index}"),
                label=str(record.get("label") or record.get("class") or record.get("type")),
                confidence=float(record.get("confidence", record.get("conf", 1.0))),
                box=Box.from_list(bounds),
            )
        )
    return detections


def load_ocr_json(path: Path) -> list[OcrToken]:
    import json

    data = json.loads(path.read_text(encoding="utf-8"))
    records = data.get("tokens", data.get("ocr", data if isinstance(data, list) else []))
    tokens: list[OcrToken] = []
    for index, record in enumerate(records, start=1):
        bounds = record.get("boundsPx") or record.get("box") or record.get("bounds")
        if bounds is None:
            raise ValueError(f"OCR record missing bounds: {record!r}")
        tokens.append(
            OcrToken(
                id=str(record.get("id") or f"ocr_{index}"),
                text=str(record.get("text", "")),
                confidence=float(record.get("confidence", record.get("conf", 1.0))),
                box=Box.from_list(bounds),
            )
        )
    return tokens


def _normalize_paddle_result(raw: Any) -> list[OcrToken]:
    lines: list[Any] = []
    if isinstance(raw, list):
        for page in raw:
            if isinstance(page, list):
                lines.extend(page)
            elif isinstance(page, dict):
                lines.extend(_lines_from_paddle_dict(page))
            elif hasattr(page, "json"):
                lines.extend(_lines_from_paddle_dict(page.json))
            elif hasattr(page, "to_dict"):
                lines.extend(_lines_from_paddle_dict(page.to_dict()))

    tokens: list[OcrToken] = []
    for line in lines:
        parsed = _parse_paddle_line(line)
        if parsed is None:
            continue
        box, text, confidence = parsed
        tokens.append(
            OcrToken(
                id=f"ocr_{len(tokens) + 1}",
                text=text,
                confidence=confidence,
                box=box,
            )
        )
    return tokens


def _create_paddle_ocr(paddle_ocr_type: Any, language: str) -> Any:
    candidates = [
        {"use_angle_cls": True, "lang": language, "show_log": False},
        {"use_angle_cls": True, "lang": language},
        {"use_textline_orientation": True, "lang": language},
        {"lang": language},
    ]
    last_error: Exception | None = None
    for kwargs in candidates:
        try:
            return paddle_ocr_type(**kwargs)
        except (TypeError, ValueError) as exc:
            last_error = exc
    assert last_error is not None
    raise last_error


def _call_paddle_ocr(ocr: Any, image_path: Path) -> Any:
    calls = [
        lambda: ocr.ocr(str(image_path), cls=True),
        lambda: ocr.ocr(str(image_path)),
        lambda: ocr.predict(str(image_path)),
    ]
    last_error: Exception | None = None
    for call in calls:
        try:
            return call()
        except (TypeError, ValueError) as exc:
            last_error = exc
    assert last_error is not None
    raise last_error


def _lines_from_paddle_dict(page: dict[str, Any]) -> list[Any]:
    texts = page.get("rec_texts") or []
    scores = page.get("rec_scores") or []
    boxes = page.get("rec_boxes") or page.get("rec_polys") or page.get("dt_polys") or []
    return [[box, (text, score if i < len(scores) else 1.0)] for i, (box, text) in enumerate(zip(boxes, texts))]


def _parse_paddle_line(line: Any) -> tuple[Box, str, float] | None:
    if not isinstance(line, (list, tuple)) or len(line) < 2:
        return None
    raw_box = line[0]
    raw_text = line[1]

    if isinstance(raw_text, (list, tuple)) and len(raw_text) >= 2:
        text = str(raw_text[0])
        confidence = float(raw_text[1])
    else:
        text = str(raw_text)
        confidence = 1.0

    return _box_from_paddle(raw_box), text, confidence


def _box_from_paddle(raw_box: Any) -> Box:
    if isinstance(raw_box, (list, tuple)) and len(raw_box) == 4 and all(
        isinstance(v, (int, float)) for v in raw_box
    ):
        x1, y1, x2, y2 = [float(v) for v in raw_box]
        return Box(x1, y1, x2, y2)

    points = raw_box.tolist() if hasattr(raw_box, "tolist") else raw_box
    xs = [float(point[0]) for point in points]
    ys = [float(point[1]) for point in points]
    return Box(min(xs), min(ys), max(xs), max(ys))
