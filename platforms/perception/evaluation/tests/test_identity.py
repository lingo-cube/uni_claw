"""Identity falsifiers: B1, B2, B3, B7, B8, B15, B17, B19."""
from __future__ import annotations

import shutil
import tempfile
import unittest
from pathlib import Path

from evaluation.asset import (
    AdmissionStance, ComponentClass, CorpusRole, Criticality, Difficulty,
    EvaluationAsset, PerceptionTask, Provenance, ScenarioDomain, SystemFamily,
    compute_asset_id_from_file,
)
from evaluation.deployment import DeploymentSnapshot
from evaluation.run import EnvironmentProfile, EvaluationRunRequest
from evaluation import EVALUATION_SCHEMA_VERSION


def _asset_at(tmp: Path, name: str, content: bytes, **cls) -> tuple[EvaluationAsset, Path]:
    p = tmp / name
    p.write_bytes(content)
    a = EvaluationAsset.from_file(p, EVALUATION_SCHEMA_VERSION, **cls)
    return a, p


_BASE_CLS = dict(
    admission=AdmissionStance.ADMITTED,
    provenance=Provenance.RECORDED_REALITY,
    corpus_roles=(CorpusRole.CALIBRATION,),
    system_family=SystemFamily.UNKNOWN,
    scenario_domain=ScenarioDomain.SETTINGS,
    perception_tasks=(PerceptionTask.ELEMENT_DETECTION,),
    component_class=ComponentClass.UNKNOWN,
    difficulty=Difficulty.UNKNOWN,
    criticality=Criticality.NORMAL,
)


class IdentityTests(unittest.TestCase):
    def test_B1_same_bytes_different_path_same_asset_id(self):
        with tempfile.TemporaryDirectory() as tmp:
            t = Path(tmp)
            a1, p1 = _asset_at(t, "a.png", b"PNGBYTES1", **_BASE_CLS)
            sub = t / "nested"
            sub.mkdir()
            p2 = sub / "renamed.png"
            shutil.copy(p1, p2)
            a2, _ = _asset_at(sub, "renamed.png", b"PNGBYTES1", **_BASE_CLS)
            self.assertEqual(a1.asset_id, a2.asset_id)
            self.assertEqual(compute_asset_id_from_file(p1),
                             compute_asset_id_from_file(p2))

    def test_B1b_different_bytes_different_asset_id(self):
        with tempfile.TemporaryDirectory() as tmp:
            t = Path(tmp)
            a1, _ = _asset_at(t, "a.png", b"PNGBYTES1", **_BASE_CLS)
            a2, _ = _asset_at(t, "b.png", b"PNGBYTES2", **_BASE_CLS)
            self.assertNotEqual(a1.asset_id, a2.asset_id)

    def test_B2_multi_role_without_byte_duplication(self):
        # same asset, roles GOLDEN + PERFORMANCE → one content identity,
        # multiple manifest relationships
        cls = dict(_BASE_CLS)
        cls["corpus_roles"] = (CorpusRole.GOLDEN, CorpusRole.PERFORMANCE)
        a = EvaluationAsset(
            asset_schema_version=EVALUATION_SCHEMA_VERSION,
            content_hash="sha256:abc", source_path="/x.png",
            **cls)
        manifest = a.to_manifest()
        self.assertEqual(manifest["assetId"], "sha256:abc")
        self.assertEqual(len(manifest["corpus_roles"]), 2)
        # byte duplication would mean a second file — identity stays one hash
        self.assertEqual(a.asset_id, "sha256:abc")

    def test_B3_provenance_unchanged_by_role_change(self):
        with tempfile.TemporaryDirectory() as tmp:
            t = Path(tmp)
            a, _ = _asset_at(t, "a.png", b"PNGBYTES1", **_BASE_CLS)
            self.assertEqual(a.provenance, Provenance.RECORDED_REALITY)
            regression_a = EvaluationAsset(
                asset_schema_version=a.asset_schema_version,
                content_hash=a.content_hash, source_path=a.source_path,
                admission=a.admission, provenance=Provenance.RECORDED_REALITY,
                corpus_roles=(CorpusRole.REGRESSION,),
                system_family=a.system_family,
                scenario_domain=a.scenario_domain,
                perception_tasks=a.perception_tasks,
                component_class=a.component_class,
                difficulty=a.difficulty, criticality=a.criticality,
            )
            self.assertEqual(regression_a.provenance, a.provenance)
            self.assertEqual(regression_a.asset_id, a.asset_id)

    def test_B15_reference_without_byte_duplication(self):
        # creating a manifest does not copy source bytes
        with tempfile.TemporaryDirectory() as tmp:
            t = Path(tmp)
            a, p = _asset_at(t, "a.png", b"PNGBYTES1", **_BASE_CLS)
            before = sorted(x.name for x in t.iterdir())
            manifests = t / "manifests"
            manifests.mkdir()
            from evaluation.asset import save_asset_manifest
            save_asset_manifest(a, manifests)
            after = sorted(x.name for x in t.iterdir())
            # only the manifest dir added; no copy of a.png inside it
            self.assertEqual(len(after), len(before) + 1)
            self.assertEqual(sorted(x.name for x in manifests.iterdir()),
                             [f"{a.asset_id.replace('sha256:', '')}.json"])

    def test_B19_unknown_system_family_stays_unknown(self):
        with tempfile.TemporaryDirectory() as tmp:
            a, _ = _asset_at(Path(tmp), "a.png", b"PNGBYTES1", **_BASE_CLS)
            self.assertEqual(a.system_family, SystemFamily.UNKNOWN)
            m = a.to_manifest()
            self.assertEqual(m["system_family"], "UNKNOWN")


class DeploymentIdentityTests(unittest.TestCase):
    def _snapshot(self, **overrides) -> DeploymentSnapshot:
        base = dict(
            service_version="1.0",
            schema_version="uniclaw.localVisionEvidence.v1",
            model_name="android_ui_detection_yolov8",
            model_id="3f39b0d64832801072ac099ba370afe113aea32a360d4de8e24960b017b6d782",
            ocr_backend="rapidocr",
            pipeline_revision="1.0.0",
            config_identity="LEGACY_PARTIAL_CONFIG_IDENTITY",
            config_hash="a85d7e78a27cde2321c64a8d62fab46179242f056f1addb6bf6698839aafddc3",
        )
        base.update(overrides)
        return DeploymentSnapshot(**base)

    def test_B7_model_id_change_invalidates_evaluation_identity(self):
        a = self._snapshot()
        b = self._snapshot(model_id="9" * 64)
        self.assertNotEqual(a.identity_hash, b.identity_hash)
        env = EnvironmentProfile(os_name="Darwin", cpu_arch="x86_64",
                                 python_version="3.11")
        run_a = EvaluationRunRequest.create("suite:1", a, "L2", "evaluator-v1", env)
        run_b = EvaluationRunRequest.create("suite:1", b, "L2", "evaluator-v1", env)
        self.assertNotEqual(run_a.run_id, run_b.run_id)

    def test_B8_config_identity_change_invalidates_evaluation_identity(self):
        a = self._snapshot()
        b = self._snapshot(config_hash="f" * 64)
        self.assertNotEqual(a.identity_hash, b.identity_hash)
        env = EnvironmentProfile(os_name="Darwin", cpu_arch="x86_64",
                                 python_version="3.11")
        run_a = EvaluationRunRequest.create("suite:1", a, "L2", "evaluator-v1", env)
        run_b = EvaluationRunRequest.create("suite:1", b, "L2", "evaluator-v1", env)
        self.assertNotEqual(run_a.run_id, run_b.run_id)

    def test_B17_evaluator_revision_change_new_run_identity(self):
        a = self._snapshot()
        env = EnvironmentProfile(os_name="Darwin", cpu_arch="x86_64",
                                 python_version="3.11")
        run_a = EvaluationRunRequest.create("suite:1", a, "L2", "evaluator-v1", env)
        run_b = EvaluationRunRequest.create("suite:1", a, "L2", "evaluator-v2", env)
        self.assertNotEqual(run_a.run_id, run_b.run_id)

    def test_B6_same_inputs_same_run_identity(self):
        a = self._snapshot()
        env = EnvironmentProfile(os_name="Darwin", cpu_arch="x86_64",
                                 python_version="3.11")
        run_a = EvaluationRunRequest.create("suite:1", a, "L2", "evaluator-v1", env)
        run_b = EvaluationRunRequest.create("suite:1", a, "L2", "evaluator-v1", env)
        self.assertEqual(run_a.run_id, run_b.run_id)


if __name__ == "__main__":
    unittest.main()
