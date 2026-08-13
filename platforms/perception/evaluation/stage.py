"""Stage + label-space contract (T0).

Pipeline Stage != Label Vocabulary (frozen).

Scoring compatibility requires BOTH:
  • targetStage compatible
  • labelSpace identical (or an explicitly versioned mapping selected)
"""
from __future__ import annotations

from enum import Enum


class EvaluationTargetStage(str, Enum):
    """Which pipeline boundary an expectation/prediction describes."""
    RAW_DETECTION = "RAW_DETECTION"
    OCR = "OCR"
    FUSED_EVIDENCE = "FUSED_EVIDENCE"
    FINAL_PERCEPTION_EVIDENCE = "FINAL_PERCEPTION_EVIDENCE"


class LabelSpace(str, Enum):
    """Which label vocabulary a label/count/class-sensitive record uses.

    Repository-audited vocabulary identities (three distinct vocabularies):
      DEKI_YOLO_RAW_V1      — raw model class-index vocabulary (model.names)
      CANONICAL_DETECTION_V1 — post YOLO_LABEL_ALIASES (11 canonical labels)
      FUSED_OUTPUT_V1       — post-fusion candidate type vocabulary
      OCR_TEXT_V1           — OCR token text (no class labels)
      MINI_SYNTHETIC_BOX_V1 — mini training run's synthetic single-class
                              vocabulary (class 0 = "box") — created by the
                              reproducibility foundation mini-run
    Special values:
      UNRESOLVED            — historical expectation whose boundary is unknown
                              (never reinterpreted — DIAGNOSTIC_ONLY)
      NOT_APPLICABLE        — purely geometric truth (normalized bounds)
    """
    DEKI_YOLO_RAW_V1 = "DEKI_YOLO_RAW_V1"
    CANONICAL_DETECTION_V1 = "CANONICAL_DETECTION_V1"
    FUSED_OUTPUT_V1 = "FUSED_OUTPUT_V1"
    OCR_TEXT_V1 = "OCR_TEXT_V1"
    MINI_SYNTHETIC_BOX_V1 = "MINI_SYNTHETIC_BOX_V1"
    UNRESOLVED = "UNRESOLVED"
    NOT_APPLICABLE = "NOT_APPLICABLE"


class CompatibilityVerdict(str, Enum):
    SCORABLE = "SCORABLE"
    STAGE_MISMATCH = "STAGE_MISMATCH"
    LABEL_SPACE_MISMATCH = "LABEL_SPACE_MISMATCH"
    UNRESOLVED_DIAGNOSTIC_ONLY = "UNRESOLVED_DIAGNOSTIC_ONLY"
    NOT_APPLICABLE_GEOMETRIC = "NOT_APPLICABLE_GEOMETRIC"


def check_compatibility(gt_stage: EvaluationTargetStage,
                        gt_label_space: LabelSpace,
                        pred_stage: EvaluationTargetStage,
                        pred_label_space: LabelSpace) -> CompatibilityVerdict:
    """T0-B/H guard: stage AND label-space compatibility.

    Never silently normalizes, remaps, or reinterprets.
    Mismatch is an evaluation-semantic incompatibility — NOT a model failure.
    """
    if gt_label_space == LabelSpace.UNRESOLVED:
        # historical expectation with unresolved vocabulary boundary
        return CompatibilityVerdict.UNRESOLVED_DIAGNOSTIC_ONLY
    if gt_label_space == LabelSpace.NOT_APPLICABLE:
        # geometric-only truth (bounds) — vocabulary not meaningful
        return CompatibilityVerdict.NOT_APPLICABLE_GEOMETRIC
    if gt_stage != pred_stage:
        return CompatibilityVerdict.STAGE_MISMATCH
    if gt_label_space != pred_label_space:
        return CompatibilityVerdict.LABEL_SPACE_MISMATCH
    return CompatibilityVerdict.SCORABLE
