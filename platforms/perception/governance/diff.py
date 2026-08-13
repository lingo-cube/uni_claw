"""Candidate vs ACTIVE identity diff (P4-D9) — mechanical only.

Reports which identity axes changed and classifies the change. NO policy.
SERVICE_ONLY with identical behavior identity → deploymentId unchanged;
it is packaging metadata difference, not perception behavior change.
"""
from __future__ import annotations

from dataclasses import dataclass

from .deployment import PerceptionDeploymentCandidate

_CHANGE_CLASSES = (
    "MODEL_ONLY", "CONFIG_ONLY", "PIPELINE_ONLY", "MODEL_AND_CONFIG",
    "SCHEMA_CHANGE", "OCR_CHANGE", "SERVICE_ONLY", "MULTI_AXIS",
    "NO_BEHAVIOR_CHANGE",
)


@dataclass(frozen=True)
class IdentityDiff:
    model_changed: bool
    config_changed: bool
    pipeline_changed: bool
    schema_changed: bool
    ocr_changed: bool = False          # derivable where config carries ocr
    service_changed: bool = False      # metadata only
    behavior_changed: bool = True

    @property
    def classification(self) -> str:
        axes = [self.model_changed, self.config_changed,
                self.pipeline_changed, self.schema_changed]
        if not self.behavior_changed:
            return "NO_BEHAVIOR_CHANGE"
        if sum(1 for a in axes if a) == 0:
            return "SERVICE_ONLY"       # metadata-only difference
        if axes == [True, False, False, False]:
            return "MODEL_ONLY"
        if axes == [False, True, False, False]:
            return "CONFIG_ONLY"
        if axes == [False, False, True, False]:
            return "PIPELINE_ONLY"
        if self.schema_changed and not self.model_changed \
                and not self.config_changed and not self.pipeline_changed:
            return "SCHEMA_CHANGE"
        if self.model_changed and self.config_changed \
                and not self.pipeline_changed and not self.schema_changed:
            return "MODEL_AND_CONFIG"
        if sum(1 for a in axes if a) > 1:
            return "MULTI_AXIS"
        return "MULTI_AXIS"

    def to_json(self) -> dict:
        return {"modelChanged": self.model_changed,
                "configChanged": self.config_changed,
                "pipelineChanged": self.pipeline_changed,
                "schemaChanged": self.schema_changed,
                "ocrChanged": self.ocr_changed,
                "serviceChanged": self.service_changed,
                "behaviorChanged": self.behavior_changed,
                "classification": self.classification}


def diff_identity(active: PerceptionDeploymentCandidate,
                  candidate: PerceptionDeploymentCandidate) -> IdentityDiff:
    """Mechanical identity comparison — no quality, no policy."""
    ocr_changed = _ocr_signature(active) != _ocr_signature(candidate)
    service_changed = active.service_version != candidate.service_version
    behavior_changed = (
        active.schema_version != candidate.schema_version
        or active.model_id != candidate.model_id
        or active.config_id != candidate.config_id
        or active.pipeline_revision != candidate.pipeline_revision
    )
    return IdentityDiff(
        model_changed=active.model_id != candidate.model_id,
        config_changed=active.config_id != candidate.config_id,
        pipeline_changed=active.pipeline_revision != candidate.pipeline_revision,
        schema_changed=active.schema_version != candidate.schema_version,
        ocr_changed=ocr_changed,
        service_changed=service_changed,
        behavior_changed=behavior_changed,
    )


def _ocr_signature(c: PerceptionDeploymentCandidate) -> str:
    """OCR evidence-affecting identity as represented transitively —
    configId carries backend/mode/textScore; no separate axis exists
    beyond config (DI-19: OCR drift travels through configId)."""
    return c.config_id
