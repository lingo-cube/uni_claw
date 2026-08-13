"""PerceptionDeploymentCandidate / Identity / Instance (P4-D5).

Canonical behavior identity (D0-1 reconciled):
  deploymentId = SHA-256({schemaVersion, modelId, configId, pipelineRevision})

serviceVersion: metadata only — a serviceVersion-only change with identical
behavior axes MUST NOT change deploymentId (IDR-01, DI-18).
labelMappingRef: diagnostic metadata — ConfigId owns the mapping's
behavior impact transitively (IDR-03).
"""
from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from evaluation.identity import canonical_hash
from persistence import write_once_json

IDENTITY_SCHEMA = "uniclaw.deploymentIdentity.v1"

# ── canonical identity axes (the ONLY four) ─────────────────────
IDENTITY_AXES: tuple[str, ...] = (
    "schemaVersion", "modelId", "configId", "pipelineRevision")


@dataclass(frozen=True)
class PerceptionDeploymentCandidate:
    """Immutable proposed combination to evaluate."""
    schema_version: str
    model_id: str                      # full SHA-256
    config_id: str
    pipeline_revision: str
    service_version: str = ""          # metadata only — NOT identity
    label_mapping_ref: str | None = None  # diagnostic — owned by ConfigId
    completeness: str = "COMPLETE"

    def identity_content(self) -> dict[str, Any]:
        return {
            "schema": IDENTITY_SCHEMA,
            "schemaVersion": self.schema_version,
            "modelId": self.model_id,
            "configId": self.config_id,
            "pipelineRevision": self.pipeline_revision,
        }

    @property
    def deployment_id(self) -> str:
        return f"deploy:{canonical_hash(self.identity_content())}"

    def to_json(self) -> dict[str, Any]:
        return {
            "deploymentId": self.deployment_id,
            "schemaVersion": self.schema_version,
            "modelId": self.model_id,
            "configId": self.config_id,
            "pipelineRevision": self.pipeline_revision,
            "serviceVersion": self.service_version,       # metadata only
            "labelMappingRef": self.label_mapping_ref,    # diagnostic only
            "completeness": self.completeness,
        }


# PerceptionDeploymentIdentity = the canonical (deploymentId, axes) pair.
@dataclass(frozen=True)
class PerceptionDeploymentIdentity:
    deployment_id: str
    schema_version: str
    model_id: str
    config_id: str
    pipeline_revision: str

    @classmethod
    def from_candidate(cls, c: PerceptionDeploymentCandidate) -> "PerceptionDeploymentIdentity":
        return cls(deployment_id=c.deployment_id, schema_version=c.schema_version,
                   model_id=c.model_id, config_id=c.config_id,
                   pipeline_revision=c.pipeline_revision)

    def to_json(self) -> dict[str, Any]:
        return {"deploymentId": self.deployment_id,
                "schemaVersion": self.schema_version,
                "modelId": self.model_id, "configId": self.config_id,
                "pipelineRevision": self.pipeline_revision}


# DeploymentInstance: operational facts — NEVER identity (DI-09).
@dataclass(frozen=True)
class DeploymentInstance:
    deployment_id: str                # which identity this instance runs
    pid: str = ""
    session_id: str = ""
    uds_path: str = ""
    started_at: str = ""              # history metadata only
    restart_count: int = 0

    def to_json(self) -> dict[str, Any]:
        return {"deploymentId": self.deployment_id, "pid": self.pid,
                "sessionId": self.session_id, "udsPath": self.uds_path,
                "startedAt": self.started_at, "restartCount": self.restart_count}


def save_candidate(candidate: PerceptionDeploymentCandidate,
                   out_dir: str | Path) -> Path:
    out = Path(out_dir)
    path = out / f"{candidate.deployment_id.replace('deploy:', '')}.json"
    return write_once_json(path, candidate.to_json())


def candidate_from_json(d: dict[str, Any]) -> PerceptionDeploymentCandidate:
    return PerceptionDeploymentCandidate(
        schema_version=d["schemaVersion"], model_id=d["modelId"],
        config_id=d["configId"], pipeline_revision=d["pipelineRevision"],
        service_version=d.get("serviceVersion", ""),
        label_mapping_ref=d.get("labelMappingRef"),
        completeness=d.get("completeness", "COMPLETE"),
    )
