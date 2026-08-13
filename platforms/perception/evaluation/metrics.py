"""Task-scoped metrics (R13/I20).

Only metrics supported by available ground truth are computed.
Missing GT for a task → NOT_SCORABLE (never zero). Denominators always
reported (I24).
"""
from __future__ import annotations

import re
from dataclasses import dataclass, field
from enum import Enum
from typing import Any

from .groundtruth import GroundTruth, GroundTruthElement, TaskStance
from .matcher import MatchResult, match
from .asset import PerceptionTask


# ── text normalization (mirrors legacy OCR normalization rules) ──

def normalize_text(text: str) -> str:
    t = text.lower()
    t = re.sub(r"\s+", " ", t).strip()
    t = re.sub(r"\s*,\s*", ", ", t)
    return t


# ── metric results ─────────────────────────────────────────────

@dataclass(frozen=True)
class TaskMetricResult:
    task: PerceptionTask
    stance: TaskStance
    metrics: dict[str, Any] = field(default_factory=dict)
    denominator: int = 0
    note: str = ""


class PredictionView(str, Enum):
    RAW_MODEL = "rawModelDetections"
    NORMALIZED_DETECTION = "normalizedDetections"
    FUSED_EVIDENCE = "fusedEvidence"


@dataclass(frozen=True)
class EvaluationScoringResult:
    request_id: str
    prediction_asset_id: str
    prediction_request_id: str
    prediction_deployment_hash: str
    ground_truth_asset_id: str
    ground_truth_version: str
    ground_truth_source: str
    prediction_view: PredictionView
    prediction_stage: str
    prediction_label_space: str
    compatibility_verdict: str
    task_results: dict[PerceptionTask, TaskMetricResult]


@dataclass(frozen=True)
class EvaluationScoringContext:
    """Immutable provenance binding for canonical scoring.

    Callers select a typed stored Prediction view only.  Stage, label space,
    and candidate arrays are derived here and cannot be supplied detached.
    """
    request_id: str
    prediction: Any
    ground_truth: GroundTruth
    deployment_hash: str
    prediction_view: PredictionView = PredictionView.FUSED_EVIDENCE

    def validate(self) -> None:
        if getattr(self.prediction, "run_id", None) != self.request_id:
            raise ValueError("PROVENANCE_MISMATCH:PREDICTION_REQUEST_ID")
        if getattr(self.prediction, "asset_id", None) != self.ground_truth.asset_id:
            raise ValueError("PROVENANCE_MISMATCH:ASSET_ID")
        if getattr(self.prediction, "deployment_hash", None) != self.deployment_hash:
            raise ValueError("PROVENANCE_MISMATCH:DEPLOYMENT_IDENTITY")

    def _stored_view(self) -> tuple[list[dict[str, Any]], Any, Any]:
        from .stage import EvaluationTargetStage, LabelSpace
        mapping = {
            PredictionView.RAW_MODEL: (
                EvaluationTargetStage.RAW_DETECTION, LabelSpace.DEKI_YOLO_RAW_V1),
            PredictionView.NORMALIZED_DETECTION: (
                EvaluationTargetStage.RAW_DETECTION, LabelSpace.CANONICAL_DETECTION_V1),
            PredictionView.FUSED_EVIDENCE: (
                EvaluationTargetStage.FUSED_EVIDENCE, LabelSpace.FUSED_OUTPUT_V1),
        }
        stage, label_space = mapping[self.prediction_view]
        if self.prediction_view == PredictionView.FUSED_EVIDENCE:
            # candidates are the canonical stored fused view; stage_views is
            # additive observability and may be absent on older Predictions.
            values = list(self.prediction.candidates)
        else:
            views = getattr(self.prediction, "stage_views", {})
            if self.prediction_view.value not in views:
                raise ValueError("PROVENANCE_MISMATCH:STORED_VIEW_NOT_AVAILABLE")
            values = list(views[self.prediction_view.value])
        return values, stage, label_space

    def score(self) -> EvaluationScoringResult:
        from .stage import check_compatibility
        self.validate()
        candidates, stage, label_space = self._stored_view()
        verdict = check_compatibility(
            self.ground_truth.evaluation_target_stage,
            self.ground_truth.label_space,
            stage,
            label_space,
        )
        return EvaluationScoringResult(
            request_id=self.request_id,
            prediction_asset_id=self.prediction.asset_id,
            prediction_request_id=self.prediction.run_id,
            prediction_deployment_hash=self.prediction.deployment_hash,
            ground_truth_asset_id=self.ground_truth.asset_id,
            ground_truth_version=self.ground_truth.gt_version,
            ground_truth_source=self.ground_truth.source,
            prediction_view=self.prediction_view,
            prediction_stage=stage.value,
            prediction_label_space=label_space.value,
            compatibility_verdict=verdict.value,
            task_results=compute_task_metrics(
                candidates, self.ground_truth, stage, label_space),
        )


def _detection_metrics(pred_candidates: list[dict[str, Any]],
                       gt: GroundTruth) -> TaskMetricResult:
    if not gt.has_task(PerceptionTask.ELEMENT_DETECTION):
        return TaskMetricResult(PerceptionTask.ELEMENT_DETECTION,
                                TaskStance.NOT_SCORABLE, note="no detection GT")
    if gt.elements:
        gt_list = [
            {"gt_class": e.gt_class, "bounds": e.bounds, "text": e.text}
            for e in gt.elements
        ]
        pred_list = [
            {"type": c.get("type", ""), "bounds": c.get("bounds")}
            for c in pred_candidates
        ]
        m = match(pred_list, gt_list)
        tp, fp, fn = m.tp, m.fp, m.fn
        precision = tp / (tp + fp) if (tp + fp) > 0 else 0.0
        recall = tp / (tp + fn) if (tp + fn) > 0 else 0.0
        f1 = (2 * precision * recall / (precision + recall)
              if (precision + recall) > 0 else 0.0)
        # bounds IoU over matched pairs
        ious = [p.iou for p in m.matches]
        mean_iou = sum(ious) / len(ious) if ious else None
        return TaskMetricResult(
            task=PerceptionTask.ELEMENT_DETECTION, stance=TaskStance.SCORED,
            metrics={"tp": tp, "fp": fp, "fn": fn,
                     "precision": round(precision, 6),
                     "recall": round(recall, 6), "f1": round(f1, 6),
                     "meanMatchedIoU": round(mean_iou, 6) if mean_iou is not None else None,
                     "matcherRevision": m.matcher_revision},
            denominator=len(gt.elements),
        )
    if gt.expected_class_counts is not None:
        # count-conformance mode: predicted count vs expected count per class
        pred_counts: dict[str, int] = {}
        for c in pred_candidates:
            t = c.get("type", "")
            pred_counts[t] = pred_counts.get(t, 0) + 1
        per_class: dict[str, Any] = {}
        total_expected = 0
        total_delta = 0
        classes_correct = 0
        for cls, expected in sorted(gt.expected_class_counts.items()):
            actual = pred_counts.get(cls, 0)
            delta = actual - expected
            per_class[cls] = {"expected": expected, "actual": actual,
                              "delta": delta, "match": delta == 0}
            total_expected += expected
            total_delta += abs(delta)
            if delta == 0:
                classes_correct += 1
        n_classes = len(gt.expected_class_counts) or 1
        return TaskMetricResult(
            task=PerceptionTask.ELEMENT_DETECTION, stance=TaskStance.SCORED,
            metrics={"mode": "count_conformance",
                     "perClass": per_class,
                     "classesExactMatch": f"{classes_correct}/{n_classes}",
                     "totalExpected": total_expected,
                     "totalAbsDelta": total_delta},
            denominator=n_classes,
        )
    return TaskMetricResult(PerceptionTask.ELEMENT_DETECTION,
                            TaskStance.NOT_SCORABLE, note="no element/count GT")


def _ocr_metrics(pred_candidates: list[dict[str, Any]],
                 gt: GroundTruth) -> TaskMetricResult:
    if not gt.has_task(PerceptionTask.OCR):
        return TaskMetricResult(PerceptionTask.OCR, TaskStance.NOT_SCORABLE,
                                note="no OCR GT")
    expected = [normalize_text(t) for t in gt.expected_texts]
    if not expected:
        return TaskMetricResult(PerceptionTask.OCR, TaskStance.NOT_SCORABLE,
                                note="OCR GT declared but empty")
    # all OCR text observed across candidates + raw ocr list carried via candidates
    observed_texts: set[str] = set()
    for c in pred_candidates:
        txt = c.get("text", "")
        if txt:
            observed_texts.add(normalize_text(txt))
    found, missing = [], []
    for original in gt.expected_texts:
        # presence check on the NORMALIZED forms; report the ORIGINAL text
        e = normalize_text(original)
        hit = any(e == o or (len(e) >= 4 and e in o) for o in observed_texts)
        (found if hit else missing).append(original)
    return TaskMetricResult(
        task=PerceptionTask.OCR, stance=TaskStance.SCORED,
        metrics={"expectedTexts": len(expected), "found": len(found),
                 "missing": len(missing), "missingTexts": missing,
                 "foundTexts": found,
                 "exactMatchRate": round(len(found) / len(expected), 6)},
        denominator=len(expected),
    )


def _bounds_metrics(pred_candidates: list[dict[str, Any]],
                    gt: GroundTruth) -> TaskMetricResult:
    if not gt.has_task(PerceptionTask.BOUNDS):
        return TaskMetricResult(PerceptionTask.BOUNDS, TaskStance.NOT_SCORABLE,
                                note="no bounds GT")
    gt_with_bounds = [e for e in gt.elements if e.bounds is not None]
    if not gt_with_bounds:
        return TaskMetricResult(PerceptionTask.BOUNDS, TaskStance.NOT_SCORABLE,
                                note="no bounds-level GT elements")
    gt_list = [{"gt_class": e.gt_class, "bounds": e.bounds} for e in gt_with_bounds]
    pred_list = [{"type": c.get("type", ""), "bounds": c.get("bounds")}
                 for c in pred_candidates]
    m = match(pred_list, gt_list)
    ious = [p.iou for p in m.matches]
    return TaskMetricResult(
        task=PerceptionTask.BOUNDS, stance=TaskStance.SCORED,
        metrics={"matched": len(ious), "meanIoU": round(sum(ious) / len(ious), 6)
                 if ious else None,
                 "unmatchedGt": m.fn, "unmatchedPred": m.fp},
        denominator=len(gt_with_bounds),
    )


def _switch_state_metrics(pred_candidates: list[dict[str, Any]],
                          gt: GroundTruth) -> TaskMetricResult:
    if not gt.has_task(PerceptionTask.SWITCH_STATE):
        return TaskMetricResult(PerceptionTask.SWITCH_STATE,
                                TaskStance.NOT_SCORABLE, note="no switch-state GT")
    states = gt.expected_switch_states or {}
    if not states:
        return TaskMetricResult(PerceptionTask.SWITCH_STATE,
                                TaskStance.NOT_SCORABLE, note="switch GT declared but empty")
    correct, total = 0, 0
    per_state = {"ON": {"n": 0, "correct": 0}, "OFF": {"n": 0, "correct": 0},
                 "UNKNOWN": {"n": 0, "correct": 0}}
    for key, expected in states.items():
        pred_state = None
        for c in pred_candidates:
            if c.get("gtKey") == key or c.get("text") == key:
                pred_state = c.get("switchState")
                break
        total += 1
        bucket = {True: "ON", False: "OFF", None: "UNKNOWN"}[expected]
        per_state[bucket]["n"] += 1
        if pred_state == expected:
            correct += 1
            per_state[bucket]["correct"] += 1
    return TaskMetricResult(
        task=PerceptionTask.SWITCH_STATE, stance=TaskStance.SCORED,
        metrics={"correct": correct, "total": total,
                 "accuracy": round(correct / total, 6) if total else None,
                 "perState": per_state},
        denominator=total,
    )


def _safety_metrics(pred_candidates: list[dict[str, Any]],
                    gt: GroundTruth) -> TaskMetricResult:
    """Safety slice: coordinate validity + fabrication visibility (B11).

    Coordinate validity is always scorable — it needs no GT.
    Fabrication rate needs element GT; NOT_SCORABLE without it.
    """
    n_with_bounds = 0
    n_invalid_bounds = 0
    for c in pred_candidates:
        b = c.get("bounds")
        if b is None:
            continue
        n_with_bounds += 1
        x1, y1, x2, y2 = b
        if not (0 <= x1 <= x2 <= 1 and 0 <= y1 <= y2 <= 1):
            n_invalid_bounds += 1
    metrics: dict[str, Any] = {
        "candidatesWithBounds": n_with_bounds,
        "invalidCoordinateBounds": n_invalid_bounds,
        "coordinateValidityRate": (
            round((n_with_bounds - n_invalid_bounds) / n_with_bounds, 6)
            if n_with_bounds else None),
    }
    stance = TaskStance.SCORED
    if gt.has_task(PerceptionTask.ELEMENT_DETECTION) and gt.elements:
        gt_list = [{"gt_class": e.gt_class, "bounds": e.bounds}
                   for e in gt.elements]
        pred_list = [{"type": c.get("type", ""), "bounds": c.get("bounds")}
                     for c in pred_candidates]
        m = match(pred_list, gt_list)
        metrics["fabricationRate"] = round(
            m.fp / (m.tp + m.fp), 6) if (m.tp + m.fp) > 0 else None
        metrics["fabricationCount"] = m.fp
    else:
        metrics["fabricationRate"] = None
        metrics["note"] = "fabrication not scorable without element GT"
    return TaskMetricResult(task=PerceptionTask.SAFETY, stance=stance,
                            metrics=metrics, denominator=n_with_bounds)


def _bounds_tuple(b: Any) -> tuple[float, float, float, float] | None:
    """Normalize bounds from evidence-JSON dict form to 4-tuple."""
    if b is None:
        return None
    if isinstance(b, dict):
        try:
            return (float(b["x1"]), float(b["y1"]), float(b["x2"]), float(b["y2"]))
        except (KeyError, TypeError, ValueError):
            return None
    if isinstance(b, (list, tuple)) and len(b) == 4:
        try:
            return tuple(float(v) for v in b)  # type: ignore[return-value]
        except (TypeError, ValueError):
            return None
    return None


def _normalize_candidates(pred_candidates: list[dict[str, Any]]) -> list[dict[str, Any]]:
    """Copy candidate list with dict-form bounds normalized to tuples."""
    out: list[dict[str, Any]] = []
    for c in pred_candidates:
        d = dict(c)
        d["bounds"] = _bounds_tuple(c.get("bounds"))
        out.append(d)
    return out


def compute_task_metrics(
    pred_candidates: list[dict[str, Any]],
    gt: GroundTruth,
    pred_stage: "EvaluationTargetStage | None" = None,
    pred_label_space: "LabelSpace | None" = None,
) -> dict[PerceptionTask, TaskMetricResult]:
    """Non-authoritative pure metric math.

    Canonical evaluation evidence MUST be produced by
    :class:`EvaluationScoringContext`; this helper has no persistence path.

    Compute all task metrics whose GT supports them (PF1 handled inside).

    T0-B/H guard: label/class-sensitive tasks are only scored when stage AND
    label space are compatible. Mismatch → NOT_SCORABLE (never model
    failure); historical UNRESOLVED label space → DIAGNOSTIC_ONLY.
    Purely geometric safety (coordinate validity) remains scorable.
    """
    from .stage import (
        CompatibilityVerdict, EvaluationTargetStage, LabelSpace, check_compatibility,
    )
    preds = _normalize_candidates(pred_candidates)

    if pred_stage is None:
        pred_stage = EvaluationTargetStage.FUSED_EVIDENCE
    if pred_label_space is None:
        pred_label_space = LabelSpace.FUSED_OUTPUT_V1

    verdict = check_compatibility(
        gt.evaluation_target_stage, gt.label_space, pred_stage, pred_label_space)

    label_tasks = (PerceptionTask.ELEMENT_DETECTION, PerceptionTask.OCR,
                   PerceptionTask.BOUNDS, PerceptionTask.SWITCH_STATE)

    if verdict == CompatibilityVerdict.SCORABLE:
        label_sensitive = {
            PerceptionTask.ELEMENT_DETECTION: _detection_metrics(preds, gt),
            PerceptionTask.OCR: _ocr_metrics(preds, gt),
            PerceptionTask.BOUNDS: _bounds_metrics(preds, gt),
            PerceptionTask.SWITCH_STATE: _switch_state_metrics(preds, gt),
        }
    elif verdict == CompatibilityVerdict.UNRESOLVED_DIAGNOSTIC_ONLY:
        label_sensitive = {
            task: (TaskMetricResult(
                task, TaskStance.DIAGNOSTIC_ONLY, metrics={},
                note="historical expectation with UNRESOLVED label space — "
                     "DIAGNOSTIC_ONLY, NOT_RELEASE_ELIGIBLE (T0-C)")
                if gt.has_task(task) else
                TaskMetricResult(task, TaskStance.NOT_SCORABLE,
                                 note="no GT declared for task"))
            for task in label_tasks
        }
    else:  # STAGE_MISMATCH / LABEL_SPACE_MISMATCH
        label_sensitive = {
            task: (TaskMetricResult(
                task, TaskStance.NOT_SCORABLE, metrics={},
                note=f"{verdict.value} — evaluation-semantic incompatibility, "
                     f"NOT a model failure (T0-B)")
                if gt.has_task(task) else
                TaskMetricResult(task, TaskStance.NOT_SCORABLE,
                                 note="no GT declared for task"))
            for task in label_tasks
        }
    return {**label_sensitive,
            PerceptionTask.SAFETY: _safety_metrics(preds, gt)}
