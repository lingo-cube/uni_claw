"""FastScreen adapter — ScreenParser 55 类 → canonical perception 标签
（FSV-001 D4）。

D4 原则：Third-party 适配 UniClaw——ScreenParser 原生 bbox/class/confidence
→ FastScreen Provider → Adapter → 现有 canonical perception 标签词汇
（button / list_item / input / switch / checkbox / tab / toolbar / icon /
image / text_block / popup / slider…）。未映射的结构类（Table/Window/List/
Calendar…）保留在 ``screenParse[]`` 作 Optional Evidence，**不**为第三方模型
重设计 Runtime ontology。映射表 = 交付物（本文件常量，Leader review）。

本文件同时拥有 D2 的确定性合并语义（纯函数，可单测）：漏检救援
（select_rescue）与 corroboration 诊断（collect_corroborations）——规则与
阈值在此单点定义，server.py 只做接线。
"""
from __future__ import annotations

from typing import Any, Sequence

from ..fusion.engine import DEFAULT_INTERACTIVE_LABELS
from ..schema import Box, Detection
from ..yolo.labels import YOLO_LABEL_ALIASES
from .provider import ScreenParseDetection


# ── 55 类 → canonical 标签（D4 交付物）─────────────────────────────────
#: 55 类映射表（键 = ScreenParser v2 model card 原名，逐字核对加载后
#: model.names）。目标词汇 = canonical perception 标签（normalize_yolo_label
#: 输出集，见 CANONICAL_VOCABULARY）。映射决策（与任务书建议一致）：
#:   交互类按语义归并（Utility Button→button；Text Input/Search Field/
#:   Search Bar/Select/Picker/Date-Time picker→input；Checkbox/Radiobox→
#:   checkbox；Switch/Toggles→switch；Slider/Steppers→slider；Tab/Tab Bar/
#:   Bottom navigation/Page control→tab；Toolbar/Navigation Bar→toolbar；
#:   List Item/Side Bar→list_item；PopUp Menu/ContextMenu/DockMenu/EditMenu/
#:   Menu/Alert/Notification→popup；Link/Heading/Code snippet/Text→text_block；
#:   App Icon/File Icon/Avatar/Logo/Scroll→icon；Video/Chart→image）。
#:   Scroll（滚动条指示）映射 icon 而非新增 scrollbar 类——canonical 词汇
#:   无独立 scrollbar 产出类，避免为第三方模型扩 ontology（D4）。
SCREENPARSE_LABEL_MAPPING: dict[str, str] = {
    "Button": "button",
    "Utility Button": "button",
    "Text Input": "input",
    "Search Field": "input",
    "Search Bar": "input",
    "Select": "input",
    "Picker": "input",
    "Date-Time picker": "input",
    "Checkbox": "checkbox",
    "Radiobox": "checkbox",
    "Switch": "switch",
    "Toggles": "switch",
    "Slider": "slider",
    "Steppers": "slider",
    "Tab": "tab",
    "Tab Bar": "tab",
    "Bottom navigation": "tab",
    "Page control": "tab",
    "Toolbar": "toolbar",
    "Navigation Bar": "toolbar",
    "List Item": "list_item",
    "Side Bar": "list_item",
    "PopUp Menu": "popup",
    "ContextMenu": "popup",
    "DockMenu": "popup",
    "EditMenu": "popup",
    "Menu": "popup",
    "Alert": "popup",
    "Notification": "popup",
    "Image": "image",
    "App Icon": "icon",
    "File Icon": "icon",
    "Avatar": "icon",
    "Logo": "icon",
    "Video": "image",
    "Chart": "image",
    "Text": "text_block",
    "Heading": "text_block",
    "Link": "text_block",
    "Code snippet": "text_block",
    "Scroll": "icon",
}

#: 结构类（Optional Evidence，只进 screenParse[]，**不**并入 detection 池）：
#: 布局/容器/装饰语义对现有 interactive ontology 无对应买家，保留原生描述
#: 供下游诊断，不改写也不丢弃（D4「未映射类保留在 screenParse[]」）。
STRUCTURAL_LABELS: frozenset[str] = frozenset({
    "Table", "Column/Browser", "Window", "Screen", "List",
    "Breadcrumb", "Pagination", "Calendar", "Carousel", "Badge",
    "Tooltip", "Progress bar", "Rating Indicator", "Status Bar",
})

#: canonical perception 标签词汇 = normalize_yolo_label 的全部可能输出（alias
#: 值 ∪ 键自映射）∪ fusion 交互标签中无 alias 的补充类（slider/toggle/back
#: ——perception 层真实消费的类，单一真相源 = fusion DEFAULT_INTERACTIVE_LABELS）。
#: 测试强制：mapping 值 ⊆ 本词汇（防为第三方模型扩 ontology）。
CANONICAL_VOCABULARY: frozenset[str] = frozenset(
    set(YOLO_LABEL_ALIASES.values()) | DEFAULT_INTERACTIVE_LABELS)


def adapt(
    dets: Sequence[ScreenParseDetection],
) -> tuple[list[Detection], list[ScreenParseDetection]]:
    """55 类 → (mapped Detections, structural ScreenParseDetections)。

    mapped：id=fs_{n}（按输入顺序），label=canonical，confidence 保留，
    raw_label / raw_class_id 保留（证据 provenance，同 yolo/inference.py 的
    raw 保留语义）。structural：原样保留（Optional Evidence）。

    fail-closed：出现 mapping/structural 之外的原生类（模型漂移/版本不
    符）→ 直接抛错——55 类完备性有测试强制，静默丢弃会掩盖漂移。
    """
    mapped: list[Detection] = []
    structural: list[ScreenParseDetection] = []
    for det in dets:
        if det.raw_label in SCREENPARSE_LABEL_MAPPING:
            mapped.append(Detection(
                id=f"fs_{len(mapped) + 1}",
                label=SCREENPARSE_LABEL_MAPPING[det.raw_label],
                confidence=det.confidence,
                box=det.box,
                raw_label=det.raw_label,
                raw_class_id=det.raw_class_id,
            ))
        elif det.raw_label in STRUCTURAL_LABELS:
            structural.append(det)
        else:
            raise ValueError(
                f"ScreenParser class {det.raw_label!r} 不在 55 类 mapping/"
                f"structural 之内——映射表与模型 class vocabulary 漂移，"
                f"fail-closed（见 changes/FSV-001/state.md D4）")
    return mapped, structural


# ── D2 确定性合并语义（纯函数；规则阈值单点拥有，server.py 只接线）──────

def box_iou(a: Box, b: Box) -> float:
    """IoU（面积交并比）。退化（零交集/零面积）→ 0.0，确定性。"""
    inter = a.intersection_area(b)
    union = a.area() + b.area() - inter
    if union <= 0.0:
        return 0.0
    return inter / union


def select_rescue(
    mapped: list[Detection],
    existing: Sequence[Detection],
    *,
    min_rescue_conf: float,
    rescue_iou_max: float,
) -> list[Detection]:
    """D2 漏检救援（确定性）：mapped 中与『现有全部 detections（任意
    label）』IoU **严格 <** rescue_iou_max 且 confidence **>=** min_rescue_conf
    的 → 返回（调用方追加进 detection 池参与 fuse）。

    判定基准 = stage 入口时现有池的一次快照（同一基准判定全部候选，不随
    追加自增长）；判定顺序 = mapped 输入顺序（确定性）。边界语义（测试
    强制）：IoU == 阈值 → 不救援（严格小于）；conf == 阈值 → 救援（>=）。
    """
    existing_pool = list(existing)
    rescued: list[Detection] = []
    for candidate in mapped:
        if candidate.confidence < min_rescue_conf:
            continue
        max_iou = max(
            (box_iou(candidate.box, d.box) for d in existing_pool), default=0.0)
        if max_iou < rescue_iou_max:
            rescued.append(candidate)
    return rescued


def collect_corroborations(
    mapped: list[Detection],
    existing: Sequence[Detection],
    *,
    iou_min: float = 0.60,
    conf_min: float = 0.80,
) -> list[dict[str, Any]]:
    """D2 corroboration 诊断（**不改写标签**）：mapped 中与某现有 detection
    IoU >= iou_min、canonical label 不同、conf >= conf_min → 记录分歧。

    每个 mapped 至多一条记录（按 existing 池顺序首个命中，确定性）；纯
    诊断，不改变任何标签/detection 池。字段 = {fsLabel, yoloLabel, iou,
    fsConf, yoloId}。阈值 0.60/0.80 = FSV-001 决策给定值。
    """
    existing_pool = list(existing)
    records: list[dict[str, Any]] = []
    for candidate in mapped:
        if candidate.confidence < conf_min:
            continue
        for other in existing_pool:
            if other.label == candidate.label:
                continue
            iou = box_iou(candidate.box, other.box)
            if iou >= iou_min:
                records.append({
                    "fsLabel": candidate.label,
                    "yoloLabel": other.label,
                    "iou": round(iou, 6),
                    "fsConf": round(candidate.confidence, 6),
                    "yoloId": other.id,
                })
                break
    return records


def build_screenparse_evidence(
    raw: Sequence[ScreenParseDetection],
    mapped: Sequence[Detection],
    structural: Sequence[ScreenParseDetection],
    corroborations: Sequence[dict[str, Any]],
    rescued: Sequence[Detection],
    width: int,
    height: int,
    *,
    dropped_off_canvas: int = 0,
) -> dict[str, Any]:
    """``screenParse`` additive 证据块（D2；原始适配证据，坐标 = preprocessed
    像素空间，归一化以 proc_w/proc_h 为基准——与 yolo[] 的 post-remap 原始
    空间不同，字段注释写明；C# 侧不消费，纯诊断）。

    WI-6（D11）：screenparse 阶段摄原图跑，经 map_original_to_proc 逆映射回
    proc 空间后才进入本函数——序列化口径（proc 空间归一化）不变，但
    summary.inputSpace="original" 标记输入域（旧 proc 行为可从该字段缺席
    区分）；droppedOffCanvas = 逆映射时因越出 proc 画布被 fail-closed 丢弃的
    检测数（含非有限坐标；state.md D11 越界语义 = 丢弃，不 clamp）。
    """
    return {
        "detections": [
            {
                "id": d.id,
                "label": d.label,
                "rawLabel": d.raw_label,
                "confidence": round(d.confidence, 6),
                "bounds": d.box.normalized(width, height),
                "boundsPx": [
                    round(d.box.x1), round(d.box.y1),
                    round(d.box.x2), round(d.box.y2),
                ],
            }
            for d in mapped
        ],
        "structural": [
            {
                "rawLabel": s.raw_label,
                "confidence": round(s.confidence, 6),
                "bounds": s.box.normalized(width, height),
                "boundsPx": [
                    round(s.box.x1), round(s.box.y1),
                    round(s.box.x2), round(s.box.y2),
                ],
            }
            for s in structural
        ],
        "corroborations": list(corroborations),
        "summary": {
            "inputSpace": "original",
            "rawCount": len(raw),
            "mappedCount": len(mapped),
            "structuralCount": len(structural),
            "rescuedCount": len(rescued),
            "droppedOffCanvas": dropped_off_canvas,
        },
    }