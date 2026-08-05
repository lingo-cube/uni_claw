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
    """从环境变量读取 OCR 并行度，默认 4，钳制到 1-8。

    默认 2→4 (D-XXX): 逐 crop 路径下 2 worker 是 det 736×736 每框 0.94s 的
    主要因素之一; 全图路径下该值仅影响 paddleocr 回退路径, 提高默认无副作用
    (i7-8750H 6c12t, ONNX 各 session 自管 intra-op 线程)。
    """
    env = os.environ.get("UNICLAW_OCR_PARALLEL", "4")
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


# ── RapidOCR（ONNX Runtime, D-198）────────────────────────────
# D-198: OCR 后端切换 paddleocr → rapidocr。理由：paddleocr 2.10 (Python 3.11 环境)
# 每请求内存泄漏（D-4 手动 gc 只是缓兵），长跑服务 OOM 死亡（实测集成 run 中途
# 1ms 连接失败）；RapidOCR 实例线程安全、无泄漏、内存 ~300-500MB、单图 10-25ms，
# 中英文混排原生支持（语言参数 no-op），与现有 executor 池复用同一批 worker。
_rapid_ocr_singleton: Any = None
_rapid_ocr_lock = threading.Lock()


def _get_rapid_ocr() -> Any:
    """RapidOCR 进程级单例（D-198）：实例本身线程安全，无需 thread-local。"""
    global _rapid_ocr_singleton
    if _rapid_ocr_singleton is None:
        with _rapid_ocr_lock:
            if _rapid_ocr_singleton is None:
                try:
                    from rapidocr_onnxruntime import RapidOCR
                except ImportError as exc:
                    raise RuntimeError(
                        "rapidocr_onnxruntime is not installed. Install "
                        "tools/local_vision/requirements.txt.") from exc
                _rapid_ocr_singleton = RapidOCR()
    return _rapid_ocr_singleton


def warmup_rapid_ocr() -> None:
    """预热 RapidOCR（D-198 + D-XXX）：构造加载 ONNX 模型 + 合成图跑一遍
    det/rec 内核。

    构造只加载模型权重（1-3s）；ORT 内核首次执行才初始化（实测首个真实请求
    的 det 额外 +0.7s）。用合成图各跑一次 det 与 rec，把内核初始化移到启动期，
    首个真实请求直接是稳态耗时（"预热完成后才开始测试"）。
    """
    ocr = _get_rapid_ocr()
    try:
        # det 内核: 640×640 黑图 (无文本 → 无框, 仅暖内核)
        black = np.zeros((640, 640, 3), dtype=np.uint8)
        ocr.text_det(black)
        # rec 内核: 白底 + 黑条模拟单行文本
        line = np.full((48, 320, 3), 255, dtype=np.uint8)
        line[8:40, 16:64] = 0
        ocr.text_rec([line])
    except Exception:
        # 预热失败不阻塞启动: 首个真实请求仍会正常执行 (仅多付内核初始化)
        return


def run_rapid_ocr_on_crops(
    image: Image.Image,
    detections: list[Detection],
    *,
    text_score: float = 0.5,
    padding: int | None = None,
    max_workers: int | None = None,
) -> list[list[OcrToken]]:
    """对每个 YOLO 检测框区域做 RapidOCR（D-198）。返回与 detections 对齐的 token 列表。

    与 run_ocr_on_crops 同接口（padding/max_workers 语义一致）；差异仅在
    token 过滤：RapidOCR 返回的置信度低于 text_score 的 token 直接丢弃。
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
        # Step 1: 并行裁剪（与 PaddleOCR 路径共用 _crop_padded/_roi_padding_px）
        crops = list(executor.map(
            lambda d: _crop_padded(
                image, d.box,
                _roi_padding_px(d.box.x2 - d.box.x1, d.box.y2 - d.box.y1)
                if padding is None else padding,
            ),
            detections,
        ))

        # Step 2: 并行 OCR（RapidOCR 推理时 GIL 释放 → 真并行；实例线程安全）
        pairs = [
            (crop, det)
            for crop, det in zip(crops, detections)
            if crop is not None
        ]
        results = list(executor.map(
            lambda pair: _rapid_ocr_one_crop(pair[0], pair[1], text_score),
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


def _run_rapid_ocr_on_pil(crop: Image.Image, text_score: float) -> list[OcrToken]:
    """RapidOCR on a single PIL crop, tokens in crop-local coordinates."""
    if crop.width < 4 or crop.height < 4:
        return []
    rgb = crop.convert("RGB")
    output = _get_rapid_ocr()(np.asarray(rgb)[:, :, ::-1])
    raw = output[0] if isinstance(output, tuple) else output
    return _normalize_rapid_result(raw, text_score)


def _rapid_ocr_one_crop(
    crop: Image.Image,
    detection: Detection,
    text_score: float,
) -> list[OcrToken]:
    """对单个裁剪区域运行 RapidOCR，token 坐标回原图。"""
    if crop.width < 4 or crop.height < 4:
        return []
    tokens = _run_rapid_ocr_on_pil(crop, text_score)
    return [_offset_token(t, detection.box.x1, detection.box.y1) for t in tokens]


def run_rapid_ocr_on_image(
    image: Image.Image,
    *,
    text_score: float = 0.5,
) -> list[OcrToken]:
    """对整张 PIL Image 运行 RapidOCR 全管道（det→cls→rec，D-XXX）。

    相比逐 crop 跑 det（R-13/R-14 时代的每框 det，实测 26 框 × 0.94s ≈ 19s），
    全图一次 det（~1.3s, limit_side_len=736）+ 批量 rec（~1.5s）≈ 2.8s 且质量
    更高：det 行是真实文本行（tight box，无图标污染），16 行全部高置信
    （'About emulated device' 0.99）。token box 为全图坐标，由融合层空间匹配
    关联到 YOLO 候选（fuse_evidence），不再需要 per-crop 对齐。
    """
    rgb = image.convert("RGB")  # RGBA → RGB（text_rec 断言 3 通道）
    output = _get_rapid_ocr()(np.asarray(rgb)[:, :, ::-1])
    raw = output[0] if isinstance(output, tuple) else output
    return _normalize_rapid_result(raw, text_score)


def _normalize_rapid_result(raw: Any, text_score: float) -> list[OcrToken]:
    """RapidOCR result [[box4points], text, score] → OcrToken，低置信丢弃。"""
    tokens: list[OcrToken] = []
    if not isinstance(raw, (list, tuple)):
        return tokens
    for item in raw:
        if not isinstance(item, (list, tuple)) or len(item) < 3:
            continue
        try:
            score = float(item[2])
        except (TypeError, ValueError):
            continue
        text = str(item[1]).strip()
        if score < text_score or not text:
            continue
        tokens.append(OcrToken(
            id=f"ocr_{len(tokens) + 1}",
            text=text,
            confidence=score,
            box=_box_from_paddle(item[0]),
        ))
    return tokens


def run_rapid_ocr(image_path: Path, *, text_score: float = 0.5) -> list[OcrToken]:
    """CLI 单图 RapidOCR（analyze.py --ocr-backend rapidocr）。"""
    from PIL import Image

    return run_rapid_ocr_on_image(Image.open(image_path), text_score=text_score)
