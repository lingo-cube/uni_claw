#!/usr/bin/env python
"""FSV-001 WI-6 — v2（整屏输入域）对照 v1（fsv001-eed2e12a65a2）delta 分析脚本。

输入：
  evaluation/reports/fsv001/fsv001-eed2e12a65a2.json   （v1 聚合，只读）
  evaluation/reports/fsv001-v2/fsv001-*.json           （v2 聚合，最新）
  evaluation/reports/fsv001/frames/*.json              （v1 帧明细）
  evaluation/reports/fsv001-v2/frames/*.json           （v2 帧明细）

产出：ANALYSIS-DELTA.md 所需的全部数字（stdout 表格）。数值口径与
compare_arms.py / ANALYSIS.md 完全一致：
  - matcher-greedy-v1：class 相等 + IoU≥0.5 贪心一对一
  - class-agnostic 配对用于 typeAcc 主口径 / rescue 命中 / 独有检出命中
  - hit margin ±8px
"""
from __future__ import annotations

import glob
import json
from pathlib import Path

REPORTS = Path(__file__).resolve().parent / "reports"  # platforms/perception/evaluation/reports


def load_latest_v2() -> dict:
    candidates = sorted(glob.glob(str(REPORTS / "fsv001-v2" / "fsv001-*.json")))
    if not candidates:
        raise SystemExit("fsv001-v2 无聚合报告")
    return json.loads(open(candidates[-1], encoding="utf-8").read())


def load_v1() -> dict:
    return json.loads(
        open(REPORTS / "fsv001" / "fsv001-eed2e12a65a2.json", encoding="utf-8").read())


def arm_table(report: dict, arm: str) -> dict:
    q = report["arms"][arm]["quality"]
    e = q.get("elements") or {}
    t = q.get("text") or {}
    g = q.get("grounding") or {}
    c = q.get("candidates") or {}
    lat = report["arms"][arm].get("latency", {})
    rss = report["arms"][arm].get("rss", {})
    return {
        "elemP": e.get("precision"), "elemR": e.get("recall"), "elemF1": e.get("f1"),
        "secP": (e.get("secondary") or {}).get("precision"),
        "secR": (e.get("secondary") or {}).get("recall"),
        "secF1": (e.get("secondary") or {}).get("f1"),
        "typeAcc": e.get("typeAccuracy"),
        "bboxIouMean": (e.get("bboxIou") or {}).get("mean"),
        "bboxIouMed": (e.get("bboxIou") or {}).get("median"),
        "centerMean": (e.get("centerDeltaPx") or {}).get("mean"),
        "centerMed": (e.get("centerDeltaPx") or {}).get("median"),
        "txtExact": t.get("exactHitRate"), "txtCer": t.get("cerMean"),
        "candF1": c.get("f1"),
        "groundHit": g.get("hitRate"),
        "wallP50": ((lat.get("wall") or {}).get("p50Ms")),
        "wallP95": ((lat.get("wall") or {}).get("p95Ms")),
        "yoloP50": ((lat.get("segments") or {}).get("yolo") or {}).get("p50Ms"),
        "spP50": ((lat.get("segments") or {}).get("screenparse") or {}).get("p50Ms"),
        "fusionP50": ((lat.get("segments") or {}).get("fusion") or {}).get("p50Ms"),
        "rssMax": (rss or {}).get("maxRssMb"),
    }


def fmt(v: float | None, nd: int = 3) -> str:
    return "—" if v is None else f"{v:.{nd}f}"


def delta(a: float | None, b: float | None) -> str:
    if a is None or b is None:
        return "—"
    return f"{a - b:+.3f}"


def fmt_pct(v: float | None) -> str:
    if v is None:
        return "—"
    return f"{v * 100:.1f}%"


def p_r_f1(tp, fp, fn):
    p = tp / (tp + fp) if tp + fp > 0 else None
    r = tp / (tp + fn) if tp + fn > 0 else None
    f1 = 2 * p * r / (p + r) if p and r else None
    return p, r, f1


def iou(a, b):
    x1 = max(a[0], b[0]); y1 = max(a[1], b[1])
    x2 = min(a[2], b[2]); y2 = min(a[3], b[3])
    inter = max(0.0, x2 - x1) * max(0.0, y2 - y1)
    if inter <= 0:
        return 0.0
    aa = max(0.0, a[2] - a[0]) * max(0.0, a[3] - a[1])
    ab = max(0.0, b[2] - b[0]) * max(0.0, b[3] - b[1])
    return inter / (aa + ab - inter) if (aa + ab - inter) > 0 else 0.0


def gt_px(elem: dict, w: int, h: int):
    b = elem["bounds"]
    return (b["x1"] * w, b["y1"] * h, b["x2"] * w, b["y2"] * h)


def rescue_and_unique_anatomy(version_dir: str, gt_dir: Path) -> dict:
    """帧明细 → rescue 解剖 + B1 独有检出 GT 命中（与 ANALYSIS.md 同法）。"""
    frames_dir = REPORTS / version_dir / "frames"
    out = {
        "rescueTotal": 0, "rescueGtHits": 0, "rescueByClass": {},
        "rescuePerFrame": [],          # (frameId, stratum, n, hits)
        "b1OnlyTotal": 0, "b1OnlyGtHits": 0, "b1OnlyByClass": {},
        "baseOnlyTotal": 0, "baseOnlyGtHits": 0,
        "spDroppedTotal": 0,           # 全帧 droppedOffCanvas 合计（v2 additive）
    }
    for path in sorted(frames_dir.glob("*.json")):
        d = json.loads(path.read_text(encoding="utf-8"))
        fid = d["frameId"]
        gt_file = gt_dir / f"{fid}.json"
        if not gt_file.exists():
            continue
        gt = json.loads(gt_file.read_text(encoding="utf-8"))
        if isinstance(gt.get("reviewStatus"), str) and \
                gt.get("reviewStatus", "").startswith("rejected"):
            continue  # D6r 排除帧
        w, h = 1080, 2400
        gts = [gt_px(e, w, h) for e in gt.get("elements") or []
               if isinstance(e.get("bounds"), dict)]
        arms = d.get("arms", {})
        base = (arms.get("baseline") or {}).get("qualityResponse")
        inte = (arms.get("integration") or {}).get("qualityResponse")
        repl = (arms.get("replacement") or {}).get("qualityResponse")
        if inte is not None:
            inte_yolo = [x for x in inte.get("yolo", []) if x.get("boundsPx")]
            fs_ = [x for x in inte_yolo if str(x.get("id", "")).startswith("fs_")]
            # rescue→GT 命中：class-agnostic IoU≥0.5 贪心（fs_ 元素与 GT 配对）
            hit = 0
            used: set[int] = set()
            pairs = []
            for fi, fs in enumerate(fs_):
                fb = tuple(fs["boundsPx"])
                best = (-1.0, -1)
                for gi, g in enumerate(gts):
                    if gi in used:
                        continue
                    s = iou(fb, g)
                    if s > best[0]:
                        best = (s, gi)
                if best[0] >= 0.5:
                    used.add(best[1]); hit += 1
                    pairs.append(fs["label"])
            out["rescueTotal"] += len(fs_)
            out["rescueGtHits"] += hit
            for x in fs_:
                out["rescueByClass"][x["label"]] = out["rescueByClass"].get(x["label"], 0) + 1
            out["rescuePerFrame"].append((fid, d["stratum"], len(fs_), hit))
            sp = inte.get("screenParse") or {}
            out["spDroppedTotal"] += (sp.get("summary") or {}).get("droppedOffCanvas", 0)
        # B1 独有 vs baseline 独有（IoU≥0.5 双侧几何配对）
        if repl is not None and base is not None:
            rb = [x for x in repl.get("yolo", []) if x.get("boundsPx")]
            bb = [x for x in base.get("yolo", []) if x.get("boundsPx")]
            def hit_rate(items, others):
                hits = 0
                used = set()
                for it in items:
                    ib = tuple(it["boundsPx"])
                    best = (-1.0, -1)
                    for oi, o in enumerate(others):
                        if oi in used:
                            continue
                        s = iou(ib, tuple(o["boundsPx"]))
                        if s > best[0]:
                            best = (s, oi)
                    if best[0] >= 0.5:
                        used.add(best[1]); hits += 1
                return hits
            def gt_hits(items):
                n = 0
                used = set()
                for it in items:
                    ib = tuple(it["boundsPx"])
                    best = (-1.0, -1)
                    for gi, g in enumerate(gts):
                        if gi in used:
                            continue
                        s = iou(ib, g)
                        if s > best[0]:
                            best = (s, gi)
                    if best[0] >= 0.5:
                        used.add(best[1]); n += 1
                return n
            b1_only = [x for x in rb if not any(
                iou(tuple(x["boundsPx"]), tuple(o["boundsPx"])) >= 0.5 for o in bb)]
            base_only = [x for x in bb if not any(
                iou(tuple(x["boundsPx"]), tuple(o["boundsPx"])) >= 0.5 for o in rb)]
            out["b1OnlyTotal"] += len(b1_only)
            out["b1OnlyGtHits"] += gt_hits(b1_only)
            for x in b1_only:
                out["b1OnlyByClass"][x["label"]] = out["b1OnlyByClass"].get(x["label"], 0) + 1
            out["baseOnlyTotal"] += len(base_only)
            out["baseOnlyGtHits"] += gt_hits(base_only)
    return out


def main() -> None:
    v1 = load_v1()
    v2 = load_latest_v2()
    print(f"v1 report: {v1['reportFile']}   v2 report: {v2['reportFile']}")
    print()
    ARMS = ("baseline", "integration", "replacement", "ocr-off")
    print("## 主表（v1 → v2 | Δ；— = 无值；单位：P/R/F1/typeAcc/text/grounding = 比例，"
          "latency = ms，RSS = MB）")
    hdr = ["arm", "elemP Δ", "elemR Δ", "elemF1 Δ", "secR Δ", "secF1 Δ", "typeAcc Δ",
           "bboxIoU Δ", "txtExact Δ", "txtCer Δ", "candF1 Δ", "groundHit Δ",
           "wallP50 Δ", "yoloP50 Δ", "spP50 Δ", "fusionP50 Δ", "RSS Δ"]
    print("| " + " | ".join(hdr) + " |")
    print("|" + "---|" * len(hdr))
    for arm in ARMS:
        a = arm_table(v1, arm)
        b = arm_table(v2, arm)
        row = [
            arm,
            delta(b["elemP"], a["elemP"]), delta(b["elemR"], a["elemR"]),
            delta(b["elemF1"], a["elemF1"]), delta(b["secR"], a["secR"]),
            delta(b["secF1"], a["secF1"]), delta(b["typeAcc"], a["typeAcc"]),
            delta(b["bboxIouMean"], a["bboxIouMean"]),
            delta(b["txtExact"], a["txtExact"]), delta(b["txtCer"], a["txtCer"]),
            delta(b["candF1"], a["candF1"]), delta(b["groundHit"], a["groundHit"]),
            delta(b["wallP50"], a["wallP50"]), delta(b["yoloP50"], a["yoloP50"]),
            delta(b["spP50"], a["spP50"]), delta(b["fusionP50"], a["fusionP50"]),
            delta(b["rssMax"], a["rssMax"]),
        ]
        print("| " + " | ".join(str(x) for x in row) + " |")
    print()
    print("## v1 → v2 绝对值对照")
    for arm in ARMS:
        a, b = arm_table(v1, arm), arm_table(v2, arm)
        print(f"{arm}: elem P/R/F1 {fmt(a['elemP'])}/{fmt(a['elemR'])}/{fmt(a['elemF1'])} "
              f"→ {fmt(b['elemP'])}/{fmt(b['elemR'])}/{fmt(b['elemF1'])} | "
              f"sec R/F1 {fmt(a['secR'])}/{fmt(a['secF1'])} → {fmt(b['secR'])}/{fmt(b['secF1'])} | "
              f"typeAcc {fmt(a['typeAcc'])} → {fmt(b['typeAcc'])} | "
              f"bboxIoU {fmt(a['bboxIouMean'])} → {fmt(b['bboxIouMean'])} | "
              f"txtExact {fmt_pct(a['txtExact'])} → {fmt_pct(b['txtExact'])} | "
              f"ground {fmt_pct(a['groundHit'])} → {fmt_pct(b['groundHit'])}")
        print(f"    wall P50/P95 {fmt(a['wallP50'],1)}/{fmt(a['wallP95'],1)} → "
              f"{fmt(b['wallP50'],1)}/{fmt(b['wallP95'],1)} | yolo P50 {fmt(a['yoloP50'],1)} → "
              f"{fmt(b['yoloP50'],1)} | sp P50 {fmt(a['spP50'],1)} → {fmt(b['spP50'],1)} | "
              f"fusion P50 {fmt(a['fusionP50'],1)} → {fmt(b['fusionP50'],1)} | "
              f"RSS {fmt(a['rssMax'],1)} → {fmt(b['rssMax'],1)}")
    # per-stratum
    print()
    print("## per-stratum element recall（v1 → v2）")
    strata_all = sorted({s for arm in ARMS for s in v1["perStratum"].get(arm, {})})
    for arm in ARMS:
        s1 = v1["perStratum"].get(arm, {})
        s2 = v2["perStratum"].get(arm, {})
        cells = []
        for st in strata_all:
            e1 = (s1.get(st) or {}).get("elements")
            e2 = (s2.get(st) or {}).get("elements")
            r1 = e1.get("recall") if e1 else None
            r2 = e2.get("recall") if e2 else None
            cells.append(f"{st}:{fmt(r1)}→{fmt(r2)}")
        print(f"{arm}: " + " | ".join(cells))
    # rescue / 独有
    gt_dir = REPORTS.parent / "validation" / "fastscreen-v1" / "gt"
    print()
    print("## rescue & 独有检出解剖（v2；帧明细重算，与 ANALYSIS.md 同法）")
    for ver, label in (("fsv001-v2", "v2"), ("fsv001", "v1")):
        a = rescue_and_unique_anatomy(ver, gt_dir)
        print(f"{label}: rescue 元素 {a['rescueTotal']}，GT 命中 {a['rescueGtHits']} "
              f"(命中率 {fmt_pct(a['rescueGtHits'] / a['rescueTotal'] if a['rescueTotal'] else None)})")
        print(f"    rescue 类别分布: {json.dumps(a['rescueByClass'], ensure_ascii=False)}")
        print(f"    B1 独有 {a['b1OnlyTotal']}，GT 命中 {a['b1OnlyGtHits']} "
              f"({fmt_pct(a['b1OnlyGtHits'] / a['b1OnlyTotal'] if a['b1OnlyTotal'] else None)})")
        print(f"    baseline 独有 {a['baseOnlyTotal']}，GT 命中 {a['baseOnlyGtHits']} "
              f"({fmt_pct(a['baseOnlyGtHits'] / a['baseOnlyTotal'] if a['baseOnlyTotal'] else None)})")
        print(f"    B1 独有类别: {json.dumps(a['b1OnlyByClass'], ensure_ascii=False)}")
        print(f"    screenParse.summary.droppedOffCanvas 全帧合计: {a['spDroppedTotal']}")


if __name__ == "__main__":
    import sys
    sys.path.insert(0, str(REPORTS.parent / "bench"))
    main()