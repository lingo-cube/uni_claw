"""Immutable evaluation request and terminal-result evidence.

New executions create an :class:`EvaluationRunRequest`, then persist exactly
one immutable :class:`EvaluationRunResult` after terminal aggregation.  The
legacy ``EvaluationRun`` record remains readable for historical artifacts;
it is not the canonical creation API.
"""
from __future__ import annotations

from dataclasses import asdict, dataclass, field
from enum import Enum
from pathlib import Path
from typing import Any

from persistence import write_once_json

from .deployment import DeploymentSnapshot
from .identity import canonical_hash


class TerminalStatus(str, Enum):
    # PENDING is legacy-loader vocabulary only.  A new terminal result cannot
    # be created with it.
    PENDING = "PENDING"
    COMPLETED = "COMPLETED"
    PARTIAL = "PARTIAL"
    INSUFFICIENT_EVIDENCE = "INSUFFICIENT_EVIDENCE"
    INFRASTRUCTURE_FAILURE = "INFRASTRUCTURE_FAILURE"


class AssetOutcomeKind(str, Enum):
    SCORABLE = "SCORABLE"
    INSUFFICIENT_EVIDENCE = "INSUFFICIENT_EVIDENCE"
    INFRASTRUCTURE_FAILURE = "INFRASTRUCTURE_FAILURE"


@dataclass(frozen=True)
class EnvironmentProfile:
    os_name: str
    cpu_arch: str
    python_version: str
    worker_topology: str = "single-process/single-worker"
    extra: dict[str, Any] = field(default_factory=dict)

    def to_json(self) -> dict[str, Any]:
        return asdict(self)


@dataclass(frozen=True)
class EvaluationRunRequest:
    request_id: str
    suite_id: str
    deployment: DeploymentSnapshot
    execution_backend: str
    evaluator_revision: str
    environment: EnvironmentProfile
    asset_scope: tuple[str, ...] = ()
    created_at: str = ""  # history metadata only; not identity

    @property
    def run_id(self) -> str:
        """Compatibility name used by Prediction/performance evidence."""
        return self.request_id

    def to_json(self) -> dict[str, Any]:
        return {
            "requestId": self.request_id,
            "suiteId": self.suite_id,
            "deployment": self.deployment.to_json(),
            "executionBackend": self.execution_backend,
            "evaluatorRevision": self.evaluator_revision,
            "environment": self.environment.to_json(),
            "assetScope": list(self.asset_scope),
            "createdAt": self.created_at,
        }

    @classmethod
    def create(
        cls,
        suite_id: str,
        deployment: DeploymentSnapshot,
        backend: str,
        evaluator_revision: str,
        environment: EnvironmentProfile,
        asset_scope: tuple[str, ...] = (),
        created_at: str = "",
    ) -> "EvaluationRunRequest":
        body = {
            "suiteId": suite_id,
            "deployment": deployment.to_json(),
            "backend": backend,
            "evaluatorRevision": evaluator_revision,
            "environment": environment.to_json(),
            "assetScope": sorted(asset_scope),
        }
        return cls(
            request_id=f"request:{canonical_hash(body)}",
            suite_id=suite_id,
            deployment=deployment,
            execution_backend=backend,
            evaluator_revision=evaluator_revision,
            environment=environment,
            asset_scope=asset_scope,
            created_at=created_at,
        )


@dataclass(frozen=True)
class AssetEvaluationOutcome:
    asset_id: str
    kind: AssetOutcomeKind
    evidence_ref: str = ""
    reason: str = ""

    @property
    def outcome_id(self) -> str:
        body = {
            "assetId": self.asset_id,
            "kind": self.kind.value,
            "evidenceRef": self.evidence_ref,
            "reason": self.reason,
        }
        return f"asset-outcome:{canonical_hash(body)}"

    def to_json(self) -> dict[str, str]:
        return {
            "outcomeId": self.outcome_id,
            "assetId": self.asset_id,
            "kind": self.kind.value,
            "evidenceRef": self.evidence_ref,
            "reason": self.reason,
        }


def terminal_status_for(
    outcomes: tuple[AssetEvaluationOutcome, ...],
) -> TerminalStatus:
    """Apply the frozen terminal truth table; score values are irrelevant."""
    if any(o.kind == AssetOutcomeKind.INFRASTRUCTURE_FAILURE for o in outcomes):
        return TerminalStatus.INFRASTRUCTURE_FAILURE
    scored = sum(o.kind == AssetOutcomeKind.SCORABLE for o in outcomes)
    if outcomes and scored == len(outcomes):
        return TerminalStatus.COMPLETED
    if scored:
        return TerminalStatus.PARTIAL
    return TerminalStatus.INSUFFICIENT_EVIDENCE


@dataclass(frozen=True)
class EvaluationRunResult:
    result_id: str
    request_id: str
    terminal_status: TerminalStatus
    asset_outcomes: tuple[AssetEvaluationOutcome, ...]
    completed_at: str = ""  # history metadata only; not identity

    @classmethod
    def create(
        cls,
        request_id: str,
        asset_outcomes: tuple[AssetEvaluationOutcome, ...],
        completed_at: str = "",
    ) -> "EvaluationRunResult":
        status = terminal_status_for(asset_outcomes)
        outcome_ids = sorted(o.outcome_id for o in asset_outcomes)
        result_id = f"result:{canonical_hash({'requestId': request_id, 'terminalStatus': status.value, 'assetOutcomeIds': outcome_ids})}"
        return cls(result_id, request_id, status, asset_outcomes, completed_at)

    def to_json(self) -> dict[str, Any]:
        return {
            "resultId": self.result_id,
            "requestId": self.request_id,
            "terminalStatus": self.terminal_status.value,
            "assetOutcomes": [
                o.to_json() for o in sorted(self.asset_outcomes, key=lambda x: x.asset_id)
            ],
            "completedAt": self.completed_at,
        }


def save_result(result: EvaluationRunResult, out_dir: str | Path) -> Path:
    if result.terminal_status == TerminalStatus.PENDING:
        raise ValueError("only terminal EvaluationRunResult evidence is canonical")
    path = Path(out_dir) / f"{result.result_id.removeprefix('result:')}.json"
    return write_once_json(path, result.to_json())


# -----------------------------------------------------------------------
# Legacy historical artifact loader.  New code must use request/result.
# -----------------------------------------------------------------------

@dataclass(frozen=True)
class EvaluationRun:
    run_id: str
    suite_id: str
    deployment: DeploymentSnapshot
    execution_backend: str
    evaluator_revision: str
    environment: EnvironmentProfile
    terminal_status: TerminalStatus
    created_at: str = ""
    asset_scope: tuple[str, ...] = ()

    @classmethod
    def from_json(cls, d: dict[str, Any]) -> "EvaluationRun":
        env = d["environment"]
        return cls(
            run_id=d["runId"],
            suite_id=d["suiteId"],
            deployment=DeploymentSnapshot(**d["deployment"]),
            execution_backend=d["executionBackend"],
            evaluator_revision=d["evaluatorRevision"],
            environment=EnvironmentProfile(
                os_name=env["os_name"],
                cpu_arch=env["cpu_arch"],
                python_version=env["python_version"],
                worker_topology=env.get(
                    "worker_topology", "single-process/single-worker"
                ),
                extra=dict(env.get("extra", {})),
            ),
            terminal_status=TerminalStatus(d["terminalStatus"]),
            created_at=d.get("createdAt", ""),
            asset_scope=tuple(d.get("assetScope", [])),
        )

    def to_json(self) -> dict[str, Any]:
        return {
            "runId": self.run_id,
            "suiteId": self.suite_id,
            "deployment": self.deployment.to_json(),
            "executionBackend": self.execution_backend,
            "evaluatorRevision": self.evaluator_revision,
            "environment": self.environment.to_json(),
            "terminalStatus": self.terminal_status.value,
            "createdAt": self.created_at,
            "assetScope": list(self.asset_scope),
        }
