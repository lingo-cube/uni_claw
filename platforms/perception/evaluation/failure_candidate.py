"""FailureEpisode → RegressionAssetCandidate structural boundary (I37/R24).

Frozen by Phase 4 gate (PF5):
  • A FailureEpisode NEVER assigns GroundTruth.
  • The boundary produces a CANDIDATE only; admission is a separate decision.
  • Source links (failureEpisodeId / traceRunId / frameId) are preserved
    where present; missing links stay missing.
  • No real FailureEpisode exists in the current corpus → the boundary is
    proven structurally with SYNTHETIC provenance only; no real failure is
    fabricated.
"""
from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any


@dataclass(frozen=True)
class RegressionAssetCandidate:
    """Candidate record produced by failure triage — NOT an admitted asset.

    candidateId is content-addressed over the structural record so that
    admission decisions are reproducible.
    """
    candidate_id: str
    source_type: str               # "failure_episode" | "synthetic_structural"
    source_failure_episode_id: str | None   # missing stays missing
    source_trace_run_id: str | None
    source_frame_id: str | None
    provenance: str                # candidate provenance, preserved (B3)
    ground_truth: None = None      # PF5: no GT can be assigned here
    notes: dict[str, Any] = field(default_factory=dict)

    @property
    def has_ground_truth(self) -> bool:
        return self.ground_truth is not None

    def to_json(self) -> dict[str, Any]:
        return {
            "candidateId": self.candidate_id,
            "sourceType": self.source_type,
            "sourceFailureEpisodeId": self.source_failure_episode_id,
            "sourceTraceRunId": self.source_trace_run_id,
            "sourceFrameId": self.source_frame_id,
            "provenance": self.provenance,
            "groundTruth": None,      # structurally impossible to set
            "notes": self.notes,
        }


def failure_episode_to_candidate(
    *, source_failure_episode_id: str | None = None,
    source_trace_run_id: str | None = None,
    source_frame_id: str | None = None,
    provenance: str = "SYNTHETIC",
) -> RegressionAssetCandidate:
    """Structural boundary: FailureEpisode → candidate, GT NOT assignable.

    Currently exercised only with SYNTHETIC provenance (no real suitable
    FailureEpisode in the perception corpus). Do not fabricate a real one.
    """
    from .identity import canonical_hash
    body = {
        "sourceType": "failure_episode",
        "sourceFailureEpisodeId": source_failure_episode_id,
        "sourceTraceRunId": source_trace_run_id,
        "sourceFrameId": source_frame_id,
        "provenance": provenance,
    }
    return RegressionAssetCandidate(
        candidate_id=f"rc:{canonical_hash(body)}",
        source_type="failure_episode",
        source_failure_episode_id=source_failure_episode_id,
        source_trace_run_id=source_trace_run_id,
        source_frame_id=source_frame_id,
        provenance=provenance,
        ground_truth=None,   # PF5 — enforced by frozen dataclass field type
    )
