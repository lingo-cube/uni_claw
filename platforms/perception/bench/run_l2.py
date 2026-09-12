#!/usr/bin/env python
"""PER-008 D6/A5 — L2 录屏基准：全生产管线 in-process 跑固定帧（非真机）。

复用 uni-agent evaluation 的核心语义：新鲜推理（历史 JSON 不作预测）、
per-stage 计时、PerformanceResult 分位守门（raw samples 恒记录；n≥10 才报
p50/p95；n≥100 才报 p99；median/mean 恒有）+ 输出哈希（性能/正确性双锚）。

用法：
  python bench/run_l2.py [--image PATH] [--runs N] [--variant NAME] [--out FILE]
默认帧 = repo corpus golden-run-v1 case-a-before.png；默认 runs=12（2 warmup
+ 10 采样 → p50/p95 达守门线）。
"""
from __future__ import annotations

import argparse
import json
import statistics
import sys
import time
from pathlib import Path

_PKG_ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(_PKG_ROOT))

from PIL import Image  # noqa: E402

from uniclaw_perception import identity, pipeline  # noqa: E402
from uniclaw_perception.config import load as load_config  # noqa: E402

_DEFAULT_IMAGE = _PKG_ROOT.parent.parent / (
    "tests/UniClaw.Kernel.Tests/Perception/Corpus/legacy-direct/"
    "golden-run-v1/case-a-before.png")


def _percentiles(values: list[float]) -> dict[str, float | None]:
    """分位守门（B13/B14 语义）。"""
    n = len(values)
    out: dict[str, float | None] = {
        "n": n,
        "medianMs": round(statistics.median(values), 2),
        "meanMs": round(statistics.fmean(values), 2),
        "p50Ms": None, "p95Ms": None, "p99Ms": None,
        "gates": {"p50p95": n >= 10, "p99": n >= 100},
    }
    if n >= 10:
        out["p50Ms"] = round(statistics.quantiles(values, n=20)[9], 2)
        out["p95Ms"] = round(statistics.quantiles(values, n=20)[18], 2)
    if n >= 100:
        out["p99Ms"] = round(statistics.quantiles(values, n=100)[98], 2)
    return out


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--image", default=str(_DEFAULT_IMAGE))
    parser.add_argument("--runs", type=int, default=12)
    parser.add_argument("--variant", default=None)
    parser.add_argument("--out", default=None)
    parser.add_argument("--gc-frequency", type=int, default=1,
                        help="每 N 帧做一次 gc.collect（0=禁用；OPT-001 S4 实验开关）")
    args = parser.parse_args()

    import uniclaw_perception.server as server

    cfg = load_config()
    pipeline_key = "default"
    if args.variant:
        variants = pipeline.load_variants()
        if args.variant not in variants:
            print(f"unknown variant: {args.variant!r}", file=sys.stderr)
            return 2
        pipeline_key = args.variant
        pipeline_config = variants[args.variant].config
    else:
        pipeline_config = pipeline.load_default()

    # 注入 lifespan 等效状态（L2 runner 先例：模拟服务启动后的模块单例）
    from uniclaw_perception.health import capture_identity, _model_id
    server._config = cfg
    server._pipelines = {pipeline_key: (pipeline_config, {})}
    capture_identity()
    revision = identity.compute_pipeline_revision()
    config_id = identity.build_config_id(
        cfg, pipeline.identity_content(pipeline_config,
                                       None if pipeline_key == "default" else pipeline_key))
    deployment_id = identity.compute_deployment_id(
        "uniclaw.localVisionEvidence.v1", _model_id(), config_id,
        revision["pipelineRevision"])

    image_path = Path(args.image)
    image = Image.open(image_path).convert("RGB")
    width, height = image.size

    warmup = 2
    if args.gc_frequency != 1:
        import uniclaw_perception.server as _server_mod
        _orig_gc = _server_mod.gc.collect
        _counter = {"n": 0}
        def _gated_gc():
            _counter["n"] += 1
            if args.gc_frequency == 0:
                return
            if _counter["n"] % args.gc_frequency == 0:
                _orig_gc()
        _server_mod.gc.collect = _gated_gc
    stage_ms: dict[str, list[float]] = {"yolo": [], "ocr": [], "fusion": []}
    total_ms: list[float] = []
    output_hashes: set[str] = set()
    for i in range(max(warmup, 0) + args.runs):
        start = time.perf_counter()
        evidence, (t0, t1, t2, t3, t_sp) = server._run_pipeline(
            image, width, height,
            pipeline=pipeline_config, pipeline_key=pipeline_key)
        if i >= warmup:
            stage_ms["yolo"].append((t1 - t0) * 1000)
            stage_ms["ocr"].append((t2 - t1) * 1000)
            # FSV-001：screenparse 作为独立串行段，fusion 从其结束边界起算
            # （变体关闭时 t_sp = None → 沿用 t3 - t2 语义）。
            stage_ms["fusion"].append(
                ((t3 - t_sp) * 1000 if t_sp is not None else (t3 - t2) * 1000))
            total_ms.append((time.perf_counter() - start) * 1000)
            output_hashes.add(identity.canonical_hash(
                {k: evidence.get(k) for k in ("yolo", "ocr", "candidates")}))

    report = {
        "schema": "uniclaw.perceptionBench.v1",
        "identity": {
            "deploymentId": deployment_id,
            "configId": config_id,
            "pipelineRevision": revision["pipelineRevision"],
            "pipelineRevisionComplete": revision["complete"],
            "pipelineVariant": pipeline_key,
        },
        "input": {
            "file": str(image_path),
            "sha256": identity.sha256_file(image_path),
            "width": width, "height": height,
        },
        "samples": args.runs, "warmup": warmup,
        "gcFrequency": args.gc_frequency,
        "outputHash": (output_hashes.pop() if len(output_hashes) == 1
                       else {"distinct": len(output_hashes)}),
        "stagesMs": {k: _percentiles(v) for k, v in stage_ms.items()},
        "totalMs": _percentiles(total_ms),
    }
    text = json.dumps(report, ensure_ascii=False, indent=2)
    if args.out:
        Path(args.out).write_text(text, encoding="utf-8")
        print(f"report → {args.out}")
    else:
        print(text)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
