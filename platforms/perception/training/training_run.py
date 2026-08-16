"""TrainingRun foundation (P4-T4).

Immutable run record with exact provenance. Dirty repository state recorded
truthfully — never silently claimed as committed HEAD.
"""
from __future__ import annotations

import json
from dataclasses import dataclass, field
from enum import Enum
from pathlib import Path
from typing import Any

from evaluation.identity import canonical_hash
from . import TRAINING_SCHEMA_VERSION
from .training_config import TrainingInvocationMismatchError
from persistence import write_once_json


class TrainingRunState(str, Enum):
    CREATED = "CREATED"
    RUNNING = "RUNNING"
    COMPLETED = "COMPLETED"
    FAILED = "FAILED"
    CANCELLED = "CANCELLED"


@dataclass(frozen=True)
class TrainingEnvironment:
    python_version: str
    ultralytics_version: str
    torch_version: str
    runtime_version: str = ""          # CUDA/CPU runtime descriptor
    device_type: str = "cpu"
    os_name: str = ""
    seed: str = "UNRESOLVED"


@dataclass(frozen=True)
class TrainingRun:
    dataset_version_id: str
    training_config_id: str
    training_code_revision: str        # git commit hash
    dirty: bool                        # uncommitted local changes during training
    base_model_artifact_id: str | None
    environment: TrainingEnvironment
    state: TrainingRunState
    terminal_outcome: str = ""
    produced_checkpoints: tuple[dict[str, str], ...] = ()
    # produced_checkpoints: ({"name": "best", "checkpointId": "sha256:...", ...},)
    training_metrics: dict[str, Any] = field(default_factory=dict)
    operational_costs: dict[str, Any] = field(default_factory=dict)
    invocation_args: dict[str, Any] = field(default_factory=dict)
    invocation_hash: str = ""
    training_admission_receipt_id: str | None = None   # GAP-006 binding

    @property
    def training_run_id(self) -> str:
        return f"trun:{canonical_hash(self._canonical())}"

    def _canonical(self) -> dict[str, Any]:
        return {
            "schema": TRAINING_SCHEMA_VERSION,
            "datasetVersionId": self.dataset_version_id,
            "trainingConfigId": self.training_config_id,
            "trainingAdmissionReceiptId": self.training_admission_receipt_id,
            "codeRevision": self.training_code_revision,
            "dirty": self.dirty,
            "baseModelArtifactId": self.base_model_artifact_id,
            "environment": {
                "python": self.environment.python_version,
                "ultralytics": self.environment.ultralytics_version,
                "torch": self.environment.torch_version,
                "runtime": self.environment.runtime_version,
                "device": self.environment.device_type,
                "os": self.environment.os_name,
                "seed": self.environment.seed,
            },
            "state": self.state.value,
            "terminalOutcome": self.terminal_outcome,
            "producedCheckpoints": sorted(
                self.produced_checkpoints, key=lambda c: c.get("name", "")),
            "trainingMetrics": self.training_metrics,
            "operationalCosts": self.operational_costs,
            "invocationArgs": self.invocation_args,
            "invocationHash": self.invocation_hash or canonical_hash(self.invocation_args),
        }

    def to_json(self) -> dict[str, Any]:
        d = self._canonical()
        d["trainingRunId"] = self.training_run_id
        return d


def _environment_from_capture(captured: dict[str, str]) -> TrainingEnvironment:
    """GAP-008 FINAL: TrainingEnvironment DERIVED from the environment
    captured during execution — never caller-declared."""
    return TrainingEnvironment(
        python_version=captured.get("pythonVersion", "UNRESOLVED"),
        ultralytics_version=captured.get("ultralyticsVersion", "UNRESOLVED"),
        torch_version=captured.get("torchVersion", "UNRESOLVED"),
        runtime_version=captured.get("deviceType", ""),
        device_type=captured.get("deviceType", "cpu"),
        os_name=captured.get("osName", "UNRESOLVED"),
        seed=captured.get("seed", "UNRESOLVED"),
    )


def _terminal_from_error(terminal_error: str) -> tuple[TrainingRunState, str]:
    """GAP-008 FINAL: terminal state/outcome DERIVED from the session's
    recorded execution error (null → COMPLETED, else FAILED)."""
    if not terminal_error:
        return TrainingRunState.COMPLETED, "completed"
    return TrainingRunState.FAILED, f"failed: {terminal_error}"


def _verify_produced_checkpoints(
    session: Any, session_evidence_dir: str | Path,
) -> tuple[dict[str, str], ...]:
    """GAP-008 FINAL: checkpoint claims are verified against the ACTUAL
    produced files (execution-location path + content hash + session
    binding).  A missing or altered checkpoint revokes the lineage."""
    from .training_config import load_execution_session_evidence
    persisted = load_execution_session_evidence(
        session.session_evidence_id, session_evidence_dir)
    if (persisted is None
            or persisted.canonical_session_evidence_id
            != session.session_evidence_id):
        raise TrainingInvocationMismatchError(
            "TRAINING_INVOCATION_MISMATCH: session does not match persisted "
            "canonical execution evidence")
    checkpoints: list[dict[str, str]] = []
    location = session.execution_location
    project = location.get("project", "")
    name = location.get("name", "")
    weights_dir = Path(project) / name / "weights" if project and name else None
    for ckpt in session.produced_checkpoints:
        ckpt_id = ckpt.get("checkpointId", "")
        ckpt_name = ckpt.get("name", "")
        if not ckpt_id.startswith("sha256:"):
            raise TrainingInvocationMismatchError(
                "TRAINING_INVOCATION_MISMATCH: checkpoint id is not a "
                "content address")
        if weights_dir is None:
            raise TrainingInvocationMismatchError(
                "TRAINING_INVOCATION_MISMATCH: session lacks execution "
                "location for checkpoint verification")
        from evaluation.identity import sha256_file
        ckpt_file = weights_dir / f"{ckpt_name}.pt"
        if not ckpt_file.is_file():
            raise TrainingInvocationMismatchError(
                "TRAINING_INVOCATION_MISMATCH: produced checkpoint file "
                f"missing: {ckpt_file}")
        if f"sha256:{sha256_file(ckpt_file)}" != ckpt_id:
            raise TrainingInvocationMismatchError(
                "TRAINING_INVOCATION_MISMATCH: produced checkpoint content "
                f"hash does not match session evidence: {ckpt_name}")
        checkpoints.append({"name": ckpt_name, "checkpointId": ckpt_id,
                            **{k: v for k, v in ckpt.items()
                               if k not in ("name", "checkpointId")}})
    return tuple(checkpoints)


def training_run_from_execution(
    *,
    session: Any,
    config: Any,
    code_revision: str,
    dirty: bool,
    receipt_dir: str | Path,
    session_evidence_dir: str | Path,
    operational_costs: dict[str, Any] | None = None,
) -> TrainingRun:
    """GAP-008 FINAL: canonical TrainingRun creation — EVERY authoritative
    field is DERIVED from the persisted execution session and the persisted
    TrainingConfig:

      dataset_version_id / training_config_id / invocation / admission
          ← session + persisted receipt (unchanged)
      environment            ← CAPTURED during execution (session)
      state / terminal_outcome ← session.terminal_error (null → COMPLETED,
                                 else FAILED)
      base_model_artifact_id ← persisted TrainingConfig
      produced_checkpoints   ← actual produced files, re-verified by hash
      training_metrics       ← actual execution output (session)

    Callers cannot declare ANY of these.  Mismatch →
    TrainingInvocationMismatchError, no lineage."""
    from .training_config import (
        TrainingInvocationMismatchError, load_execution_session_evidence,
    )
    from .dataset import load_training_admission_receipt

    if not session.congruent:
        raise TrainingInvocationMismatchError(
            "TRAINING_INVOCATION_MISMATCH: resolved vs captured invocation "
            "differ — no valid completed lineage")
    if not session.session_evidence_id:
        raise TrainingInvocationMismatchError(
            "TRAINING_INVOCATION_MISMATCH: session lacks persisted canonical "
            "execution evidence")
    persisted_session = load_execution_session_evidence(
        session.session_evidence_id, session_evidence_dir)
    if (persisted_session is None
            or persisted_session.canonical_session_evidence_id
            != session.session_evidence_id
            or persisted_session._evidence_payload() != session._evidence_payload()):
        raise TrainingInvocationMismatchError(
            "TRAINING_INVOCATION_MISMATCH: session does not match persisted "
            "canonical execution evidence")
    if session.training_config_id != config.training_config_id:
        raise TrainingInvocationMismatchError(
            "TRAINING_INVOCATION_MISMATCH: session config identity "
            f"{session.training_config_id} != loaded config "
            f"{config.training_config_id}")
    if not session.admission_receipt_id:
        raise TrainingInvocationMismatchError(
            "TRAINING_INVOCATION_MISMATCH: session lacks verified "
            "admission receipt identity")
    location = session.execution_location
    required_location = ("data", "project", "baseModel", "name")
    if any(not isinstance(location.get(key), str) or not location[key]
           for key in required_location):
        raise TrainingInvocationMismatchError(
            "TRAINING_INVOCATION_MISMATCH: session lacks canonical "
            "execution location evidence")
    expected_kwargs = {
        **config.ultralytics_kwargs(),
        "data": location["data"], "project": location["project"],
        "name": location["name"], "device": "cpu", "workers": 0,
        "verbose": False,
    }
    if session.resolved_kwargs != expected_kwargs or session.captured_kwargs != expected_kwargs:
        raise TrainingInvocationMismatchError(
            "TRAINING_INVOCATION_MISMATCH: session invocation is not the "
            "exact config-derived canonical invocation")
    receipt = load_training_admission_receipt(
        session.admission_receipt_id, receipt_dir)
    if (receipt is None or receipt.receipt_id != session.admission_receipt_id
            or receipt.dataset_version_id != session.dataset_version_id
            or receipt.admission_result != "ADMITTED"):
        raise TrainingInvocationMismatchError(
            "TRAINING_ADMISSION_PERSISTENCE_MISMATCH: session receipt is "
            "not a persisted record bound to its dataset")

    state, terminal_outcome = _terminal_from_error(session.terminal_error)
    checkpoints = _verify_produced_checkpoints(session, session_evidence_dir)
    return TrainingRun(
        dataset_version_id=session.dataset_version_id,        # derived
        training_config_id=session.training_config_id,       # derived
        training_code_revision=code_revision,
        dirty=dirty,
        base_model_artifact_id=config.base_model_artifact_id,  # derived
        environment=_environment_from_capture(
            session.captured_environment),                     # derived
        state=state,                                           # derived
        terminal_outcome=terminal_outcome,                     # derived
        produced_checkpoints=checkpoints,                      # derived
        training_metrics=dict(session.training_metrics),       # derived
        operational_costs=operational_costs or {},
        invocation_args=session.resolved_kwargs,               # derived
        invocation_hash=canonical_hash(session.resolved_kwargs),  # derived
        training_admission_receipt_id=session.admission_receipt_id,  # derived
    )


def save_training_run(run: TrainingRun, out_dir: str | Path) -> Path:
    """GAP-008 record-minting closure: PUBLIC save refuses all terminal
    states. Canonical terminal runs can only be persisted through
    commit_execution_run (the execution-session-derived path)."""
    raise ValueError(
        "canonical terminal TrainingRun persistence requires "
        "commit_execution_run — direct save_training_run has no "
        "record-minting authority")


def commit_execution_run(
    *,
    session_evidence_id: str,
    config_dir: str | Path,
    receipt_dir: str | Path,
    session_evidence_dir: str | Path,
    code_revision: str,
    dirty: bool,
    operational_costs: dict[str, Any] | None = None,
    out_dir: str | Path | None = None,
) -> tuple[TrainingRun, Path | None]:
    """GAP-008 FINAL: CANONICAL terminal TrainingRun mint + persist — a
    DERIVATION/COMMIT boundary, NOT a second data-entry API.

    The ONLY path that can create and persist a terminal TrainingRun.
    The caller supplies ONLY the content-addressed session evidence id
    (+ storage locations + non-authoritative code revision context +
    operational costs).  Everything authoritative is DERIVED inside:

      persisted TrainingExecutionSession   ← loaded by evidence id
      persisted TrainingConfig             ← loaded by session.training_config_id
      state / terminal_outcome             ← session.terminal_error
      base_model_artifact_id               ← persisted config
      environment                          ← captured during execution
      produced_checkpoints / metrics       ← actual execution evidence

    There are NO state / terminal_outcome / base_model_artifact_id /
    environment / checkpoints / metrics parameters — caller-declared
    terminal authority does not exist."""
    from .training_config import (
        TrainingInvocationMismatchError,
        load_execution_session_evidence, load_training_config,
    )

    session = load_execution_session_evidence(
        session_evidence_id, session_evidence_dir)
    if session is None:
        raise TrainingInvocationMismatchError(
            "TRAINING_INVOCATION_MISMATCH: session evidence "
            f"{session_evidence_id} is not a persisted canonical execution")
    config = load_training_config(session.training_config_id, config_dir)
    if config is None:
        raise TrainingInvocationMismatchError(
            "TRAINING_INVOCATION_MISMATCH: persisted TrainingConfig "
            f"{session.training_config_id} is not loadable from {config_dir}")
    run = training_run_from_execution(
        session=session, config=config, code_revision=code_revision,
        dirty=dirty, receipt_dir=receipt_dir,
        session_evidence_dir=session_evidence_dir,
        operational_costs=operational_costs,
    )
    path = None
    if out_dir is not None:
        out = Path(out_dir)
        out.mkdir(parents=True, exist_ok=True)
        target = out / f"{run.training_run_id.replace('trun:', '')}.json"
        write_once_json(target, run.to_json())
        path = target
    return run, path


def git_revision(repo_root: str | Path) -> tuple[str, bool]:
    """Current git commit + dirty flag (truthful — TR-09)."""
    import subprocess
    try:
        commit = subprocess.run(
            ["git", "rev-parse", "HEAD"], cwd=str(repo_root),
            capture_output=True, text=True, timeout=10)
        rev = commit.stdout.strip() if commit.returncode == 0 else "UNRESOLVED"
    except Exception:
        return "UNRESOLVED", True
    try:
        status = subprocess.run(
            ["git", "status", "--porcelain"], cwd=str(repo_root),
            capture_output=True, text=True, timeout=10)
        dirty = bool(status.stdout.strip())
    except Exception:
        dirty = True
    return (rev if rev else "UNRESOLVED"), dirty
