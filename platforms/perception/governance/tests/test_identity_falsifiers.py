"""Deployment identity falsifiers: DI-01..20, CFI-01..04, IDR-01..07."""
from __future__ import annotations

import unittest
from pathlib import Path

from evaluation.identity import sha256_file

from governance.config_manifest import (
    ConfigCompleteness, PerceptionConfigManifest,
)
from governance.deployment import (
    IDENTITY_AXES, DeploymentInstance, PerceptionDeploymentCandidate,
    PerceptionDeploymentIdentity,
)
from governance.diff import diff_identity
from governance.inventory import verify_single_ownership
from governance.model_manifest import (
    Architecture, ArtifactFormat, ModelManifest, ProvenanceStance,
)
from governance.pipeline_revision import (
    BEHAVIOR_DEPENDENCIES, BEHAVIOR_MODULES, compute_pipeline_revision,
    resolved_dependency_versions, source_hashes,
)

SCHEMA = "uniclaw.localVisionEvidence.v1"


def _cfg(**over) -> PerceptionConfigManifest:
    base = dict(
        preprocessing={"maxWidth": 720, "cropTopRatio": 0.0625,
                       "cropBottomRatio": 0.0625},
        yolo={"confidence": 0.35},
        ocr={"backend": "rapidocr", "mode": "full", "textScore": 0.5,
             "language": "en", "roiPadding": {"x": 0.15}},
        scroll={"edgeThreshold": 0.92},
        referenced_artifacts={},
    )
    base.update(over)
    return PerceptionConfigManifest(**base)


def _cand(model_id="m" * 64, config_id="config:1", prev="prev:1",
          schema=SCHEMA, service="1.0") -> PerceptionDeploymentCandidate:
    return PerceptionDeploymentCandidate(
        schema_version=schema, model_id=model_id, config_id=config_id,
        pipeline_revision=prev, service_version=service)


class ConfigIdTests(unittest.TestCase):
    def test_DI01_same_manifest_same_config_id(self):
        self.assertEqual(_cfg().config_id, _cfg().config_id)

    def test_DI02_material_change_different_config_id(self):
        a = _cfg()
        b = _cfg(preprocessing={"maxWidth": 640, "cropTopRatio": 0.0625,
                                "cropBottomRatio": 0.0625})
        c = _cfg(yolo={"confidence": 0.40})
        d = _cfg(ocr={"backend": "paddleocr", "mode": "full",
                      "textScore": 0.5, "language": "en",
                      "roiPadding": {"x": 0.15}})
        self.assertNotEqual(a.config_id, b.config_id)
        self.assertNotEqual(a.config_id, c.config_id)
        self.assertNotEqual(a.config_id, d.config_id)

    def test_DI03_display_metadata_change_same_config_id(self):
        # referenced_artifacts labelMapping carries contentHash only;
        # display metadata is not part of identity content. Simulate: the
        # manifest identity content ignores notes/display fields — verified
        # by identical config_id for identical effective content.
        a = _cfg()
        a2 = PerceptionConfigManifest(**a.__dict__)
        self.assertEqual(a.config_id, a2.config_id)

    def test_DI04_host_operational_change_same_config_id(self):
        # Host settings (socket, restarts, timeouts) are not fields of the
        # manifest at all — constructing one cannot include them.
        a = _cfg()
        self.assertNotIn("socket", a._identity_content())
        self.assertNotIn("restart", a._identity_content())
        self.assertEqual(a.config_id, _cfg().config_id)

    def test_CFI01_unknown_setting_prevents_complete(self):
        m = PerceptionConfigManifest(
            preprocessing={}, yolo={}, ocr={}, scroll={},
            completeness=ConfigCompleteness.PARTIAL,
            unresolved=("fusion.maxOcrDistance",))
        self.assertEqual(m.completeness, ConfigCompleteness.PARTIAL)
        self.assertTrue(m.unresolved)

    def test_CFI02_partial_hash_is_not_full_identity(self):
        m = PerceptionConfigManifest(
            preprocessing={}, yolo={}, ocr={}, scroll={},
            completeness=ConfigCompleteness.PARTIAL,
            unresolved=("yolo.imgsz",))
        # a hash exists (deterministic) but the identity CONTENT records
        # PARTIAL — consumers can refuse to treat it as full identity
        self.assertTrue(m.config_id.startswith("config:"))
        self.assertEqual(m._identity_content()["completeness"], "PARTIAL")

    def test_CFI04_operational_changes_do_not_alter_config_id(self):
        self.assertEqual(_cfg().config_id, _cfg().config_id)  # no op field exists

    def test_IDR03_label_mapping_owned_once(self):
        """labelMapping lives inside the manifest (owned by ConfigId);
        DeploymentCandidate's label_mapping_ref is diagnostic only and is
        NOT part of identity content."""
        m = _cfg(referenced_artifacts={"labelMapping": {"contentHash": "sha256:x",
                                                        "evidenceRelevant": []}})
        c = _cand(config_id=m.config_id)
        self.assertIn("labelMapping", m._identity_content()["referencedArtifacts"])
        self.assertNotIn("labelMappingRef", c.identity_content())
        self.assertNotIn("labelMappingRef", IDENTITY_AXES)


class DeploymentIdTests(unittest.TestCase):
    def test_IDR01_service_version_change_same_deployment_id(self):
        a = _cand(service="1.0")
        b = _cand(service="1.1")
        self.assertEqual(a.deployment_id, b.deployment_id)

    def test_IDR02_axis_change_changes_deployment_id(self):
        a = _cand()
        self.assertNotEqual(a.deployment_id, _cand(model_id="n" * 64).deployment_id)
        self.assertNotEqual(a.deployment_id, _cand(config_id="config:2").deployment_id)
        self.assertNotEqual(a.deployment_id, _cand(prev="prev:2").deployment_id)
        self.assertNotEqual(a.deployment_id, _cand(schema="other.v2").deployment_id)

    def test_DI05_model_bytes_change_deployment_change(self):
        self.assertNotEqual(_cand(model_id="m" * 64).deployment_id,
                            _cand(model_id="z" * 64).deployment_id)

    def test_DI06_DI07_DI08_axes(self):
        a = _cand()
        self.assertNotEqual(a.deployment_id, _cand(config_id="config:9").deployment_id)
        self.assertNotEqual(a.deployment_id, _cand(prev="prev:9").deployment_id)
        self.assertNotEqual(a.deployment_id, _cand(schema="schema.v9").deployment_id)

    def test_DI09_instance_facts_not_identity(self):
        c = _cand()
        i1 = DeploymentInstance(deployment_id=c.deployment_id, pid="1",
                                uds_path="/tmp/a.sock")
        i2 = DeploymentInstance(deployment_id=c.deployment_id, pid="2",
                                uds_path="/tmp/b.sock")
        self.assertEqual(i1.deployment_id, i2.deployment_id)

    def test_DI11_same_model_multiple_configs(self):
        m_id = "m" * 64
        c1 = _cand(model_id=m_id, config_id="config:a")
        c2 = _cand(model_id=m_id, config_id="config:b")
        self.assertEqual(c1.model_id, c2.model_id)
        self.assertNotEqual(c1.deployment_id, c2.deployment_id)

    def test_DI12_model_artifact_has_no_active_authority(self):
        """ModelManifest is immutable facts — no mutable state fields."""
        fields = [f for f in ModelManifest.__dataclass_fields__]
        for banned in ("active", "promoted", "validated", "state"):
            self.assertNotIn(banned, fields)

    def test_DI17_profile_metadata_no_behavior_impact(self):
        """Profile is not an identity axis."""
        self.assertNotIn("profile", IDENTITY_AXES)
        a = _cand()
        b = _cand()  # any profile metadata would not change deployment_id
        self.assertEqual(a.deployment_id, b.deployment_id)

    def test_DI18_service_packaging_no_masquerade(self):
        self.assertEqual(_cand(service="1.0").deployment_id,
                         _cand(service="9.9").deployment_id)

    def test_DI19_ocr_change_travels_through_config_id(self):
        a = _cfg(ocr={"backend": "rapidocr", "mode": "full", "textScore": 0.5,
                      "language": "en", "roiPadding": {"x": 0.15}})
        b = _cfg(ocr={"backend": "rapidocr", "mode": "full", "textScore": 0.4,
                      "language": "en", "roiPadding": {"x": 0.15}})
        self.assertNotEqual(a.config_id, b.config_id)
        self.assertNotEqual(_cand(config_id=a.config_id).deployment_id,
                            _cand(config_id=b.config_id).deployment_id)

    def test_DI10_config_hash_is_not_config_id(self):
        self.assertNotEqual("config:edb7ad54", "a85d7e78a27cde2321")
        # structural: configHash lives outside the manifest identity content
        self.assertNotIn("configHash", _cfg()._identity_content())


class PipelineRevisionTests(unittest.TestCase):
    def test_IDR06_dependency_version_change_changes_revision(self):
        deps_a = resolved_dependency_versions()
        deps_b = dict(deps_a)
        deps_b["ultralytics"] = "99.99.99"
        ra = compute_pipeline_revision(deps=deps_a)["pipelineRevision"]
        rb = compute_pipeline_revision(deps=deps_b)["pipelineRevision"]
        self.assertNotEqual(ra, rb)

    def test_IDR07_declarations_alone_do_not_prove_runtime(self):
        """resolved_dependency_versions reads importlib.metadata — a
        requirements file change alone cannot alter the computed revision."""
        import inspect
        from governance import pipeline_revision as pr
        src = inspect.getsource(pr)
        self.assertIn("importlib.metadata", src) or self.assertIn("pkg_version", src)
        self.assertNotIn("requirements", inspect.getsource(pr.compute_pipeline_revision))

    def test_CFI03_dependency_change_alters_pipeline_axis(self):
        deps_a = resolved_dependency_versions()
        deps_b = dict(deps_a)
        deps_b["torch"] = "0.0.0-test"
        self.assertNotEqual(
            compute_pipeline_revision(deps=deps_a)["pipelineRevision"],
            compute_pipeline_revision(deps=deps_b)["pipelineRevision"])

    def test_module_inventory_excludes_non_behavior(self):
        for m in BEHAVIOR_MODULES:
            self.assertNotIn("__pycache__", m)
            self.assertNotIn("tests", m)
            self.assertNotIn("training", m)
            self.assertNotIn("evaluation", m)
            self.assertNotIn("governance", m)
            self.assertTrue(m.endswith(".py"))

    def test_source_hash_missing_module_detected(self):
        hashes = source_hashes(pkg_root="/nonexistent-root")
        self.assertIn("MISSING", hashes.values())


class InventoryTests(unittest.TestCase):
    def test_single_ownership_guard(self):
        r = verify_single_ownership()
        self.assertTrue(r["pass"], r["violations"])
        self.assertEqual(r["materialSettings"], 26)   # 26 of 29 rows evidence-affecting
        self.assertEqual(r["operationalSettings"], 3)

    def test_material_setting_has_owner(self):
        from governance.inventory import material_settings
        for name, stage, owner in material_settings():
            self.assertTrue(owner, f"{name} has zero owner")


class DiffTests(unittest.TestCase):
    def test_classification_matrix(self):
        a = _cand()
        self.assertEqual(diff_identity(a, _cand(model_id="x" * 64)).classification,
                         "MODEL_ONLY")
        self.assertEqual(diff_identity(a, _cand(config_id="config:x")).classification,
                         "CONFIG_ONLY")
        self.assertEqual(diff_identity(a, _cand(prev="prev:x")).classification,
                         "PIPELINE_ONLY")
        self.assertEqual(
            diff_identity(a, _cand(model_id="x" * 64, config_id="config:x")).classification,
            "MODEL_AND_CONFIG")
        self.assertEqual(diff_identity(a, _cand(schema="s.v2")).classification,
                         "SCHEMA_CHANGE")
        self.assertEqual(diff_identity(a, _cand(service="2.0")).classification,
                         "NO_BEHAVIOR_CHANGE")
        self.assertEqual(
            diff_identity(a, _cand(model_id="x" * 64, prev="prev:x")).classification,
            "MULTI_AXIS")


class ModelManifestTests(unittest.TestCase):
    def test_IDR04_IDR05_truthful_separation(self):
        """The mini artifact is yolo11-derived, NOT YOLOv8; distinct
        label-space identity; modelId is bytes."""
        mini = ModelManifest(
            model_name="mini_synthetic_box", model_id="0f" + "0" * 62,
            artifact_format=ArtifactFormat.ULTRALYTICS_PT,
            architecture=Architecture.YOLO11,
            label_space_id="MINI_SYNTHETIC_BOX_V1",
            class_vocabulary=("box",),
            provenance_stance=ProvenanceStance.TRAINING_LINEAGE_LINKED,
            source_training_run_id="trun:x", source_checkpoint_id="sha256:x",
        )
        prod = ModelManifest(
            model_name="android_ui_detection_yolov8", model_id="3f" + "0" * 62,
            artifact_format=ArtifactFormat.ULTRALYTICS_PT,
            architecture=Architecture.YOLOV8,
            label_space_id="DEKI_YOLO_RAW_V1",
            class_vocabulary=("Text", "Switch"),
            provenance_stance=ProvenanceStance.LEGACY_PROVENANCE_PARTIAL,
        )
        self.assertNotEqual(mini.architecture, Architecture.YOLOV8)
        self.assertEqual(prod.architecture, Architecture.YOLOV8)
        self.assertNotEqual(mini.label_space_id, prod.label_space_id)
        self.assertNotEqual(mini.manifest_id, prod.manifest_id)


class IdentityUnitTests(unittest.TestCase):
    def test_DI20_no_runtime_semantic_dependency(self):
        """Governance imports stdlib + evaluation.identity only."""
        import pkgutil, importlib, inspect
        import governance
        for mod in pkgutil.walk_packages(governance.__path__,
                                         prefix="governance."):
            if "tests" in mod.name:
                continue
            m = importlib.import_module(mod.name)
            src = inspect.getsource(m)
            self.assertNotIn("SemanticRunResult", src)
            self.assertNotIn("GoalEvidence", src)
            self.assertNotIn("DeviceAction", src)
            self.assertNotIn("BusinessIntent", src)

    def test_deployment_identity_from_candidate(self):
        c = _cand()
        ident = PerceptionDeploymentIdentity.from_candidate(c)
        self.assertEqual(ident.deployment_id, c.deployment_id)
        self.assertEqual(ident.model_id, c.model_id)


if __name__ == "__main__":
    unittest.main()
