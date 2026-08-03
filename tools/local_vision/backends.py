from __future__ import annotations

import os
import tempfile
import threading
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from typing import Any

import numpy as np

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
    "imageview": "icon",       # deki-yolo: ImageView → icon
    "line": "icon",            # deki-yolo: Line → icon (decorative, not interactive)
    "map": "image",
    "modal": "popup",
    "multi_tab": "tab",
    "pageindicator": "icon",
    "remember": "checkbox",
    "spinner": "input",
    "switch": "switch",
    "text": "text_block",
    "textbutton": "button",
    "view": "list_item",       # deki-yolo: View container → list_item
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

    model = _get_yolo_model(model_path)
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


# ── YOLO 内存推理（零磁盘）──────────────────────────────────
_yolo_model_cache: dict[str, Any] = {}


def _get_yolo_model(model_path: str) -> Any:
    """模块级模型缓存：同一 model_path 只加载一次（server 预热生效）。"""
    try:
        from ultralytics import YOLO
    except ImportError as exc:
        raise RuntimeError(
            "ultralytics is not installed. Install tools/local_vision/requirements.txt."
        ) from exc
    if model_path not in _yolo_model_cache:
        _yolo_model_cache[model_path] = YOLO(model_path)
    return _yolo_model_cache[model_path]


def run_yolo_on_image(
    image: Image.Image,
    *,
    model_path: str,
    image_size: int,
    confidence: float,
    device: str,
) -> list[Detection]:
    """PIL Image 内存推理（零磁盘），模型模块级缓存复用。

    ultralytics `predict(source=...)` 原生接受 PIL Image，内部按 RGB 处理。
    """
    results = _get_yolo_model(model_path).predict(
        source=image, imgsz=image_size, conf=confidence,
        device=device, verbose=False)
    detections: list[Detection] = []
    for result in results:
        names = result.names
        boxes = result.boxes
        if boxes is None:
            continue
        for box in boxes:
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


def _call_paddle_ocr(ocr: Any, source: Path | np.ndarray) -> Any:
    """调用 PaddleOCR 推理（Path | ndarray 双输入，V21）。

    Path → str 归一化（paddleocr 2.8 的 `_check_img` 只接受 str/ndarray，
    不接受 Path 对象）；ndarray 原样直传（零磁盘）。内部调用链不变：
    `ocr.ocr(...)` → `ocr.predict(...)` fallback。
    """
    if isinstance(source, Path):
        source = str(source)
    calls = [
        lambda: ocr.ocr(source, cls=True),
        lambda: ocr.ocr(source),
        lambda: ocr.predict(source),
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


# ── ROI 裁剪 + 多线程 OCR（零磁盘）──────────────────────────
_ROI_PADDING_SPEC: dict[str, float] = {}
_ocr_local = threading.local()
_ocr_executor: ThreadPoolExecutor | None = None


def configure_roi_padding(spec: dict[str, float]) -> None:
    """设置 ROI padding 规格（server.py 启动时从 label-mapping.json 读取）。"""
    global _ROI_PADDING_SPEC
    _ROI_PADDING_SPEC = dict(spec)


def _roi_padding_px(box_width: float, box_height: float) -> int:
    """R-14: ROI padding 按框尺寸比例（x/y），像素下限/上限钳制，替换硬编码 4px。"""
    spec = _ROI_PADDING_SPEC
    px = max(
        spec.get("x", 0.15) * box_width,
        spec.get("y", 0.1) * box_height,
        float(spec.get("minPx", 8)),
    )
    return int(min(px, spec.get("maxPx", 64)))


def _get_ocr(language: str = "ch") -> Any:
    """每个线程懒加载独立的 PaddleOCR 实例（线程安全）。"""
    if not hasattr(_ocr_local, "instance"):
        try:
            from paddleocr import PaddleOCR
        except ImportError as exc:
            raise RuntimeError(
                "paddleocr is not installed. Install tools/local_vision/requirements.txt."
            ) from exc
        _ocr_local.instance = _create_paddle_ocr(PaddleOCR, language)
    return _ocr_local.instance


def _ocr_parallelism() -> int:
    """从环境变量读取 OCR 并行度，默认 2，钳制到 1-8。"""
    env = os.environ.get("UNICLAW_OCR_PARALLEL", "2")
    try:
        n = int(env)
        return max(1, min(n, 8))
    except ValueError:
        return 2


def _get_ocr_executor() -> ThreadPoolExecutor:
    """模块级长生命周期 OCR 线程池（R-13）：请求间复用，不每请求创建。"""
    global _ocr_executor
    if _ocr_executor is None:
        _ocr_executor = ThreadPoolExecutor(max_workers=_ocr_parallelism())
    return _ocr_executor


def warmup_ocr(language: str = "ch") -> None:
    """预热 OCR（R-13）：主线程实例 + executor 各 worker 线程实例。

    PaddleOCR 构造即加载检测/识别模型（首次 2-5s）。提交 dummy 任务让每个
    worker 线程建立 threading.local 实例，首个真实请求不再支付实例创建成本。
    """
    _get_ocr(language)
    executor = _get_ocr_executor()
    list(executor.map(lambda _: _get_ocr(language), range(_ocr_parallelism())))


def run_ocr_on_crops(
    image: Image.Image,
    detections: list[Detection],
    *,
    language: str = "ch",
    padding: int | None = None,
    max_workers: int | None = None,
) -> list[list[OcrToken]]:
    """对每个 YOLO 检测框区域做 OCR。返回与 detections 对齐的 token 列表。

    padding 为 None 时按 label-mapping.json `spatial.roiPadding` 配置逐框计算
    （R-14）；max_workers 为 None 时复用模块级长生命周期 executor（R-13）。
    """
    if not detections:
        return []

    if max_workers is None:
        executor = _get_ocr_executor()
        owns_executor = False
    else:
        executor = ThreadPoolExecutor(max_workers=max_workers)
        owns_executor = True

    try:
        # Step 1: 并行裁剪 (PIL 线程安全, CPU 轻量)
        crops = list(executor.map(
            lambda d: _crop_padded(
                image, d.box,
                _roi_padding_px(d.box.x2 - d.box.x1, d.box.y2 - d.box.y1)
                if padding is None else padding,
            ),
            detections,
        ))

        # Step 2: 并行 OCR (每线程独立 PaddleOCR 实例, C++ 推理时 GIL 释放 → 真并行)
        pairs = [
            (crop, det)
            for crop, det in zip(crops, detections)
            if crop is not None
        ]
        results = list(executor.map(
            lambda pair: _ocr_one_crop(pair[0], pair[1], language),
            pairs,
        ))
    finally:
        if owns_executor:
            executor.shutdown(wait=True)

    # 重建与 detections 对齐的结果列表
    aligned: list[list[OcrToken]] = []
    idx = 0
    for crop in crops:
        if crop is None:
            aligned.append([])
        else:
            aligned.append(results[idx])
            idx += 1
    return aligned


def _ocr_one_crop(
    crop: Image.Image,
    detection: Detection,
    language: str,
) -> list[OcrToken]:
    """对单个裁剪区域运行 OCR，token 坐标回原图。"""
    if crop.width < 4 or crop.height < 4:
        return []
    ocr = _get_ocr(language)
    tokens = _run_ocr_on_pil(ocr, crop)
    return [_offset_token(t, detection.box.x1, detection.box.y1) for t in tokens]


def _crop_padded(
    image: Image.Image,
    box: Box,
    padding: int,
) -> Image.Image | None:
    x1 = max(0, int(box.x1) - padding)
    y1 = max(0, int(box.y1) - padding)
    x2 = min(image.width, int(box.x2) + padding)
    y2 = min(image.height, int(box.y2) + padding)
    if x2 <= x1 or y2 <= y1:
        return None
    return image.crop((x1, y1, x2, y2))


def _run_ocr_on_pil(ocr: Any, crop: Image.Image) -> list[OcrToken]:
    """对 PIL Image 运行 PaddleOCR（V21）。

    ndarray 直传（`np.asarray(crop)[:, :, ::-1]` RGB→BGR，匹配 cv2.imread 语义）
    → 每请求零磁盘；文件路径仅作旧 API / 未知版本 fallback。
    """
    try:
        raw = _call_paddle_ocr(ocr, np.asarray(crop)[:, :, ::-1])
    except (TypeError, ValueError):
        with tempfile.NamedTemporaryFile(suffix=".png", delete=False) as f:
            crop.save(f, format="PNG")
            tmp_path = f.name
        try:
            raw = _call_paddle_ocr(ocr, Path(tmp_path))
        finally:
            os.unlink(tmp_path)
    return _normalize_paddle_result(raw)


def _offset_token(token: OcrToken, dx: float, dy: float) -> OcrToken:
    """crop 坐标系 token → 原图坐标系（回移裁剪偏移）。"""
    return OcrToken(
        id=token.id,
        text=token.text,
        confidence=token.confidence,
        box=Box(token.box.x1 + dx, token.box.y1 + dy,
                token.box.x2 + dx, token.box.y2 + dy),
    )
