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
    training_input_binding: dict[str, Any] = field(default_factory=dict)
    # GAP-006 FINAL: full content-binding evidence record (binding id,
    # datasetVersionId, resolvedMemberCount, imageContentIds, label bindings).
    captured_environment: dict[str, str] = field(default_factory=dict)
    # GAP-008 FINAL: environment CAPTURED during execution (python/os/device/
    # seed/framework versions) — never caller-declared.
    produced_checkpoints: tuple[dict[str, Any], ...] = ()
    # GAP-008 FINAL: checkpoints scanned from the ACTUAL execution output
    # ({"name": ..., "checkpointId": "sha256:..."}).
    training_metrics: dict[str, Any] = field(default_factory=dict)
    # GAP-008 FINAL: metrics read from the ACTUAL execution results object.

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
            "trainingInputBinding": self.training_input_binding,
            "capturedEnvironment": self.captured_environment,
            "producedCheckpoints": list(self.produced_checkpoints),
            "trainingMetrics": self.training_metrics,
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
            training_input_binding=dict(record.get("trainingInputBinding", {})),
            captured_environment=dict(record.get("capturedEnvironment", {})),
            produced_checkpoints=tuple(record.get("producedCheckpoints", [])),
            training_metrics=dict(record.get("trainingMetrics", {})),
        )
        return (session if record.get("executionEvidenceId")
                == session.canonical_session_evidence_id else None)


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
        training_input_binding=session.training_input_binding,
        captured_environment=session.captured_environment,
        produced_checkpoints=session.produced_checkpoints,
        training_metrics=session.training_metrics,
        session_evidence_id=evidence_id)


def _capture_runtime_environment(seed: Any = None) -> dict[str, str]:
    """GAP-008 FINAL: environment facts CAPTURED at the execution boundary.

    Framework versions are resolved from the interpreter actually running
    the training; anything unresolvable is recorded as UNRESOLVED — never
    invented."""
    import importlib
    import importlib.util
    import platform

    def version_of(mod_name: str) -> str:
        try:
            if importlib.util.find_spec(mod_name) is None:
                return UNRESOLVED
            mod = importlib.import_module(mod_name)
            return str(getattr(mod, "__version__", UNRESOLVED))
        except Exception:
            return UNRESOLVED

    return {
        "pythonVersion": platform.python_version(),
        "osName": platform.system(),
        "deviceType": "cpu",
        "seed": UNRESOLVED if seed is None else str(seed),
        "ultralyticsVersion": version_of("ultralytics"),
        "torchVersion": version_of("torch"),
    }


def _checkpoints_from_results(results: Any) -> tuple[dict[str, Any], ...]:
    """GAP-008 FINAL: produced checkpoints scanned from the ACTUAL
    execution output (results.save_dir/weights/*.pt) with content hashes."""
    if results is None:
        return ()
    save_dir = getattr(results, "save_dir", None)
    if not save_dir:
        return ()
    weights = Path(save_dir) / "weights"
    if not weights.is_dir():
        return ()
    from evaluation.identity import sha256_file
    out: list[dict[str, Any]] = []
    for p in sorted(weights.glob("*.pt")):
        out.append({"name": p.stem, "checkpointId": f"sha256:{sha256_file(p)}"})
    return tuple(out)


def _metrics_from_results(results: Any) -> dict[str, Any]:
    """GAP-008 FINAL: metrics read from the ACTUAL execution results object.
    Omitted (not fabricated) when the framework exposes none."""
    if results is None:
        return {}
    rd = getattr(results, "results_dict", None)
    if not isinstance(rd, dict):
        return {}
    out: dict[str, Any] = {}
    for k, v in rd.items():
        try:
            out[str(k)] = float(v) if isinstance(v, (int, float)) else str(v)
        except Exception:
            out[str(k)] = str(v)
    return out


def load_training_config(
    config_id: str, out_dir: str | Path
) -> "TrainingConfig | None":
    """Content-addressed TrainingConfig loader: the loaded record must
    recompute to the requested identity (GAP-008 — commit derives the
    base model + invocation from the PERSISTED config, never a caller
    object)."""
    if not config_id.startswith("tcfg:"):
        return None
    path = Path(out_dir) / f"{config_id.removeprefix('tcfg:')}.json"
    try:
        record = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError, TypeError):
        return None
    cfg = TrainingConfig.from_json(record)
    return cfg if cfg.training_config_id == config_id else None


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

    GAP-006 FINAL: BEFORE any train invocation, the bytes reachable from
    data_path are verified — by content identity — to be EXACTLY the
    admitted DatasetVersion membership (images + canonical label records).
    data_path is a LOCATION only; any binding mismatch raises
    TrainingDataBindingError and training does not start.

    GAP-008 FINAL: the environment is CAPTURED at this boundary and the
    produced checkpoints + training metrics are read from the ACTUAL
    execution output (never caller-declared) and persisted into the
    canonical session evidence.
    """
    from .dataset import (
        admit_dataset_for_training, load_training_admission_receipt,
        resolve_training_input_binding,
    )

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

    # ── GAP-006 FINAL: executed bytes ↔ admitted manifest binding ──
    # BEFORE any model/train invocation: the bytes reachable from data_path
    # must be EXACTLY the admitted dataset membership (content identity).
    # data_path is LOCATION ONLY — never semantic identity.  Any mismatch
    # fails closed before training starts.
    binding = resolve_training_input_binding(
        data_path, dataset, annotation_dir=annotation_dir)

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

    # ── GAP-008 FINAL: environment captured at the execution boundary ──
    captured_environment = _capture_runtime_environment(config.seed)

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
        training_input_binding=binding.to_json(),
        captured_environment=captured_environment,
        produced_checkpoints=_checkpoints_from_results(results),
        training_metrics=_metrics_from_results(results),
    )
    # Canonical execution evidence is minted only by this real execution
    # boundary.  A separately callable writer would let a caller persist a
    # forged TrainingExecutionSession and then present it to terminal commit.
    session_root = Path(session_evidence_dir)
    session_root.mkdir(parents=True, exist_ok=True)
    evidence_id = session.canonical_session_evidence_id
    write_once_json(
        session_root / f"{evidence_id.replace('execution:', '')}.json",
        session.to_evidence_json())
    return TrainingExecutionSession(
        training_config_id=session.training_config_id,
        resolved_kwargs=session.resolved_kwargs,
        captured_kwargs=session.captured_kwargs, congruent=session.congruent,
        results=session.results, terminal_error=session.terminal_error,
        admission_receipt_id=session.admission_receipt_id,
        dataset_version_id=session.dataset_version_id,
        execution_location=session.execution_location,
        training_input_binding=session.training_input_binding,
        captured_environment=session.captured_environment,
        produced_checkpoints=session.produced_checkpoints,
        training_metrics=session.training_metrics,
        session_evidence_id=evidence_id)


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
