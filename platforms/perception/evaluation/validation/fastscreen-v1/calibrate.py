#!/usr/bin/env python
"""FSV-001 Leader 校正（deterministic calibration）。

D6 修订（本会话 Leader 模型无图像输入，路由无视觉 tier）：以确定性
规则 + 交叉验证旗标替代视觉核对。原则：模型输出只用于**旗标/排除**，
绝不用于书写 GT 元素（避免循环性）；两条文本规则源自 a11y 元数据语义
（content-desc 非可见文字 / QS 合并串），非模型推断。

输入：gt/<id>.json × 39 + /tmp/fsv-calib-resp/<id>.json（baseline 响应）
输出：就地修订 GT（reviewStatus/provenance/knownBiases/text 规则）+
gt/CALIBRATION-REPORT.md + gt/calibration-decisions.json
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

VAL = Path(__file__).resolve().parent
GT = VAL / "gt"
RESP = Path("/tmp/fsv-calib-resp")

SWITCH_MERGED = re.compile(r"^(.*), (On|Off)$")


def iou(a, b):
    x1, y1 = max(a[0], b[0]), max(a[1], b[1])
    x2, y2 = min(a[2], b[2]), min(a[3], b[3])
    inter = max(0.0, x2 - x1) * max(0.0, y2 - y1)
    ua = (max(0.0, a[2] - a[0]) * max(0.0, a[3] - a[1])
          + max(0.0, b[2] - b[0]) * max(0.0, b[3] - b[1])) - inter
    return inter / ua if ua > 0 else 0.0


def norm_text(t):
    return re.sub(r"\s+", " ", (t or "").strip()).lower()


def main():
    decisions = []
    stats = {"frames": 0, "r1_stripped": 0, "r2_nulled": 0,
             "excluded": [], "flagged": []}
    for gt_path in sorted(GT.glob("*.json")):
        if gt_path.name == "calibration-decisions.json":
            continue
        fid = gt_path.stem
        gt = json.loads(gt_path.read_text())
        if "elements" not in gt:  # 非帧 GT 文件防御
            continue
        resp_path = RESP / f"{fid}.json"
        resp = json.loads(resp_path.read_text()) if resp_path.exists() else None

        # ── 规则修正（确定性，源自 a11y 语义）──
        applied = []
        for el in gt["elements"]:
            # R1: QS tile a11y 合并串 "Bluetooth, On" → 可见标题 "Bluetooth"
            if el["gtClass"] in ("switch", "toggle") and el.get("text"):
                m = SWITCH_MERGED.match(el["text"].strip())
                if m and m.group(1).strip():
                    el["text"] = m.group(1).strip()
                    stats["r1_stripped"] += 1
                    applied.append(f"R1:{el['gtId']}")
            # R2: icon/image/slider 的 text 来自 content-desc（非可见文字）→ null
            if el["gtClass"] in ("icon", "image", "slider") and el.get("text"):
                el["text"] = None
                stats["r2_nulled"] += 1
                applied.append(f"R2:{el['gtId']}")

        # ── 交叉验证旗标（模型输出只旗标，不书写）──
        flags = []
        review = "calibrated"
        reason = None
        if resp is not None:
            W, H = 1080, 2400
            ocr_texts = {norm_text(t.get("text")) for t in resp.get("ocr", [])
                         if norm_text(t.get("text"))}
            gt_texts = {norm_text(e.get("text")) for e in gt["elements"]
                        if e.get("text")} | {norm_text(x) for x in
                        gt.get("expectedTexts", [])}
            gt_texts.discard("")
            ocr_hit = (len(ocr_texts & gt_texts) / len(ocr_texts)
                       if ocr_texts else None)
            gt_hit = (len(ocr_texts & gt_texts) / len(gt_texts)
                      if gt_texts else None)

            yolo_boxes = [d["boundsPx"] for d in resp.get("yolo", [])]
            gt_boxes = [[round(e["bounds"]["x1"] * W), round(e["bounds"]["y1"] * H),
                         round(e["bounds"]["x2"] * W), round(e["bounds"]["y2"] * H)]
                        for e in gt["elements"]]
            yolo_cov = (sum(1 for yb in yolo_boxes
                            if any(iou(yb, gb) >= 0.3 for gb in gt_boxes))
                        / len(yolo_boxes) if yolo_boxes else None)
            gt_cov = (sum(1 for gb in gt_boxes
                          if any(iou(gb, yb) >= 0.3 for yb in yolo_boxes))
                      / len(gt_boxes) if gt_boxes else None)

            # 排除判定（v2，归因修正后）：
            # - stale-dump：仅非 dialog 帧（dialog 的 GT 按设计不含被遮挡
            #   背景，OCR 读到背景文本导致低命中是预期而非 stale）且
            #   OCR 可见文本与 GT 严重不符（<0.30，样本 ≥4）。
            # - 撤销 v1 的 count-ratio partial 排除：YOLO text_block（文字
            #   extent）vs GT list_item（整行 extent）的框语义差异使
            #   覆盖率/计数比成为混杂信号；欠完整 GT 对四臂对称，只旗标。
            stratum = (VAL / "frames" / f"{fid}.meta.json").exists() and \
                json.loads((VAL / "frames" / f"{fid}.meta.json").read_text()).get("stratum") or ""
            if (stratum != "dialog" and ocr_hit is not None
                    and len(ocr_texts) >= 4 and ocr_hit < 0.30):
                review, reason = "rejected-stale-dump", (
                    f"ocr-vs-gt text overlap {ocr_hit:.2f} < 0.30 "
                    f"({len(ocr_texts)} ocr texts)")
                flags.append(f"stale-dump:ocr_hit={ocr_hit:.2f}")
            else:
                if stratum == "dialog" and ocr_hit is not None and ocr_hit < 0.30:
                    flags.append(f"dialog-expected-low-ocrhit:{ocr_hit:.2f}")
                if gt_hit is not None and gt_hit < 0.4:
                    flags.append(f"low-gt-text-hit:{gt_hit:.2f}")
                if yolo_cov is not None and yolo_cov < 0.4:
                    flags.append(f"low-yolo-cov:{yolo_cov:.2f}")
                if (gt_cov is not None and gt_cov <= 0.25
                        and len(gt["elements"]) <= 3):
                    flags.append(f"suspect-partial-dump:gt_cov={gt_cov:.2f}")
            decisions.append({
                "frameId": fid, "stratum": gt.get("stratum"),
                "elements": len(gt["elements"]),
                "ocrTextOverlapVsGt": round(ocr_hit, 3) if ocr_hit is not None else None,
                "gtTextHitByOcr": round(gt_hit, 3) if gt_hit is not None else None,
                "yoloCovByGt": round(yolo_cov, 3) if yolo_cov is not None else None,
                "gtCovByYolo": round(gt_cov, 3) if gt_cov is not None else None,
                "appliedRules": applied, "flags": flags,
                "reviewStatus": review, **({"reason": reason} if reason else {}),
            })
        else:
            decisions.append({"frameId": fid, "elements": len(gt["elements"]),
                              "appliedRules": applied, "flags": ["no-response"],
                              "reviewStatus": "candidate"})

        gt["reviewStatus"] = review
        if reason:
            gt.setdefault("provenance", {})["exclusionReason"] = reason
        gt.setdefault("provenance", {})["annotator"] = \
            "wi4-agent + leader-deterministic-calibration"
        kb = gt.setdefault("provenance", {}).setdefault("knownBiases", [])
        if applied:
            kb.append("calibration rules applied: " + ", ".join(applied))
        stats["frames"] += 1
        if review.startswith("rejected"):
            stats["excluded"].append(f"{fid}({review})")
        if flags:
            stats["flagged"].append(f"{fid}:{'|'.join(flags)}")
        gt_path.write_text(json.dumps(gt, ensure_ascii=False, indent=1) + "\n")

    (GT / "calibration-decisions.json").write_text(
        json.dumps({"schema": "uniclaw.fsv001.calibration.v1",
                    "decisions": decisions}, ensure_ascii=False, indent=1))
    lines = [
        "# FSV-001 Leader 校正报告（deterministic calibration）", "",
        "方法：D6 修订——本会话无图像输入能力，采用确定性规则修正（R1 QS "
        "合并串剥离 / R2 icon-image-slider 的 content-desc 文本置 null）+ "
        "baseline 交叉旗标（OCR 文本重叠 / YOLO-GT 覆盖）。模型输出仅用于"
        "旗标与排除，不用于书写 GT。", "",
        f"- 帧数：{stats['frames']}",
        f"- R1 剥离：{stats['r1_stripped']}；R2 置 null：{stats['r2_nulled']}",
        f"- 排除：{stats['excluded'] or '无'}",
        f"- 旗标（未排除）：", "",
    ]
    lines += [f"  - {f}" for f in stats["flagged"]] or ["  - 无"]
    (GT / "CALIBRATION-REPORT.md").write_text("\n".join(lines) + "\n")
    print(json.dumps({"ok": True, **{k: v for k, v in stats.items()}},
                     ensure_ascii=False, indent=1))


if __name__ == "__main__":
    sys.exit(main())
