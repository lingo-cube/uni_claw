#!/usr/bin/env python3
"""CLI tool: run perception pipeline on a single image and emit evidence JSON.

Uses the uniclaw_perception production package. Developer tooling — NOT the
production service path (that's server.py, launched by VisionServiceHost).
"""
from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image

from uniclaw_perception.yolo.inference import run_yolo_on_image
from uniclaw_perception.ocr.rapid import run_rapid_ocr_on_image
from uniclaw_perception.ocr.paddle import run_ocr_on_crops
from uniclaw_perception.fusion.engine import fuse_evidence, fuse_evidence_from_crops
from uniclaw_perception.schema import Detection, OcrToken, Box
from uniclaw_perception.config import load as load_config


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Run local YOLO + OCR and emit UniClaw evidence JSON."
    )
    parser.add_argument("--image", required=True, type=Path, help="Input screenshot path.")
    parser.add_argument("--out", type=Path, help="Output evidence JSON path.")
    parser.add_argument("--yolo-model", default=None, help="YOLO model path (default: package default).")
    parser.add_argument("--yolo-json", type=Path, help="Use precomputed YOLO detections JSON.")
    parser.add_argument("--ocr-json", type=Path, help="Use precomputed OCR token JSON.")
    parser.add_argument("--ocr-backend", default="rapidocr",
                        choices=["rapidocr", "paddleocr"],
                        help="OCR backend (default: rapidocr).")
    parser.add_argument("--promote-unmatched-ocr", action="store_true",
                        help="Promote OCR-only text into text_block candidates.")
    args = parser.parse_args()

    # Load config (for defaults)
    try:
        load_config()
    except Exception:
        pass  # CLI may run without full config; use defaults

    image_path = args.image.resolve()
    if not image_path.exists():
        raise FileNotFoundError(image_path)

    with Image.open(image_path) as image:
        width, height = image.size

    # YOLO
    if args.yolo_json:
        detections = _load_detections_json(args.yolo_json)
    else:
        detections = run_yolo_on_image(
            image,
            model_path=args.yolo_model,
        )

    # OCR
    if args.ocr_json:
        ocr_tokens = _load_ocr_json(args.ocr_json)
    elif args.ocr_backend == "rapidocr":
        ocr_tokens = run_rapid_ocr_on_image(image)
    else:
        ocr_tokens_list = run_ocr_on_crops(image, detections)
        # paddleocr per-crop path returns aligned lists
        all_tokens = []
        for tokens in ocr_tokens_list:
            all_tokens.extend(tokens)
        ocr_tokens = all_tokens

    evidence = fuse_evidence(
        detections, ocr_tokens,
        image_width=width, image_height=height,
        promote_unmatched_ocr=args.promote_unmatched_ocr,
    )
    evidence["metadata"] = _metadata(args, image_path)

    output = json.dumps(evidence, ensure_ascii=False, indent=2)
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(output + "\n", encoding="utf-8")
    else:
        print(output)
    return 0


def _load_detections_json(path: Path) -> list[Detection]:
    data = json.loads(path.read_text(encoding="utf-8"))
    records = data.get("detections", data if isinstance(data, list) else [])
    detections: list[Detection] = []
    for index, record in enumerate(records, start=1):
        bounds = record.get("boundsPx") or record.get("box") or record.get("bounds")
        if bounds is None:
            raise ValueError(f"detection record missing bounds: {record!r}")
        detections.append(Detection(
            id=str(record.get("id") or f"det_{index}"),
            label=str(record.get("label") or record.get("class") or record.get("type")),
            confidence=float(record.get("confidence", record.get("conf", 1.0))),
            box=Box.from_list(bounds),
        ))
    return detections


def _load_ocr_json(path: Path) -> list[OcrToken]:
    data = json.loads(path.read_text(encoding="utf-8"))
    records = data.get("tokens", data.get("ocr", data if isinstance(data, list) else []))
    tokens: list[OcrToken] = []
    for index, record in enumerate(records, start=1):
        bounds = record.get("boundsPx") or record.get("box") or record.get("bounds")
        if bounds is None:
            raise ValueError(f"OCR record missing bounds: {record!r}")
        tokens.append(OcrToken(
            id=str(record.get("id") or f"ocr_{index}"),
            text=str(record.get("text", "")),
            confidence=float(record.get("confidence", record.get("conf", 1.0))),
            box=Box.from_list(bounds),
        ))
    return tokens


def _metadata(args: argparse.Namespace, image_path: Path) -> dict[str, Any]:
    return {
        "schema": "uniclaw.localVisionEvidence.v1",
        "createdAt": datetime.now(timezone.utc).isoformat(),
        "imagePath": str(image_path),
        "yolo": {
            "source": "json" if args.yolo_json else "ultralytics",
            "model": str(args.yolo_json or args.yolo_model or "default"),
        },
        "ocr": {
            "source": "json" if args.ocr_json else args.ocr_backend,
        },
    }


if __name__ == "__main__":
    raise SystemExit(main())
