#!/usr/bin/env python
"""FSV-001 WI-5a — 四臂 A/B 评测 harness（可重放：起服务→四臂跑帧→scorecards）。

契约：workitems/FSV-001-eval-spec.md（四臂表 + 指标定义 + GT schema）
      changes/FSV-001/state.md（D7 指标 / D8 变量隔离）
      bench/score.py 的 matcher-greedy-v1 语义（class 相等 + IoU≥0.5 贪心一对一）。

四臂：
  baseline      → 无变体头（default 管道，回归锚兼对照）
  integration   → X-Pipeline-Variant: fastscreen-integration（A：YOLO/OCR 原样 + FastScreen step）
  replacement   → X-Pipeline-Variant: fastscreen-replacement（B1：detect=screenparser，OCR 留）
  ocr-off       → B2 消融（进程内复用 bench/run_replacement_ablation.py 语义：OCR 恒空）

架构：
- 纯函数评分层（matcher/text/CER/resolver/grounding/percentile/Server-Timing 解析）——
  模块顶部，**零重依赖**（不 import uniclaw_perception/PIL/torch），可直接单测。
- 运行器层（HttpArm / B2Arm / RssSampler / UdsClient）——惰性 import 重依赖。
- CLI 入口。

确定性：同输入同报告；时间戳单独字段（timestamps）；latency 为测量数据如实记录。

用法：
  python bench/compare_arms.py \
    --valset evaluation/validation/fastscreen-v1 \
    [--gt-suffix json] [--arms baseline,integration,replacement,ocr-off] \
    [--runs 5] [--warmup 1] [--device cpu] [--out-dir evaluation/reports/fsv001] \
    [--frames frameId1,frameId2]

输出（默认 platforms/perception/evaluation/reports/fsv001/）：
  fsv001-<sha12>.json   聚合报告（文件名 = 确定性 payload 内容寻址）
  summary.md            markdown 摘要（arm × 指标 / latency / RSS / 混淆 / miss 分布）
  frames/<frameId>.json per-frame 明细（内容寻址文件名；含首次响应 JSON 供复评）
  logs/                 uvicorn server 日志
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import re
import statistics
import subprocess
import sys
import time
from pathlib import Path
from typing import Any

MATCHER_REVISION = "matcher-greedy-v1"
CENTER_MATCH_REVISION = "matcher-center-v1"
IOU_THRESHOLD = 0.5
HIT_MARGIN_PX = 8
SCORER_REVISION = "compare-arms-v2"
#: 混淆对 top-N（WI-5b：predLabel→gtClass 计数降序取前 N）
CONFUSION_TOP_N = 10

#: click-nth-item 的 interactive 标签集合（任务书 WI-5a 给定清单）。
INTERACTIVE_LABELS = frozenset({
    "button", "list_item", "toggle", "switch", "input",
    "tab", "checkbox", "slider", "toolbar", "popup",
})

#: Server-Timing 分段名（与 server._server_timing 的键一致）。
SEGMENT_NAMES = ("yolo", "ocr", "screenparse", "fusion", "scroll", "serialize", "gc")


# ── Matcher（matcher-greedy-v1；语义 = bench/score.py::match 的本地纯函数副本）──

def _iou(a: tuple, b: tuple) -> float:
    """两个 (x1, y1, x2, y2) 框的 IoU（score.py._iou 同语义）。"""
    x1 = max(a[0], b[0]); y1 = max(a[1], b[1])
    x2 = min(a[2], b[2]); y2 = min(a[3], b[3])
    inter = max(0.0, x2 - x1) * max(0.0, y2 - y1)
    if inter <= 0:
        return 0.0
    area_a = max(0.0, a[2] - a[0]) * max(0.0, a[3] - a[1])
    area_b = max(0.0, b[2] - b[0]) * max(0.0, b[3] - b[1])
    union = area_a + area_b - inter
    return inter / union if union > 0 else 0.0


def match_greedy(predictions: list[dict], gt_elements: list[dict],
                 iou_threshold: float = IOU_THRESHOLD,
                 require_class_equal: bool = True) -> dict[str, Any]:
    """Greedy one-to-one：IoU ≥ threshold，最高 IoU 优先。

    输入形状（与 score.py::match 完全一致）：
      predictions = [{"type": label, "bounds": (x1,y1,x2,y2)}, ...]
      gt_elements = [{"gtClass": cls, "bounds": (x1,y1,x2,y2)}, ...]

    require_class_equal=True：class 相等 + IoU ≥ threshold（matcher-greedy-v1
    原语义）。require_class_equal=False：class-agnostic 贪心 IoU 匹配（WI-5b
    修正 b——type accuracy 主口径的配对基：去掉 class 相等约束，仅几何配对，
    度量"找对了元素但类型判错"）。
    """
    candidates: list[tuple[float, int, int]] = []
    for pi, p in enumerate(predictions):
        pb = p.get("bounds")
        if pb is None:
            continue
        for gi, g in enumerate(gt_elements):
            gb = g.get("bounds")
            if gb is None:
                continue
            if require_class_equal and p.get("type") != g.get("gtClass"):
                continue
            score = _iou(tuple(pb), tuple(gb))
            if score >= iou_threshold:
                candidates.append((score, pi, gi))
    candidates.sort(key=lambda t: (-t[0], t[1], t[2]))
    used_p: set[int] = set()
    used_g: set[int] = set()
    pairs = []
    for score, pi, gi in candidates:
        if pi in used_p or gi in used_g:
            continue
        used_p.add(pi)
        used_g.add(gi)
        pairs.append({"predIndex": pi, "gtIndex": gi, "iou": round(score, 6)})
    unmatched_p = sorted(
        i for i in range(len(predictions))
        if i not in used_p and predictions[i].get("bounds") is not None)
    unmatched_g = sorted(
        i for i in range(len(gt_elements))
        if i not in used_g and gt_elements[i].get("bounds") is not None)
    return {
        "matcherRevision": MATCHER_REVISION,
        "matches": pairs,
        "unmatchedPredictions": unmatched_p,
        "unmatchedGroundTruth": unmatched_g,
        "tp": len(pairs),
        "fp": len(unmatched_p),
        "fn": len(unmatched_g),
    }


def match_center(predictions: list[dict], gt_elements: list[dict]) -> dict[str, Any]:
    """matchMode=center 次级配对（WI-5b 修正 c）：class 相等 ∧ 预测中心 ∈
    GT 框（无 margin）——行容忍口径（动机：YOLO 文字 extent vs GT 整行
    extent 的框语义差异，state.md D6r 几何归因）。

    贪心一对一：同样按 IoU 降序（几何最贴近者优先配对），但配对条件从
    "IoU ≥ 0.5" 放宽为 "预测框中心落在 GT 框内"（中心包含，无需面积重叠
    达标）。输入形状同 match_greedy。
    """
    candidates: list[tuple[float, int, int]] = []
    for pi, p in enumerate(predictions):
        pb = p.get("bounds")
        if pb is None:
            continue
        cx, cy = box_center_px(tuple(pb))
        for gi, g in enumerate(gt_elements):
            gb = g.get("bounds")
            if gb is None:
                continue
            if p.get("type") != g.get("gtClass"):
                continue
            gbx1, gby1, gbx2, gby2 = tuple(gb)
            if not (gbx1 <= cx <= gbx2 and gby1 <= cy <= gby2):
                continue
            score = _iou(tuple(pb), tuple(gb))
            candidates.append((score, pi, gi))
    candidates.sort(key=lambda t: (-t[0], t[1], t[2]))
    used_p: set[int] = set()
    used_g: set[int] = set()
    pairs = []
    for score, pi, gi in candidates:
        if pi in used_p or gi in used_g:
            continue
        used_p.add(pi)
        used_g.add(gi)
        pairs.append({"predIndex": pi, "gtIndex": gi, "iou": round(score, 6),
                      "centerContain": True})
    unmatched_p = sorted(
        i for i in range(len(predictions))
        if i not in used_p and predictions[i].get("bounds") is not None)
    unmatched_g = sorted(
        i for i in range(len(gt_elements))
        if i not in used_g and gt_elements[i].get("bounds") is not None)
    return {
        "matcherRevision": CENTER_MATCH_REVISION,
        "matches": pairs,
        "unmatchedPredictions": unmatched_p,
        "unmatchedGroundTruth": unmatched_g,
        "tp": len(pairs),
        "fp": len(unmatched_p),
        "fn": len(unmatched_g),
    }


def p_r_f1(tp: int, fp: int, fn: int) -> dict[str, float | int | None]:
    """量表 P/R/F1（分母为 0 → None；NOT_SCORABLE 语义，永不为零）。"""
    precision = tp / (tp + fp) if (tp + fp) > 0 else None
    recall = tp / (tp + fn) if (tp + fn) > 0 else None
    f1 = (2 * precision * recall / (precision + recall)
          if precision and recall else None)
    return {
        "tp": tp, "fp": fp, "fn": fn,
        "precision": round(precision, 4) if precision is not None else None,
        "recall": round(recall, 4) if recall is not None else None,
        "f1": round(f1, 4) if f1 is not None else None,
    }


# ── BBox / 距离辅助 ──────────────────────────────────────────────────────────

def box_center_px(bounds_px: list | tuple) -> tuple[float, float]:
    return ((bounds_px[0] + bounds_px[2]) / 2.0,
            (bounds_px[1] + bounds_px[3]) / 2.0)


def box_area_px(bounds_px: list | tuple) -> float:
    return max(0.0, bounds_px[2] - bounds_px[0]) * max(0.0, bounds_px[3] - bounds_px[1])


def center_distance_px(a_center: tuple[float, float],
                       b_center: tuple[float, float]) -> float:
    return math.hypot(a_center[0] - b_center[0], a_center[1] - b_center[1])


def gt_bounds_to_px(gt_element: dict, width: int, height: int) -> tuple[float, float, float, float]:
    """GT normalized bounds → 原屏 px（contract：scorer 用 boundsPx 对 bounds×(W,H)）。"""
    b = gt_element["bounds"]
    return (b["x1"] * width, b["y1"] * height,
            b["x2"] * width, b["y2"] * height)


def expand_bounds_px(bounds_px: tuple, margin: float) -> tuple[float, float, float, float]:
    x1, y1, x2, y2 = bounds_px
    return (x1 - margin, y1 - margin, x2 + margin, y2 + margin)


def hit_test(center_px: tuple[float, float], gt_bounds_px: tuple,
             margin: float = HIT_MARGIN_PX) -> bool:
    """center（原屏 px）落在 expectGtId bounds（±margin px）内 = hit。"""
    x1, y1, x2, y2 = expand_bounds_px(tuple(gt_bounds_px), margin)
    return x1 <= center_px[0] <= x2 and y1 <= center_px[1] <= y2


# ── Levenshtein / CER ────────────────────────────────────────────────────────

def levenshtein(a: str, b: str) -> int:
    """编辑距离（DP，纯函数）。空串对任意串 = len(另一串)。"""
    if a == b:
        return 0
    la, lb = len(a), len(b)
    if la == 0:
        return lb
    if lb == 0:
        return la
    prev = list(range(lb + 1))
    for i, ca in enumerate(a, 1):
        cur = [i] + [0] * lb
        for j, cb in enumerate(b, 1):
            cost = 0 if ca == cb else 1
            cur[j] = min(prev[j] + 1, cur[j - 1] + 1, prev[j - 1] + cost)
        prev = cur
    return prev[lb]


def text_similarity(pred_text: str, target_text: str) -> float:
    """编辑距离相似度 = 1 − lev / max(len)（单字符编辑占满 → 0）。"""
    denom = max(len(pred_text), len(target_text))
    if denom == 0:
        return 1.0
    return 1.0 - levenshtein(pred_text, target_text) / denom


# ── 文本指标（OCR vs expectedTexts ∪ elements[].text）────────────────────────

def text_targets(gt: dict) -> list[str]:
    """GT 文本目标：expectedTexts ∪ elements[].text；strip 去重、跳过空串、保序。"""
    seen: set[str] = set()
    out: list[str] = []
    for src in (gt.get("expectedTexts") or [],):
        for t in src:
            s = str(t).strip()
            if s and s not in seen:
                seen.add(s)
                out.append(s)
    for e in gt.get("elements") or []:
        t = e.get("text")
        if t is None:
            continue
        s = str(t).strip()
        if s and s not in seen:
            seen.add(s)
            out.append(s)
    return out


def score_text(ocr_tokens: list[dict], gt: dict) -> dict[str, Any]:
    """OCR 文本指标：贪心一对一的 exact 命中率 + CER（best-effort 配对）。

    B2 空 ocr → exact 如实 0、CER 每个 target 未定义（None）并显式标记 ocr-empty。
    GT 无文本目标 → NOT_SCORABLE（永不为零）。
    """
    targets = text_targets(gt)
    if not targets:
        return {
            "stance": "NOT_SCORABLE",
            "note": "GT 无 expectedTexts 且 elements 无 text",
            "targetCount": 0,
        }
    ocr_texts = [str(t.get("text", "")).strip() for t in ocr_tokens]
    used: set[int] = set()
    per_target: list[dict] = []
    exact_hits = 0
    cer_values: list[float] = []
    for target in targets:
        best_idx: int | None = None
        best_sim = -1.0
        for ti, text in enumerate(ocr_texts):
            if ti in used:
                continue
            sim = text_similarity(text, target)
            if sim > best_sim:
                best_sim = sim
                best_idx = ti
        if best_idx is None:
            per_target.append({
                "target": target, "exact": False, "cer": None,
                "bestOcr": None, "reason": "no-ocr-token",
            })
            continue
        used.add(best_idx)
        exact = (ocr_texts[best_idx] == target)
        cer = 1.0 - best_sim
        if exact:
            exact_hits += 1
        cer_values.append(cer)
        per_target.append({
            "target": target, "exact": exact, "cer": round(cer, 4),
            "bestOcr": ocr_texts[best_idx],
            "similarity": round(best_sim, 4),
        })
    ocr_empty = not ocr_tokens
    return {
        "stance": "SCORED",
        "targetCount": len(targets),
        "ocrTokenCount": len(ocr_tokens),
        "exactHits": exact_hits,
        "exactHitRate": round(exact_hits / len(targets), 4),
        "cerMean": (round(statistics.fmean(cer_values), 4)
                    if cer_values else None),
        "cerValues": [round(v, 4) for v in cer_values],
        "cerDefinedCount": len(cer_values),
        "ocrEmpty": ocr_empty,
        "note": ("B2 消融：OCR 空 → exact=0、CER 未定义" if ocr_empty else ""),
        "perTarget": per_target,
    }


# ── Element / Candidate 指标 ─────────────────────────────────────────────────

def _yolo_predictions(response: dict) -> list[dict]:
    return [
        {"type": d["label"], "bounds": tuple(d["boundsPx"])}
        for d in response.get("yolo", [])
        if d.get("boundsPx") is not None
    ]


def _candidate_predictions(response: dict) -> list[dict]:
    return [
        {"type": c["type"], "bounds": tuple(c["boundsPx"]), "_cand": c}
        for c in response.get("candidates", [])
        if c.get("boundsPx") is not None
    ]


def _gt_element_list(gt: dict, width: int, height: int) -> list[dict]:
    """GT elements → matcher 输入；跳过缺 valid normalized bounds 的元素
    （schema 要求 bounds 四键；容忍缺键/坏键，数据驱动不崩）。"""
    out = []
    for e in gt.get("elements") or []:
        b = e.get("bounds")
        if not isinstance(b, dict) or not all(k in b for k in ("x1", "y1", "x2", "y2")):
            continue
        out.append({"gtClass": e["gtClass"], "bounds": gt_bounds_to_px(e, width, height),
                    "_gt": e})
    return out


def score_elements(response: dict, gt: dict, width: int, height: int) -> dict[str, Any]:
    """Element P/R/F1 + bbox 质量（IoU 分布 + center 偏差 px）+ type accuracy + 混淆。

    WI-5b 修正：
    b) type accuracy 主口径 = class-agnostic 贪心 IoU≥0.5 配对（去掉 class 相等
       约束）后 matched pairs 的 label==gtClass 比例；混淆对计数 top-N
       （predLabel→gtClass）。主口径 P/R/F1（class 相等配对）保持不变；
    c) center 次级配对（matchMode=center：class 相等 ∧ 预测中心 ∈ GT 框，
       无 margin）→ secondaryElementRecall/Precision/F1（行容忍口径，
       YOLO 文字 extent vs GT 整行 extent 的框语义差异，D6r）。
    """
    if not gt.get("elements"):
        return {"stance": "NOT_SCORABLE", "note": "GT 无 elements（counts/texts 级）"}
    preds = _yolo_predictions(response)
    gts = _gt_element_list(gt, width, height)
    m = match_greedy(preds, gts)
    prf = p_r_f1(m["tp"], m["fp"], m["fn"])

    iou_vals: list[float] = []
    center_vals: list[float] = []
    for pair in m["matches"]:
        pred = preds[pair["predIndex"]]
        gt_entry = gts[pair["gtIndex"]]
        iou_vals.append(pair["iou"])
        c_pred = box_center_px(pred["bounds"])
        c_gt = box_center_px(gt_entry["bounds"])
        center_vals.append(center_distance_px(c_pred, c_gt))

    # b) type accuracy 主口径：class-agnostic 配对 → label==gtClass 比例 + 混淆
    m_type = match_greedy(preds, gts, require_class_equal=False)
    type_correct = sum(
        1 for pm in m_type["matches"]
        if preds[pm["predIndex"]]["type"] == gts[pm["gtIndex"]]["gtClass"])
    confusion: dict[str, dict[str, int]] = {}
    for pm in m_type["matches"]:
        pl = preds[pm["predIndex"]]["type"]
        gc = gts[pm["gtIndex"]]["gtClass"]
        confusion.setdefault(pl, {}).setdefault(gc, 0)
        confusion[pl][gc] += 1
    confusion_top = sorted(
        ((pl, gc, n) for pl, gs in confusion.items() for gc, n in gs.items()),
        key=lambda t: (-t[2], t[0], t[1]))[:CONFUSION_TOP_N]

    # c) center 次级配对（行容忍口径）
    m_center = match_center(preds, gts)
    sec_prf = p_r_f1(m_center["tp"], m_center["fp"], m_center["fn"])

    return {
        "stance": "SCORED",
        "matcherRevision": MATCHER_REVISION,
        "iouThreshold": IOU_THRESHOLD,
        **prf,
        "bboxIou": {
            "mean": round(statistics.fmean(iou_vals), 4) if iou_vals else None,
            "median": round(statistics.median(iou_vals), 4) if iou_vals else None,
            "pairs": len(iou_vals),
        },
        "bboxIouPairs": [round(v, 4) for v in iou_vals],
        "centerDeltaPx": {
            "mean": round(statistics.fmean(center_vals), 2) if center_vals else None,
            "median": round(statistics.median(center_vals), 2) if center_vals else None,
            "pairs": len(center_vals),
        },
        "centerDeltaPairsPx": [round(v, 2) for v in center_vals],
        # type accuracy 主口径（class-agnostic 配对；不再是恒 1.0 的构造值）
        "typeAccuracy": (
            round(type_correct / len(m_type["matches"]), 4)
            if m_type["matches"] else None),
        "typeMatchedPairs": len(m_type["matches"]),
        "typeCorrectPairs": type_correct,
        "typeAccuracyMode": "class-agnostic-iou0.5",
        "confusion": confusion,
        "confusionTopN": [
            {"predLabel": pl, "gtClass": gc, "count": n}
            for pl, gc, n in confusion_top
        ],
        # center 次级口径（行容忍）
        "secondary": {
            "matcherRevision": CENTER_MATCH_REVISION,
            "mode": "center",
            **sec_prf,
            "matches": m_center["matches"],
        },
        "matches": m["matches"],
        "unmatchedPredictions": m["unmatchedPredictions"],
        "unmatchedGroundTruth": m["unmatchedGroundTruth"],
    }


def score_candidates(response: dict, gt: dict, width: int, height: int) -> dict[str, Any]:
    """Candidate 级（A 专属增益面）：candidates[] 对 GT interactive 子集匹配 +
    text association 正确率。"""
    interactive_gt = [e for e in gt.get("elements") or [] if e.get("interactive")]
    if not interactive_gt:
        return {"stance": "NOT_SCORABLE", "note": "GT 无 interactive elements"}
    preds = _candidate_predictions(response)
    gts = [
        {"gtClass": e["gtClass"], "bounds": gt_bounds_to_px(e, width, height), "_gt": e}
        for e in interactive_gt
    ]
    m = match_greedy(preds, gts)
    prf = p_r_f1(m["tp"], m["fp"], m["fn"])
    assoc_true = 0
    assoc_denom = 0
    for pair in m["matches"]:
        cand = preds[pair["predIndex"]]["_cand"]
        gt_elem = gts[pair["gtIndex"]]["_gt"]
        gt_text = gt_elem.get("text")
        if gt_text is None or not str(gt_text).strip():
            continue
        assoc_denom += 1
        if str(cand.get("text", "")).strip() == str(gt_text).strip():
            assoc_true += 1
    return {
        "stance": "SCORED",
        **prf,
        "interactiveGtCount": len(interactive_gt),
        "textAssociation": {
            "correct": assoc_true,
            "denominator": assoc_denom,
            "rate": (round(assoc_true / assoc_denom, 4) if assoc_denom else None),
        },
    }


# ── Grounding resolver（镜像 runtime 边界 join 语义）──────────────────────────
# Runtime 消费 = yolo[]（class+bounds）+ ocr[]（text+bounds）；candidates 无 C#
# production buyer（state.md 关键事实）→ resolver 只用这两数组，与
# LiveFrameOccurrenceStrategy 同构 join。

def _ocr_token_for_query(ocr_tokens: list[dict], query: str) -> dict | None:
    """ocr[] 中 text.strip()==query（精确；无命中 → None）。"""
    for t in ocr_tokens:
        if str(t.get("text", "")).strip() == query:
            return t
    return None


def _token_center_px(token: dict) -> tuple[float, float] | None:
    if token.get("boundsPx"):
        return box_center_px(token["boundsPx"])
    if token.get("centerPx"):
        cx, cy = token["centerPx"]
        return float(cx), float(cy)
    return None


def _smallest_containing_yolo(yolo: list[dict], center_px: tuple[float, float]) -> dict | None:
    """中心位于框内的 yolo[]（多个取面积最小者；无包含关系 → None）。"""
    candidates = []
    for d in yolo:
        bp = d.get("boundsPx")
        if bp is None:
            continue
        b = tuple(bp)
        if (b[0] <= center_px[0] <= b[2] and b[1] <= center_px[1] <= b[3]):
            candidates.append((box_area_px(b), d))
    if not candidates:
        return None
    candidates.sort(key=lambda pair: (pair[0], pair[1].get("id", "")))
    return candidates[0][1]


def _resolve_click_text(yolo: list[dict], ocr_tokens: list[dict], query: str):
    """click-text / click-state 共用前端：精确 OCR 文本 → 最小包含 yolo 框。

    返回 (box, reason)；reason 非 None = 解析失败分支
    （no-text-match：OCR 未命中 query；contain-fail：token 中心无包含框）。
    """
    token = _ocr_token_for_query(ocr_tokens, query)
    if token is None:
        return None, "no-text-match"
    token_center = _token_center_px(token)
    if token_center is None:
        return None, "no-text-match"
    box = _smallest_containing_yolo(yolo, token_center)
    if box is None:
        return None, "contain-fail"
    return box, None


def _resolve_click_nth(yolo: list[dict], nth: int):
    """click-nth-item：interactive 标签按 (y1, x1) 排序取第 nth 个（1-based）。

    返回 (box, reason)；超出范围 → (None, "no-box")。
    """
    if nth < 1:
        return None, "no-box"
    interactive = [d for d in yolo
                   if d.get("label") in INTERACTIVE_LABELS and d.get("boundsPx")]
    interactive.sort(key=lambda d: (d["boundsPx"][1], d["boundsPx"][0], d.get("id", "")))
    if nth > len(interactive):
        return None, "no-box"
    return interactive[nth - 1], None


def resolve_grounding_task(response: dict, task: dict, gt_elements_by_id: dict,
                           width: int, height: int) -> dict[str, Any]:
    """单条 grounding task → 判定结果（hit/miss + miss 原因分类）。

    已实现 kind：click-text / click-nth-item / click-state。
      * click-state：resolver 只解析目标框并做中心命中判定；state 感知超出
        当前 runtime 边界能力（GT expectGtId 的 state 由 GT 侧保证），如实注明。
    其他 kind（如 eval-spec schema 示例的 click-selected）：unsupported-kind
    （如实计数，不进 hit-rate 分母）。
    """
    kind = task.get("kind")
    expect_gt_id = task.get("expectGtId")
    gt_elem = gt_elements_by_id.get(expect_gt_id) if expect_gt_id else None
    base = {
        "taskId": task.get("taskId"),
        "kind": kind,
        "query": task.get("query"),
        "expectGtId": expect_gt_id,
    }
    if gt_elem is None:
        return {
            **base, "status": "NOT_SCORABLE",
            "reason": "gt-element-missing",
            "note": "expectGtId 在 GT elements 中不可解析（GT 缺陷，不计入 hit-rate）",
        }

    yolo = response.get("yolo", [])
    ocr = response.get("ocr", [])
    if kind == "click-text":
        query = task.get("query")
        box, reason = _resolve_click_text(yolo, ocr, str(query))
    elif kind == "click-nth-item":
        nth = int((task.get("query") or {}).get("nth", 0))
        box, reason = _resolve_click_nth(yolo, nth)
    elif kind == "click-state":
        query = task.get("query")
        box, reason = _resolve_click_text(yolo, ocr, str(query))
        if box is not None:
            base["note"] = ("state 感知超出当前 runtime 边界能力：resolver 不判 "
                            "state（GT expectGtId 的 state 由 GT 侧保证）")
    else:
        return {
            **base, "status": "UNSUPPORTED",
            "reason": "unsupported-kind",
            "note": f"kind {kind!r} 不在 resolver 实现集（click-text/click-nth-item/"
                    "click-state）——如实记录，不进 hit-rate",
        }

    if box is None:
        return {**base, "status": "MISS", "hit": False, "reason": reason}
    center = box_center_px(tuple(box["boundsPx"]))
    gt_px = gt_bounds_to_px(gt_elem, width, height)
    hit = hit_test(center, gt_px, HIT_MARGIN_PX)
    return {
        **base, "status": "HIT" if hit else "MISS", "hit": hit,
        "reason": None if hit else "off-target",
        "resolved": {
            "yoloId": box.get("id"),
            "label": box.get("label"),
            "boundsPx": box.get("boundsPx"),
            "centerPx": [round(center[0]), round(center[1])],
            "gtBoundsPx": [round(v) for v in gt_px],
        },
        "marginPx": HIT_MARGIN_PX,
    }


def score_grounding(response: dict, gt: dict, width: int, height: int) -> dict[str, Any]:
    tasks = gt.get("groundingTasks") or []
    if not tasks:
        return {"stance": "NOT_SCORABLE", "note": "GT 无 groundingTasks"}
    gt_by_id = {e.get("gtId"): e for e in gt.get("elements") or []}
    results = [
        resolve_grounding_task(response, t, gt_by_id, width, height)
        for t in tasks
    ]
    scored = [r for r in results if r["status"] in ("HIT", "MISS")]
    hits = sum(1 for r in results if r.get("hit"))
    misses = sum(1 for r in results if r["status"] == "MISS")
    not_scorable = sum(1 for r in results if r["status"] == "NOT_SCORABLE")
    unsupported = sum(1 for r in results if r["status"] == "UNSUPPORTED")
    by_reason: dict[str, int] = {}
    for r in results:
        if r["status"] == "MISS":
            reason = r.get("reason") or "unknown"
            by_reason[reason] = by_reason.get(reason, 0) + 1
    return {
        "stance": "SCORED",
        "taskCount": len(tasks),
        "scoredCount": len(scored),
        "hitCount": hits,
        "missCount": misses,
        "notScorableCount": not_scorable,
        "unsupportedCount": unsupported,
        "hitRate": (round(hits / len(scored), 4) if scored else None),
        "missReasons": by_reason,
        "tasks": results,
    }


# ── 质量评分入口（per-frame）─────────────────────────────────────────────────

def score_frame(response: dict, gt: dict, width: int, height: int) -> dict[str, Any]:
    """单帧质量评分：elements/text/candidates/grounding 四族；每族自带
    NOT_SCORABLE 语义（缺 GT 面 → null + 计数，永不为零）。"""
    elements = score_elements(response, gt, width, height)
    text = score_text(response.get("ocr", []), gt)
    candidates = score_candidates(response, gt, width, height)
    grounding = score_grounding(response, gt, width, height)
    not_scorable = {
        "elements": elements["stance"] == "NOT_SCORABLE",
        "text": text["stance"] == "NOT_SCORABLE",
        "candidates": candidates["stance"] == "NOT_SCORABLE",
        "grounding": grounding["stance"] == "NOT_SCORABLE",
    }
    return {
        "elements": elements,
        "text": text,
        "candidates": candidates,
        "grounding": grounding,
        "notScorable": not_scorable,
        "resolution": {"width": width, "height": height},
    }


# ── 统计辅助 ─────────────────────────────────────────────────────────────────

def _percentiles(values: list[float]) -> dict[str, float | int | None]:
    """分位（run_l2 语义）：n<10 → p50/p95 报 None；median/mean 恒有。"""
    n = len(values)
    out: dict[str, float | int | None] = {
        "n": n,
        "medianMs": round(statistics.median(values), 2),
        "meanMs": round(statistics.fmean(values), 2),
        "p50Ms": None, "p95Ms": None,
        "gates": {"p50p95": n >= 10},
    }
    if n >= 10:
        out["p50Ms"] = round(statistics.quantiles(values, n=20)[9], 2)
        out["p95Ms"] = round(statistics.quantiles(values, n=20)[18], 2)
    return out


def latency_stats(samples_ms: list[float], gated: bool = True) -> dict[str, Any]:
    """latency 分位。gated=True（pooled）→ run_l2 守门；gated=False（per-frame
    runs 少）→ nearest-rank p50/p95（小样本如实标注 gates）。"""
    if not samples_ms:
        return {"n": 0, "medianMs": None, "meanMs": None, "p50Ms": None,
                "p95Ms": None, "gates": {"p50p95": False}}
    if gated and len(samples_ms) >= 10:
        return _percentiles(samples_ms)
    n = len(samples_ms)
    s = sorted(samples_ms)
    def nr(p: float) -> float:
        idx = max(1, math.ceil(p * n)) - 1
        return s[idx]
    return {
        "n": n,
        "medianMs": round(statistics.median(s), 2),
        "meanMs": round(statistics.fmean(s), 2),
        "p50Ms": round(nr(0.50), 2),
        "p95Ms": round(nr(0.95), 2),
        "gates": {"p50p95": n >= 10},
        "method": "nearest-rank",
    }


def parse_server_timing(header: str | None) -> dict[str, float | None]:
    """解析 Server-Timing 头 → {segment: dur_ms}（缺分段 → None）。"""
    out: dict[str, float | None] = {s: None for s in SEGMENT_NAMES}
    if not header:
        return out
    for name, dur in _SEGMENT_RE.findall(header):
        if name in out:
            out[name] = round(float(dur), 3)
    return out


_SEGMENT_RE = re.compile(r"([A-Za-z_]+)\s*;\s*dur=([0-9.]+)")

#: 帧 id 形如 12 位 hex（gt/<sha12>.json）；非帧工件（calibration-decisions.json
#: 等）不入帧清单（D5/D6 契约：gt 目录可含非帧文件）。
_FRAME_ID_RE = re.compile(r"^[0-9a-f]{12}$")


def is_rejected_gt(gt: dict | None) -> bool:
    """reviewStatus 以 'rejected' 开头 = D6r 排除帧（质量指标跳过，
    latency/RSS 保留）。见 changes/FSV-001/state.md D6r + CALIBRATION-REPORT.md。"""
    if not isinstance(gt, dict):
        return False
    rs = gt.get("reviewStatus")
    return isinstance(rs, str) and rs.startswith("rejected")


def _arm_sock_path(arm_name: str, out_dir: Path) -> Path:
    """UDS socket 短路径（macOS AF_UNIX sun_path 上限 ~104B）：放 OS 临时
    目录。out_dir（如 evaluation/reports/fsv001）路径过长会直接导致
    uvicorn --uds 启动即崩（OSError: AF_UNIX path too long）——socket 是
    瞬态资源，报告/log 仍在 out_dir。"""
    import tempfile
    tag = _sha256_hex(str(out_dir.resolve()))[:8]
    return Path(tempfile.gettempdir()) / f"fsv001-{tag}-{arm_name}.sock"


# ── 运行器层（惰性 import 重依赖；纯逻辑层之上）───────────────────────────────
# 以下函数/类只在被调用时 import 重依赖（测试不触达）。

def _perception_root() -> Path:
    return Path(__file__).resolve().parent.parent


def _venv_python() -> Path:
    # repo 根 .perception/venv/bin/python（相对 platforms/perception = ../../.perception/...）
    return _perception_root().parent.parent / ".perception" / "venv" / "bin" / "python"


#: 臂定义：name → (label, variant header, in-process 与否)
ARM_SPECS: dict[str, dict[str, Any]] = {
    "baseline": {"label": "baseline", "variantHeader": None, "inProcess": False},
    "integration": {"label": "A", "variantHeader": "fastscreen-integration", "inProcess": False},
    "replacement": {"label": "B1", "variantHeader": "fastscreen-replacement", "inProcess": False},
    "ocr-off": {"label": "B2", "variantHeader": None, "inProcess": True},
}
ARM_ORDER = ("baseline", "integration", "replacement", "ocr-off")


# ── UDS HTTP 客户端（stdlib；http.client 子类化走 AF_UNIX）───────────────────

class UdsClient:
    """UDS 上的 HTTP 客户端：连接中断时自动重建（请求级鲁棒），请求级新连接。"""

    def __init__(self, sock_path: str):
        self._sock_path = sock_path

    def request(self, method: str, path: str, body: bytes | None = None,
                headers: dict[str, str] | None = None) -> tuple[int, dict[str, str], bytes]:
        import http.client
        import socket

        headers = dict(headers or {})
        headers.setdefault("Host", "uniclaw-perception.local")
        headers.setdefault("Accept", "application/json")

        attempts = 0
        while True:
            conn = http.client.HTTPConnection("uniclaw-perception.local")
            conn._sock_path = self._sock_path  # type: ignore[attr-defined]

            def _connect(conn=conn):  # closure 绑定本次连接
                conn.sock = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
                conn.sock.connect(self._sock_path)

            conn.connect = _connect  # type: ignore[method-assign]
            try:
                conn.request(method, path, body=body or b"", headers=headers)
                resp = conn.getresponse()
                data = resp.read()
                return resp.status, dict(resp.getheaders()), data
            except (ConnectionError, OSError):
                attempts += 1
                if attempts >= 2:
                    raise
            finally:
                conn.close()

    def close(self):
        pass


# ── RSS 采样（psutil 优先；运行期采样线程）───────────────────────────────────

class RssSampler:
    """推理期 RSS 采样：psutil 线程每 ~50ms 采样一次；记录 max + 样本数。"""

    def __init__(self, pid: int, interval_s: float = 0.05):
        self._pid = pid
        self._interval = interval_s
        self._samples_mb: list[float] = []
        self._stop: Any = None
        self._thread: Any = None
        self._error: str | None = None

    def start(self):
        import threading
        self._stop = threading.Event()
        self._samples_mb = []
        self._error = None
        self._thread = threading.Thread(target=self._run, daemon=True)
        self._thread.start()

    def _run(self):
        import psutil
        try:
            proc = psutil.Process(self._pid)
            while not self._stop.is_set():
                try:
                    rss = proc.memory_info().rss
                    self._samples_mb.append(rss / (1024 * 1024))
                except (psutil.NoSuchProcess, psutil.AccessDenied) as exc:
                    self._error = f"rss 采样失败: {exc}"
                    break
                self._stop.wait(self._interval)
        except Exception as exc:  # psutil 缺失等
            self._error = f"rss 采样线程异常: {exc}"

    def stop(self) -> dict[str, Any]:
        if self._thread is not None and self._stop is not None:
            self._stop.set()
            self._thread.join(timeout=2.0)
        return {
            "maxRssMb": (round(max(self._samples_mb), 1)
                         if self._samples_mb else None),
            "samples": len(self._samples_mb),
            "error": self._error,
        }


# ── HTTP 臂（baseline / integration / replacement）───────────────────────────

class HttpArm:
    """起一次 uvicorn 服务（UDS）→ 对每帧 POST /v1/analyze（PNG body）。"""

    def __init__(self, arm_name: str, sock_path: Path, log_path: Path,
                 timeout_s: float = 180.0):
        self.name = arm_name
        self.variant_header = ARM_SPECS[arm_name]["variantHeader"]
        self._sock_path = sock_path
        self._log_path = log_path
        self._timeout_s = timeout_s
        self._proc: subprocess.Popen | None = None
        self._client: UdsClient | None = None

    def start(self):
        python = _venv_python()
        if not python.exists():
            raise RuntimeError(f"venv python 不存在: {python}")
        self._sock_path.parent.mkdir(parents=True, exist_ok=True)
        try:
            self._sock_path.unlink()
        except FileNotFoundError:
            pass
        log_fh = open(self._log_path, "wb")
        cmd = [str(python), "-m", "uvicorn",
               "uniclaw_perception.server:app", "--uds", str(self._sock_path)]
        self._proc = subprocess.Popen(
            cmd, cwd=str(_perception_root()), env=dict(os.environ),
            stdout=log_fh, stderr=subprocess.STDOUT)
        self._client = UdsClient(str(self._sock_path))
        self._wait_ready()

    def _wait_ready(self):
        deadline = time.monotonic() + self._timeout_s
        last_error: str | None = None
        while time.monotonic() < deadline:
            if self._proc is not None and self._proc.poll() is not None:
                tail = self._tail_log()
                raise RuntimeError(
                    f"uvicorn（arm={self.name}）启动即退出 rc="
                    f"{self._proc.returncode}:\n{tail}")
            try:
                status, _, body = self._client.request("GET", "/version")
                if status == 200:
                    return
                last_error = f"HTTP {status}: {body[:200]!r}"
            except (ConnectionError, OSError) as exc:
                last_error = str(exc)
            time.sleep(0.5)
        tail = self._tail_log()
        raise RuntimeError(
            f"uvicorn（arm={self.name}）启动超时 {self._timeout_s}s；"
            f"last_error={last_error}\nlog tail:\n{tail}")

    def _tail_log(self, n: int = 40) -> str:
        try:
            lines = self._log_path.read_text(errors="replace").splitlines()
            return "\n".join(lines[-n:])
        except OSError:
            return "<log 不可读>"

    def analyze(self, png_bytes: bytes) -> dict[str, Any]:
        """POST /v1/analyze（PNG body）→ {status, body, serverTimingMs, wallMs}。"""
        headers = {"Content-Type": "image/png"}
        if self.variant_header:
            headers["X-Pipeline-Variant"] = self.variant_header
        wall_start = time.perf_counter()
        status, resp_headers, body = self._client.request(
            "POST", "/v1/analyze", body=png_bytes, headers=headers)
        wall_ms = (time.perf_counter() - wall_start) * 1000
        timing = parse_server_timing(
            next((v for k, v in resp_headers.items() if k.lower() == "server-timing"),
                 None))
        return {"status": status, "body": body, "serverTimingMs": timing,
                "wallMs": wall_ms}

    def pid(self) -> int | None:
        return self._proc.pid if self._proc is not None else None

    def stop(self):
        if self._client is not None:
            self._client.close()
            self._client = None
        if self._proc is not None and self._proc.poll() is None:
            self._proc.terminate()
            try:
                self._proc.wait(timeout=10.0)
            except subprocess.TimeoutExpired:
                self._proc.kill()
                self._proc.wait(timeout=5.0)
        self._proc = None
        try:
            self._sock_path.unlink()
        except FileNotFoundError:
            pass


# ── B2 臂（ocr-off；进程内消融）──────────────────────────────────────────────

class B2Arm:
    """进程内复刻 bench/run_replacement_ablation.py 的 setup（WI-3 交付）：
    detect=screenparser（replacement 变体）+ OCR 恒空。与 B1 同图同 device 必须
    同 yolo（WI-3 test 保证；本 harness 再抽查断言）。"""

    def __init__(self, device: str):
        if device not in ("cpu", "mps"):
            raise ValueError(f"device {device!r} 不在 (cpu, mps)")
        self.name = "ocr-off"
        self.device = device
        self._ready = False
        self._server: Any = None
        self._variant_key = ""
        self._variant_config: Any = None

    def ensure_ready(self):
        if self._ready:
            return
        bench_dir = _perception_root() / "bench"
        if str(bench_dir) not in sys.path:
            sys.path.insert(0, str(bench_dir))
        import run_replacement_ablation as rra
        import uniclaw_perception.server as server
        from uniclaw_perception import identity, pipeline
        from uniclaw_perception.config import load as load_config
        from uniclaw_perception.health import _model_id

        self._server = server
        cfg = load_config()
        server._config = cfg
        variant_name = rra._VARIANT_BY_DEVICE[self.device]
        variants = pipeline.load_variants()
        if variant_name not in variants:
            raise RuntimeError(f"unknown variant: {variant_name!r} "
                               f"(declared: {sorted(variants)})")
        pipeline_config = variants[variant_name].config
        pipeline.lint_against_config(pipeline_config, cfg)

        from uniclaw_perception.health import capture_identity
        capture_identity()
        revision = identity.compute_pipeline_revision()
        config_id = identity.build_config_id(
            cfg, pipeline.identity_content(pipeline_config, variant_name))
        deployment_id = identity.compute_deployment_id(
            "uniclaw.localVisionEvidence.v1", _model_id(), config_id,
            revision["pipelineRevision"])
        server._pipelines = {variant_name: (pipeline_config, {
            "configId": config_id,
            "pipelineRevision": revision["pipelineRevision"],
            "deploymentId": deployment_id,
        })}

        if cfg.ocr_backend == "rapidocr":
            from uniclaw_perception.ocr.rapid import (
                _rapid_ocr_kwargs, configure_ocr_models)
            _rapid_ocr_kwargs.update(configure_ocr_models(language=cfg.ocr_lang))

        rra._ablate_ocr(server)
        self._variant_key = variant_name
        self._variant_config = pipeline_config
        self._ready = True

    def analyze(self, png_bytes: bytes) -> dict[str, Any]:
        """进程内跑 replacement + OCR 恒空；返回与 HTTP 臂同构的 sample。"""
        self.ensure_ready()
        from io import BytesIO
        from PIL import Image
        image = Image.open(BytesIO(png_bytes)).convert("RGB")
        width, height = image.size
        wall_start = time.perf_counter()
        evidence, (t0, t1, t2, t3, t_sp) = self._server._run_pipeline(
            image, width, height, pipeline=self._variant_config,
            pipeline_key=self._variant_key)
        wall_ms = (time.perf_counter() - wall_start) * 1000
        timing: dict[str, float | None] = {s: None for s in SEGMENT_NAMES}
        timing["yolo"] = round((t1 - t0) * 1000, 3)
        timing["ocr"] = round((t2 - t1) * 1000, 3)
        timing["fusion"] = round(
            ((t3 - t_sp) * 1000 if t_sp is not None else (t3 - t2) * 1000), 3)
        return {"status": 200, "body": json.dumps(evidence, ensure_ascii=False),
                "serverTimingMs": timing, "wallMs": wall_ms,
                "inProcess": True}

    def pid(self) -> int | None:
        return os.getpid()

    def stop(self):
        self._ready = False


# ── 编排 ─────────────────────────────────────────────────────────────────────

def _sha256_hex(data: bytes | str) -> str:
    if isinstance(data, str):
        data = data.encode("utf-8")
    return hashlib.sha256(data).hexdigest()


def _png_bytes(path: Path) -> bytes:
    return path.read_bytes()


def discover_frames(valset_dir: Path, gt_suffix: str = "json") -> list[dict[str, Any]]:
    """数据驱动帧发现：frames/*.png ∪ gt/*.<suffix>（去重、排序；不硬编码帧清单）。

    每帧：{frameId, pngPath, gtPath, metaPath, stratum}。
    """
    frames_dir = valset_dir / "frames"
    gt_dir = valset_dir / "gt"
    by_id: dict[str, dict[str, Any]] = {}
    if frames_dir.is_dir():
        for png in sorted(frames_dir.glob("*.png")):
            fid = png.stem
            by_id.setdefault(fid, {"frameId": fid, "pngPath": png, "gtPath": None})["pngPath"] = png
    if gt_dir.is_dir():
        for gt_file in sorted(gt_dir.glob(f"*.{gt_suffix}")):
            fid = gt_file.stem
            if not _FRAME_ID_RE.fullmatch(fid):
                # 非帧工件（如 calibration-decisions.json）不入帧清单
                continue
            by_id.setdefault(fid, {"frameId": fid, "pngPath": None, "gtPath": None})["gtPath"] = gt_file
    frames = []
    for fid in sorted(by_id):
        entry = by_id[fid]
        meta_path = frames_dir / f"{fid}.meta.json" if frames_dir.is_dir() else None
        stratum = "unknown"
        if meta_path is not None and meta_path.exists():
            try:
                meta = json.loads(meta_path.read_text(encoding="utf-8"))
                stratum = str(meta.get("stratum") or "unknown")
            except (OSError, json.JSONDecodeError):
                stratum = "error"
        entry["metaPath"] = meta_path if meta_path is not None and meta_path.exists() else None
        entry["stratum"] = stratum
        frames.append(entry)
    return frames


def load_gt(gt_path: Path | None) -> dict[str, Any] | None:
    if gt_path is None or not gt_path.exists():
        return None
    try:
        return json.loads(gt_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        return {"_gtError": str(exc)}


def run_frame_on_arm(arm: Any, frame: dict, png_bytes: bytes,
                     runs: int, warmup: int,
                     in_process: bool) -> dict[str, Any]:
    """每帧：warmup 1 次 + measured N 次；质量响应 = 首次成功的 measured 响应。"""
    warmup_statuses: list[int] = []
    for _ in range(warmup):
        try:
            warmup_statuses.append(arm.analyze(png_bytes)["status"])
        except Exception:
            warmup_statuses.append(-1)

    requests: list[dict[str, Any]] = []
    errors: list[str] = []
    quality_response: dict[str, Any] | None = None
    quality_body_sha256: str | None = None
    for i in range(runs):
        try:
            sample = arm.analyze(png_bytes)
        except Exception as exc:
            errors.append(f"req#{i}: {type(exc).__name__}: {exc}")
            continue
        req = {
            "index": i,
            "status": sample["status"],
            "wallMs": round(sample["wallMs"], 2),
            "segments": {k: (round(v, 2) if v is not None else None)
                         for k, v in sample["serverTimingMs"].items()},
            "bodySha256": _sha256_hex(sample["body"])[:16],
            "inProcess": in_process,
        }
        requests.append(req)
        if sample["status"] != 200:
            errors.append(f"req#{i}: HTTP {sample['status']}")
            continue
        if quality_response is None:
            try:
                quality_response = json.loads(sample["body"])
                quality_body_sha256 = req["bodySha256"]
            except json.JSONDecodeError as exc:
                errors.append(f"req#{i}: 响应非 JSON: {exc}")

    wall_samples = [r["wallMs"] for r in requests]
    segment_samples: dict[str, list[float]] = {s: [] for s in SEGMENT_NAMES}
    for r in requests:
        for seg, v in r["segments"].items():
            if v is not None:
                segment_samples[seg].append(v)
    latency: dict[str, Any] = {
        "wall": latency_stats(wall_samples, gated=False),
        "segments": {seg: latency_stats(segment_samples[seg], gated=False)
                     for seg in SEGMENT_NAMES},
    }

    status = ("ok" if (requests and not errors and quality_response is not None)
              else ("partial" if requests else "error"))
    return {
        "frameId": frame["frameId"],
        "stratum": frame["stratum"],
        "arm": getattr(arm, "name", "?"),
        "status": status,
        "warmupStatuses": warmup_statuses,
        "requests": requests,
        "latency": latency,
        "errors": errors,
        "qualityResponse": quality_response,
        "qualityResponseSha256": quality_body_sha256,
    }


def _frame_resolution(response: dict | None) -> tuple[int, int]:
    if response is not None:
        meta = response.get("metadata") or {}
        w = meta.get("width")
        h = meta.get("height")
        if w and h:
            return int(w), int(h)
    return 1080, 2400


def run_arms(valset_dir: Path, arms: list[str], runs: int, warmup: int,
             device: str, out_dir: Path, gt_suffix: str = "json",
             frames_subset: list[str] | None = None) -> dict[str, Any]:
    """全流程：起服务（每 HTTP 臂一进程；B2 进程内）→ 四臂跑帧 → 质量评分 →
    汇总 + 落盘。返回完整报告 dict（已写入 out_dir）。"""
    out_dir.mkdir(parents=True, exist_ok=True)
    log_dir = out_dir / "logs"
    log_dir.mkdir(parents=True, exist_ok=True)

    frames = discover_frames(valset_dir, gt_suffix)
    if frames_subset:
        frames = [f for f in frames
                  if any(f["frameId"].startswith(p) for p in frames_subset)]
    if not frames:
        raise RuntimeError(f"valset {valset_dir} 未发现可评测帧"
                           f"（frames/*.png 或 gt/*.{gt_suffix}）")

    if "ocr-off" in arms and device not in ("cpu", "mps"):
        raise ValueError(f"--device {device!r} 仅支持 cpu/mps（B2 变体推导）")

    frames_dir_out = out_dir / "frames"
    frames_dir_out.mkdir(parents=True, exist_ok=True)

    frame_details: dict[str, dict[str, Any]] = {
        f["frameId"]: {
            "frameId": f["frameId"], "stratum": f["stratum"],
            "hasPng": f["pngPath"] is not None,
            "gt": {"status": "NO_GT", "reviewStatus": None},
            "arms": {},
        } for f in frames
    }

    report: dict[str, Any] = {
        "schema": "uniclaw.fsv001.compareArms.v1",
        "scorerRevision": SCORER_REVISION,
        "matcherRevision": MATCHER_REVISION,
        "iouThreshold": IOU_THRESHOLD,
        "hitMarginPx": HIT_MARGIN_PX,
        "cli": {
            "valset": str(valset_dir),
            "arms": arms,
            "runs": runs,
            "warmup": warmup,
            "device": device,
            "outDir": str(out_dir),
            "gtSuffix": gt_suffix,
        },
        "frames": [{"frameId": f["frameId"], "stratum": f["stratum"],
                    "hasPng": f["pngPath"] is not None,
                    "hasGt": f["gtPath"] is not None} for f in frames],
        "arms": {},
        "perStratum": {},
        "b2B1YoloSpotChecks": {},
        "timestamps": {"runStartUtc": time.strftime("%Y-%m-%dT%H:%M:%S%z")},
    }

    for arm_name in arms:
        label = ARM_SPECS[arm_name]["label"]
        spec = ARM_SPECS[arm_name]
        sock_path = _arm_sock_path(arm_name, out_dir)
        log_path = log_dir / f"server-{arm_name}.log"
        if spec["inProcess"]:
            arm: Any = B2Arm(device)
            arm.ensure_ready()
        else:
            arm = HttpArm(arm_name, sock_path=sock_path, log_path=log_path)
            arm.start()
        sampler = RssSampler(pid=arm.pid() or os.getpid())
        sampler.start()
        arm_records: dict[str, dict[str, Any]] = {}
        quality_responses: dict[str, dict[str, Any]] = {}
        try:
            for frame in frames:
                if frame["pngPath"] is None:
                    arm_records[frame["frameId"]] = {
                        "frameId": frame["frameId"],
                        "arm": arm_name, "status": "missing-image",
                        "errors": ["帧 PNG 缺失，无法推理"],
                    }
                    print(f"[{arm_name}] {frame['frameId']} status=missing-image")
                    continue
                png = _png_bytes(frame["pngPath"])
                rec = run_frame_on_arm(arm, frame, png, runs, warmup,
                                       in_process=spec["inProcess"])
                arm_records[frame["frameId"]] = rec
                if rec["qualityResponse"] is not None:
                    quality_responses[frame["frameId"]] = rec["qualityResponse"]
                wall = rec["latency"]["wall"]
                shown = wall.get("medianMs") if wall.get("medianMs") is not None \
                    else wall.get("p50Ms")
                print(f"[{arm_name}] {frame['frameId']} status={rec['status']} "
                      f"wall_median={shown}ms")
        finally:
            rss = sampler.stop()
            arm.stop()
        rss_report = {
            **rss,
            "scope": "self (in-process)" if spec["inProcess"] else "uvicorn server proc",
        }

        # 质量评分（每帧首响应 vs GT；WI-5b：rejected 帧 → element/candidate/
        # text/grounding 指标跳过计入 skippedForQuality，latency/RSS 保留）
        scored: list[dict[str, Any]] = []
        gt_statuses: dict[str, int] = {}
        skipped_for_quality: list[dict[str, str]] = []
        for frame in frames:
            fid = frame["frameId"]
            gt = load_gt(frame["gtPath"])
            frame_detail = frame_details[fid]
            if gt is None or gt.get("_gtError"):
                status = "NO_GT" if gt is None else "GT_ERROR"
                frame_detail["gt"] = {"status": status,
                                      "note": (None if gt is None
                                               else gt["_gtError"])}
                gt_statuses[status] = gt_statuses.get(status, 0) + 1
            elif is_rejected_gt(gt):
                # D6r 排除帧：只跑不改——质量跳过，latency/RSS 如常包含。
                frame_detail["gt"] = {
                    "status": "SKIPPED_REJECTED",
                    "schemaVersion": gt.get("schemaVersion"),
                    "reviewStatus": gt.get("reviewStatus"),
                    "provenance": gt.get("provenance"),
                }
                gt_statuses["SKIPPED_REJECTED"] = gt_statuses.get("SKIPPED_REJECTED", 0) + 1
                skipped_for_quality.append({
                    "frameId": fid, "reviewStatus": gt.get("reviewStatus") or ""})
                resp = quality_responses.get(fid)
                frame_detail["arms"][arm_name] = {
                    "arm": arm_name, "status": "rejected-skip",
                    "quality": None,
                    "reviewStatus": gt.get("reviewStatus"),
                    "latency": arm_records.get(fid, {}).get("latency"),
                    "qualityResponse": resp,
                    "qualityResponseSha256": arm_records.get(fid, {}).get("qualityResponseSha256"),
                }
            else:
                frame_detail["gt"] = {
                    "status": "SCORED",
                    "schemaVersion": gt.get("schemaVersion"),
                    "reviewStatus": gt.get("reviewStatus"),
                    "provenance": gt.get("provenance"),
                }
                gt_statuses["SCORED"] = gt_statuses.get("SCORED", 0) + 1
                resp = quality_responses.get(fid)
                if resp is None:
                    frame_detail["arms"][arm_name] = {
                        "arm": arm_name, "status": "no-quality-response",
                        "errors": arm_records.get(fid, {}).get("errors", []),
                    }
                    continue
                w, h = _frame_resolution(resp)
                quality = score_frame(resp, gt, w, h)
                scored.append(quality)
                frame_detail["arms"][arm_name] = {
                    "arm": arm_name, "status": "scored",
                    "quality": quality,
                    "latency": arm_records.get(fid, {}).get("latency"),
                    "qualityResponse": resp,
                    "qualityResponseSha256": arm_records.get(fid, {}).get("qualityResponseSha256"),
                }

        quality_agg = aggregate_quality(scored)
        quality_agg["skippedForQuality"] = len(skipped_for_quality)
        quality_agg["skippedForQualityFrames"] = skipped_for_quality
        report["arms"][arm_name] = {
            "label": label,
            "variantHeader": spec["variantHeader"],
            "gtStatuses": gt_statuses,
            "quality": quality_agg,
            "latency": aggregate_latency(list(arm_records.values())),
            "rss": rss_report,
        }

    # per-frame 明细落盘（内容寻址文件名 = frameId）
    for fid, detail in frame_details.items():
        (frames_dir_out / f"{fid}.json").write_text(
            json.dumps(detail, ensure_ascii=False, indent=2), encoding="utf-8")

    # per-stratum 分解（meta.stratum）
    report["perStratum"] = stratify_all(arms, frames, frame_details)

    # B2 vs B1 yolo 抽查断言（同图同 device 必须同 yolo——WI-3 已 test 保证）
    if "replacement" in arms and "ocr-off" in arms:
        for frame in frames:
            fid = frame["frameId"]
            arms_of_frame = frame_details[fid].get("arms", {})
            b1 = arms_of_frame.get("replacement", {}).get("qualityResponse")
            b2 = arms_of_frame.get("ocr-off", {}).get("qualityResponse")
            if b1 is None or b2 is None:
                report["b2B1YoloSpotChecks"].setdefault(fid, {
                    "status": "skipped", "note": "一侧缺质量响应"})
                continue
            same = json.dumps(b1.get("yolo", []), ensure_ascii=False) == \
                json.dumps(b2.get("yolo", []), ensure_ascii=False)
            report["b2B1YoloSpotChecks"][fid] = {
                "status": "equal" if same else "DIFF",
                "b1YoloCount": len(b1.get("yolo", [])),
                "b2YoloCount": len(b2.get("yolo", [])),
            }

    # 内容寻址聚合报告（确定性 payload：剥离 timestamps 与 latency）
    digest = hashlib.sha256(
        json.dumps(_canonical_payload(report), ensure_ascii=False,
                   sort_keys=True).encode("utf-8")).hexdigest()[:12]
    report["reportFile"] = f"fsv001-{digest}.json"
    report_path = out_dir / report["reportFile"]
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2),
                           encoding="utf-8")
    (out_dir / "summary.md").write_text(
        render_markdown_report(report), encoding="utf-8")

    report["_out"] = {
        "aggregate": str(report_path),
        "summary": str(out_dir / "summary.md"),
        "framesDir": str(frames_dir_out),
        "logsDir": str(log_dir),
    }
    return report


def _canonical_payload(report: dict) -> dict:
    """确定性 payload：除 timestamps + latency/rss（测量数据）外全量。"""
    import copy
    payload = copy.deepcopy(report)
    payload.pop("timestamps", None)
    payload.pop("_out", None)
    for arm in payload.get("arms", {}).values():
        arm.pop("latency", None)
        arm.pop("rss", None)
    # perStratum 的 latencyWall 属测量数据，剥离（其余 stratum 指标确定性）
    for _, strata in payload.get("perStratum", {}).items():
        for _, s in strata.items():
            s.pop("latencyWall", None)
    return payload


# ── 汇总（pooled）────────────────────────────────────────────────────────────

def aggregate_quality(scored: list[dict[str, Any]]) -> dict[str, Any]:
    """跨帧 pooled 质量汇总（只聚合 SCORED 帧；NOT_SCORABLE 计数）。"""
    elem_scored = [q["elements"] for q in scored if q["elements"]["stance"] == "SCORED"]
    text_scored = [q["text"] for q in scored if q["text"]["stance"] == "SCORED"]
    cand_scored = [q["candidates"] for q in scored if q["candidates"]["stance"] == "SCORED"]
    ground_scored = [q["grounding"] for q in scored if q["grounding"]["stance"] == "SCORED"]

    elem: dict[str, Any] = {"stance": "NOT_SCORABLE"}
    if elem_scored:
        tp = sum(q["tp"] for q in elem_scored)
        fp = sum(q["fp"] for q in elem_scored)
        fn = sum(q["fn"] for q in elem_scored)
        elem = {"stance": "SCORED", **p_r_f1(tp, fp, fn), "_frames": len(elem_scored)}
        iou_vals = [v for q in elem_scored for v in q["bboxIouPairs"]]
        center_vals = [v for q in elem_scored for v in q["centerDeltaPairsPx"]]
        elem["bboxIou"] = {
            "mean": round(statistics.fmean(iou_vals), 4) if iou_vals else None,
            "median": round(statistics.median(iou_vals), 4) if iou_vals else None,
            "pairs": len(iou_vals),
        }
        elem["centerDeltaPx"] = {
            "mean": round(statistics.fmean(center_vals), 2) if center_vals else None,
            "median": round(statistics.median(center_vals), 2) if center_vals else None,
            "pairs": len(center_vals),
        }
        # typeAccuracy pooled（WI-5b 主口径）：class-agnostic 配对跨帧加权汇总
        type_pairs = sum(q.get("typeMatchedPairs", 0) for q in elem_scored)
        type_correct = sum(q.get("typeCorrectPairs", 0) for q in elem_scored)
        elem["typeAccuracy"] = (
            round(type_correct / type_pairs, 4) if type_pairs else None)
        elem["typeMatchedPairs"] = type_pairs
        elem["typeCorrectPairs"] = type_correct
        conf: dict[str, dict[str, int]] = {}
        for q in elem_scored:
            for pred, gs in q["confusion"].items():
                for gt_c, n in gs.items():
                    conf.setdefault(pred, {}).setdefault(gt_c, 0)
                    conf[pred][gt_c] += n
        elem["confusion"] = conf
        elem["confusionTopN"] = [
            {"predLabel": pl, "gtClass": gc, "count": n}
            for pl, gc, n in sorted(
                ((pl, gc, n) for pl, gs in conf.items() for gc, n in gs.items()),
                key=lambda t: (-t[2], t[0], t[1]))[:CONFUSION_TOP_N]
        ]
        # center 次级口径（WI-5b 修正 c）pooled
        sec_tp = sum(q["secondary"]["tp"] for q in elem_scored)
        sec_fp = sum(q["secondary"]["fp"] for q in elem_scored)
        sec_fn = sum(q["secondary"]["fn"] for q in elem_scored)
        elem["secondary"] = {
            "matcherRevision": CENTER_MATCH_REVISION,
            "mode": "center",
            **p_r_f1(sec_tp, sec_fp, sec_fn),
            "frames": len(elem_scored),
        }

    text: dict[str, Any] = {"stance": "NOT_SCORABLE"}
    if text_scored:
        total_targets = sum(q["targetCount"] for q in text_scored)
        exact = sum(q["exactHits"] for q in text_scored)
        cer_vals = [v for q in text_scored for v in q["cerValues"]]
        text = {
            "stance": "SCORED",
            "frames": len(text_scored),
            "targets": total_targets,
            "exactHits": exact,
            "exactHitRate": round(exact / total_targets, 4) if total_targets else None,
            "cerMean": (round(statistics.fmean(cer_vals), 4) if cer_vals else None),
            "cerDefined": len(cer_vals),
            "ocrEmptyFrames": sum(1 for q in text_scored if q["ocrEmpty"]),
        }
        if text["ocrEmptyFrames"]:
            text["note"] = ("含 OCR 空帧（B2）：exact 如实为 0，CER 仅对已定义配对求均值")

    cand: dict[str, Any] = {"stance": "NOT_SCORABLE"}
    if cand_scored:
        tp = sum(q["tp"] for q in cand_scored)
        fp = sum(q["fp"] for q in cand_scored)
        fn = sum(q["fn"] for q in cand_scored)
        cand = {"stance": "SCORED", **p_r_f1(tp, fp, fn), "_frames": len(cand_scored)}
        assoc_t = sum(q["textAssociation"]["correct"] for q in cand_scored)
        assoc_d = sum(q["textAssociation"]["denominator"] for q in cand_scored)
        cand["textAssociation"] = {
            "correct": assoc_t, "denominator": assoc_d,
            "rate": round(assoc_t / assoc_d, 4) if assoc_d else None,
        }
    else:
        cand = {"stance": "NOT_SCORABLE"}

    ground: dict[str, Any] = {"stance": "NOT_SCORABLE"}
    if ground_scored:
        scored_n = sum(q["scoredCount"] for q in ground_scored)
        hits = sum(q["hitCount"] for q in ground_scored)
        by_reason: dict[str, int] = {}
        for q in ground_scored:
            for r, n in q["missReasons"].items():
                by_reason[r] = by_reason.get(r, 0) + n
        ground = {
            "stance": "SCORED",
            "frames": len(ground_scored),
            "tasks": sum(q["taskCount"] for q in ground_scored),
            "scored": scored_n,
            "hits": hits,
            "misses": sum(q["missCount"] for q in ground_scored),
            "notScorable": sum(q["notScorableCount"] for q in ground_scored),
            "unsupported": sum(q["unsupportedCount"] for q in ground_scored),
            "hitRate": round(hits / scored_n, 4) if scored_n else None,
            "missReasons": by_reason,
        }

    return {
        "elements": elem,
        "text": text,
        "candidates": cand,
        "grounding": ground,
        "scoredFrames": len(scored),
        "notScorable": {
            "elements": sum(1 for q in scored if q["elements"]["stance"] == "NOT_SCORABLE"),
            "text": sum(1 for q in scored if q["text"]["stance"] == "NOT_SCORABLE"),
            "candidates": sum(1 for q in scored if q["candidates"]["stance"] == "NOT_SCORABLE"),
            "grounding": sum(1 for q in scored if q["grounding"]["stance"] == "NOT_SCORABLE"),
        },
    }


def aggregate_latency(records: list[dict[str, Any]]) -> dict[str, Any]:
    """跨帧 pooled latency（wall + 各分段；gated run_l2 语义，n≥10 才报 p50/p95）。"""
    wall: list[float] = []
    segments: dict[str, list[float]] = {s: [] for s in SEGMENT_NAMES}
    for rec in records:
        for req in rec.get("requests", []):
            wall.append(req["wallMs"])
            for seg in SEGMENT_NAMES:
                v = req["segments"].get(seg)
                if v is not None:
                    segments[seg].append(v)
    out: dict[str, Any] = {"wall": latency_stats(wall, gated=True), "segments": {}}
    for seg in SEGMENT_NAMES:
        out["segments"][seg] = latency_stats(segments[seg], gated=True)
    return out


def stratify_all(arms: list[str], frames: list[dict],
                 frame_details: dict[str, dict[str, Any]]) -> dict[str, Any]:
    """per-stratum 分解（meta.stratum）：elements P/R/F1 + text exact + grounding
    hitRate + wall p50/p95（每臂每 stratum 一 bucket）。"""
    out: dict[str, dict[str, Any]] = {}
    for arm_name in arms:
        strata: dict[str, dict[str, Any]] = {}
        for frame in frames:
            fid = frame["frameId"]
            stratum = frame["stratum"]
            info = frame_details.get(fid, {}).get("arms", {}).get(arm_name)
            if info is None or info.get("status") != "scored":
                continue
            q = info.get("quality")
            lat = info.get("latency") or {}
            bucket = strata.setdefault(stratum, {
                "frames": 0, "elemTp": 0, "elemFp": 0, "elemFn": 0,
                "textTargets": 0, "textExact": 0,
                "groundScored": 0, "groundHits": 0,
                "wallSamples": [],
            })
            bucket["frames"] += 1
            e = q.get("elements", {})
            if e.get("stance") == "SCORED":
                bucket["elemTp"] += e.get("tp", 0)
                bucket["elemFp"] += e.get("fp", 0)
                bucket["elemFn"] += e.get("fn", 0)
            t = q.get("text", {})
            if t.get("stance") == "SCORED":
                bucket["textTargets"] += t.get("targetCount", 0)
                bucket["textExact"] += t.get("exactHits", 0)
            g = q.get("grounding", {})
            if g.get("stance") == "SCORED":
                bucket["groundScored"] += g.get("scoredCount", 0)
                bucket["groundHits"] += g.get("hitCount", 0)
            for req_wall in _latency_wall_samples_lazy(lat):
                bucket["wallSamples"].append(req_wall)

        strata_out: dict[str, Any] = {}
        for stratum, b in sorted(strata.items()):
            prf = p_r_f1(b["elemTp"], b["elemFp"], b["elemFn"]) \
                if (b["elemTp"] + b["elemFp"] + b["elemFn"]) else None
            strata_out[stratum] = {
                "frames": b["frames"],
                "elements": prf,
                "text": {
                    "targets": b["textTargets"],
                    "exactHits": b["textExact"],
                    "exactHitRate": (round(b["textExact"] / b["textTargets"], 4)
                                     if b["textTargets"] else None),
                },
                "grounding": {
                    "scored": b["groundScored"],
                    "hits": b["groundHits"],
                    "hitRate": (round(b["groundHits"] / b["groundScored"], 4)
                                if b["groundScored"] else None),
                },
                "latencyWall": latency_stats(b["wallSamples"], gated=True),
            }
        out[arm_name] = strata_out
    return out


def _latency_wall_samples_lazy(lat: dict) -> list[float]:
    """latency dict 无 raw samples；改为从 frame_details 的 latency 无法取
    raw——per-stratum wall pooled 用每帧 latency.wall 的 median 近似。
    （说明：per-frame latency 只存分位；stratum 聚合以帧 median 为样本。）"""
    if lat is None:
        return []
    w = lat.get("wall") or {}
    med = w.get("medianMs")
    return [med] if med is not None else []


def _fmt(v: Any, digits: int = 3) -> str:
    if v is None:
        return "—"
    if isinstance(v, float):
        return f"{v:.{digits}f}"
    return str(v)


def render_markdown_report(report: dict[str, Any]) -> str:
    """Markdown 摘要：arm × 指标 + latency 表 + RSS 表 + 混淆 + grounding miss。"""
    arms = report["arms"]
    lines: list[str] = []
    lines.append("# FSV-001 四臂 A/B 评测摘要")
    lines.append("")
    lines.append(f"- runId: {report.get('reportFile', '')}")
    lines.append("- 起跑时间: "
                 + str((report.get("timestamps") or {}).get("runStartUtc"))
                 + "（仅记录，不参与确定性）")
    lines.append(f"- matcher: {report['matcherRevision']} "
                 f"(class 相等 + IoU ≥ {report['iouThreshold']})")
    lines.append(f"- grounding hit margin: ±{report['hitMarginPx']}px")
    lines.append(f"- 帧数: {len(report['frames'])}；臂: "
                 + ", ".join(report["cli"]["arms"]))
    lines.append("")

    lines.append("## 质量指标（arm × 指标 pooled；N/S = NOT_SCORABLE，— = 无值）")
    lines.append("")
    lines.append("| arm | element P | element R | element F1 | "
                 "elem R(center) | elem P(center) | elem F1(center) | typeAcc | "
                 "text exact | text CER | cand F1 | grounding hit |")
    lines.append("|---|---|---|---|---|---|---|---|---|---|---|---|")
    for arm_name in ARM_ORDER:
        if arm_name not in arms:
            continue
        q = arms[arm_name]["quality"]
        e = q.get("elements") or {}
        t = q.get("text") or {}
        c = q.get("candidates") or {}
        g = q.get("grounding") or {}
        e_scored = e.get("stance") == "SCORED"
        e_p = _fmt(e.get("precision")) if e_scored else "N/S"
        e_r = _fmt(e.get("recall")) if e_scored else "N/S"
        e_f = _fmt(e.get("f1")) if e_scored else "N/S"
        sec = e.get("secondary") or {}
        e_cr = _fmt(sec.get("recall")) if e_scored else "N/S"
        e_cp = _fmt(sec.get("precision")) if e_scored else "N/S"
        e_cf = _fmt(sec.get("f1")) if e_scored else "N/S"
        e_t = _fmt(e.get("typeAccuracy")) if e_scored else "N/S"
        t_scored = t.get("stance") == "SCORED"
        t_x = _fmt(t.get("exactHitRate")) if t_scored else "N/S"
        t_c = _fmt(t.get("cerMean")) if t_scored else "N/S"
        c_scored = c.get("stance") == "SCORED"
        c_f = _fmt(c.get("f1")) if c_scored else "N/S"
        g_scored = g.get("stance") == "SCORED"
        g_h = _fmt(g.get("hitRate")) if g_scored else "N/S"
        lines.append(f"| {arm_name} | {e_p} | {e_r} | {e_f} | {e_cr} | {e_cp} | "
                     f"{e_cf} | {e_t} | {t_x} | {t_c} | {c_f} | {g_h} |")
    lines.append("")
    lines.append("- element 主口径 = matcher-greedy-v1（class 相等 + IoU ≥ 0.5）；"
                 "elem R/P/F1(center) = 次级行容忍口径（matchMode=center："
                 "class 相等 ∧ 预测中心 ∈ GT 框，无 margin——YOLO 文字 extent vs "
                 "GT 整行 extent 的框语义差异，D6r 几何归因）。")
    lines.append("")

    lines.append("## Latency（跨帧 pooled；wall + 分段 P50/P95；n≥10 才报 p50/p95）")
    lines.append("")
    header = ["arm", "wall n", "wall P50", "wall P95", "yolo P50", "yolo P95",
              "ocr P50", "ocr P95", "fusion P50", "fusion P95",
              "screenparse P50", "screenparse P95"]
    lines.append("| " + " | ".join(header) + " |")
    lines.append("|" + "---|" * len(header))
    for arm_name in ARM_ORDER:
        if arm_name not in arms:
            continue
        lat = arms[arm_name].get("latency") or {}
        w = lat.get("wall") or {}
        segs = lat.get("segments") or {}
        lines.append("| " + " | ".join([
            arm_name,
            str(w.get("n", 0)),
            _fmt(w.get("p50Ms"), 1),
            _fmt(w.get("p95Ms"), 1),
            _fmt((segs.get("yolo") or {}).get("p50Ms"), 1),
            _fmt((segs.get("yolo") or {}).get("p95Ms"), 1),
            _fmt((segs.get("ocr") or {}).get("p50Ms"), 1),
            _fmt((segs.get("ocr") or {}).get("p95Ms"), 1),
            _fmt((segs.get("fusion") or {}).get("p50Ms"), 1),
            _fmt((segs.get("fusion") or {}).get("p95Ms"), 1),
            _fmt((segs.get("screenparse") or {}).get("p50Ms"), 1),
            _fmt((segs.get("screenparse") or {}).get("p95Ms"), 1),
        ]) + " |")
    lines.append("")

    lines.append("## RSS（推理期采样 max）")
    lines.append("")
    lines.append("| arm | max RSS (MB) | 样本数 | scope |")
    lines.append("|---|---|---|---|")
    for arm_name in ARM_ORDER:
        if arm_name not in arms:
            continue
        rss = arms[arm_name].get("rss") or {}
        lines.append(f"| {arm_name} | {_fmt(rss.get('maxRssMb'), 1)} | "
                     f"{rss.get('samples', 0)} | {rss.get('scope', '')} |")
    lines.append("")

    lines.append("## Grounding miss 原因分布")
    lines.append("")
    lines.append("| arm | reason | 计数 |")
    lines.append("|---|---|---|")
    for arm_name in ARM_ORDER:
        if arm_name not in arms:
            continue
        g = arms[arm_name]["quality"].get("grounding", {})
        if g.get("stance") != "SCORED":
            lines.append(f"| {arm_name} | (GT 无 groundingTasks / N/S) | — |")
            continue
        reasons = g.get("missReasons") or {}
        if not reasons:
            lines.append(f"| {arm_name} | (无 miss) | 0 |")
        for reason, n in sorted(reasons.items()):
            lines.append(f"| {arm_name} | {reason} | {n} |")
    lines.append("")

    lines.append("## 混淆（type accuracy 主口径：class-agnostic 贪心 IoU≥0.5 配对，"
             "pred→gt 计数）")
    lines.append("")
    for arm_name in ARM_ORDER:
        if arm_name not in arms:
            continue
        e = arms[arm_name]["quality"].get("elements", {})
        lines.append(f"### {arm_name} — typeAcc={_fmt(e.get('typeAccuracy'))}"
                     f"（配对 {e.get('typeMatchedPairs', 0)} pairs）")
        if e.get("stance") != "SCORED" or not e.get("confusion"):
            lines.append("（无 matched pairs 或 N/S）")
            continue
        all_gt = sorted({gtc for gs in e["confusion"].values() for gtc in gs})
        all_pred = sorted(e["confusion"].keys())
        lines.append("| pred \\ gt | " + " | ".join(all_gt) + " |")
        lines.append("|---|" + "---|" * len(all_gt))
        for pred in all_pred:
            cells = [str(e["confusion"][pred].get(gtc, 0)) for gtc in all_gt]
            lines.append("| " + pred + " | " + " | ".join(cells) + " |")
        lines.append("")
        top = e.get("confusionTopN") or []
        lines.append(f"混淆对 top-{min(len(top), CONFUSION_TOP_N) if top else 0}（predLabel→gtClass）:")
        lines.append("")
        if not top:
            lines.append("（无混淆对）")
        else:
            lines.append("| predLabel | gtClass | count |")
            lines.append("|---|---|---|")
            for item in top:
                lines.append(f"| {item['predLabel']} | {item['gtClass']} | "
                             f"{item['count']} |")
        lines.append("")

    lines.append("## per-stratum 分解（meta.stratum）")
    lines.append("")
    per_stratum = report.get("perStratum", {})
    for arm_name in ARM_ORDER:
        if arm_name not in per_stratum:
            continue
        lines.append(f"### {arm_name}")
        lines.append("")
        lines.append("| stratum | frames | elem P | elem R | elem F1 | "
                     "text exact | ground hit | wall P50 |")
        lines.append("|---|---|---|---|---|---|---|---|")
        for stratum, s in sorted(per_stratum[arm_name].items()):
            e = s["elements"]
            lines.append(
                f"| {stratum} | {s['frames']} | "
                f"{_fmt(e['precision']) if e else '—'} | "
                f"{_fmt(e['recall']) if e else '—'} | "
                f"{_fmt(e['f1']) if e else '—'} | "
                f"{_fmt(s['text']['exactHitRate'])} | "
                f"{_fmt(s['grounding']['hitRate'])} | "
                f"{_fmt(s['latencyWall'].get('p50Ms'), 1)} |")
        lines.append("")

    lines.append("## NOT_SCORABLE 帧计数（每臂；缺 GT 面 → 对应指标 null）")
    lines.append("")
    lines.append("| arm | elements | text | candidates | grounding | scoredFrames | "
                 "skippedForQuality |")
    lines.append("|---|---|---|---|---|---|---|")
    for arm_name in ARM_ORDER:
        if arm_name not in arms:
            continue
        ns = arms[arm_name]["quality"].get("notScorable", {})
        lines.append(f"| {arm_name} | {ns.get('elements')} | {ns.get('text')} | "
                     f"{ns.get('candidates')} | {ns.get('grounding')} | "
                     f"{arms[arm_name]['quality'].get('scoredFrames')} | "
                     f"{arms[arm_name]['quality'].get('skippedForQuality', 0)} |")
    lines.append("")

    lines.append("## skippedForQuality（rejected 帧：质量跳过、latency/RSS 保留）")
    lines.append("")
    lines.append("reviewStatus 以 `rejected` 开头（D6r 排除帧，见 CALIBRATION-REPORT.md）："
                 "element/candidate/text/grounding 指标不参与评分；latency/RSS "
                 "正常包含。")
    lines.append("")
    for arm_name in ARM_ORDER:
        if arm_name not in arms:
            continue
        frames_list = arms[arm_name]["quality"].get("skippedForQualityFrames", [])
        lines.append(f"- {arm_name}: {len(frames_list)} 帧 "
                     + ", ".join(f"{f['frameId']}({f['reviewStatus']})"
                                 for f in frames_list))
    lines.append("")

    checks = report.get("b2B1YoloSpotChecks", {})
    if checks:
        lines.append("## B2 vs B1 yolo 抽查（同图同 device 必须同 yolo；WI-3 保证）")
        lines.append("")
        lines.append("| frame | status | b1 | b2 |")
        lines.append("|---|---|---|---|")
        for fid, c in sorted(checks.items()):
            lines.append(f"| {fid} | {c['status']} | "
                         f"{c.get('b1YoloCount', '')} | {c.get('b2YoloCount', '')} |")
        lines.append("")

    lines.append("## B2 已知限制（如实记录）")
    lines.append("")
    lines.append("- B2（ocr-off）：进程内跑 _run_pipeline，无 HTTP 序列化/GC 段"
                 "（serialize/gc 为 null）；该臂 latency = 管道 wall。")
    lines.append("- click-state：state 感知超出当前 runtime 边界能力，resolver "
                 "只做目标框中心命中判定。")
    lines.append("- typeAccuracy 主口径 = class-agnostic 贪心 IoU≥0.5 配对下的 "
                 "label==gtClass 比例（WI-5b 修正 b——严格 class 相等配对下按构造"
                 "恒 1.0，无信息量）；混淆对计数（predLabel→gtClass top-N）随表。")
    lines.append("- 次级行容忍口径（elem P/R/F1(center)）：matchMode=center，"
                 "class 相等 ∧ 预测中心 ∈ GT 框，无 margin——YOLO 文字 extent vs "
                 "GT 整行 extent 的框语义差异（D6r 几何归因），与主口径并列。")
    lines.append("- skippedForQuality：reviewStatus 以 rejected 开头（D6r 排除 "
                 "帧）→ 质量指标跳过、latency/RSS 保留，报告显式列出。")
    lines.append("")
    return "\n".join(lines)


def _resolve_valset(valset_arg: str) -> Path:
    p = Path(valset_arg)
    if not p.is_absolute():
        p = _perception_root() / p
    return p.resolve()


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description="FSV-001 WI-5a 四臂 A/B 评测 harness",
        formatter_class=argparse.ArgumentDefaultsHelpFormatter)
    parser.add_argument("--valset", required=True,
                        help="验证集目录（含 frames/ + gt/；相对路径以 "
                             "platforms/perception 为基准）")
    parser.add_argument("--gt-suffix", default="json", help="GT 文件后缀")
    parser.add_argument("--arms", default="baseline,integration,replacement,ocr-off",
                        help="逗号分隔臂名: " + ",".join(ARM_ORDER))
    parser.add_argument("--runs", type=int, default=5, help="每帧每臂 measured 次数")
    parser.add_argument("--warmup", type=int, default=1, help="每帧每臂 warmup 次数")
    parser.add_argument("--device", choices=["cpu", "mps"], default="cpu",
                        help="B2（ocr-off）变体 device（cpu→fastscreen-replacement；"
                             "mps→fastscreen-replacement-mps）")
    parser.add_argument("--out-dir", default="evaluation/reports/fsv001",
                        help="输出目录（相对 platforms/perception）")
    parser.add_argument("--frames", default=None,
                        help="帧子集（逗号分隔的 frameId 前缀；调试用）")
    args = parser.parse_args(argv)

    arms = [a.strip() for a in args.arms.split(",") if a.strip()]
    unknown = [a for a in arms if a not in ARM_SPECS]
    if unknown:
        print(f"未知臂: {unknown}（允许: {', '.join(ARM_ORDER)}）", file=sys.stderr)
        return 2
    frames_subset = [f.strip() for f in args.frames.split(",") if f.strip()] \
        if args.frames else None
    out_dir = Path(args.out_dir)
    if not out_dir.is_absolute():
        out_dir = _perception_root() / out_dir

    valset = _resolve_valset(args.valset)
    start = time.monotonic()
    report = run_arms(valset, arms, runs=args.runs, warmup=args.warmup,
                      device=args.device, out_dir=out_dir,
                      gt_suffix=args.gt_suffix, frames_subset=frames_subset)
    elapsed_s = time.monotonic() - start
    print(f"\n聚合报告 → {report['_out']['aggregate']}")
    print(f"markdown 摘要 → {report['_out']['summary']}")
    print(f"per-frame 明细 → {report['_out']['framesDir']}")
    print(f"总耗时 {elapsed_s:.1f}s；runId={report['reportFile']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())