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


def training_run_from_execution(
    *,
    config: Any,
    session: Any,
    environment: TrainingEnvironment,
    code_revision: str,
    dirty: bool,
    base_model_artifact_id: str | None,
    state: TrainingRunState,
    terminal_outcome: str,
    receipt_dir: str | Path,
    session_evidence_dir: str | Path,
    produced_checkpoints: tuple[dict[str, str], ...] = (),
    training_metrics: dict[str, Any] | None = None,
    operational_costs: dict[str, Any] | None = None,
) -> TrainingRun:
    """GAP-008 canonical TrainingRun creation: identity facts are DERIVED
    from the execution session — callers cannot declare config/invocation/
    admission truth. Mismatch → TrainingInvocationMismatchError, no lineage."""
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
    return TrainingRun(
        dataset_version_id=session.dataset_version_id,        # derived
        training_config_id=session.training_config_id,       # derived
        training_code_revision=code_revision,
        dirty=dirty,
        base_model_artifact_id=base_model_artifact_id,
        environment=environment,
        state=state,
        terminal_outcome=terminal_outcome,
        produced_checkpoints=produced_checkpoints,
        training_metrics=training_metrics or {},
        operational_costs=operational_costs or {},
        invocation_args=session.resolved_kwargs,             # derived
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
    config: Any,
    session: Any,
    environment: TrainingEnvironment,
    code_revision: str,
    dirty: bool,
    base_model_artifact_id: str | None,
    state: TrainingRunState,
    terminal_outcome: str,
    receipt_dir: str | Path,
    session_evidence_dir: str | Path,
    produced_checkpoints: tuple[dict[str, str], ...] = (),
    training_metrics: dict[str, Any] | None = None,
    operational_costs: dict[str, Any] | None = None,
    out_dir: str | Path | None = None,
) -> tuple[TrainingRun, Path | None]:
    """CANONICAL terminal TrainingRun mint + persist.

    The ONLY path that can create and persist a terminal TrainingRun:
    identity derived from the execution session (config, invocation,
    admission receipt) — never caller declarations."""
    run = training_run_from_execution(
        config=config, session=session, environment=environment,
        code_revision=code_revision, dirty=dirty,
        base_model_artifact_id=base_model_artifact_id,
        state=state, terminal_outcome=terminal_outcome,
        receipt_dir=receipt_dir, session_evidence_dir=session_evidence_dir,
        produced_checkpoints=produced_checkpoints,
        training_metrics=training_metrics,
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
