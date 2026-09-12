"""ScreenParser 坐标域修正：原图（original）→ proc（preprocess）空间逆映射
（FSV-001 WI-6 / state.md D11）。

背景：preprocess 对原屏做 crop（top/bottom 6.25%）+ 等比 resize（≤720w）得到
proc 空间；YOLO/OCR 在该空间工作。ScreenParser 官方 operating point = 整屏
截图（model card；imgsz=1280），因此 screenparse 阶段改摄**原始 image**，
其输出 bbox 在**原图像素空间**——本模块负责把 bbox 确定性逆映射回 proc 空间，
使下游 rescue 合并 / detect 输出 / fusion / remap / enforce_geometry 语义
与修正前完全一致（后续 remap_coords 照常把 proc 空间映回原屏）。

逆映射公式（以 preprocessing.preprocess 源码为准推导）：

    前向（preprocess，orig → proc）：
      crop:   x_crop = x_orig            （水平无裁剪）
              y_crop = y_orig - top_px
      resize: proc_x = x_crop * (proc_w / orig_w)
              proc_y = y_crop * (proc_h / crop_h)
      （crop_h = orig_h - top_px - bottom_px；proc_w/proc_h = preprocess 输出
        尺寸。两轴用各自实际比例——PIL resize 的 int 取整只影响 proc_h，
        用 proc_h/crop_h 保证像素级精确，避免统一 1/scale 的 ≤1px 偏差。）

    逆映射（本模块，orig → proc）：
      proc_x = x_orig * (proc_w / orig_w)
      proc_y = (y_orig - top_px) * (proc_h / crop_h)

因此逆映射是前向 crop+resize 的精确浮点逆——构造已知点可双向验证
（unit test 锁定）。

越界语义（fail-closed，state.md D11）：映射后 bbox 不完全落在 proc 画布
[0, proc_w] × [0, proc_h]（含边界）内 → **丢弃**（不 clamp）。被裁剪带的
检测（顶部/底部 6.25% 只在原图可见，proc 空间无对应像素）与跨界元素一律
丢弃，数量记录进 screenParse.summary.droppedOffCanvas——绝不带着不可信
坐标进入 rescue/fusion。
"""
from __future__ import annotations

import math
from typing import Sequence

from ..schema import Box
from .provider import ScreenParseDetection


def _clamp_factor(value: float, name: str) -> float:
    if not (math.isfinite(value) and value > 0.0):
        raise ValueError(f"几何参数 {name} 必须为正有限值，got {value!r}")
    return float(value)


def _positive_geo(name: str, value: float) -> float:
    if not (math.isfinite(value) and value > 0.0):
        raise ValueError(f"几何参数 {name} 必须为正有限值，got {value!r}")
    return float(value)


def map_original_to_proc(
    dets: Sequence[ScreenParseDetection],
    *,
    orig_w: int,
    orig_h: int,
    top_px: float,
    bottom_px: float,
    proc_w: int,
    proc_h: int,
) -> tuple[list[ScreenParseDetection], int]:
    """原图 space → proc space 确定性逆映射（state.md D11）。

    Args:
        dets: ScreenParser 在原图上的检测输出（bbox = 原图像素空间）。
        orig_w/orig_h: 原图尺寸（px）。
        top_px/bottom_px: preprocess crop 顶部/底部像素（原图像素）。
        proc_w/proc_h: preprocess 输出尺寸（px，= preprocess 返回值尺寸）。

    Returns:
        (kept, dropped): kept = 逆映射后完全落在 proc 画布内的检测（bbox 已
        为 proc 空间）；dropped = 越界丢弃数量（含非有限坐标，fail-closed）。
        顺序与输入一致（确定性）。

    Raises:
        ValueError: 几何参数非正/非有限，或 crop_h = orig_h - top_px -
        bottom_px ≤ 0（调用方接线错误，fail-closed）。
    """
    _positive_geo("orig_w", orig_w)
    _positive_geo("orig_h", orig_h)
    _positive_geo("proc_w", proc_w)
    _positive_geo("proc_h", proc_h)
    if not (math.isfinite(top_px) and math.isfinite(bottom_px)):
        raise ValueError(f"几何参数 top_px/bottom_px 必须有限，got "
                         f"{top_px!r}/{bottom_px!r}")
    fx = _clamp_factor(proc_w / orig_w, "proc_w/orig_w")
    crop_h = orig_h - top_px - bottom_px
    fy = _clamp_factor(proc_h / crop_h, "proc_h/crop_h")

    kept: list[ScreenParseDetection] = []
    dropped = 0
    for det in dets:
        box = det.box
        x1 = box.x1 * fx
        x2 = box.x2 * fx
        y1 = (box.y1 - top_px) * fy
        y2 = (box.y2 - top_px) * fy
        if not (
            math.isfinite(x1) and math.isfinite(y1)
            and math.isfinite(x2) and math.isfinite(y2)
        ):
            dropped += 1
            continue
        # fail-closed：不完全落在 [0, proc_w] × [0, proc_h]（含边界）→ 丢弃
        if not (0.0 <= x1 < x2 <= proc_w and 0.0 <= y1 < y2 <= proc_h):
            dropped += 1
            continue
        kept.append(ScreenParseDetection(
            raw_label=det.raw_label,
            raw_class_id=det.raw_class_id,
            confidence=det.confidence,
            box=Box(x1, y1, x2, y2),
        ))
    return kept, dropped