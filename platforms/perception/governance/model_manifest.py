"""ModelManifest (P4-D3).

Truthful separation (D0-3):
  artifactFormat — container format (ULTRALYTICS_PT)
  architecture    — detected model architecture (YOLOV8 / YOLO11 / UNKNOWN)
  modelName       — stable family/product identity
  modelId         — exact artifact bytes SHA-256
  labelSpaceId    — exact vocabulary identity for THIS artifact

The mini yolo11-derived test artifact must NOT be described as YOLOv8;
it receives its own truthful label-space identity.
ModelManifest REFERENCES training lineage — never duplicates TrainingRun.
"""
from __future__ import annotations

import json
from dataclasses import dataclass, field
from enum import Enum
from pathlib import Path
from typing import Any

from evaluation.identity import canonical_hash
from persistence import write_once_json

MANIFEST_SCHEMA = "uniclaw.modelManifest.v1"


class ArtifactFormat(str, Enum):
    ULTRALYTICS_PT = "ULTRALYTICS_PT"
    UNKNOWN = "UNKNOWN"


class Architecture(str, Enum):
    YOLOV8 = "YOLOV8"
    YOLO11 = "YOLO11"
    UNKNOWN = "UNKNOWN"


class ProvenanceStance(str, Enum):
    LEGACY_PROVENANCE_PARTIAL = "LEGACY_PROVENANCE_PARTIAL"
    TRAINING_LINEAGE_LINKED = "TRAINING_LINEAGE_LINKED"


@dataclass(frozen=True)
class ModelManifest:
    model_name: str
    model_id: str                      # full 64-hex SHA-256
    artifact_format: ArtifactFormat
    architecture: Architecture
    label_space_id: str
    class_vocabulary: tuple[str, ...]
    provenance_stance: ProvenanceStance
    source_training_run_id: str | None = None
    source_checkpoint_id: str | None = None

    @property
    def manifest_id(self) -> str:
        return f"mmf:{canonical_hash(self._canonical())}"

    def _canonical(self) -> dict[str, Any]:
        return {
            "schema": MANIFEST_SCHEMA,
            "modelName": self.model_name,
            "modelId": self.model_id,
            "artifactFormat": self.artifact_format.value,
            "architecture": self.architecture.value,
            "labelSpaceId": self.label_space_id,
            "classVocabulary": sorted(self.class_vocabulary),
            "provenanceStance": self.provenance_stance.value,
            "sourceTrainingRunId": self.source_training_run_id,
            "sourceCheckpointId": self.source_checkpoint_id,
        }

    def to_json(self) -> dict[str, Any]:
        d = self._canonical()
        d["manifestId"] = self.manifest_id
        return d


def detect_architecture(model_path: str | Path) -> Architecture:
    """Detect architecture from the artifact's own model yaml.

    Repository-grounded signatures (verified against production + mini):
      YOLO11: C3k2 / C2PSA blocks, or yaml_file 'yolo11*'
      YOLOv8: C2f blocks without C3k2/C2PSA
    Never inferred from filename or family name (D0-3).
    """
    p = Path(model_path)
    if not p.exists():
        return Architecture.UNKNOWN
    try:
        from ultralytics import YOLO
        m = YOLO(str(p))
        yaml = getattr(m.model, "yaml", None) or {}
        backbone = str(yaml.get("backbone", ""))
        yaml_file = str(yaml.get("yaml_file", ""))
        if "C3k2" in backbone or "C2PSA" in backbone \
                or yaml_file.startswith("yolo11"):
            return Architecture.YOLO11
        if "C2f" in backbone:
            return Architecture.YOLOV8
        return Architecture.UNKNOWN
    except Exception:
        return Architecture.UNKNOWN


def detect_class_vocabulary(model_path: str | Path) -> tuple[str, ...]:
    """Class names from the artifact's own metadata (model.names)."""
    p = Path(model_path)
    if not p.exists():
        return ()
    try:
        from ultralytics import YOLO
        m = YOLO(str(p))
        names = m.names
        return tuple(str(names.get(i, i)) for i in range(len(names)))
    except Exception:
        return ()


def build_current_active_manifest(model_path: str | Path,
                                  model_id: str | None = None) -> ModelManifest:
    """Truthful ACTIVE backfill (D18): verifiable facts from the artifact,
    UNKNOWN training lineage — never fabricated."""
    from evaluation.identity import sha256_file
    p = Path(model_path)
    mid = model_id or sha256_file(p)
    arch = detect_architecture(p)
    vocab = detect_class_vocabulary(p)
    return ModelManifest(
        model_name="android_ui_detection_yolov8",
        model_id=mid,
        artifact_format=ArtifactFormat.ULTRALYTICS_PT,
        architecture=arch,
        label_space_id="DEKI_YOLO_RAW_V1" if arch == Architecture.YOLOV8 else "UNKNOWN",
        class_vocabulary=vocab,
        provenance_stance=ProvenanceStance.LEGACY_PROVENANCE_PARTIAL,
        source_training_run_id=None,   # UNKNOWN — not fabricated
        source_checkpoint_id=None,     # UNKNOWN — not fabricated
    )


def build_test_candidate_manifest(model_path: str | Path,
                                  model_id: str,
                                  training_run_id: str,
                                  checkpoint_id: str) -> ModelManifest:
    """Truthful mini test candidate manifest (D0-3/IDR-04/IDR-05)."""
    p = Path(model_path)
    arch = detect_architecture(p)
    vocab = detect_class_vocabulary(p)
    return ModelManifest(
        model_name="mini_synthetic_box",
        model_id=model_id,
        artifact_format=ArtifactFormat.ULTRALYTICS_PT,
        architecture=arch,                    # YOLO11-derived — never YOLOV8
        label_space_id="MINI_SYNTHETIC_BOX_V1",
        class_vocabulary=vocab,
        provenance_stance=ProvenanceStance.TRAINING_LINEAGE_LINKED,
        source_training_run_id=training_run_id,
        source_checkpoint_id=checkpoint_id,
    )


def save_manifest(manifest: ModelManifest, out_dir: str | Path) -> Path:
    out = Path(out_dir)
    path = out / f"{manifest.manifest_id.replace('mmf:', '')}.json"
    return write_once_json(path, manifest.to_json())
