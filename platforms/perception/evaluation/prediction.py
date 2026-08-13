"""Prediction artifact — current-model fresh inference output.

Frozen by Phase 4 gate:
  • Prediction != GroundTruth (B4: never becomes truth automatically).
  • Prediction references run/asset/deployment; modifies nothing (B20).
  • Stored historical JSON perception output is never a current Prediction
    (B5/PF3) — only fresh L2 inference produces Predictions.
"""
from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from persistence import write_once_json


@dataclass(frozen=True)
class Prediction:
    run_id: str
    asset_id: str
    deployment_hash: str
    schema_version: str                       # evidence schema of raw output
    candidates: tuple[dict[str, Any], ...]    # fresh raw candidate list
    yolo_count: int
    ocr_count: int
    timings_ms: dict[str, float] = field(default_factory=dict)
    note: str = ""
    stage_views: dict[str, Any] = field(default_factory=dict)
    source_content_hash: str = ""
    # stage_views (T0-E): stage-scoped views from the SAME fresh inference.
    #   rawModelDetections   — RAW_DETECTION / DEKI_YOLO_RAW_V1
    #   normalizedDetections — RAW_DETECTION / CANONICAL_DETECTION_V1
    #   fusedEvidence        — FUSED_EVIDENCE / FUSED_OUTPUT_V1
    # Missing view = key absent → NOT_AVAILABLE, never fabricated.

    def to_json(self) -> dict[str, Any]:
        return {
            "runId": self.run_id,
            "assetId": self.asset_id,
            "deploymentHash": self.deployment_hash,
            "schemaVersion": self.schema_version,
            "candidates": list(self.candidates),
            "yoloCount": self.yolo_count,
            "ocrCount": self.ocr_count,
            "timingsMs": self.timings_ms,
            "note": self.note,
            "stageViews": dict(self.stage_views),
            "sourceContentHash": self.source_content_hash,
        }

    @classmethod
    def from_json(cls, d: dict[str, Any]) -> "Prediction":
        return cls(
            run_id=d["runId"], asset_id=d["assetId"],
            deployment_hash=d["deploymentHash"],
            schema_version=d["schemaVersion"],
            candidates=tuple(d.get("candidates", [])),
            yolo_count=d.get("yoloCount", 0), ocr_count=d.get("ocrCount", 0),
            timings_ms=dict(d.get("timingsMs", {})),
            note=d.get("note", ""),
            stage_views=dict(d.get("stageViews", {})),
            source_content_hash=d.get("sourceContentHash", ""),
        )


def save_prediction(pred: Prediction, out_dir: str | Path) -> Path:
    out = Path(out_dir)
    name = f"{pred.run_id.replace('run:', '')}-{pred.asset_id.replace('sha256:', '')}.json"
    path = out / name
    return write_once_json(path, pred.to_json())
