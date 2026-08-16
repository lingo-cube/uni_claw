"""DatasetVersion foundation (P4-T2).

Immutable membership manifest — NOT a mutable folder.
Identity = SHA-256 of canonical membership (assets + annotation refs +
split assignment + capture grouping). Display metadata excluded from identity.

GAP-006 FINAL: the manifest is the SEMANTIC identity of what training may
consume.  `resolve_training_input_binding` verifies that the actual bytes
reachable from a data.yaml are exactly the admitted membership — content
identity, never path identity.
"""
from __future__ import annotations

import json
from dataclasses import dataclass, field
from enum import Enum
from pathlib import Path
from typing import Any

from evaluation.identity import canonical_hash, sha256_file
from . import TRAINING_SCHEMA_VERSION
from persistence import write_once_json

_IMAGE_SUFFIXES = (".png", ".jpg", ".jpeg", ".bmp")

TRAINING_DATA_BINDING_MISMATCH = "TRAINING_DATA_BINDING_MISMATCH"


class TrainingDataBindingError(ValueError):
    """Executed training bytes do not bind to the admitted dataset manifest."""


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


# ── GAP-006 FINAL: executed-bytes ↔ admitted-manifest content binding ────

class TrainingInputBindingError(TrainingDataBindingError):
    """GAP-006 fail-closed binding rejection with a stable reason code."""


def _binding_fail(code: str, detail: str) -> TrainingInputBindingError:
    return TrainingInputBindingError(
        f"{TRAINING_DATA_BINDING_MISMATCH}:{code}: {detail}")


@dataclass(frozen=True)
class TrainingInputBinding:
    """GAP-006 FINAL: content-addressed evidence that the executed training
    bytes ARE the admitted dataset membership.

    data_yaml_path is LOCATION ONLY — it never participates in identity.
    Identity derives from the actual image content ids and the canonical
    label→annotation bindings.
    """
    dataset_version_id: str
    data_yaml_path: str              # location, not identity
    resolved_member_count: int       # admitted members bound to executed bytes
    image_content_ids: tuple[str, ...]
    split_counts: dict[str, int]
    label_annotation_bindings: tuple[dict[str, Any], ...]
    binding_evidence: dict[str, Any] = field(default_factory=dict)

    @property
    def binding_id(self) -> str:
        return f"training-input-binding:{canonical_hash(self._canonical())}"

    def _canonical(self) -> dict[str, Any]:
        return {
            "schema": TRAINING_SCHEMA_VERSION,
            "datasetVersionId": self.dataset_version_id,
            "imageContentIds": sorted(self.image_content_ids),
            "splitCounts": {k: self.split_counts[k]
                            for k in sorted(self.split_counts)},
            "labelBindings": sorted(
                ({"assetId": b["assetId"], "annotationId": b["annotationId"]}
                 for b in self.label_annotation_bindings),
                key=lambda x: x["assetId"]),
        }

    def to_json(self) -> dict[str, Any]:
        return {
            "bindingId": self.binding_id,
            "datasetVersionId": self.dataset_version_id,
            "dataYamlPath": self.data_yaml_path,
            "resolvedMemberCount": self.resolved_member_count,
            "imageContentIds": list(self.image_content_ids),
            "splitCounts": dict(self.split_counts),
            "labelAnnotationBindings": [dict(b) for b in
                                        self.label_annotation_bindings],
            "bindingEvidence": dict(self.binding_evidence),
        }


def _parse_data_yaml(data_yaml: Path) -> dict[str, Any]:
    """Minimal data.yaml reader.  Tries PyYAML when available; a tiny
    key: value fallback keeps the binding independent of optional deps."""
    text = data_yaml.read_text(encoding="utf-8")
    try:
        import yaml  # optional — ultralytics dependency, not required here
        data = yaml.safe_load(text)
        if isinstance(data, dict):
            return data
    except Exception:
        pass
    parsed: dict[str, Any] = {}
    for line in text.splitlines():
        line = line.strip()
        if not line or line.startswith("#") or ":" not in line:
            continue
        key, _, value = line.partition(":")
        parsed[key.strip()] = value.strip().strip('"').strip("'")
    return parsed


def _resolve_data_root(cfg: dict[str, Any],
                       data_yaml: Path) -> Path:
    root = cfg.get("path")
    if root is None:
        return data_yaml.parent
    root_p = Path(str(root))
    return root_p if root_p.is_absolute() else data_yaml.parent / root_p


def _as_path_list(value: Any) -> list[str]:
    if value is None:
        return []
    if isinstance(value, (list, tuple)):
        return [str(v) for v in value]
    return [str(value)]


def _label_dir_for(image_dir: Path) -> Path:
    """Ultralytics convention: images/train → labels/train."""
    if image_dir.parent.name == "images":
        return image_dir.parent.parent / "labels" / image_dir.name
    return image_dir.parent / "labels" / image_dir.name


def _canonical_boxes(label_payload: dict[str, Any]) -> list[tuple[Any, ...]]:
    """Annotation boxes → sorted canonical tuples (class cx cy w h @6dp)."""
    boxes = label_payload.get("boxes", [])
    if not isinstance(boxes, list):
        return []
    out: list[tuple[Any, ...]] = []
    for b in boxes:
        cls, cx, cy, w, h = (b.get(k) for k in ("class", "cx", "cy", "w", "h"))
        if any(v is None for v in (cls, cx, cy, w, h)):
            return []   # unparseable payload — content mismatch will surface
        try:
            out.append((int(cls), round(float(cx), 6), round(float(cy), 6),
                        round(float(w), 6), round(float(h), 6)))
        except (TypeError, ValueError):
            return []
    return sorted(out)


def _parse_yolo_label(label_file: Path) -> list[tuple[Any, ...]]:
    """YOLO label file → sorted canonical tuples; malformed → fail closed."""
    out: list[tuple[Any, ...]] = []
    for line in label_file.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line:
            continue
        parts = line.split()
        if len(parts) != 5:
            raise _binding_fail(
                "LABEL_CONTENT_MISMATCH",
                f"malformed YOLO label line in {label_file}: {line!r}")
        try:
            cls, cx, cy, w, h = (float(p) for p in parts)
        except ValueError:
            raise _binding_fail(
                "LABEL_CONTENT_MISMATCH",
                f"malformed YOLO label line in {label_file}: {line!r}")
        out.append((int(cls), round(cx, 6), round(cy, 6),
                    round(w, 6), round(h, 6)))
    return sorted(out)


def resolve_training_input_binding(
    data_yaml_path: str | Path,
    dataset: DatasetVersion,
    *,
    annotation_dir: str | Path,
) -> TrainingInputBinding:
    """GAP-006 FINAL: verify that the bytes reachable from data.yaml ARE the
    admitted DatasetVersion membership — by content identity, never path.

    Resolves data.yaml → per-split image population and label population,
    hashes every reachable image (sha256 → asset id), and compares EXACT
    set equality against the DatasetVersion membership for each split.
    Every label file is parsed back into canonical boxes and compared
    against the canonical Annotation record referenced by that membership
    (loaded by content-addressed id — the YOLO filename is never trusted).

    Fail closed on ANY of:
      DATA_YAML_UNRESOLVABLE   data.yaml missing/unreadable/undeclared splits
      SPLIT_DIR_UNRESOLVABLE   declared split directory does not exist
      UNRELATED_DATA_PATH      no materialized image matches the membership
      EXTRA_IMAGE              image bytes not in the admitted membership
      MISSING_REQUIRED_IMAGE   admitted member bytes not materialized
      CHANGED_BYTES            same count, different content (bytes replaced)
      AMBIGUOUS_MATERIALIZATION same image content under more than one split
      LABEL_FILE_MISSING       admitted member has no label file
      LABEL_CONTENT_MISMATCH   label bytes do not match the canonical record
      LABEL_ANNOTATION_MISMATCH annotation record missing or belongs to
                                another asset (label of another annotation)

    Returns the content-addressed binding evidence; never mutates anything.
    """
    from .annotation import load_annotation_record

    data_yaml = Path(data_yaml_path)
    if not data_yaml.is_file():
        raise _binding_fail("DATA_YAML_UNRESOLVABLE",
                            f"no data.yaml at {data_yaml_path}")
    try:
        cfg = _parse_data_yaml(data_yaml)
    except OSError as exc:
        raise _binding_fail("DATA_YAML_UNRESOLVABLE", str(exc))
    root = _resolve_data_root(cfg, data_yaml)
    for key in ("train", "val"):
        if key not in cfg:
            raise _binding_fail(
                "DATA_YAML_UNRESOLVABLE",
                f"data.yaml {data_yaml} must declare '{key}'")

    members_by_split: dict[Split, list[DatasetMembership]] = {
        Split.TRAIN: [], Split.VALIDATION: []}
    for m in dataset.members:
        if m.split in members_by_split:
            members_by_split[m.split].append(m)

    actual_by_split: dict[Split, list[tuple[Path, str]]] = {}
    split_names = {Split.TRAIN: "train", Split.VALIDATION: "val"}
    for split, key in ((Split.TRAIN, "train"), (Split.VALIDATION, "val")):
        images: list[tuple[Path, str]] = []
        for d in _as_path_list(cfg.get(key)):
            split_dir = Path(d) if Path(d).is_absolute() else root / d
            if not split_dir.is_dir():
                raise _binding_fail(
                    "SPLIT_DIR_UNRESOLVABLE",
                    f"{split_names[split]} split dir missing: {split_dir}")
            for p in sorted(split_dir.iterdir()):
                if p.suffix.lower() in _IMAGE_SUFFIXES and p.is_file():
                    images.append((p, f"sha256:{sha256_file(p)}"))
        actual_by_split[split] = images

    all_ids = [aid for split in (Split.TRAIN, Split.VALIDATION)
               for _, aid in actual_by_split[split]]
    if len(set(all_ids)) != len(all_ids):
        raise _binding_fail(
            "AMBIGUOUS_MATERIALIZATION",
            "same image content materialized under more than one split")

    image_content_ids: list[str] = []
    label_bindings: list[dict[str, Any]] = []
    for split in (Split.TRAIN, Split.VALIDATION):
        actual = actual_by_split[split]
        expected_ids = {m.asset_id for m in members_by_split[split]}
        actual_set = {aid for _, aid in actual}
        image_content_ids.extend(sorted(actual_set))
        if not actual_set and not expected_ids:
            continue    # split unused by both sides — nothing to bind
        if not (actual_set & expected_ids):
            raise _binding_fail(
                "UNRELATED_DATA_PATH",
                f"{split_names[split]}: none of the materialized images "
                f"({len(actual_set)}) match admitted membership "
                f"({len(expected_ids)}) — data points at a different dataset")
        missing = expected_ids - actual_set
        extra = actual_set - expected_ids
        if missing or extra:
            if len(actual_set) == len(expected_ids):
                raise _binding_fail(
                    "CHANGED_BYTES",
                    f"{split_names[split]}: {len(missing)} admitted member(s) "
                    "materialized with altered content (same count, "
                    "different bytes)")
            if missing:
                raise _binding_fail(
                    "MISSING_REQUIRED_IMAGE",
                    f"{split_names[split]}: admitted member(s) not "
                    f"materialized: {sorted(missing)[:3]}")
            raise _binding_fail(
                "EXTRA_IMAGE",
                f"{split_names[split]}: image(s) not in admitted membership: "
                f"{sorted(extra)[:3]}")

        hash_to_path = {aid: p for p, aid in actual}
        for m in sorted(members_by_split[split], key=lambda m: m.asset_id):
            img_path = hash_to_path[m.asset_id]
            label_file = _label_dir_for(img_path.parent) / f"{img_path.stem}.txt"
            if not label_file.is_file():
                raise _binding_fail(
                    "LABEL_FILE_MISSING",
                    f"{label_file} for admitted member {m.asset_id}")
            ann = load_annotation_record(m.annotation_id, annotation_dir)
            if ann is None:
                raise _binding_fail(
                    "LABEL_ANNOTATION_MISMATCH",
                    f"annotation record {m.annotation_id} for admitted member "
                    f"{m.asset_id} is not resolvable from {annotation_dir}")
            if ann.asset_id != m.asset_id:
                raise _binding_fail(
                    "LABEL_ANNOTATION_MISMATCH",
                    f"annotation {m.annotation_id} belongs to asset "
                    f"{ann.asset_id}, but membership binds it to "
                    f"{m.asset_id} — label of another annotation")
            expected_boxes = _canonical_boxes(ann.label_payload)
            actual_boxes = _parse_yolo_label(label_file)
            if actual_boxes != expected_boxes:
                raise _binding_fail(
                    "LABEL_CONTENT_MISMATCH",
                    f"{label_file} does not match canonical annotation "
                    f"{m.annotation_id} (expected {len(expected_boxes)} "
                    f"boxes, materialized {len(actual_boxes)})")
            label_bindings.append({
                "assetId": m.asset_id,
                "annotationId": m.annotation_id,
                "labelFile": str(label_file),
                "boxCount": len(expected_boxes),
            })

    return TrainingInputBinding(
        dataset_version_id=dataset.dataset_version_id,
        data_yaml_path=str(data_yaml),
        resolved_member_count=len(image_content_ids),
        image_content_ids=tuple(sorted(image_content_ids)),
        split_counts={split_names[s]: len(actual_by_split[s])
                      for s in (Split.TRAIN, Split.VALIDATION)},
        label_annotation_bindings=tuple(label_bindings),
        binding_evidence={
            "bindingVersion": "GAP-006-v1",
            "contentIdentity": "sha256",
            "labelFormat": "yolo",
            "labelDirRule": "images->labels",
            "dataYamlResolved": True,
        },
    )
