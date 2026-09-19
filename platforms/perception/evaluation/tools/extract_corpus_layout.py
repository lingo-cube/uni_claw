#!/usr/bin/env python
"""Export RAW perception recognition (YOLO detections + OCR tokens) for corpus
screenshots — no fusion, no clustering, no classification layer.

Intended consumer: downstream experiments that want the raw position+text
evidence — e.g. optimizing the fusion/clustering algorithm, or feeding YOLO+OCR
output into an LLM as slow perception.

Pipeline stages are still executed in-process (same pattern as
bench/run_l2.py); only the RAW outputs are persisted:
  - yolo[]: model detections, original full-screen coordinates
    (normalized bounds + pixel bounds + center), normalized label + raw
    model label + raw class id.
  - ocr[]: OCR tokens, original full-screen coordinates, text + confidence.
Fused/clustered candidates are discarded — nothing is classified here.

Usage (repo root):
  .perception/venv/bin/python \
      platforms/perception/evaluation/tools/extract_corpus_layout.py \
      --assets-dir tests/UniClaw.Kernel.Tests/Perception/Corpus/artifacts \
      --out-dir evidence/2026-09-13-corpus-raw-recognition/artifacts
"""
from __future__ import annotations

import argparse
import json
import sys
import time
from pathlib import Path

_PKG_ROOT = Path(__file__).resolve().parent.parent.parent  # platforms/perception/
sys.path.insert(0, str(_PKG_ROOT))

from PIL import Image  # noqa: E402

from uniclaw_perception import identity, pipeline  # noqa: E402
from uniclaw_perception.config import load as load_config  # noqa: E402
import uniclaw_perception.server as server  # noqa: E402


def _raw_yolo(evidence: dict, views: dict) -> list[dict]:
    """yolo[] from evidence (remapped original-space) merged with the raw
    model label/class id from the stage views (raw provenance fields are only
    carried there)."""
    raw_by_id = {d["id"]: d for d in views.get("rawModelDetections", [])}
    out = []
    for det in evidence.get("yolo", []):
        raw = raw_by_id.get(det.get("id"), {})
        out.append({
            **det,
            "rawLabel": raw.get("rawLabel"),
            "rawClassId": raw.get("rawClassId"),
        })
    return out


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--assets-dir", required=True, help="corpus artifacts dir")
    parser.add_argument("--out-dir", required=True, help="output dir for JSONs")
    parser.add_argument("--scenarios", default=None,
                        help="comma-separated scenario ids to extract; default: all PNGs")
    args = parser.parse_args()

    assets_dir = Path(args.assets_dir)
    out_dir = Path(args.out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    pngs = sorted(assets_dir.glob("*.png"))
    if not pngs:
        print(f"no PNGs in {assets_dir}", file=sys.stderr)
        return 2
    if args.scenarios:
        wanted = set(args.scenarios.split(","))
        pngs = [p for p in pngs if p.stem in wanted]
        missing = wanted - {p.stem for p in pngs}
        if missing:
            print(f"unknown scenarios: {sorted(missing)}", file=sys.stderr)
            return 2

    # ── lifespan-equivalent state (bench/run_l2.py precedent) ──
    cfg = load_config()
    default = pipeline.load_default()
    server._config = cfg
    server._pipelines = {"default": (default, {})}
    from uniclaw_perception import __version__
    from uniclaw_perception.health import capture_identity, _model_id
    capture_identity()
    revision = identity.compute_pipeline_revision()
    config_id = identity.build_config_id(
        cfg, pipeline.identity_content(default, None))
    deployment_id = identity.compute_deployment_id(
        "uniclaw.localVisionEvidence.v1", _model_id(), config_id,
        revision["pipelineRevision"])
    run_identity = {
        "deploymentId": deployment_id,
        "configId": config_id,
        "pipelineRevision": revision["pipelineRevision"],
        "pipelineRevisionComplete": revision["complete"],
        "pipelineVariant": "default",
        "packageVersion": __version__,
        "yoloModel": cfg.model_path,
        "ocrBackend": cfg.ocr_backend,
        "ocrLang": cfg.ocr_lang,
    }

    manifest = {
        "schema": "uniclaw.corpusRawRecognition.v1",
        "identity": run_identity,
        "scenarios": [],
    }
    for idx, png in enumerate(pngs):
        image = Image.open(png).convert("RGB")
        width, height = image.size
        started = time.perf_counter()
        evidence, (t0, t1, t2, t3, t_sp), views = server._run_pipeline(
            image, width, height, pipeline=default, pipeline_key="default",
            capture_stage_views=True)
        wall_ms = (time.perf_counter() - started) * 1000

        raw = {
            "schema": "uniclaw.yoloOcrRaw.v1",
            "scenarioId": png.stem,
            "artifact": str(png),
            "frame": {"width": width, "height": height},
            "sample": {
                "yoloDetections": len(evidence.get("yolo", [])),
                "ocrTokens": len(evidence.get("ocr", [])),
            },
            "yolo": _raw_yolo(evidence, views),
            "ocr": evidence.get("ocr", []),
        }
        out_path = out_dir / f"{png.stem}.yolo-ocr.raw.json"
        out_path.write_text(json.dumps(raw, ensure_ascii=False, indent=2),
                            encoding="utf-8")

        yolo_labels: dict[str, int] = {}
        for d in raw["yolo"]:
            yolo_labels[d["label"]] = yolo_labels.get(d["label"], 0) + 1
        manifest["scenarios"].append({
            "scenarioId": png.stem,
            "artifact": str(png),
            "width": width, "height": height,
            "stagesMs": {
                "yolo": round((t1 - t0) * 1000, 1),
                "ocr": round((t2 - t1) * 1000, 1),
                "fusion": round(((t3 - t_sp) * 1000 if t_sp is not None
                                 else (t3 - t2) * 1000), 1),
            },
            "wallMs": round(wall_ms, 1),
            "yoloDetections": len(raw["yolo"]),
            "yoloLabelCounts": yolo_labels,
            "ocrTokens": len(raw["ocr"]),
            "output": out_path.name,
        })
        print(f"[{idx + 1}/{len(pngs)}] {png.stem}: yolo={len(raw['yolo'])} "
              f"ocr={len(raw['ocr'])} wall {wall_ms:.0f}ms -> {out_path.name}")

    manifest_path = out_dir / "run-manifest.json"
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2),
                             encoding="utf-8")
    print(f"manifest -> {manifest_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())