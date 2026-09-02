"""OcrModelManifest — managed OCR model artifact registration (P-OCR).

Governed by `openspec/changes/perception-ocr-en-v4-normalization`
(spec: perception/ocr-backend-selection). Truthful separation:
  artifactId — exact artifact bytes SHA-256 (content-addressed)
  fileName  — file name within the managed ocr/models directory
  language  — the OCR config language this model serves (en / zh / ...)
  purpose   — det | rec | cls

Mirrors model_manifest.py governance style (D0-3): identity derives from
artifact bytes, never from filenames or names; registration is write-once;
unregistered weights must be rejected at load time.
"""
from __future__ import annotations

import json
from dataclasses import dataclass, field
from enum import Enum
from pathlib import Path
from typing import Any, Mapping

from evaluation.identity import canonical_hash
from persistence import write_once_json

MANIFEST_SCHEMA = "uniclaw.ocrModelManifest.v1"
OCR_MODELS_REL = "ocr/models"          # relative to platforms/perception/
GOVERNANCE_OCR_MODELS_DIR_REL = "governance/artifacts/ocr-models"


class OcrRole(str, Enum):
    DET = "det"
    REC = "rec"
    CLS = "cls"


@dataclass(frozen=True)
class OcrModelManifest:
    file_name: str
    artifact_id: str               # full 64-hex SHA-256 of file bytes
    language: str                  # config language this model serves
    role: OcrRole                  # det / rec / cls
    purpose: str = ""              # human note (e.g. "English rec model")

    @property
    def manifest_id(self) -> str:
        return f"ocrm:{canonical_hash(self._canonical())}"

    def _canonical(self) -> dict[str, Any]:
        return {
            "schema": MANIFEST_SCHEMA,
            "fileName": self.file_name,
            "artifactId": self.artifact_id,
            "language": self.language,
            "role": self.role.value,
            "purpose": self.purpose,
        }

    def to_json(self) -> dict[str, Any]:
        d = self._canonical()
        d["manifestId"] = self.manifest_id
        return d


def build_ocr_model_manifest(file_path: str | Path, *,
                             language: str, role: OcrRole,
                             purpose: str = "") -> OcrModelManifest:
    """Truthful registration from artifact bytes (never inferred)."""
    from evaluation.identity import sha256_file
    p = Path(file_path)
    return OcrModelManifest(
        file_name=p.name,
        artifact_id=sha256_file(p),
        language=language,
        role=role,
        purpose=purpose,
    )


def ocr_models_dir(perception_root: str | Path) -> Path:
    return Path(perception_root) / OCR_MODELS_REL


def governance_ocr_models_dir(perception_root: str | Path) -> Path:
    return Path(perception_root) / GOVERNANCE_OCR_MODELS_DIR_REL


def save_ocr_manifest(manifest: OcrModelManifest,
                      perception_root: str | Path) -> Path:
    out = governance_ocr_models_dir(perception_root)
    path = out / f"{manifest.manifest_id.replace('ocrm:', '')}.json"
    return write_once_json(path, manifest.to_json())


def load_ocr_manifests(perception_root: str | Path) -> list[OcrModelManifest]:
    """Load all registered OCR model manifests (empty if none yet)."""
    out = governance_ocr_models_dir(perception_root)
    if not out.exists():
        return []
    result: list[OcrModelManifest] = []
    for f in sorted(out.glob("*.json")):
        d = json.loads(f.read_text(encoding="utf-8"))
        result.append(OcrModelManifest(
            file_name=d["fileName"],
            artifact_id=d["artifactId"],
            language=d["language"],
            role=OcrRole(d["role"]),
            purpose=d.get("purpose", ""),
        ))
    return result


def find_registered_manifest(perception_root: str | Path,
                             file_path: str | Path) -> OcrModelManifest | None:
    """Return the registered manifest whose file matches *file_path* bytes.

    Identity is by content: the file's SHA-256 must equal a registered
    artifactId. This is the guard used by the loader: unregistered weights
    fail here and are rejected (spec: unregistered-reject).
    """
    from evaluation.identity import sha256_file
    p = Path(file_path)
    if not p.exists():
        return None
    digest = sha256_file(p)
    for m in load_ocr_manifests(perception_root):
        if m.artifact_id == digest and m.file_name == p.name:
            return m
    return None