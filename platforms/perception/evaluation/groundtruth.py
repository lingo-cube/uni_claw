"""Task-scoped GroundTruth.

Frozen by Phase 4 gate:
  • PerceptionPrediction != GroundTruth (separate record types).
  • Model output never becomes truth by being copied into an annotation.
  • Task-scoped: an asset may have Detection GT but no OCR GT.
  • Missing GT for a task → NOT_SCORABLE, never 0/100/PASS/FAIL.
"""
from __future__ import annotations

import json
from dataclasses import dataclass, field
from enum import Enum
from pathlib import Path
from typing import Any

from persistence import write_once_json

from .asset import PerceptionTask
from .stage import EvaluationTargetStage, LabelSpace


class TaskStance(str, Enum):
    SCORED = "SCORED"
    NOT_SCORABLE = "NOT_SCORABLE"
    INSUFFICIENT_EVIDENCE = "INSUFFICIENT_EVIDENCE"
    DIAGNOSTIC_ONLY = "DIAGNOSTIC_ONLY"


@dataclass(frozen=True)
class GroundTruthElement:
    """One expected element for detection/bounds matching."""
    gt_class: str
    bounds: tuple[float, float, float, float] | None = None  # normalized [0,1] x1,y1,x2,y2
    text: str | None = None


@dataclass(frozen=True)
class GroundTruth:
    """Immutable task-scoped ground truth record.

    source is mandatory provenance of the truth itself:
      "harness-manifest-v1"  — authoritative repository verification credential
      "synthetic-fixture"    — test-only synthetic fixture truth
      "reviewed-annotation"  — human-reviewed truth (future)
    review_status: unreviewed | reviewed | challenged | corrected

    evaluation_target_stage: which pipeline boundary this truth describes
    (T0/F purchased delta). label_space: which vocabulary its labels use
    (UNRESOLVED for historical expectations with unknown boundaries).
    """
    schema_version: str
    asset_id: str                       # which asset this truth is bound to
    gt_version: str                     # truth version (semantic, human label)
    source: str
    review_status: str = "unreviewed"
    evaluation_target_stage: EvaluationTargetStage = EvaluationTargetStage.FUSED_EVIDENCE
    label_space: LabelSpace = LabelSpace.FUSED_OUTPUT_V1
    declared_tasks: tuple[PerceptionTask, ...] = ()
    elements: tuple[GroundTruthElement, ...] = ()          # detection/bounds GT
    expected_class_counts: dict[str, int] | None = None    # count-conformance GT
    expected_texts: tuple[str, ...] = ()                   # OCR presence GT
    expected_switch_states: dict[str, bool | None] | None = None
    expected_absent_classes: tuple[str, ...] = ()
    notes: dict[str, Any] = field(default_factory=dict)

    def task_stance(self, task: PerceptionTask) -> TaskStance:
        """PF1: missing GT for a task → NOT_SCORABLE (never zero)."""
        if task in self.declared_tasks:
            return TaskStance.SCORED
        return TaskStance.NOT_SCORABLE

    def has_task(self, task: PerceptionTask) -> bool:
        return task in self.declared_tasks

    def to_json(self) -> dict[str, Any]:
        return {
            "schemaVersion": self.schema_version,
            "assetId": self.asset_id,
            "gtVersion": self.gt_version,
            "source": self.source,
            "reviewStatus": self.review_status,
            "evaluationTargetStage": self.evaluation_target_stage.value,
            "labelSpace": self.label_space.value,
            "declaredTasks": [t.value for t in self.declared_tasks],
            "elements": [
                {"gtClass": e.gt_class, "bounds": e.bounds, "text": e.text}
                for e in self.elements
            ],
            "expectedClassCounts": self.expected_class_counts,
            "expectedTexts": list(self.expected_texts),
            "expectedSwitchStates": self.expected_switch_states,
            "expectedAbsentClasses": list(self.expected_absent_classes),
            "notes": self.notes,
        }

    @classmethod
    def from_json(cls, d: dict[str, Any]) -> "GroundTruth":
        return cls(
            schema_version=d["schemaVersion"],
            asset_id=d["assetId"],
            gt_version=d["gtVersion"],
            source=d["source"],
            review_status=d.get("reviewStatus", "unreviewed"),
            evaluation_target_stage=EvaluationTargetStage(
                d.get("evaluationTargetStage",
                      EvaluationTargetStage.FUSED_EVIDENCE.value)),
            label_space=LabelSpace(d.get("labelSpace", LabelSpace.FUSED_OUTPUT_V1.value)),
            declared_tasks=tuple(PerceptionTask(t) for t in d.get("declaredTasks", [])),
            elements=tuple(
                GroundTruthElement(
                    gt_class=e["gtClass"],
                    bounds=tuple(e["bounds"]) if e.get("bounds") else None,
                    text=e.get("text"),
                )
                for e in d.get("elements", [])
            ),
            expected_class_counts=dict(d["expectedClassCounts"]) if d.get("expectedClassCounts") else None,
            expected_texts=tuple(d.get("expectedTexts", [])),
            expected_switch_states=dict(d["expectedSwitchStates"]) if d.get("expectedSwitchStates") else None,
            expected_absent_classes=tuple(d.get("expectedAbsentClasses", [])),
            notes=dict(d.get("notes", {})),
        )


def load_groundtruth(path: str | Path) -> GroundTruth:
    return GroundTruth.from_json(json.loads(Path(path).read_text(encoding="utf-8")))


def save_groundtruth(gt: GroundTruth, out_dir: str | Path) -> Path:
    out = Path(out_dir)
    path = out / f"gt-{gt.asset_id.replace('sha256:', '')}-v{gt.gt_version}.json"
    return write_once_json(path, gt.to_json())


def load_groundtruth_exact(
    asset_id: str, gt_version: str, out_dir: str | Path,
) -> GroundTruth | None:
    """GAP-004 FINAL: resolve GroundTruth by EXACT canonical identity
    (asset + version) — the deterministic filename, verified against the
    record's own asset/version fields.  No glob, no directory ordering,
    no first-match authority."""
    if not asset_id.startswith("sha256:") or not gt_version:
        return None
    path = Path(out_dir) / f"gt-{asset_id.removeprefix('sha256:')}-v{gt_version}.json"
    try:
        record = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError, TypeError):
        return None
    gt = GroundTruth.from_json(record)
    if gt.asset_id != asset_id or gt.gt_version != gt_version:
        return None
    return gt
