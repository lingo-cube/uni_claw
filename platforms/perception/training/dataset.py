"""DatasetVersion foundation (P4-T2).

Immutable membership manifest — NOT a mutable folder.
Identity = SHA-256 of canonical membership (assets + annotation refs +
split assignment + capture grouping). Display metadata excluded from identity.
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


class Split(str, Enum):
    TRAIN = "TRAIN"
    VALIDATION = "VALIDATION"
    TEST = "TEST"
    CALIBRATION = "CALIBRATION"


@dataclass(frozen=True)
class DatasetMembership:
    asset_id: str                  # reference to existing AssetId — no byte copy
    split: Split
    annotation_id: str             # exact annotation version used
    capture_group_id: str | None = None   # L-2 leakage grouping where known


@dataclass(frozen=True)
class DatasetVersion:
    """Immutable dataset membership + annotation references."""
    members: tuple[DatasetMembership, ...]
    description: str = ""          # display metadata — NOT in identity

    @property
    def dataset_version_id(self) -> str:
        return f"dataset:{canonical_hash(self._canonical())}"

    def _canonical(self) -> dict[str, Any]:
        return {
            "schema": TRAINING_SCHEMA_VERSION,
            "members": sorted(
                (
                    {
                        "assetId": m.asset_id,
                        "split": m.split.value,
                        "annotationId": m.annotation_id,
                        "captureGroupId": m.capture_group_id,
                    }
                    for m in self.members
                ),
                key=lambda x: (x["split"], x["assetId"]),
            ),
        }

    def with_members(self, members: tuple[DatasetMembership, ...],
                     description: str | None = None) -> "DatasetVersion":
        """TR-02/03/04: membership/annotation/split change → NEW version."""
        return DatasetVersion(
            members=members,
            description=description if description is not None else self.description)

    def to_json(self) -> dict[str, Any]:
        return {
            "datasetId": self.dataset_version_id,
            "members": [
                {"assetId": m.asset_id, "split": m.split.value,
                 "annotationId": m.annotation_id,
                 "captureGroupId": m.capture_group_id}
                for m in self.members
            ],
            "description": self.description,
        }

    @classmethod
    def from_json(cls, d: dict[str, Any]) -> "DatasetVersion":
        return cls(
            members=tuple(
                DatasetMembership(
                    asset_id=m["assetId"], split=Split(m["split"]),
                    annotation_id=m["annotationId"],
                    capture_group_id=m.get("captureGroupId"))
                for m in d.get("members", [])),
            description=d.get("description", ""))

@dataclass(frozen=True)
class TrainingAdmissionReceipt:
    dataset_version_id: str
    protected_set_id: str
    policy_version: str = "LEAK-01..07-v1"
    findings: tuple[str, ...] = ()
    exact_content_clear: bool = True
    capture_group_clear: bool = True
    admission_result: str = "ADMITTED"

    @property
    def receipt_id(self) -> str:
        return f"admission:{canonical_hash(self._canonical())}"

    def _canonical(self) -> dict[str, Any]:
        return {
            "schema": TRAINING_SCHEMA_VERSION,
            "datasetVersionId": self.dataset_version_id,
            "protectedSetId": self.protected_set_id,
            "policyVersion": self.policy_version,
            "findings": list(self.findings),
            "exactContentClear": self.exact_content_clear,
            "captureGroupClear": self.capture_group_clear,
            "admissionResult": self.admission_result,
        }

    def to_json(self) -> dict[str, Any]:
        record = self._canonical()
        record["receiptId"] = self.receipt_id
        return record

    @classmethod
    def from_json(cls, record: dict[str, Any]) -> "TrainingAdmissionReceipt":
        receipt = cls(
            dataset_version_id=record["datasetVersionId"],
            protected_set_id=record["protectedSetId"],
            policy_version=record.get("policyVersion", "LEAK-01..07-v1"),
            findings=tuple(record.get("findings", ())),
            exact_content_clear=record.get("exactContentClear", True),
            capture_group_clear=record.get("captureGroupClear", True),
            admission_result=record.get("admissionResult", "ADMITTED"),
        )
        return receipt if record.get("receiptId") == receipt.receipt_id else None

def protected_set_id(asset_ids: set[str]) -> str:
    return f"protected:{canonical_hash(sorted(asset_ids))}"

def validate_training_admission(dataset: DatasetVersion, protected_asset_ids: set[str] | None = None, policy_version: str = "LEAK-01..07-v1", requested_protected_set_id: str | None = None) -> TrainingAdmissionReceipt:
    protected = protected_asset_ids or set()
    findings = check_leakage(dataset, protected)
    pid = protected_set_id(protected)
    if requested_protected_set_id is not None and requested_protected_set_id != pid:
        raise ValueError("protected snapshot mismatch")
    if findings:
        raise ValueError("training admission rejected leakage: " + "; ".join(f.kind for f in findings))
    return TrainingAdmissionReceipt(dataset.dataset_version_id, pid, policy_version)


def admit_dataset_for_training(
    dataset: DatasetVersion,
    protected_asset_ids: set[str],
    *,
    annotation_dir: str | Path,
    event_dir: str | Path,
    policy_version: str = "LEAK-01..07-v1",
) -> TrainingAdmissionReceipt:
    """GAP-006 + GAP-007 canonical admission boundary.

    Both gates must pass:
      L-1/L-2 leakage against the exact declared protected set
      verifiable acceptance chain for EVERY referenced annotation record

    Returns the immutable receipt bound to the exact dataset snapshot and
    protected-set snapshot. No receipt → NO TRAINING.
    """
    from .annotation import (
        LEGACY_ACCEPTANCE_PROVENANCE, acceptance_stance, load_annotation_record,
    )

    chain_failures: list[str] = []
    for m in dataset.members:
        ann = load_annotation_record(m.annotation_id, annotation_dir)
        if ann is None:
            chain_failures.append(
                f"annotation record missing: {m.annotation_id}")
            continue
        stance = acceptance_stance(
            ann, annotation_dir=annotation_dir, event_dir=event_dir)
        if stance == LEGACY_ACCEPTANCE_PROVENANCE:
            chain_failures.append(
                f"{m.annotation_id}: legacy acceptance provenance — "
                "not admissible into new canonical training")
        elif stance != "CANONICAL":
            chain_failures.append(f"{m.annotation_id}: {stance}")
    if chain_failures:
        raise ValueError(
            "training admission rejected annotation chains: "
            + "; ".join(chain_failures))
    return validate_training_admission(
        dataset, protected_asset_ids, policy_version)


# ── Leakage checks (L-1 exact content, L-2 capture group) ──────

@dataclass(frozen=True)
class LeakageFinding:
    kind: str                       # EXACT_CONTENT | SAME_CAPTURE
    asset_a: str
    asset_b: str
    detail: str = ""


def check_leakage(dataset: DatasetVersion,
                  protected_asset_ids: set[str] | None = None) -> list[LeakageFinding]:
    """L-1: same AssetId in training and protected evaluation.
    L-2: same captureGroupId across split boundaries.

    protected_asset_ids: PROTECTED_EVALUATION_ONLY assets (future holdout).
    """
    findings: list[LeakageFinding] = []
    protected = protected_asset_ids or set()

    # L-1: protected assets appearing in any training split
    for m in dataset.members:
        if m.asset_id in protected:
            findings.append(LeakageFinding(
                kind="EXACT_CONTENT", asset_a=m.asset_id, asset_b=m.asset_id,
                detail="protected evaluation asset appears in training dataset"))

    # L-2: same capture group across TRAIN and VALIDATION/TEST
    groups_by_split: dict[str, set[str]] = {}
    for m in dataset.members:
        if not m.capture_group_id:
            continue
        groups_by_split.setdefault(m.split.value, set()).add(m.capture_group_id)
    train_groups = groups_by_split.get(Split.TRAIN.value, set())
    for split in (Split.VALIDATION.value, Split.TEST.value):
        for g in groups_by_split.get(split, set()) & train_groups:
            findings.append(LeakageFinding(
                kind="SAME_CAPTURE", asset_a=f"group:{g}", asset_b=f"split:{split}",
                detail="capture group appears in both TRAIN and "
                       f"{split} — false independence risk"))

    return sorted(findings, key=lambda f: (f.kind, f.asset_a, f.asset_b))


def save_dataset(ds: DatasetVersion, out_dir: str | Path) -> Path:
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    path = out / f"{ds.dataset_version_id.replace('dataset:', '')}.json"
    return write_once_json(path, ds.to_json())


def save_training_admission_receipt(
    receipt: TrainingAdmissionReceipt, out_dir: str | Path
) -> Path:
    """Persist the canonical admission evidence under its content address."""
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    return write_once_json(
        out / f"{receipt.receipt_id.replace('admission:', '')}.json",
        receipt.to_json())


def load_training_admission_receipt(
    receipt_id: str, out_dir: str | Path
) -> TrainingAdmissionReceipt | None:
    """Content-addressed receipt loader; malformed or mismatched content fails closed."""
    if not receipt_id.startswith("admission:"):
        return None
    path = Path(out_dir) / f"{receipt_id.replace('admission:', '')}.json"
    try:
        record = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError, TypeError):
        return None
    receipt = TrainingAdmissionReceipt.from_json(record)
    return receipt if receipt is not None and receipt.receipt_id == receipt_id else None
