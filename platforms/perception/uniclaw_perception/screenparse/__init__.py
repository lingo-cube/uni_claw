"""FastScreen（ScreenParser）——FSV-001 Test A 集成变体（provider + adapter +
坐标域修正（WI-6/D11））。

Public surface：provider 推理（run_screenparse_on_image / ScreenParseDetection）
+ adapter 适配与确定性合并语义（SCREENPARSE_LABEL_MAPPING / STRUCTURAL_LABELS
/ adapt / select_rescue / collect_corroborations / build_screenparse_evidence）
+ 坐标域逆映射（map_original_to_proc，原图→proc；WI-6）。
pipeline 变体接线在 server.py / pipeline.py（本包不拥有管道语义）。
"""
from .adapter import (
    CANONICAL_VOCABULARY,
    SCREENPARSE_LABEL_MAPPING,
    STRUCTURAL_LABELS,
    adapt,
    box_iou,
    build_screenparse_evidence,
    collect_corroborations,
    select_rescue,
)
from .geom import map_original_to_proc
from .provider import ScreenParseDetection, run_screenparse_on_image

__all__ = [
    "CANONICAL_VOCABULARY",
    "SCREENPARSE_LABEL_MAPPING",
    "STRUCTURAL_LABELS",
    "ScreenParseDetection",
    "adapt",
    "box_iou",
    "build_screenparse_evidence",
    "collect_corroborations",
    "map_original_to_proc",
    "run_screenparse_on_image",
    "select_rescue",
]