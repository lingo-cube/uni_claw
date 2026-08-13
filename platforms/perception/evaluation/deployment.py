"""DeploymentSnapshot — the exact deployment being evaluated.

Truthfully available identity only. Canonical configId is NOT fabricated
(LEGACY_PARTIAL_CONFIG_IDENTITY until Phase 4 P4-11).
"""
from __future__ import annotations

from dataclasses import dataclass, asdict
from typing import Any

from .identity import canonical_hash


@dataclass(frozen=True)
class DeploymentSnapshot:
    service_version: str
    schema_version: str
    model_name: str
    model_id: str                       # full 64-char SHA-256 — frozen identity
    ocr_backend: str
    pipeline_revision: str
    config_identity: str                # "LEGACY_PARTIAL_CONFIG_IDENTITY" | "CANONICAL_CONFIG_ID"
    config_hash: str                    # SHA-256(label-mapping.json) — historical/compat
    profile: str = "INITIAL_CURRENT_DEPLOYMENT_PROFILE"
    config_id: str | None = None        # P4-D8: canonical (None for historical)
    deployment_id: str | None = None    # P4-D8: canonical deployment identity

    @property
    def identity_hash(self) -> str:
        """B7/B8 falsifier anchor: any identity-relevant field change →
        different hash."""
        return canonical_hash(asdict(self))

    @property
    def is_canonical(self) -> bool:
        return (self.config_id is not None and self.deployment_id is not None
                and self.config_identity == "CANONICAL_CONFIG_ID")

    def to_json(self) -> dict[str, Any]:
        return asdict(self)

    @classmethod
    def current_active(cls, *, service_version: str = "1.0",
                       schema_version: str = "uniclaw.localVisionEvidence.v1",
                       model_name: str = "android_ui_detection_yolov8",
                       model_id: str = (
                           "3f39b0d64832801072ac099ba370afe113aea32a360d4de8e24960b017b6d782"),
                       ocr_backend: str = "rapidocr",
                       pipeline_revision: str | None = None,
                       config_hash: str | None = None,
                       config_id: str | None = None,
                       deployment_id: str | None = None,
                       canonical: bool = False) -> "DeploymentSnapshot":
        """I12: current ACTIVE deployment truth.

        modelId = full SHA-256 of models/yolo/android_ui_detection_yolov8/best.pt
        (frozen at Phase 3 graduation).

        P4-D8: canonical=True attaches configId + deploymentId
        (CANONICAL_CONFIG_ID stance); historical snapshots stay partial.
        """
        if pipeline_revision is None:
            from uniclaw_perception import __version__ as _pv
            pipeline_revision = _pv
        if config_hash is None:
            from .identity import sha256_file
            from pathlib import Path
            config_hash = sha256_file(
                Path(__file__).resolve().parent.parent / "config" / "label-mapping.json")
        return cls(
            service_version=service_version,
            schema_version=schema_version,
            model_name=model_name,
            model_id=model_id,
            ocr_backend=ocr_backend,
            pipeline_revision=pipeline_revision,
            config_identity=("CANONICAL_CONFIG_ID" if canonical
                             else "LEGACY_PARTIAL_CONFIG_IDENTITY"),
            config_hash=config_hash,
            config_id=config_id if canonical else None,
            deployment_id=deployment_id if canonical else None,
        )
