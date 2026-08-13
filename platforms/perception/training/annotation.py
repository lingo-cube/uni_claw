"""Annotation foundation (P4-T1).

Immutable-per-version, content-addressed. Correcting a label creates a NEW
annotation identity — historical versions referenced by existing TrainingRuns
remain intact (TR-19).

MODEL_PREDICTION != ACCEPTED_ANNOTATION (frozen): a model-assisted label
requires an explicit acceptance event before it is canonical training truth.
"""
from __future__ import annotations

import json
from dataclasses import dataclass, field
from enum import Enum
from pathlib import Path
from typing import Any

from evaluation.identity import canonical_hash
from evaluation.stage import EvaluationTargetStage, LabelSpace
from . import TRAINING_SCHEMA_VERSION
from persistence import write_once_json


class AnnotationSource(str, Enum):
    HUMAN_CREATED = "HUMAN_CREATED"
    HUMAN_CORRECTED = "HUMAN_CORRECTED"
    IMPORTED = "IMPORTED"
    MODEL_ASSISTED = "MODEL_ASSISTED"


class ReviewStatus(str, Enum):
    DRAFT = "DRAFT"
    REVIEWED = "REVIEWED"
    ACCEPTED = "ACCEPTED"
    REJECTED = "REJECTED"

@dataclass(frozen=True)
class AcceptanceProvenance:
    review_event_id: str
    reviewer_identity: str
    predecessor_annotation_id: str


@dataclass(frozen=True)
class Annotation:
    """Immutable-per-version training annotation record."""
    asset_id: str
    target_stage: EvaluationTargetStage
    label_space: LabelSpace
    source: AnnotationSource
    review_status: ReviewStatus
    label_payload: dict[str, Any]
    provenance: str = ""
    predecessor_annotation_id: str | None = None
    acceptance_provenance: AcceptanceProvenance | None = None
    # label_payload shape (RAW_DETECTION training): {"boxes": [{"class": str,
    # "bounds": [cx, cy, w, h] normalized}, ...]} — YOLO label format
    # semantics; class ids use the model's class-index vocabulary.

    @property
    def annotation_id(self) -> str:
        body = {
            "schema": TRAINING_SCHEMA_VERSION,
            "assetId": self.asset_id,
            "targetStage": self.target_stage.value,
            "labelSpace": self.label_space.value,
            "source": self.source.value,
            "reviewStatus": self.review_status.value,
            "labelPayload": self.label_payload,
            "provenance": self.provenance,
            "predecessor": self.predecessor_annotation_id,
        }
        if self.acceptance_provenance is not None:
            body["acceptanceProvenance"] = {
                "reviewEventId": self.acceptance_provenance.review_event_id,
                "reviewerIdentity": self.acceptance_provenance.reviewer_identity,
                "predecessorAnnotationId":
                    self.acceptance_provenance.predecessor_annotation_id,
            }
        return f"annotation:{canonical_hash(body)}"

    @property
    def is_accepted_training_truth(self) -> bool:
        return self.review_status == ReviewStatus.ACCEPTED and self.acceptance_provenance is not None and self.predecessor_annotation_id is not None

    def to_json(self) -> dict[str, Any]:
        return {
            "annotationId": self.annotation_id,
            "assetId": self.asset_id,
            "targetStage": self.target_stage.value,
            "labelSpace": self.label_space.value,
            "source": self.source.value,
            "reviewStatus": self.review_status.value,
            "labelPayload": self.label_payload,
            "provenance": self.provenance,
            "predecessorAnnotationId": self.predecessor_annotation_id,
            "acceptanceProvenance": (self.acceptance_provenance.__dict__ if self.acceptance_provenance else None),
        }

    @classmethod
    def from_json(cls, d: dict[str, Any]) -> "Annotation":
        return cls(
            asset_id=d["assetId"],
            target_stage=EvaluationTargetStage(d["targetStage"]),
            label_space=LabelSpace(d["labelSpace"]),
            source=AnnotationSource(d["source"]),
            review_status=ReviewStatus(d["reviewStatus"]),
            label_payload=dict(d["labelPayload"]),
            provenance=d.get("provenance", ""),
            predecessor_annotation_id=d.get("predecessorAnnotationId"),
            acceptance_provenance=(AcceptanceProvenance(d["acceptanceProvenance"]["review_event_id"], d["acceptanceProvenance"]["reviewer_identity"], d["acceptanceProvenance"]["predecessor_annotation_id"]) if d.get("acceptanceProvenance") else None),
        )


def create_annotation(*, asset_id: str, target_stage: EvaluationTargetStage,
                      label_space: LabelSpace, source: AnnotationSource,
                      label_payload: dict[str, Any], provenance: str = "",
                      review_status: ReviewStatus = ReviewStatus.DRAFT,
                      predecessor_annotation_id: str | None = None) -> Annotation:
    """Creation boundary. DRAFT by default; ACCEPTED requires an explicit
    acceptance event (never automatic — TR-05)."""
    if review_status == ReviewStatus.ACCEPTED:
        raise ValueError("accepted annotations require accept_annotation")
    return Annotation(
        asset_id=asset_id, target_stage=target_stage, label_space=label_space,
        source=source, review_status=review_status, label_payload=label_payload,
        provenance=provenance, predecessor_annotation_id=predecessor_annotation_id)


# ── Acceptance events (GAP-007: real verifiable chain) ──────────

@dataclass(frozen=True)
class AnnotationAcceptanceEvent:
    """Immutable repository-native review event.

    Field presence is NOT authority — the EVENT must exist on disk and be
    validated by the admission boundary (validate_acceptance_chain).
    """
    review_event_id: str
    predecessor_annotation_id: str
    accepted_annotation_id: str      # full accepted-record content binding
    accepted_payload_hash: str       # content binding to the accepted payload
    reviewer_identity: str
    decision: str                    # "ACCEPT"
    asset_id: str
    target_stage: str
    label_space: str

    def to_json(self) -> dict[str, Any]:
        return {
            "reviewEventId": self.review_event_id,
            "predecessorAnnotationId": self.predecessor_annotation_id,
            "acceptedAnnotationId": self.accepted_annotation_id,
            "acceptedPayloadHash": self.accepted_payload_hash,
            "reviewerIdentity": self.reviewer_identity,
            "decision": self.decision,
            "assetId": self.asset_id,
            "targetStage": self.target_stage,
            "labelSpace": self.label_space,
        }

    @classmethod
    def from_json(cls, d: dict[str, Any]) -> "AnnotationAcceptanceEvent":
        return cls(
            review_event_id=d["reviewEventId"],
            predecessor_annotation_id=d["predecessorAnnotationId"],
            accepted_annotation_id=d.get("acceptedAnnotationId", ""),
            accepted_payload_hash=d.get("acceptedPayloadHash", ""),
            reviewer_identity=d["reviewerIdentity"],
            decision=d["decision"],
            asset_id=d["assetId"],
            target_stage=d["targetStage"],
            label_space=d["labelSpace"],
        )


def acceptance_event_for(ann: Annotation, corrected_by: str = "human") -> AnnotationAcceptanceEvent:
    """Reconstruct the acceptance event bound to an ACCEPTED annotation.

    Event identity scheme (canonical, single owner): the review event id
    is a pure function of (predecessor annotation id, reviewer identity) —
    the same scheme accept_annotation uses. Persisting this reconstruction
    next to the annotation record makes the chain verifiable on disk.
    """
    if not ann.acceptance_provenance or ann.predecessor_annotation_id is None:
        raise ValueError("annotation has no acceptance provenance")
    pred_id = ann.predecessor_annotation_id
    reviewer = ann.acceptance_provenance.reviewer_identity or corrected_by
    event_id = f"review:{canonical_hash((pred_id, reviewer))}"
    return AnnotationAcceptanceEvent(
        review_event_id=event_id,
        predecessor_annotation_id=pred_id,
        accepted_annotation_id=ann.annotation_id,
        accepted_payload_hash=canonical_hash(ann.label_payload),
        reviewer_identity=reviewer,
        decision="ACCEPT",
        asset_id=ann.asset_id,
        target_stage=ann.target_stage.value,
        label_space=ann.label_space.value,
    )


def load_annotation_record(
    annotation_id: str, annotation_dir: str | Path,
) -> Annotation | None:
    """Load one canonical annotation by its content-derived identity.

    This is a read-only boundary.  The path is only an index: the loaded
    record must recompute to *annotation_id* before it can participate in
    acceptance or admission.  Legacy/malformed records remain inspectable by
    callers through their own tools, but are not canonical records here.
    """
    if not annotation_id.startswith("annotation:"):
        return None
    path = Path(annotation_dir) / f"{annotation_id.removeprefix('annotation:')}.json"
    try:
        record = Annotation.from_json(json.loads(path.read_text(encoding="utf-8")))
    except (FileNotFoundError, KeyError, TypeError, ValueError, json.JSONDecodeError):
        return None
    return record if record.annotation_id == annotation_id else None


def load_acceptance_event(
    review_event_id: str, event_dir: str | Path,
) -> AnnotationAcceptanceEvent | None:
    """Load an acceptance event only when its deterministic identity holds.

    A caller-selected filename or ``reviewEventId`` is not event authority.
    Canonical identity is derived from the predecessor annotation and reviewer
    recorded in the event itself; that value must also equal the requested id.
    """
    if not review_event_id.startswith("review:"):
        return None
    path = Path(event_dir) / f"{review_event_id.removeprefix('review:')}.json"
    try:
        event = AnnotationAcceptanceEvent.from_json(
            json.loads(path.read_text(encoding="utf-8")))
    except (FileNotFoundError, KeyError, TypeError, ValueError, json.JSONDecodeError):
        return None
    expected_id = f"review:{canonical_hash((event.predecessor_annotation_id, event.reviewer_identity))}"
    if event.review_event_id != review_event_id or event.review_event_id != expected_id:
        return None
    return event


def accept_and_persist(
    draft: "Annotation",
    reviewer: str,
    *,
    annotation_dir: str | Path,
    event_dir: str | Path,
) -> "Annotation":
    """CANONICAL acceptance mint path (GAP-007 closure).

    The ONLY way an ACCEPTED annotation + its acceptance event enter
    canonical storage:
      existing predecessor draft (caller-persisted via public save_annotation)
      → accept_annotation (derives accepted content + event identity)
      → _persist_acceptance_event (write-once)
      → _persist_annotation_record (write-once)

    Direct public construction of AnnotationAcceptanceEvent / ACCEPTED
    Annotation carries zero minting authority — public save surfaces
    refuse authoritative content and no separately callable authoritative
    writer exists.
    """
    if not reviewer.strip():
        raise ValueError("acceptance actor required")
    if draft.review_status == ReviewStatus.ACCEPTED:
        raise ValueError("accepted annotation cannot be its own predecessor")

    # The predecessor must already be a content-addressed persisted record.
    # An arbitrary in-memory object cannot become review authority.
    annotation_root = Path(annotation_dir)
    predecessor = load_annotation_record(draft.annotation_id, annotation_root)
    if predecessor is None:
        raise ValueError("acceptance predecessor must already be persisted")
    if predecessor != draft:
        raise ValueError("acceptance predecessor identity/content mismatch")

    accepted = accept_annotation(draft, corrected_by=reviewer)
    event = acceptance_event_for(accepted)
    event_root = Path(event_dir)
    event_root.mkdir(parents=True, exist_ok=True)
    write_once_json(
        event_root / f"{event.review_event_id.replace('review:', '')}.json",
        event.to_json())
    annotation_root.mkdir(parents=True, exist_ok=True)
    write_once_json(
        annotation_root / f"{accepted.annotation_id.replace('annotation:', '')}.json",
        accepted.to_json())
    return accepted


def validate_acceptance_chain(
    annotation: Annotation,
    *,
    annotation_dir: str | Path,
    event_dir: str | Path,
) -> tuple[bool, str]:
    """GAP-007 admission boundary: a verifiable acceptance chain.

    Requirements:
      • review_status == ACCEPTED
      • acceptance_provenance present (event id + predecessor id)
      • predecessor Annotation RECORD exists on disk
      • acceptance EVENT exists on disk
      • event binds predecessor → accepted (both ids match)
      • event.decision == ACCEPT
      • asset/stage/LabelSpace lineage compatible with the predecessor
    Returns (ok, reason). Field presence alone grants nothing.
    """
    if annotation.review_status != ReviewStatus.ACCEPTED:
        return False, "not ACCEPTED"
    if annotation.acceptance_provenance is None or annotation.predecessor_annotation_id is None:
        return False, "missing acceptance provenance"
    pred_id = annotation.predecessor_annotation_id
    event_id = annotation.acceptance_provenance.review_event_id
    if not event_id:
        return False, "empty review event id"
    if annotation.acceptance_provenance.predecessor_annotation_id != pred_id:
        return False, "provenance predecessor mismatch"

    predecessor = load_annotation_record(pred_id, annotation_dir)
    if predecessor is None:
        return False, f"predecessor {pred_id} not found"
    event = load_acceptance_event(event_id, event_dir)
    if event is None:
        return False, f"review event {event_id} not found"
    if event.decision != "ACCEPT":
        return False, f"event decision is {event.decision}"
    expected_event_id = f"review:{canonical_hash((pred_id, event.reviewer_identity))}"
    if event.review_event_id != expected_event_id:
        return False, "review event identity mismatch"
    if annotation.acceptance_provenance.reviewer_identity != event.reviewer_identity:
        return False, "reviewer identity mismatch"
    if event.predecessor_annotation_id != pred_id:
        return False, "event predecessor mismatch"
    if event.accepted_annotation_id != annotation.annotation_id:
        return False, "event accepted-annotation identity mismatch"
    if event.accepted_payload_hash != canonical_hash(annotation.label_payload):
        return False, "event accepted-binding mismatch"
    # lineage compatibility with predecessor
    if (event.asset_id != predecessor.asset_id
            or event.asset_id != annotation.asset_id):
        return False, "asset lineage mismatch"
    if event.target_stage != annotation.target_stage.value:
        return False, "stage lineage mismatch"
    if event.label_space != annotation.label_space.value:
        return False, "label-space lineage mismatch"
    if annotation.target_stage != predecessor.target_stage:
        return False, "predecessor stage mismatch"
    if annotation.label_space != predecessor.label_space:
        return False, "predecessor label-space mismatch"
    return True, ""


LEGACY_ACCEPTANCE_PROVENANCE = "LEGACY_ACCEPTANCE_PROVENANCE"


def acceptance_stance(annotation: Annotation,
                      *, annotation_dir: str | Path,
                      event_dir: str | Path) -> str:
    """Classify an ACCEPTED record's admissibility.

    Returns "CANONICAL" (verifiable chain) or
    LEGACY_ACCEPTANCE_PROVENANCE (readable history, NOT admissible into
    new canonical training).
    """
    if annotation.review_status != ReviewStatus.ACCEPTED:
        return "NOT_ACCEPTED"
    ok, _ = validate_acceptance_chain(
        annotation, annotation_dir=annotation_dir, event_dir=event_dir)
    return "CANONICAL" if ok else LEGACY_ACCEPTANCE_PROVENANCE


def accept_annotation(ann: Annotation, corrected_by: str = "human") -> Annotation:
    """Explicit acceptance event → new annotation identity with ACCEPTED
    status. The DRAFT version remains referenced by its own id (immutable)."""
    if not corrected_by.strip():
        raise ValueError("acceptance actor required")
    if ann.review_status == ReviewStatus.ACCEPTED:
        raise ValueError("annotation is already accepted")
    return Annotation(
        asset_id=ann.asset_id, target_stage=ann.target_stage,
        label_space=ann.label_space, source=ann.source,
        review_status=ReviewStatus.ACCEPTED, label_payload=ann.label_payload,
        provenance=f"{ann.provenance} accepted-by:{corrected_by}",
        predecessor_annotation_id=ann.annotation_id,
        acceptance_provenance=AcceptanceProvenance(f"review:{canonical_hash((ann.annotation_id, corrected_by))}", corrected_by, ann.annotation_id))


def save_annotation(ann: Annotation, out_dir: str | Path) -> Path:
    """PUBLIC writer restricted to NON-authoritative records (RM-ANN-02):
    drafts and inspection artifacts only. ACCEPTED records can only enter
    canonical storage through accept_and_persist."""
    if ann.review_status == ReviewStatus.ACCEPTED:
        raise ValueError(
            "ACCEPTED annotations have no public save authority — "
            "canonical acceptance requires accept_and_persist")
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    path = out / f"{ann.annotation_id.replace('annotation:', '')}.json"
    return write_once_json(path, ann.to_json())
