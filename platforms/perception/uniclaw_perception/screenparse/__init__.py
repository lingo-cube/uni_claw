"""FastScreen（ScreenParser）——FSV-001 Test A 集成变体（provider + adapter）。

Public surface：provider 推理（run_screenparse_on_image / ScreenParseDetection）
+ adapter 适配与确定性合并语义（SCREENPARSE_LABEL_MAPPING / STRUCTURAL_LABELS
/ adapt / select_rescue / collect_corroborations / build_screenparse_evidence）。
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
    "run_screenparse_on_image",
    "select_rescue",
]