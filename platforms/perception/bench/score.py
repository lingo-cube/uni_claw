#!/usr/bin/env python
"""OPT-001 S3 — 评分器（matcher 移植 + task metrics + scorecard）。

Matcher 语义 = uni-agent evaluation/matcher.py 的 greedy one-to-one IoU
（MATCHER_REVISION = matcher-greedy-v1，class compatibility + IoU ≥ 0.5）。
Task metrics 按 GT 可支撑面计算（缺 GT → NOT_SCORABLE，永不为零）。
产出 scorecard JSON，结构与历史基线（reports/baselines/）的 qualityScorecard
段同口径可对照。

用法：
  python bench/score.py [--asset-dir evaluation/assets] [--out FILE]
"""
from __future__ import annotations

import argparse
import json
import sys
import time
from pathlib import Path
from typing import Any

_PKG_ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(_PKG_ROOT))

from PIL import Image  # noqa: E402

from uniclaw_perception import identity, pipeline  # noqa: E402
from uniclaw_perception.config import load as load_config  # noqa: E402

MATCHER_REVISION = "matcher-greedy-v1"
IOU_THRESHOLD = 0.5
SCORER_REVISION = "scorer-v1"


def _iou(a: tuple, b: tuple) -> float:
    x1 = max(a[0], b[0]); y1 = max(a[1], b[1])
    x2 = min(a[2], b[2]); y2 = min(a[3], b[3])
    inter = max(0.0, x2 - x1) * max(0.0, y2 - y1)
    if inter <= 0:
        return 0.0
    area_a = max(0.0, a[2] - a[0]) * max(0.0, a[3] - a[1])
    area_b = max(0.0, b[2] - b[0]) * max(0.0, b[3] - b[1])
    union = area_a + area_b - inter
    return inter / union if union > 0 else 0.0


def match(predictions: list[dict], gt_elements: list[dict],
          iou_threshold: float = IOU_THRESHOLD) -> dict[str, Any]:
    """Greedy one-to-one：class 兼容 + IoU ≥ threshold，最高 IoU 优先。"""
    candidates: list[tuple[float, int, int]] = []
    for pi, p in enumerate(predictions):
        pb = p.get("bounds")
        if pb is None:
            continue
        for gi, g in enumerate(gt_elements):
            gb = g.get("bounds")
            if gb is None:
                continue
            if p.get("type") != g.get("gtClass"):
                continue
            score = _iou(tuple(pb), tuple(gb))
            if score >= iou_threshold:
                candidates.append((score, pi, gi))
    candidates.sort(key=lambda t: (-t[0], t[0 + 1], t[0 + 2]))
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


def task_metrics(match_result: dict, gt: dict) -> dict[str, Any]:
    """按 GT 可支撑面计算（缺 GT → NOT_SCORABLE，永不为零）。"""
    tp = match_result["tp"]
    fp = match_result["fp"]
    fn = match_result["fn"]
    precision = tp / (tp + fp) if (tp + fp) > 0 else None
    recall = tp / (tp + fn) if (tp + fn) > 0 else None
    f1 = (2 * precision * recall / (precision + recall)
          if precision and recall else None)
    return {
        "task": "ELEMENT_DETECTION",
        "stance": "SCORED" if gt["elements"] else "NOT_SCORABLE",
        "tp": tp, "fp": fp, "fn": fn,
        "precision": round(precision, 4) if precision is not None else None,
        "recall": round(recall, 4) if recall is not None else None,
        "f1": round(f1, 4) if f1 is not None else None,
        "denominator": tp + fn,
        "note": "" if gt["elements"] else "GT 无 elements",
    }


def extract_predictions(response: dict) -> list[dict]:
    """服务响应 → matcher 输入格式（normalized bounds）。"""
    return [
        {"type": det["label"], "bounds": (
            det["bounds"]["x1"], det["bounds"]["y1"],
            det["bounds"]["x2"], det["bounds"]["y2"])}
        for det in response.get("yolo", [])
    ]


def run_in_process(image_path: Path) -> dict[str, Any]:
    """In-process 全管线推理（与 bench/run_l2.py 同法）。"""
    import uniclaw_perception.server as server
    cfg = load_config()
    default = pipeline.load_default()
    server._config = cfg
    server._pipelines = {"default": (default, {})}
    from uniclaw_perception.health import capture_identity, _model_id
    capture_identity()
    revision = identity.compute_pipeline_revision()
    config_id = identity.build_config_id(
        cfg, pipeline.identity_content(default, None))
    deployment_id = identity.compute_deployment_id(
        "uniclaw.localVisionEvidence.v1", _model_id(), config_id,
        revision["pipelineRevision"])
    image = Image.open(image_path).convert("RGB")
    width, height = image.size
    evidence, _ = server._run_pipeline(image, width, height, pipeline=default)
    evidence["_deploymentId"] = deployment_id
    return evidence


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--asset-dir", default=str(_PKG_ROOT / "evaluation" / "assets"))
    parser.add_argument("--out", default=None)
    args = parser.parse_args()
    asset_dir = Path(args.asset_dir)

    gt_files = sorted((asset_dir / "groundtruth").glob("gt-*-v*.json"))
    if not gt_files:
        print("no groundtruth files found", file=sys.stderr)
        return 1

    asset_results = []
    for gt_path in gt_files:
        gt = json.loads(gt_path.read_text())
        asset_hash = gt["assetId"][7:]  # strip "sha256:"

        # 找对应字节（captures 或 fixtures）
        image_path = None
        for search in [asset_dir / "fixtures", asset_dir / "captures"]:
            for png in search.rglob("*.png"):
                import hashlib
                if hashlib.sha256(png.read_bytes()).hexdigest() == asset_hash:
                    image_path = png
                    break
            if image_path:
                break
        if image_path is None:
            asset_results.append({
                "assetId": gt["assetId"],
                "status": "ASSET_BYTES_MISSING",
                "gtVersion": gt.get("gtVersion", "1"),
            })
            continue

        response = run_in_process(image_path)
        predictions = extract_predictions(response)
        match_result = match(predictions, gt["elements"])
        metrics = task_metrics(match_result, gt)
        asset_results.append({
            "assetId": gt["assetId"],
            "assetPath": str(image_path),
            "gtVersion": gt.get("gtVersion", "1"),
            "gtSource": gt.get("source", ""),
            "status": "SCORED" if gt["elements"] else "NOT_SCORABLE",
            "deploymentId": response.get("_deploymentId", ""),
            "match": match_result,
            "taskResults": [metrics],
        })

    scored = [r for r in asset_results if r["status"] == "SCORED"]
    not_scorable = [r for r in asset_results if r["status"] == "NOT_SCORABLE"]
    missing = [r for r in asset_results if r["status"] == "ASSET_BYTES_MISSING"]

    # 汇总（仅 SCORED 进 aggregate；NOT_SCORABLE 如实计数）
    if scored:
        agg_tp = sum(r["taskResults"][0]["tp"] for r in scored)
        agg_fp = sum(r["taskResults"][0]["fp"] for r in scored)
        agg_fn = sum(r["taskResults"][0]["fn"] for r in scored)
        agg_p = agg_tp / (agg_tp + agg_fp) if (agg_tp + agg_fp) else None
        agg_r = agg_tp / (agg_tp + agg_fn) if (agg_tp + agg_fn) else None
        agg_f1 = (2 * agg_p * agg_r / (agg_p + agg_r)
                  if agg_p and agg_r else None)
    else:
        agg_p = agg_r = agg_f1 = None
        agg_tp = agg_fp = agg_fn = 0

    scorecard = {
        "scorecardSchemaVersion": "uniclaw.qualityScorecard.v2",
        "scorerRevision": SCORER_REVISION,
        "matcherRevision": MATCHER_REVISION,
        "iouThreshold": IOU_THRESHOLD,
        "assetCount": len(asset_results),
        "scoredCount": len(scored),
        "notScorableCount": len(not_scorable),
        "missingCount": len(missing),
        "aggregate": {
            "task": "ELEMENT_DETECTION",
            "tp": agg_tp, "fp": agg_fp, "fn": agg_fn,
            "precision": round(agg_p, 4) if agg_p is not None else None,
            "recall": round(agg_r, 4) if agg_r is not None else None,
            "f1": round(agg_f1, 4) if agg_f1 is not None else None,
        },
        "assets": asset_results,
    }

    text = json.dumps(scorecard, ensure_ascii=False, indent=2)
    if args.out:
        Path(args.out).write_text(text, encoding="utf-8")
        print(f"scorecard → {args.out}")
    else:
        print(text)

    # 与历史基线的对照输出
    print(f"\n=== 汇总（{len(scored)} scored / {len(not_scorable)} not-scorable / {len(missing)} missing）===")
    print(f"ELEMENT_DETECTION: P={agg_p:.3f} R={agg_r:.3f} F1={agg_f1:.3f}" if agg_p else "无 scored 资产")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
