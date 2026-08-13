"""TrainingConfig foundation (P4-T3).

Immutable record of materially training-affecting inputs.
trainingConfigId = SHA-256(canonical effective config).
UNRESOLVED values recorded honestly — no full-reproducibility claim for
what is not captured. TrainingConfig != PerceptionConfig (frozen).
"""
from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from evaluation.identity import canonical_hash
from . import TRAINING_SCHEMA_VERSION
from persistence import write_once_json

UNRESOLVED = "UNRESOLVED"

@dataclass(frozen=True)
class ResolvedTrainingInvocation:
    """INTERNAL execution-evidence type (GAP-008): produced INSIDE the
    canonical runner — never caller authority."""
    kwargs: dict[str, Any]
    def as_kwargs(self) -> dict[str, Any]: return dict(self.kwargs)


class TrainingInvocationMismatchError(RuntimeError):
    """Resolved-vs-captured training invocation congruence failure."""


@dataclass(frozen=True)
class TrainingExecutionSession:
    """Immutable evidence of one canonical training execution."""
    training_config_id: str          # derived from the LOADED config
    resolved_kwargs: dict[str, Any]  # derived from the config inside runner
    captured_kwargs: dict[str, Any]  # independently observed model.train call
    congruent: bool
    results: Any = None              # framework results object (evidence)
    terminal_error: str = ""         # framework execution failure (not mismatch)
    admission_receipt_id: str = ""   # VERIFIED canonical admission identity
    dataset_version_id: str = ""     # dataset actually executed (evidence)
    execution_location: dict[str, str] = field(default_factory=dict)
    session_evidence_id: str = ""    # content address of persisted session evidence

    def _evidence_payload(self) -> dict[str, Any]:
        return {
            "schema": TRAINING_SCHEMA_VERSION,
            "trainingConfigId": self.training_config_id,
            "resolvedKwargs": self.resolved_kwargs,
            "capturedKwargs": self.captured_kwargs,
            "congruent": self.congruent,
            "terminalError": self.terminal_error,
            "admissionReceiptId": self.admission_receipt_id,
            "datasetVersionId": self.dataset_version_id,
            "executionLocation": self.execution_location,
        }

    @property
    def canonical_session_evidence_id(self) -> str:
        return f"execution:{canonical_hash(self._evidence_payload())}"

    def to_evidence_json(self) -> dict[str, Any]:
        record = self._evidence_payload()
        record["executionEvidenceId"] = self.canonical_session_evidence_id
        return record

    @classmethod
    def from_evidence_json(cls, record: dict[str, Any]) -> "TrainingExecutionSession | None":
        session = cls(
            training_config_id=record["trainingConfigId"],
            resolved_kwargs=dict(record["resolvedKwargs"]),
            captured_kwargs=dict(record["capturedKwargs"]),
            congruent=record["congruent"], terminal_error=record.get("terminalError", ""),
            admission_receipt_id=record["admissionReceiptId"],
            dataset_version_id=record["datasetVersionId"],
            execution_location=dict(record["executionLocation"]),
        )
        return (session if record.get("executionEvidenceId")
                == session.canonical_session_evidence_id else None)


def save_execution_session_evidence(
    session: TrainingExecutionSession, out_dir: str | Path
) -> TrainingExecutionSession:
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    evidence_id = session.canonical_session_evidence_id
    write_once_json(
        out / f"{evidence_id.replace('execution:', '')}.json",
        session.to_evidence_json())
    return TrainingExecutionSession(
        training_config_id=session.training_config_id,
        resolved_kwargs=session.resolved_kwargs,
        captured_kwargs=session.captured_kwargs, congruent=session.congruent,
        results=session.results, terminal_error=session.terminal_error,
        admission_receipt_id=session.admission_receipt_id,
        dataset_version_id=session.dataset_version_id,
        execution_location=session.execution_location,
        session_evidence_id=evidence_id)


def load_execution_session_evidence(
    evidence_id: str, out_dir: str | Path
) -> TrainingExecutionSession | None:
    if not evidence_id.startswith("execution:"):
        return None
    path = Path(out_dir) / f"{evidence_id.replace('execution:', '')}.json"
    try:
        record = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError, TypeError):
        return None
    session = TrainingExecutionSession.from_evidence_json(record)
    if session is None or session.canonical_session_evidence_id != evidence_id:
        return None
    return TrainingExecutionSession(
        training_config_id=session.training_config_id,
        resolved_kwargs=session.resolved_kwargs,
        captured_kwargs=session.captured_kwargs, congruent=session.congruent,
        terminal_error=session.terminal_error,
        admission_receipt_id=session.admission_receipt_id,
        dataset_version_id=session.dataset_version_id,
        execution_location=session.execution_location,
        session_evidence_id=evidence_id)


def execute_training(
    *,
    config: "TrainingConfig",
    admission_receipt_id: str,       # canonical persisted admission identity
    dataset: Any,                    # actual DatasetVersion being executed
    declared_protected_set: set[str],
    annotation_dir: str | Path,
    event_dir: str | Path,
    receipt_dir: str | Path,
    session_evidence_dir: str | Path,
    data_path: str,
    project_path: str,
    base_model_path: str,
    run_name: str = "train",         # non-behavior execution location context
    model_factory: Any = None,
) -> TrainingExecutionSession:
    """GAP-006 record-minting closure + GAP-008 canonical execution seam.

    NO in-memory receipt object is accepted. Admission authority is
    RE-DERIVED here from the canonical dataset + declared protected-set
    snapshot + annotation/event records, and the recomputed receipt
    identity must equal the claimed admission_receipt_id. A forged
    in-memory receipt, a valid-looking never-persisted receipt, or wrong
    content under a claimed id all fail (RM-LEAK-01..03).

    The Ultralytics invocation is derived from the TrainingConfig INSIDE
    the runner; congruence against the captured model.train call is
    verified (GAP-008).
    """
    from .dataset import admit_dataset_for_training, load_training_admission_receipt

    if not admission_receipt_id:
        raise ValueError(
            "training admission receipt id required (no receipt → no training)")
    # ── canonical admission re-derivation + persisted evidence verification ──
    recomputed = admit_dataset_for_training(
        dataset, declared_protected_set, annotation_dir=annotation_dir,
        event_dir=event_dir)
    if recomputed.receipt_id != admission_receipt_id:
        raise ValueError(
            "TRAINING_ADMISSION_MISMATCH: claimed receipt "
            f"{admission_receipt_id} != recomputed canonical admission "
            f"{recomputed.receipt_id}")
    persisted = load_training_admission_receipt(admission_receipt_id, receipt_dir)
    if persisted is None or persisted != recomputed:
        raise ValueError(
            "TRAINING_ADMISSION_PERSISTENCE_MISMATCH: claimed receipt is "
            "not a persisted canonical admission record")
    dataset_version_id = dataset.dataset_version_id

    # ── C3-C invocation derivation (inside runner only) ──
    resolved = {
        **config.ultralytics_kwargs(),
        "data": data_path,
        "project": project_path,
        "name": run_name,
        "device": "cpu",
        "workers": 0,
        "verbose": False,
    }

    if model_factory is None:
        from ultralytics import YOLO
        model_factory = YOLO
    model = model_factory(base_model_path)

    # independently capture the actual framework-bound call
    captured: dict[str, Any] = {}
    original_train = model.train

    def recording_train(**kwargs: Any) -> Any:
        captured.update(kwargs)
        return original_train(**kwargs)

    model.train = recording_train
    results = None
    terminal_error = ""
    try:
        results = model.train(**resolved)
    except Exception as exc:  # framework execution failure — recorded truthfully
        terminal_error = f"{type(exc).__name__}: {exc}"
    finally:
        model.train = original_train

    # congruence on the config-owned keys actually observed at the boundary
    observed_keys = [k for k in ("epochs", "batch", "imgsz", "seed")
                     if k in captured]
    congruent = all(captured[k] == resolved[k] for k in observed_keys)
    session = TrainingExecutionSession(
        training_config_id=config.training_config_id,
        resolved_kwargs=dict(resolved),
        captured_kwargs=dict(captured),
        congruent=congruent,
        results=results,
        terminal_error=terminal_error,
        admission_receipt_id=recomputed.receipt_id,
        dataset_version_id=dataset_version_id,
        execution_location={
            "data": data_path, "project": project_path,
            "baseModel": base_model_path, "name": run_name,
        },
    )
    return save_execution_session_evidence(session, session_evidence_dir)


def execute_ultralytics_training(model: Any, resolved: ResolvedTrainingInvocation) -> Any:
    """INTERNAL_PURE_HELPER — noncanonical. Not part of any canonical
    training path; retained only for isolated math/framework tests."""
    return model.train(**resolved.as_kwargs())


@dataclass(frozen=True)
class TrainingConfig:
    base_model_artifact_id: str | None      # initialization artifact (modelId)
    epochs: int | str                       # int or UNRESOLVED
    batch_size: int | str
    imgsz: int | str
    optimizer: str
    learning_rate: float | str
    scheduler: str | None
    augmentation: str | None                # declared augmentation policy
    seed: int | str
    class_vocabulary: tuple[str, ...]       # model class-index vocabulary
    label_space: str                        # DEKI_YOLO_RAW_V1 etc.
    framework: str = "ultralytics"
    framework_parameters: dict[str, Any] = field(default_factory=dict)

    @property
    def training_config_id(self) -> str:
        return f"tcfg:{canonical_hash(self._canonical())}"

    def _canonical(self) -> dict[str, Any]:
        return {
            "schema": TRAINING_SCHEMA_VERSION,
            "baseModelArtifactId": self.base_model_artifact_id,
            "epochs": self.epochs,
            "batchSize": self.batch_size,
            "imgsz": self.imgsz,
            "optimizer": self.optimizer,
            "learningRate": self.learning_rate,
            "scheduler": self.scheduler,
            "augmentation": self.augmentation,
            "seed": self.seed,
            "classVocabulary": sorted(self.class_vocabulary),
            "labelSpace": self.label_space,
            "framework": self.framework,
            "frameworkParameters": self.framework_parameters,
        }

    def to_json(self) -> dict[str, Any]:
        d = self._canonical()
        d["trainingConfigId"] = self.training_config_id
        return d

    def ultralytics_kwargs(self) -> dict[str, Any]:
        """Single derived invocation surface; unresolved values are rejected.

        scheduler/augmentation are TRAINING-CONFIG identity declarations,
        not valid model.train kwargs — they are excluded from the
        invocation surface (their truth lives in TrainingConfig only)."""
        vals = (self.epochs, self.batch_size, self.imgsz, self.seed)
        if any(v == UNRESOLVED for v in vals):
            raise ValueError("unresolved training invocation input")
        kwargs: dict[str, Any] = {
            "epochs": self.epochs, "batch": self.batch_size,
            "imgsz": self.imgsz, "seed": self.seed,
        }
        if self.optimizer != UNRESOLVED:
            kwargs["optimizer"] = self.optimizer
        if self.learning_rate != UNRESOLVED and self.learning_rate != "auto":
            kwargs["lr0"] = self.learning_rate
        kwargs.update(self.framework_parameters)
        return kwargs

    def resolved_invocation(self, *, data: str, output: str) -> ResolvedTrainingInvocation:
        return ResolvedTrainingInvocation({"data": data, **self.ultralytics_kwargs(),
                                           "project": output, "device": "cpu", "workers": 0})

    @classmethod
    def from_json(cls, d: dict[str, Any]) -> "TrainingConfig":
        return cls(
            base_model_artifact_id=d.get("baseModelArtifactId"),
            epochs=d.get("epochs", UNRESOLVED),
            batch_size=d.get("batchSize", UNRESOLVED),
            imgsz=d.get("imgsz", UNRESOLVED),
            optimizer=d.get("optimizer", UNRESOLVED),
            learning_rate=d.get("learningRate", UNRESOLVED),
            scheduler=d.get("scheduler"),
            augmentation=d.get("augmentation"),
            seed=d.get("seed", UNRESOLVED),
            class_vocabulary=tuple(d.get("classVocabulary", [])),
            label_space=d.get("labelSpace", UNRESOLVED),
            framework=d.get("framework", "ultralytics"),
            framework_parameters=dict(d.get("frameworkParameters", {})),
        )


def save_training_config(cfg: TrainingConfig, out_dir: str | Path) -> Path:
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    path = out / f"{cfg.training_config_id.replace('tcfg:', '')}.json"
    return write_once_json(path, cfg.to_json())
