"""ScreenParser provider — FastScreen 推理（FSV-001 D1/D2）。

Owns: ScreenParser v2（YOLO11-L，55 类）模型加载缓存（模块级单例）+ 推理
调用 + 推理后处理。推理参数默认 = model card（imgsz=1280 conf=0.10
iou=0.10）；配置唯一真相源归 config.py（env 覆盖，照 cfg.model_path 模式）。

与 yolo/inference.py 的 _get_yolo_model 同构：同 model_path 只加载一次
（server warmup/首请求复用）。输出 = ScreenParseDetection（raw label/class id
+ confidence + Box）——**不**在此处做 55→canonical 映射（D4：第三方模型适
配 UniClaw 由 adapter.py 单点拥有）。

权重 provenance：docling-project/ScreenParser（screenparser_v2，55 类，
apache-2.0；weights 不入 git）。训练分布 = web 截图，对 Android 原生 UI 属
OOD——按 D1 如实测量，推理参数不高于 model card 推荐值。
"""
from __future__ import annotations

from dataclasses import dataclass
from typing import Any

from PIL import Image

from ..config import get_config
from ..schema import Box


@dataclass(frozen=True)
class ScreenParseDetection:
    """ScreenParser 原生检测（未适配）：55 类原名 + class id + confidence +
    bbox（**输入图像像素空间**——WI-6/D11 起 screenparse 摄原图，bbox 即原屏
    像素；server.py 接线经 map_original_to_proc 逆映射回 proc 空间后再适配）。
    适配职责归 adapter.py（D4）。"""
    raw_label: str
    raw_class_id: int
    confidence: float
    box: Box


# ── ScreenParser model cache (module-level singleton) ───────────
_screenparse_model_cache: dict[str, Any] = {}


def _get_screenparse_model(model_path: str) -> Any:
    """模块级模型缓存：同 model_path 只加载一次（镜像 yolo/inference.py
    _get_yolo_model；ultralytics 缺失 → fail-closed）。"""
    try:
        from ultralytics import YOLO
    except ImportError as exc:
        raise RuntimeError(
            "ultralytics is not installed. Install requirements/runtime.txt."
        ) from exc
    if model_path not in _screenparse_model_cache:
        _screenparse_model_cache[model_path] = YOLO(model_path)
    return _screenparse_model_cache[model_path]


def run_screenparse_on_image(
    image: Image.Image,
    *,
    model_path: str | None = None,
    image_size: int | None = None,
    confidence: float | None = None,
    iou: float | None = None,
    device: str = "cpu",
) -> list[ScreenParseDetection]:
    """PIL Image in-memory ScreenParser 推理（零磁盘）。模型模块级缓存。

    ultralytics predict(source=...) 原生接受 PIL Image（内部按 RGB 处理）。
    默认参数从 config.py 读取（screenparse 配置段，env 覆盖：路径/imgsz/
    conf/iou）；显式传参优先。device 由调用方按管道 detect impl 推导
    （cpu 默认；FSV-001 本机 CPU 主表）。
    """
    cfg = get_config()
    model_path = model_path or cfg.screenparse_model_path
    image_size = image_size or cfg.screenparse_image_size
    confidence = cfg.screenparse_confidence if confidence is None else confidence
    iou = cfg.screenparse_iou if iou is None else iou

    results = _get_screenparse_model(model_path).predict(
        source=image, imgsz=image_size, conf=confidence, iou=iou,
        device=device, verbose=False)

    detections: list[ScreenParseDetection] = []
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
            detections.append(ScreenParseDetection(
                raw_label=raw_label,
                raw_class_id=cls,
                confidence=conf,
                box=Box.from_list(xyxy),
            ))
    return detections