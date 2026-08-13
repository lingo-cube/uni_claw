"""EvaluationAsset: orthogonal taxonomy + content-addressed identity + admission.

Frozen by Phase 4 gate:
  • Nine independent classification dimensions — no mega-enum.
  • Identity = {ContentHash, AssetSchemaVersion}; filename/path never identity.
  • Moving/renaming bytes never changes AssetId.
  • Multiple corpus roles / suite memberships are manifest relationships,
    never byte duplication.
"""
from __future__ import annotations

import json
from dataclasses import dataclass, field, asdict
from enum import Enum
from pathlib import Path
from typing import Any

from .identity import content_id, sha256_file
from persistence import write_once_json


ASSET_CONTENT_IDENTITY_MISMATCH = "ASSET_CONTENT_IDENTITY_MISMATCH"


class AssetContentIdentityError(ValueError):
    """A manifest or execution input does not bind its declared asset bytes."""


# ── Taxonomy dimensions (orthogonal — never combined) ──────────

class Provenance(str, Enum):
    SYNTHETIC = "SYNTHETIC"
    REALITY_SEEDED = "REALITY_SEEDED"
    RECORDED_REALITY = "RECORDED_REALITY"
    LIVE_CAPTURE = "LIVE_CAPTURE"


class CorpusRole(str, Enum):
    GOLDEN = "GOLDEN"
    REGRESSION = "REGRESSION"
    CHALLENGE = "CHALLENGE"
    HOLDOUT = "HOLDOUT"
    CALIBRATION = "CALIBRATION"
    PERFORMANCE = "PERFORMANCE"


class SystemFamily(str, Enum):
    ANDROID_AOSP = "ANDROID_AOSP"   # evidenced only (emulator AOSP image)
    OTHER = "OTHER"
    UNKNOWN = "UNKNOWN"


class ScenarioDomain(str, Enum):
    SETTINGS = "SETTINGS"
    PERMISSION = "PERMISSION"
    DIALOG = "DIALOG"
    APP_CONTENT = "APP_CONTENT"
    NAVIGATION = "NAVIGATION"
    SYSTEM_UI = "SYSTEM_UI"
    INPUT = "INPUT"
    UNKNOWN = "UNKNOWN"


class PerceptionTask(str, Enum):
    ELEMENT_DETECTION = "ELEMENT_DETECTION"
    OCR = "OCR"
    SWITCH_STATE = "SWITCH_STATE"
    BOUNDS = "BOUNDS"
    LABEL_CLASSIFICATION = "LABEL_CLASSIFICATION"
    FUSION = "FUSION"
    PAGE_STRUCTURE = "PAGE_STRUCTURE"
    SAFETY = "SAFETY"


class ComponentClass(str, Enum):
    SWITCH = "SWITCH"
    BUTTON = "BUTTON"
    TEXT = "TEXT"
    INPUT = "INPUT"
    CHEVRON = "CHEVRON"
    DIALOG = "DIALOG"
    ICON = "ICON"
    LIST_ITEM = "LIST_ITEM"
    SCROLL_CONTAINER = "SCROLL_CONTAINER"
    UNKNOWN = "UNKNOWN"


class Difficulty(str, Enum):
    NORMAL = "NORMAL"
    HARD = "HARD"
    ADVERSARIAL = "ADVERSARIAL"
    UNKNOWN = "UNKNOWN"            # never fabricate difficulty


class Criticality(str, Enum):
    CRITICAL = "CRITICAL"
    IMPORTANT = "IMPORTANT"
    NORMAL = "NORMAL"
    UNKNOWN = "UNKNOWN"


class AdmissionStance(str, Enum):
    ADMITTED = "ADMITTED"
    NEEDS_GROUND_TRUTH = "NEEDS_GROUND_TRUTH"
    INFORMATIONAL_ONLY = "INFORMATIONAL_ONLY"
    NOT_SUITABLE = "NOT_SUITABLE"


# ── Asset manifest ─────────────────────────────────────────────

@dataclass(frozen=True)
class EvaluationAsset:
    """Immutable evaluation asset record.

    Identity: assetId (content-addressed). Everything else is metadata.
    Ground truth is attached via a separate GroundTruth record (§groundtruth).
    """
    asset_schema_version: str
    content_hash: str                        # "sha256:<hex>" over source bytes
    source_path: str                         # reference only — NOT identity
    admission: AdmissionStance
    provenance: Provenance
    corpus_roles: tuple[CorpusRole, ...]     # multiple roles = multiple relationships
    system_family: SystemFamily
    scenario_domain: ScenarioDomain
    perception_tasks: tuple[PerceptionTask, ...]
    component_class: ComponentClass
    difficulty: Difficulty
    criticality: Criticality
    theme_tags: tuple[str, ...] = ()
    source_relations: dict[str, str] = field(default_factory=dict)
    # source_relations: explicit links (scenarioId, captureSessionId,
    # failureEpisodeId, ...) — missing links stay missing.

    @property
    def asset_id(self) -> str:
        """Content-addressed identity — invariant under move/rename."""
        return self.content_hash

    @classmethod
    def from_bytes(cls, data: bytes, source_path: str,
                   asset_schema_version: str, **classification) -> "EvaluationAsset":
        """Create an asset whose identity comes from bytes, never from path."""
        return cls(
            asset_schema_version=asset_schema_version,
            content_hash=content_id(data),
            source_path=source_path,
            **classification,
        )

    @classmethod
    def from_file(cls, path: str | Path, asset_schema_version: str,
                  **classification) -> "EvaluationAsset":
        p = Path(path)
        return cls.from_bytes(p.read_bytes(), str(p.resolve()), asset_schema_version,
                              **classification)

    def to_manifest(self) -> dict[str, Any]:
        d = asdict(self)
        d["admission"] = self.admission.value
        d["provenance"] = self.provenance.value
        d["corpus_roles"] = [r.value for r in self.corpus_roles]
        d["system_family"] = self.system_family.value
        d["scenario_domain"] = self.scenario_domain.value
        d["perception_tasks"] = [t.value for t in self.perception_tasks]
        d["component_class"] = self.component_class.value
        d["difficulty"] = self.difficulty.value
        d["criticality"] = self.criticality.value
        d["theme_tags"] = list(self.theme_tags)
        d["assetId"] = self.asset_id
        return d

    @classmethod
    def from_manifest(cls, d: dict[str, Any]) -> "EvaluationAsset":
        content_hash = d["content_hash"]
        declared_asset_id = d.get("assetId")
        if declared_asset_id is not None and declared_asset_id != content_hash:
            raise AssetContentIdentityError(
                f"{ASSET_CONTENT_IDENTITY_MISMATCH}: manifest assetId "
                f"{declared_asset_id} != content_hash {content_hash}")
        return cls(
            asset_schema_version=d["asset_schema_version"],
            content_hash=content_hash,
            source_path=d["source_path"],
            admission=AdmissionStance(d["admission"]),
            provenance=Provenance(d["provenance"]),
            corpus_roles=tuple(CorpusRole(r) for r in d["corpus_roles"]),
            system_family=SystemFamily(d["system_family"]),
            scenario_domain=ScenarioDomain(d["scenario_domain"]),
            perception_tasks=tuple(PerceptionTask(t) for t in d["perception_tasks"]),
            component_class=ComponentClass(d["component_class"]),
            difficulty=Difficulty(d["difficulty"]),
            criticality=Criticality(d["criticality"]),
            theme_tags=tuple(d.get("theme_tags", ())),
            source_relations=dict(d.get("source_relations", {})),
        )


def save_asset_manifest(asset: EvaluationAsset, out_dir: str | Path) -> Path:
    """Persist manifest as {assetId}.json. Content-addressed name."""
    out = Path(out_dir)
    path = out / f"{asset.asset_id.replace('sha256:', '')}.json"
    return write_once_json(path, asset.to_manifest())


def load_asset_manifest(path: str | Path) -> EvaluationAsset:
    return EvaluationAsset.from_manifest(json.loads(Path(path).read_text(encoding="utf-8")))


def compute_asset_id_from_bytes(data: bytes) -> str:
    """B1 falsifier helper: identity is a pure function of bytes."""
    return content_id(data)


def compute_asset_id_from_file(path: str | Path) -> str:
    """B1 falsifier helper: same bytes at any path → same AssetId."""
    return f"sha256:{sha256_file(path)}"
