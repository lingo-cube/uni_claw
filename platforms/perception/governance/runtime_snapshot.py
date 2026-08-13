"""Runtime identity snapshot (G9/G10/G11 closure).

The identity a running process reports MUST describe what it actually
loaded/executes — never current disk state that diverged after load.

Snapshot timing: computed once at service startup (after model warmup),
then frozen for the process lifetime:
  • modelId — SHA-256 of the artifact bytes AT LOAD TIME
  • config manifest — built from the resolved in-memory config snapshot,
    with the label-mapping content hash captured from the SAME bytes read
    at load (never re-read per /version)
  • pipelineRevision — source/dependency/OCR-model identity AT STARTUP
  • deploymentId — derived from the snapshot constituents

Post-start disk mutation cannot alter the reported identity (RSI-01..03).
A legitimate artifact replacement takes effect on the NEXT process start
(RSI-04..06).
"""
from __future__ import annotations

import threading
from dataclasses import dataclass, field
from typing import Any

from evaluation.identity import sha256_file


@dataclass(frozen=True)
class IdentitySnapshot:
    model_id: str
    model_name: str
    config_id: str
    config_completeness: str
    pipeline_revision: str
    deployment_id: str
    schema_version: str
    config_hash: str                    # legacy compat (from loaded bytes)
    created_at_label: str = ""          # history metadata only

    def to_json(self) -> dict[str, Any]:
        return {
            "modelId": self.model_id,
            "modelName": self.model_name,
            "configId": self.config_id,
            "configCompleteness": self.config_completeness,
            "pipelineRevision": self.pipeline_revision,
            "deploymentId": self.deployment_id,
            "schemaVersion": self.schema_version,
            "configHash": self.config_hash,
        }


_snapshot: IdentitySnapshot | None = None
_lock = threading.Lock()


def capture_snapshot(*, model_path: str, model_name: str,
                     config: Any, config_hash: str,
                     label_mapping_path: str | None) -> IdentitySnapshot:
    """Compute the process identity snapshot once (at startup).

    All hashes are computed HERE, at capture time — later disk changes
    cannot leak into the reported identity.
    """
    from governance.config_manifest import build_from_perception_config
    from governance.deployment import PerceptionDeploymentCandidate
    from governance.pipeline_revision import compute_pipeline_revision

    model_id = sha256_file(model_path)
    manifest = build_from_perception_config(
        config, label_mapping_path,
        label_mapping_content_hash=(
            f"sha256:{config_hash}" if not str(config_hash).startswith("sha256:")
            else config_hash))  # loaded bytes, never re-read
    rev = compute_pipeline_revision()
    candidate = PerceptionDeploymentCandidate(
        schema_version="uniclaw.localVisionEvidence.v1",
        model_id=model_id,
        config_id=manifest.config_id,
        pipeline_revision=rev["pipelineRevision"],
        service_version="1.0",
    )
    return IdentitySnapshot(
        model_id=model_id,
        model_name=model_name,
        config_id=manifest.config_id,
        config_completeness=manifest.completeness.value,
        pipeline_revision=rev["pipelineRevision"],
        deployment_id=candidate.deployment_id,
        schema_version="uniclaw.localVisionEvidence.v1",
        config_hash=config_hash,        # legacy hash from the LOADED bytes
    )


def set_snapshot(snap: IdentitySnapshot) -> None:
    global _snapshot
    with _lock:
        _snapshot = snap


def get_snapshot() -> IdentitySnapshot | None:
    with _lock:
        return _snapshot
