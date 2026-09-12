#!/usr/bin/env python
"""FSV-001 D3 — B2 消融（replacement + OCR 关闭）。

进程内直接复刻 server._run_pipeline 的调用序（读 server.py 实现）：唯一干预 =
把 recognize 的 rapidocr 入口（run_rapid_ocr_on_image / rapid_ocr_one_crop）
替换为恒空——B2 语义 = detector 用 ScreenParser、recognize 关闭（ocr_tokens=[]）；
fuse(空 OCR) + remap/enforce_geometry + envelope 组装（含 metadata/summary/
screenParse[]）与 B1 走同一代码路径（同源），因此 ocr[] 恒空、candidates 无
文字、promote_unmatched_ocr 无从发生（无 unmatched OCR token 可晋升）。

输出：`--out` 写入与 `/v1/analyze` 响应 body 同构的 JSON（json.dumps(evidence,
ensure_ascii=False)，含 metadata 身份字段）；stdout 打印 B2 摘要（summary +
screenParse summary + candidates 无文字检查 + 分段计时）。Server-Timing 段
语义与 B1 保持一致（screenparser 推理计入 yolo;dur；无独立 screenparse 段）。

用法：
  python bench/run_replacement_ablation.py --image <png> --out <json> [--device cpu]
--device 选择 replacement 变体（cpu → fastscreen-replacement [impl=screenparser]；
mps → fastscreen-replacement-mps [impl=screenparser-mps]）——device 即变体
（照 torch-yolo/torch-mps 双胞胎语义；device 推导唯一真相源 = pipeline.py
DETECT_IMPL_DEVICE）。
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

_VARIANT_BY_DEVICE = {
    "cpu": "fastscreen-replacement",
    "mps": "fastscreen-replacement-mps",
}


def _ablate_ocr(server: Any) -> None:
    """B2：recognize 入口替换为恒空（不跑 OCR 模型）。

    覆盖 full-image（run_rapid_ocr_on_image，并行路径）与 roi 单 crop
    （rapid_ocr_one_crop）两处调用点。paddle 回退路径不属于 FSV-001 评测
    backend（评测/基线均 rapidocr/full），显式断言防静默走错路径。
    """
    cfg = server._get_config()
    if cfg.ocr_backend != "rapidocr":
        raise SystemExit(
            "B2 消融仅定义于 rapidocr backend（FSV-001 评测后端 = rapidocr/full）")
    server.run_rapid_ocr_on_image = lambda *a, **kw: []
    server.rapid_ocr_one_crop = lambda *a, **kw: []


def main() -> int:
    parser = argparse.ArgumentParser(
        description="FSV-001 B2 消融：detect=screenparser + OCR 关闭（进程内）")
    parser.add_argument("--image", required=True, help="输入 PNG（全屏帧）")
    parser.add_argument("--out", default=None, help="输出 JSON 路径（响应同构）")
    parser.add_argument("--device", choices=["cpu", "mps"], default="cpu",
                        help="推理 device（= replacement 变体选择；默认 cpu 主表）")
    args = parser.parse_args()

    import uniclaw_perception.server as server
    from uniclaw_perception import identity, pipeline
    from uniclaw_perception.config import load as load_config
    from uniclaw_perception.health import _model_id

    cfg = load_config()
    server._config = cfg

    variant_name = _VARIANT_BY_DEVICE[args.device]
    variants = pipeline.load_variants()
    if variant_name not in variants:
        print(f"unknown variant: {variant_name!r} "
              f"(declared: {sorted(variants)})", file=sys.stderr)
        return 2
    pipeline_config = variants[variant_name].config
    # 启动期 lint 语义（照 server.lifespan）：mps 变体在无 MPS 机器上 fail-closed
    pipeline.lint_against_config(pipeline_config, cfg)

    # lifespan 等价身份注入（照 tests/test_screenparse_integration._build_registry）：
    # 响应 metadata 携带与真实服务同构的四层身份（configId/pipelineRevision/
    # deploymentId/pipelineVariant）。
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

    # OCR 配置（照 server.lifespan 的 rapidocr 分支）：B2 恒空不跑模型，仍保持
    # 服务启动语义完整（B1 对拍的 ocrCount=0 不依赖此项，但注册表一致性照抄）。
    if cfg.ocr_backend == "rapidocr":
        from uniclaw_perception.ocr.rapid import (
            _rapid_ocr_kwargs, configure_ocr_models)
        _rapid_ocr_kwargs.update(configure_ocr_models(language=cfg.ocr_lang))

    _ablate_ocr(server)

    image_path = Path(args.image)
    if not image_path.exists():
        print(f"image not found: {image_path}", file=sys.stderr)
        return 2
    image = Image.open(image_path).convert("RGB")
    width, height = image.size

    wall_start = time.perf_counter()
    evidence, (t0, t1, t2, t3, t_sp) = server._run_pipeline(
        image, width, height, pipeline=pipeline_config,
        pipeline_key=variant_name)
    wall_ms = (time.perf_counter() - wall_start) * 1000

    body = json.dumps(evidence, ensure_ascii=False)
    if args.out:
        Path(args.out).write_text(body, encoding="utf-8")
        written = f"json → {args.out}"
    else:
        written = "...\n(json 到 --out 路径；stdout 仅摘要)"

    summary = evidence["summary"]
    sp_summary = (evidence.get("screenParse") or {}).get("summary")
    print(json.dumps({
        "arm": "B2",
        "image": str(image_path),
        "device": args.device,
        "variant": variant_name,
        "summary": summary,
        "screenParseSummary": sp_summary,
        "ocrEmpty": evidence["ocr"] == [],
        "candidatesAllEmptyText": all(
            c.get("text", "") == "" for c in evidence["candidates"]),
        "timingMs": {
            "yolo": round((t1 - t0) * 1000, 1),
            "ocr": round((t2 - t1) * 1000, 1),
            "fusion": round(((t3 - t_sp) * 1000 if t_sp is not None
                             else (t3 - t2) * 1000), 1),
            "wall": round(wall_ms, 1),
        },
    }, ensure_ascii=False, indent=2))
    print(written)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())