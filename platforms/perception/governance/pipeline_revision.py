"""PipelineRevision (P4-D4).

Content-addressed behavior revision:
  SHA-256(canonical { behaviorModuleSourceHashes, actualDependencyVersions })

• Only behavior-defining production modules enter the hash — never
  __pycache__, tests, reports, training/evaluation modules, docs.
• Dependency identity uses ACTUAL resolved runtime versions
  (importlib.metadata), never requirements-file declarations alone (IDR-07).
• Not whole-repo git commit; not the stale "1.0.0" string.
"""
from __future__ import annotations

import json
from importlib.metadata import PackageNotFoundError, version as pkg_version
from pathlib import Path
from typing import Any

from evaluation.identity import canonical_hash, sha256_file
from persistence import write_once_json

# ── Behavior-defining production module inventory (explicit) ────
BEHAVIOR_MODULES: tuple[str, ...] = (
    "uniclaw_perception/server.py",
    "uniclaw_perception/config.py",
    "uniclaw_perception/preprocessing.py",
    "uniclaw_perception/remap.py",
    "uniclaw_perception/schema.py",
    "uniclaw_perception/health.py",
    "uniclaw_perception/yolo/inference.py",
    "uniclaw_perception/yolo/labels.py",
    "uniclaw_perception/ocr/common.py",
    "uniclaw_perception/ocr/rapid.py",
    "uniclaw_perception/ocr/paddle.py",
    "uniclaw_perception/fusion/engine.py",
    "uniclaw_perception/fusion/heuristics.py",
    "uniclaw_perception/fusion/scoring.py",
)

# ── Behavior-affecting dependency set (D0-4) ────────────────────
BEHAVIOR_DEPENDENCIES: tuple[str, ...] = (
    "ultralytics",
    "rapidocr-onnxruntime",
    "onnxruntime",
    "torch",
    "pillow",
    "numpy",
)

# Package-relative resolution root: platforms/perception/
_PKG_ROOT = Path(__file__).resolve().parent.parent


def source_hashes(pkg_root: str | Path | None = None) -> dict[str, str]:
    """SHA-256 of each behavior-defining module's exact bytes."""
    root = Path(pkg_root) if pkg_root is not None else _PKG_ROOT
    out: dict[str, str] = {}
    for rel in BEHAVIOR_MODULES:
        p = root / rel
        out[rel] = f"sha256:{sha256_file(p)}" if p.exists() else "MISSING"
    return out


def ocr_model_file_hashes() -> dict[str, str]:
    """Content hashes of the OCR ONNX model files ACTUALLY ON DISK.

    OCR-03 closure: the RapidOCR det/rec/cls models are independent files
    inside the installed package directory — replaceable without a package
    version change. Their bytes are therefore evidence-affecting runtime
    identity, owned by PipelineRevision (existing axis — no new axis).
    Missing/unlocatable files → "MISSING" (revision incomplete).
    """
    out: dict[str, str] = {}
    try:
        import rapidocr_onnxruntime
        from pathlib import Path as _P
        pkg_dir = _P(rapidocr_onnxruntime.__file__).parent
        models_dir = pkg_dir / "models"
        if not models_dir.exists():
            return {"ocrModels": "MISSING"}
        for f in sorted(models_dir.glob("*.onnx")):
            out[f"ocrModels/{f.name}"] = f"sha256:{sha256_file(f)}"
    except Exception:
        return {"ocrModels": "MISSING"}
    return out if out else {"ocrModels": "MISSING"}


def resolved_dependency_versions() -> dict[str, str]:
    """ACTUAL installed versions via importlib.metadata (D0-4/IDR-07).

    UNRESOLVED where not importable — a PipelineRevision containing
    UNRESOLVED for a behavior dependency is PARTIAL by definition.
    """
    out: dict[str, str] = {}
    for dep in BEHAVIOR_DEPENDENCIES:
        try:
            out[dep] = pkg_version(dep)
        except PackageNotFoundError:
            out[dep] = "UNRESOLVED"
    return out


def compute_pipeline_revision(pkg_root: str | Path | None = None,
                              deps: dict[str, str] | None = None,
                              ocr_hashes: dict[str, str] | None = None) -> dict[str, Any]:
    """Return {pipelineRevision, modules, dependencies, ocrModels, complete}."""
    hashes = source_hashes(pkg_root)
    versions = deps if deps is not None else resolved_dependency_versions()
    ocr = ocr_hashes if ocr_hashes is not None else ocr_model_file_hashes()
    missing_modules = [m for m, h in hashes.items() if h == "MISSING"]
    unresolved_deps = [d for d, v in versions.items() if v == "UNRESOLVED"]
    missing_ocr = [o for o, h in ocr.items() if h == "MISSING"]
    content = {
        "schema": "uniclaw.pipelineRevision.v1",
        "behaviorModules": {m: h for m, h in sorted(hashes.items())},
        "dependencies": {d: v for d, v in sorted(versions.items())},
        "ocrModels": {o: h for o, h in sorted(ocr.items())},
    }
    return {
        "pipelineRevision": f"prev:{canonical_hash(content)}",
        "modules": hashes,
        "dependencies": versions,
        "ocrModels": ocr,
        "complete": not missing_modules and not unresolved_deps and not missing_ocr,
        "missingModules": missing_modules,
        "unresolvedDependencies": unresolved_deps,
        "missingOcrModels": missing_ocr,
    }


def save_revision(rev: dict[str, Any], out_dir: str | Path) -> Path:
    out = Path(out_dir)
    path = out / f"{rev['pipelineRevision'].replace('prev:', '')}.json"
    return write_once_json(path, rev)
