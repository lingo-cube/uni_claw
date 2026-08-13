"""Candidate creation boundary (P4-T6).

TrainingRun → Checkpoint → ModelArtifact → Candidate is EXPLICIT.
Candidate creation: NO quality evaluation, NO promotion, NO ACTIVE mutation.
For the foundation mini-run: CANDIDATE_TEST_ONLY — structurally impossible
to become ACTIVE in this slice (no activation API exists).
"""
from __future__ import annotations

import json
from dataclasses import dataclass
from enum import Enum
from pathlib import Path
from typing import Any

from evaluation.identity import canonical_hash
from persistence import write_once_json
from . import TRAINING_SCHEMA_VERSION


class CandidateStatus(str, Enum):
    CANDIDATE_TEST_ONLY = "CANDIDATE_TEST_ONLY"
    CANDIDATE = "CANDIDATE"
    # future: EVALUATED / REJECTED — release lifecycle, not this slice


@dataclass(frozen=True)
class Candidate:
    """A model artifact + its training provenance, awaiting evaluation.

    Distinct from PerceptionDeploymentCandidate (deployment unit adds
    PerceptionConfig + profile). Evaluation happens NEXT in the frozen
    workflow — never here.
    """
    model_artifact_id: str         # modelId (full SHA-256)
    model_name: str
    training_run_id: str
    dataset_version_id: str
    training_config_id: str
    status: CandidateStatus = CandidateStatus.CANDIDATE_TEST_ONLY

    @property
    def candidate_id(self) -> str:
        return f"cand:{canonical_hash(self._canonical())}"

    def _canonical(self) -> dict[str, Any]:
        return {
            "schema": TRAINING_SCHEMA_VERSION,
            "modelArtifactId": self.model_artifact_id,
            "modelName": self.model_name,
            "trainingRunId": self.training_run_id,
            "datasetVersionId": self.dataset_version_id,
            "trainingConfigId": self.training_config_id,
            "status": self.status.value,
        }

    def to_json(self) -> dict[str, Any]:
        d = self._canonical()
        d["candidateId"] = self.candidate_id
        return d


def create_candidate(*, model_artifact_id: str, model_name: str,
                     training_run_id: str, dataset_version_id: str,
                     training_config_id: str) -> Candidate:
    """Candidate creation requires artifact + training provenance only.
    No evaluation result required (evaluation happens next)."""
    return Candidate(
        model_artifact_id=model_artifact_id, model_name=model_name,
        training_run_id=training_run_id, dataset_version_id=dataset_version_id,
        training_config_id=training_config_id,
        status=CandidateStatus.CANDIDATE_TEST_ONLY)


def save_candidate(cand: Candidate, out_dir: str | Path) -> Path:
    out = Path(out_dir)
    path = out / f"{cand.candidate_id.replace('cand:', '')}.json"
    return write_once_json(path, cand.to_json())
