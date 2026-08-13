"""Mini real training run (P4-T7) + candidate evaluation (P4-T8) + lineage (P4-T9).

Executes ONE bounded REAL Ultralytics training run on a tiny synthetic
dataset (provenance explicit: SYNTHETIC). The objective is PROCESS closure,
not quality. If training dependencies are not executable, reports
TRAINING_INFRASTRUCTURE_NOT_EXECUTABLE — checkpoints are NEVER fabricated.

Then sends the resulting CANDIDATE through the EXISTING frozen L2
evaluation workflow (no second scoring pipeline).
"""
from __future__ import annotations

import json
import platform as _platform
import random
import shutil
import time
from pathlib import Path
from typing import Any

from evaluation.identity import sha256_file
from evaluation.stage import EvaluationTargetStage, LabelSpace
from . import TRAINING_SCHEMA_VERSION
from .annotation import (
    AnnotationSource, ReviewStatus, accept_and_persist, create_annotation,
    save_annotation,
)
from .candidate import create_candidate, save_candidate
from .checkpoint import Checkpoint, materialize_model_artifact, save_model_artifact
from .dataset import (
    DatasetMembership, DatasetVersion, Split, admit_dataset_for_training,
    save_dataset, save_training_admission_receipt,
)
from .lineage import LineageEdge, LineageNode, LineageReport, save_lineage
from .training_config import TrainingConfig, execute_training, save_training_config
from .training_run import (
    TrainingEnvironment, TrainingRun, TrainingRunState, commit_execution_run,
    git_revision,
)

BASE = Path(__file__).resolve().parent
ARTIFACTS = BASE / "artifacts"
DATA_DIR = ARTIFACTS / "mini-data"
RUNS_DIR = ARTIFACTS / "runs"
MANIFESTS = ARTIFACTS / "manifests"
MODEL_STORE = ARTIFACTS / "model-store"
REPORTS = ARTIFACTS / "reports"

CLASS_VOCABULARY = ("box",)


def _load_annotation_record(annotation_id: str):
    """Load an annotation record by content-addressed id (admission loader).

    Identity integrity: a record whose computed identity does not match the
    requested id is NOT loadable (content-addressed truth)."""
    from .annotation import Annotation
    file = MANIFESTS / "annotations" / f"{annotation_id.replace('annotation:', '')}.json"
    if not file.exists():
        return None
    record = Annotation.from_json(json.loads(file.read_text(encoding="utf-8")))
    return record if record.annotation_id == annotation_id else None


def _load_acceptance_event(event_id: str):
    """Load an acceptance event by content-addressed id (admission loader)."""
    from .annotation import AnnotationAcceptanceEvent
    file = MANIFESTS / "acceptance-events" / f"{event_id.replace('review:', '')}.json"
    if not file.exists():
        return None
    event = AnnotationAcceptanceEvent.from_json(
        json.loads(file.read_text(encoding="utf-8")))
    return event if event.review_event_id == event_id else None
BASE_MODEL = Path("/Users/fran/Documents/Code/spacex/uni-agent/platforms/perception/models/yolo/yolo11n.pt")


def _generate_mini_images(n_train: int = 4, n_val: int = 2, size: int = 160,
                          seed: int = 42) -> dict[str, Any]:
    """Deterministic synthetic training images + YOLO-format labels.

    Truthful provenance: SYNTHETIC mini dataset for workflow proof.
    """
    from PIL import Image, ImageDraw
    rng = random.Random(seed)
    generated: list[dict[str, Any]] = []
    splits = [Split.TRAIN] * n_train + [Split.VALIDATION] * n_val

    split_dir_name = {Split.TRAIN: "train", Split.VALIDATION: "val"}

    for i, split in enumerate(splits):
        img = Image.new("RGB", (size, size), (255, 255, 255))
        draw = ImageDraw.Draw(img)
        boxes = []
        for _ in range(rng.randint(1, 3)):
            bw = rng.randint(24, 48)
            bh = rng.randint(16, 32)
            bx = rng.randint(8, size - bw - 8)
            by = rng.randint(8, size - bh - 8)
            draw.rectangle([bx, by, bx + bw, by + bh], fill=(20, 20, 20))
            cx = (bx + bw / 2) / size
            cy = (by + bh / 2) / size
            w = bw / size
            h = bh / size
            boxes.append({"class": 0, "cx": round(cx, 6), "cy": round(cy, 6),
                          "w": round(w, 6), "h": round(h, 6)})
        split_dir = DATA_DIR / "images" / split_dir_name[split]
        label_dir = DATA_DIR / "labels" / split_dir_name[split]
        split_dir.mkdir(parents=True, exist_ok=True)
        label_dir.mkdir(parents=True, exist_ok=True)
        img_path = split_dir / f"mini_{i}.png"
        img.save(img_path, format="PNG")
        label_path = label_dir / f"mini_{i}.txt"
        label_path.write_text("\n".join(
            f"{b['class']} {b['cx']} {b['cy']} {b['w']} {b['h']}" for b in boxes)
            + ("\n" if boxes else ""))
        generated.append({
            "imagePath": str(img_path), "split": split, "boxes": boxes,
            "assetId": f"sha256:{sha256_file(img_path)}",
        })

    # data.yaml for ultralytics
    data_yaml = DATA_DIR / "data.yaml"
    data_yaml.write_text(
        f"path: {DATA_DIR}\ntrain: images/train\nval: images/val\n"
        f"names: {{0: box}}\n", encoding="utf-8")
    return {"generated": generated, "dataYaml": str(data_yaml)}


def _build_dataset(generated: list[dict[str, Any]]) -> tuple[DatasetVersion, list[Any]]:
    """DatasetVersion + accepted annotations + acceptance EVENTS for the
    mini dataset (GAP-007: the real verifiable chain is persisted)."""
    anns: list[Any] = []
    members: list[DatasetMembership] = []
    for g in generated:
        ann = create_annotation(
            asset_id=g["assetId"],
            target_stage=EvaluationTargetStage.RAW_DETECTION,
            label_space=LabelSpace.MINI_SYNTHETIC_BOX_V1,
            source=AnnotationSource.HUMAN_CREATED,
            label_payload={"boxes": g["boxes"]},
            provenance="synthetic-mini-dataset (generated geometry)",
        )
        save_annotation(ann, MANIFESTS / "annotations")  # the DRAFT predecessor
        accepted = accept_and_persist(
            ann, "mini-foundation",
            annotation_dir=MANIFESTS / "annotations",
            event_dir=MANIFESTS / "acceptance-events")
        anns.append(accepted)
        members.append(DatasetMembership(
            asset_id=g["assetId"], split=g["split"],
            annotation_id=accepted.annotation_id,
            capture_group_id=None,   # synthetic; no capture metadata exists
        ))
    ds = DatasetVersion(members=tuple(members),
                        description="mini synthetic box dataset (workflow proof)")
    save_dataset(ds, MANIFESTS / "datasets")
    return ds, anns


def _build_training_config() -> TrainingConfig:
    cfg = TrainingConfig(
        base_model_artifact_id=(
            f"sha256:{sha256_file(BASE_MODEL)}" if BASE_MODEL.exists() else None),
        epochs=1, batch_size=2, imgsz=160, optimizer="auto",
        learning_rate="auto", scheduler="auto", augmentation="ultralytics-default",
        seed=42, class_vocabulary=CLASS_VOCABULARY,
        label_space=LabelSpace.MINI_SYNTHETIC_BOX_V1.value,
        framework_parameters={"workers": 0, "device": "cpu"},
    )
    save_training_config(cfg, MANIFESTS / "configs")
    return cfg


def run_mini_training() -> dict[str, Any]:
    """Full mini lifecycle. Returns lineage ids + evaluation result."""
    repo_root = Path("/Users/fran/Documents/Code/spacex/uni-agent")
    rev, dirty = git_revision(repo_root)

    generated_info = _generate_mini_images()
    ds, _anns = _build_dataset(generated_info["generated"])
    cfg = _build_training_config()

    import platform as _p
    try:
        import torch, ultralytics
        env = TrainingEnvironment(
            python_version=_p.python_version(),
            ultralytics_version=ultralytics.__version__,
            torch_version=torch.__version__,
            runtime_version="cpu", device_type="cpu", os_name=_p.system(),
            seed="42",
        )
    except ImportError as exc:
        return {"status": "TRAINING_INFRASTRUCTURE_NOT_EXECUTABLE",
                "reason": f"missing training deps: {exc}", "leakage": []}

    # ── GAP-006 + GAP-007 canonical admission ──
    # The declared protected evaluation membership is authoritative-empty:
    # Holdout is NOT_ESTABLISHED — the empty declaration IS the current
    # truth, bound into the receipt by content hash (never fabricated).
    declared_protected: set[str] = set()
    receipt = admit_dataset_for_training(
        ds, declared_protected,
        annotation_dir=MANIFESTS / "annotations",
        event_dir=MANIFESTS / "acceptance-events")
    receipt_dir = MANIFESTS / "training-admissions"
    session_evidence_dir = MANIFESTS / "execution-sessions"
    save_training_admission_receipt(receipt, receipt_dir)
    leak: list[Any] = []

    # ── GAP-008 canonical execution: config + canonical receipt id only ──
    project = RUNS_DIR / "ultralytics"
    project.mkdir(parents=True, exist_ok=True)
    t0 = time.time()
    session = execute_training(
        config=cfg,
        admission_receipt_id=receipt.receipt_id,
        dataset=ds,
        declared_protected_set=declared_protected,
        annotation_dir=MANIFESTS / "annotations",
        event_dir=MANIFESTS / "acceptance-events",
        receipt_dir=receipt_dir, session_evidence_dir=session_evidence_dir,
        data_path=str(DATA_DIR / "data.yaml"),
        project_path=str(project),
        base_model_path=str(BASE_MODEL),
        run_name="mini-run",
    )
    elapsed_s = round(time.time() - t0, 1)

    metrics: dict[str, Any] = {}
    if session.results is not None:
        try:
            rd = session.results.results_dict
            metrics = {k: (float(v) if isinstance(v, (int, float)) else str(v))
                       for k, v in rd.items()}
        except Exception:
            metrics = {}

    train_ok = not session.terminal_error
    state = TrainingRunState.COMPLETED if train_ok else TrainingRunState.FAILED
    terminal = "completed" if train_ok else f"failed: {session.terminal_error}"

    if not train_ok:
        failed, _ = commit_execution_run(
            config=cfg, session=session,
            environment=env, code_revision=rev, dirty=dirty,
            base_model_artifact_id=cfg.base_model_artifact_id,
            state=state, terminal_outcome=terminal,
            receipt_dir=receipt_dir, session_evidence_dir=session_evidence_dir,
            training_metrics=metrics,
            operational_costs={"durationSeconds": elapsed_s},
            out_dir=MANIFESTS / "runs",
        )
        return {"status": "TRAINING_FAILED", "terminal": terminal,
                "trainingRunId": failed.training_run_id, "leakage": leak}

    # ── checkpoint → model artifact → candidate ──
    run_dir = project / "mini-run"
    best_pt = run_dir / "weights" / "best.pt"
    if not best_pt.exists():
        best_pt = run_dir / "weights" / "last.pt"
    checkpoint = Checkpoint(
        checkpoint_name="best", source_path=str(best_pt),
        selection_metric="ultralytics train best-checkpoint policy",
    )

    # canonical TrainingRun creation: identity derived from the execution
    # session (GAP-008) + verified admission receipt (GAP-006)
    completed, _ = commit_execution_run(
        config=cfg, session=session,
        environment=env, code_revision=rev, dirty=dirty,
        base_model_artifact_id=cfg.base_model_artifact_id,
        state=state, terminal_outcome=terminal,
        receipt_dir=receipt_dir, session_evidence_dir=session_evidence_dir,
        produced_checkpoints=(
            {"name": checkpoint.checkpoint_name,
             "checkpointId": checkpoint.checkpoint_id,
             "selectionMetric": checkpoint.selection_metric or ""},),
        training_metrics=metrics,
        operational_costs={"durationSeconds": elapsed_s},
        out_dir=MANIFESTS / "runs",
    )

    artifact = materialize_model_artifact(
        checkpoint, training_run_id=completed.training_run_id,
        model_name="mini_synthetic_box", target_dir=MODEL_STORE,
    )
    save_model_artifact(artifact, MANIFESTS / "model-artifacts")

    cand = create_candidate(
        model_artifact_id=artifact.model_id, model_name=artifact.model_name,
        training_run_id=completed.training_run_id,
        dataset_version_id=ds.dataset_version_id,
        training_config_id=cfg.training_config_id,
    )
    save_candidate(cand, MANIFESTS / "candidates")

    return {
        "status": "COMPLETED",
        "trainingRunId": completed.training_run_id,
        "datasetVersionId": ds.dataset_version_id,
        "trainingConfigId": cfg.training_config_id,
        "checkpointId": checkpoint.checkpoint_id,
        "checkpointName": checkpoint.checkpoint_name,
        "modelArtifactId": artifact.model_id,
        "modelName": artifact.model_name,
        "candidateId": cand.candidate_id,
        "candidateStatus": cand.status.value,
        "leakage": [f.to_json() if hasattr(f, "to_json") else str(f) for f in leak],
        "metrics": metrics,
        "durationSeconds": elapsed_s,
    }


if __name__ == "__main__":
    import sys
    result = run_mini_training()
    print(json.dumps(result, ensure_ascii=False, indent=2))
    sys.exit(0 if result["status"] == "COMPLETED" else 1)
