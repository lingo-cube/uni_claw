from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image

from .backends import load_detections_json, load_ocr_json, run_paddle_ocr, run_rapid_ocr, run_yolo
from .fusion import fuse_evidence


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Run local YOLO + OCR and emit UniClaw evidence JSON."
    )
    parser.add_argument("--image", required=True, type=Path, help="Input screenshot path.")
    parser.add_argument("--out", type=Path, help="Output evidence JSON path.")
    parser.add_argument("--yolo-model", default="artifacts/local-vision/models/android_ui_detection_yolov8/best.pt", help="Ultralytics model path/name (default: Deki-Yolo).")
    parser.add_argument("--imgsz", default=640, type=int, help="YOLO input size.")
    parser.add_argument("--conf", default=0.35, type=float, help="YOLO confidence threshold.")
    parser.add_argument("--device", default="cpu", help="Ultralytics device, e.g. cpu or mps.")
    parser.add_argument("--ocr-lang", default="ch", help="PaddleOCR language, e.g. ch or en.")
    parser.add_argument(
        "--ocr-backend",
        default="paddleocr",
        choices=["paddleocr", "rapidocr"],
        help="OCR backend: paddleocr (Paddle Inference) or rapidocr (ONNX Runtime).",
    )
    parser.add_argument("--yolo-json", type=Path, help="Use precomputed YOLO detections JSON.")
    parser.add_argument("--ocr-json", type=Path, help="Use precomputed OCR token JSON.")
    parser.add_argument(
        "--promote-unmatched-ocr",
        action="store_true",
        help="Promote OCR-only text into low-confidence text_block candidates.",
    )
    args = parser.parse_args()

    image_path = args.image.resolve()
    if not image_path.exists():
        raise FileNotFoundError(image_path)

    with Image.open(image_path) as image:
        width, height = image.size

    detections = (
        load_detections_json(args.yolo_json)
        if args.yolo_json
        else run_yolo(
            image_path,
            model_path=args.yolo_model,
            image_size=args.imgsz,
            confidence=args.conf,
            device=args.device,
        )
    )
    if args.ocr_json:
        ocr_tokens = load_ocr_json(args.ocr_json)
    elif args.ocr_backend == "rapidocr":
        ocr_tokens = run_rapid_ocr(image_path)
    else:
        ocr_tokens = run_paddle_ocr(image_path, language=args.ocr_lang)

    evidence = fuse_evidence(
        detections,
        ocr_tokens,
        image_width=width,
        image_height=height,
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


def _metadata(args: argparse.Namespace, image_path: Path) -> dict[str, Any]:
    return {
        "schema": "uniclaw.localVisionEvidence.v1",
        "createdAt": datetime.now(timezone.utc).isoformat(),
        "imagePath": str(image_path),
        "yolo": {
            "source": "json" if args.yolo_json else "ultralytics",
            "model": str(args.yolo_json or args.yolo_model),
            "imgsz": args.imgsz,
            "conf": args.conf,
            "device": args.device,
        },
        "ocr": {
            "source": "json" if args.ocr_json else args.ocr_backend,
            "model": str(args.ocr_json or f"{args.ocr_backend}:{args.ocr_lang}"),
        },
    }


if __name__ == "__main__":
    raise SystemExit(main())
