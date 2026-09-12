"""Content-addressed identity for the perception pipeline（PER-008 D2）.

Ported from uni-agent governance (config_manifest / pipeline_revision /
deployment) as in-package pure functions — no evaluation/persistence deps.

Four identity axes:
  modelId          — YOLO artifact content SHA-256 (health._model_id, existing)
  configId         — "config:" + canonical hash over effective config axes
                     (preprocessing / yolo / ocr / scroll / labelMapping /
                     ruleset / **pipeline config incl. variant**)
  pipelineRevision — "prev:" + canonical hash over behavior-module source
                     hashes + ACTUAL dependency versions + OCR ONNX file hashes
  deploymentId     — "deploy:" + canonical hash over the four axes

Semantics preserved from upstream: content-addressed (path-independent),
sorted canonical JSON, actual resolved versions (never declared-only),
service/config metadata excluded from identity, startup-frozen snapshot
reporting (PER-007 R2 capture_identity).
"""
from __future__ import annotations

import hashlib
import json
from importlib.metadata import PackageNotFoundError, version as pkg_version
from pathlib import Path
from typing import Any

_PKG_ROOT = Path(__file__).resolve().parent.parent  # platforms/perception/

# ── canonical serialization (inlined from evaluation.identity) ──────────

def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: str | Path) -> str:
    return sha256_bytes(Path(path).read_bytes())


def canonical_json(obj: Any) -> str:
    return json.dumps(obj, sort_keys=True, ensure_ascii=False, separators=(",", ":"))


def canonical_hash(obj: Any) -> str:
    return sha256_bytes(canonical_json(obj).encode("utf-8"))


# ── pipelineRevision ─────────────────────────────────────────────────────
# Only behavior-defining production modules enter the hash — never tests,
# bench, __pycache__, docs, or this reporting-only module itself.

BEHAVIOR_MODULES: tuple[str, ...] = (
    "uniclaw_perception/server.py",
    "uniclaw_perception/config.py",
    "uniclaw_perception/pipeline.py",
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
    "uniclaw_perception/fusion/row_grouping.py",
    "uniclaw_perception/fusion/scoring.py",
)

BEHAVIOR_DEPENDENCIES: tuple[str, ...] = (
    "ultralytics",
    "rapidocr-onnxruntime",
    "onnxruntime",
    "torch",
    "pillow",
    "numpy",
)


def source_hashes(pkg_root: str | Path | None = None) -> dict[str, str]:
    root = Path(pkg_root) if pkg_root is not None else _PKG_ROOT
    out: dict[str, str] = {}
    for rel in BEHAVIOR_MODULES:
        p = root / rel
        out[rel] = f"sha256:{sha256_file(p)}" if p.exists() else "MISSING"
    return out


def ocr_model_file_hashes() -> dict[str, str]:
    """Content hashes of the rapidocr-package ONNX det/rec/cls files on disk
    (OCR-03: replaceable independent of package version)."""
    out: dict[str, str] = {}
    try:
        import rapidocr_onnxruntime
        pkg_dir = Path(rapidocr_onnxruntime.__file__).parent
        models_dir = pkg_dir / "models"
        if not models_dir.exists():
            return {"ocrModels": "MISSING"}
        for f in sorted(models_dir.glob("*.onnx")):
            out[f"ocrModels/{f.name}"] = f"sha256:{sha256_file(f)}"
    except Exception:
        return {"ocrModels": "MISSING"}
    return out if out else {"ocrModels": "MISSING"}


def resolved_dependency_versions() -> dict[str, str]:
    out: dict[str, str] = {}
    for dep in BEHAVIOR_DEPENDENCIES:
        try:
            out[dep] = pkg_version(dep)
        except PackageNotFoundError:
            out[dep] = "UNRESOLVED"
    return out


def compute_pipeline_revision(pkg_root: str | Path | None = None) -> dict[str, Any]:
    hashes = source_hashes(pkg_root)
    versions = resolved_dependency_versions()
    ocr = ocr_model_file_hashes()
    content = {
        "schema": "uniclaw.pipelineRevision.v1",
        "behaviorModules": dict(sorted(hashes.items())),
        "dependencies": dict(sorted(versions.items())),
        "ocrModels": dict(sorted(ocr.items())),
    }
    return {
        "pipelineRevision": f"prev:{canonical_hash(content)}",
        "complete": "MISSING" not in hashes.values()
        and "UNRESOLVED" not in versions.values()
        and "MISSING" not in ocr.values(),
    }


# ── configId（v2：相对 uni-agent v1 增 pipeline 轴；recModel 内容寻址） ──

def ruleset_content_hash(ruleset_content: str | None) -> str:
    """Absent vs present never collide (marker is not valid rule-set JSON)."""
    from .config import DEFAULT_RULESET_MARKER
    if ruleset_content is None:
        return f"marker:{DEFAULT_RULESET_MARKER}"
    return f"sha256:{sha256_bytes(ruleset_content.encode('utf-8'))}"


def build_config_id(cfg: Any, pipeline_identity_content: dict[str, Any]) -> str:
    """canonical configId over effective config axes + pipeline config.

    cfg: uniclaw_perception.config.PerceptionConfig (post-load snapshot —
    env overrides already applied). pipeline_identity_content: the effective
    pipeline config identity block (variant-aware; see pipeline.py).
    """
    try:
        rec_model_hash = f"sha256:{sha256_file(cfg.ocr_rec_model)}"
    except (OSError, AttributeError):
        rec_model_hash = "MISSING"
    content = {
        "schema": "uniclaw.perceptionConfig.v2",
        "preprocessing": {
            "maxWidth": cfg.max_width,
            "cropTopRatio": round(float(cfg.crop_top), 6),
            "cropBottomRatio": round(float(cfg.crop_bottom), 6),
        },
        "yolo": {"confidence": round(float(cfg.detection_confidence), 6)},
        "ocr": {
            "backend": cfg.ocr_backend,
            "mode": cfg.ocr_mode,
            "textScore": round(float(cfg.ocr_text_score), 6),
            "language": cfg.ocr_lang,
            "roiPadding": dict(cfg.spatial.get("roiPadding", {})),
            "recModel": rec_model_hash,
        },
        "scroll": {
            "edgeThreshold": cfg.spatial.get("edgeThreshold", 0.92),
        },
        "labelMapping": {"contentHash": cfg.config_hash},
        "ruleset": {"contentHash": ruleset_content_hash(cfg.ruleset_content)},
        "pipeline": pipeline_identity_content,
    }
    return f"config:{canonical_hash(content)}"


# ── deploymentId ─────────────────────────────────────────────────────────

IDENTITY_AXES: tuple[str, ...] = (
    "schemaVersion", "modelId", "configId", "pipelineRevision")


def compute_deployment_id(
        schema_version: str, model_id: str, config_id: str,
        pipeline_revision: str) -> str:
    """deploy:<canonical hash over the four identity axes> (serviceVersion
    is metadata only — changing it never changes deploymentId; IDR-01)."""
    content = {
        "schema": "uniclaw.deploymentIdentity.v1",
        "schemaVersion": schema_version,
        "modelId": model_id,
        "configId": config_id,
        "pipelineRevision": pipeline_revision,
    }
    return f"deploy:{canonical_hash(content)}"
