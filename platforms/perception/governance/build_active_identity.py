"""P4-D10 — construct CURRENT ACTIVE canonical deployment identity.

Builds from ACTUAL production state (resolved config snapshot, artifact
bytes, source/dependency identity) and persists immutable manifests.
Also produces the first vertical proof: ACTIVE identity A vs TEST
candidate identity B, with the mechanical diff (ModelChanged).
"""
from __future__ import annotations

import json
import os
from pathlib import Path
from typing import Any

from evaluation.identity import sha256_file

from governance.config_manifest import build_from_perception_config, save_manifest
from governance.deployment import (
    PerceptionDeploymentCandidate, save_candidate,
)
from governance.diff import diff_identity
from governance.model_manifest import (
    build_current_active_manifest, build_test_candidate_manifest, save_manifest as save_mm,
)
from governance.pipeline_revision import compute_pipeline_revision, save_revision

BASE = Path(__file__).resolve().parent
ARTIFACTS = BASE / "artifacts"
MODEL_ACTIVE = BASE.parent / "models" / "yolo" / "android_ui_detection_yolov8" / "best.pt"
TEST_MODEL_STORE = BASE.parent / "training" / "artifacts" / "model-store"

SCHEMA = "uniclaw.localVisionEvidence.v1"


def _save_new_or_verify_legacy(path: Path, payload: dict[str, Any], save) -> Path:
    """Preserve pre-write-once history while allowing new identities.

    Existing pretty-printed legacy JSON is never normalized.  It is accepted
    only when its parsed semantic payload exactly equals the newly derived
    payload; otherwise the collision remains an integrity failure.
    """
    if path.exists():
        if json.loads(path.read_text(encoding="utf-8")) != payload:
            raise RuntimeError(f"canonical legacy artifact collision: {path}")
        return path
    return save()


def build_active_identity() -> dict[str, Any]:
    ARTIFACTS.mkdir(parents=True, exist_ok=True)

    # 1. resolved effective config snapshot (env overrides applied)
    from uniclaw_perception.config import load as load_config
    cfg = load_config()
    manifest = build_from_perception_config(
        cfg, cfg.config_path,
        label_mapping_content_hash=f"sha256:{cfg.config_hash}")
    config_path = ARTIFACTS / "config-manifests" / (
        f"{manifest.config_id.removeprefix('config:')}.json")
    _save_new_or_verify_legacy(
        config_path, manifest.to_json(),
        lambda: save_manifest(manifest, ARTIFACTS / "config-manifests"))

    # 2. pipeline revision (actual source + dependency identity)
    rev = compute_pipeline_revision()
    revision_path = ARTIFACTS / "pipeline-revisions" / (
        f"{rev['pipelineRevision'].removeprefix('prev:')}.json")
    _save_new_or_verify_legacy(
        revision_path, rev,
        lambda: save_revision(rev, ARTIFACTS / "pipeline-revisions"))

    # 3. model manifests
    active_mm = build_current_active_manifest(MODEL_ACTIVE)
    active_model_path = ARTIFACTS / "model-manifests" / (
        f"{active_mm.manifest_id.removeprefix('mmf:')}.json")
    _save_new_or_verify_legacy(
        active_model_path, active_mm.to_json(),
        lambda: save_mm(active_mm, ARTIFACTS / "model-manifests"))

    # 4. ACTIVE deployment candidate/identity A
    active_candidate = PerceptionDeploymentCandidate(
        schema_version=SCHEMA,
        model_id=active_mm.model_id,
        config_id=manifest.config_id,
        pipeline_revision=rev["pipelineRevision"],
        service_version="1.0",
    )
    active_deployment_path = ARTIFACTS / "deployments" / (
        f"{active_candidate.deployment_id.removeprefix('deploy:')}.json")
    _save_new_or_verify_legacy(
        active_deployment_path, active_candidate.to_json(),
        lambda: save_candidate(active_candidate, ARTIFACTS / "deployments"))

    # 5. TEST candidate identity B (from training foundation artifacts)
    test_candidates = sorted((BASE.parent / "training" / "artifacts" /
                              "manifests" / "candidates").glob("*.json"))
    test = {}
    if test_candidates:
        cand = json.loads(test_candidates[-1].read_text(encoding="utf-8"))
        test_model_path = next(iter(TEST_MODEL_STORE.glob("*.pt")), None)
        test_mm = build_test_candidate_manifest(
            test_model_path or MODEL_ACTIVE,
            model_id=cand["modelArtifactId"],
            training_run_id=cand["trainingRunId"],
            checkpoint_id=sha256_file(test_model_path) if test_model_path else "UNKNOWN",
        )
        test_model_path_manifest = ARTIFACTS / "model-manifests" / (
            f"{test_mm.manifest_id.removeprefix('mmf:')}.json")
        _save_new_or_verify_legacy(
            test_model_path_manifest, test_mm.to_json(),
            lambda: save_mm(test_mm, ARTIFACTS / "model-manifests"))
        test_candidate = PerceptionDeploymentCandidate(
            schema_version=SCHEMA,
            model_id=test_mm.model_id,
            config_id=manifest.config_id,
            pipeline_revision=rev["pipelineRevision"],
            service_version="1.0",
        )
        test_deployment_path = ARTIFACTS / "deployments" / (
            f"{test_candidate.deployment_id.removeprefix('deploy:')}.json")
        _save_new_or_verify_legacy(
            test_deployment_path, test_candidate.to_json(),
            lambda: save_candidate(test_candidate, ARTIFACTS / "deployments"))
        test_diff = diff_identity(active_candidate, test_candidate)
        test = {
            "deploymentId": test_candidate.deployment_id,
            "modelId": test_candidate.model_id,
            "modelName": test_mm.model_name,
            "architecture": test_mm.architecture.value,
            "labelSpaceId": test_mm.label_space_id,
            "classVocabulary": list(test_mm.class_vocabulary),
            "diff": test_diff.to_json(),
        }

    result = {
        "active": {
            "schemaVersion": SCHEMA,
            "deploymentId": active_candidate.deployment_id,
            "configId": manifest.config_id,
            "configCompleteness": manifest.completeness.value,
            "pipelineRevision": rev["pipelineRevision"],
            "modelId": active_mm.model_id,
            "modelName": active_mm.model_name,
            "architecture": active_mm.architecture.value,
            "labelSpaceId": active_mm.label_space_id,
            "classVocabulary": list(active_mm.class_vocabulary),
            "provenanceStance": active_mm.provenance_stance.value,
        },
        "pipeline": {
            "modulesHashed": len(rev["modules"]),
            "dependencies": rev["dependencies"],
            "complete": rev["complete"],
        },
        "testCandidate": test,
    }
    # Atomic canonical activation (admission): the complete receipt is built in
    # memory, then exposed via temp-file + os.replace so the old canonical
    # receipt stays authoritative until the new complete receipt is ready.
    # Partial JSON / half-updated axes / truncated receipt are never visible.
    out = ARTIFACTS / "current-active-identity.json"
    payload = json.dumps(result, ensure_ascii=False, indent=2)
    tmp = out.with_name(out.name + ".tmp")
    tmp.write_text(payload, encoding="utf-8")
    tmp.flush() if hasattr(tmp, "flush") else None
    os.replace(tmp, out)
    return result


if __name__ == "__main__":
    import sys
    r = build_active_identity()
    print(json.dumps(r, ensure_ascii=False, indent=2))
    sys.exit(0)
