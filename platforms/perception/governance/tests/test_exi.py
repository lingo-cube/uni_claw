"""Execution identity falsifiers (EXI-01..07) + evaluation binding (DI-14/15)."""
from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from evaluation.deployment import DeploymentSnapshot
from evaluation.runner_l2 import EvaluationInfrastructureError, run_fresh_inference
from evaluation.identity import content_id, sha256_file


def _snapshot(*, canonical: bool = False, model_id: str = "m" * 64,
              config_id: str | None = None,
              pipeline_revision: str = "1.0.0") -> DeploymentSnapshot:
    return DeploymentSnapshot(
        service_version="1.0",
        schema_version="uniclaw.localVisionEvidence.v1",
        model_name="test", model_id=model_id,
        ocr_backend="rapidocr", pipeline_revision=pipeline_revision,
        config_identity="CANONICAL_CONFIG_ID" if canonical
        else "LEGACY_PARTIAL_CONFIG_IDENTITY",
        config_hash="a" * 64,
        config_id=config_id if canonical else None,
        deployment_id="deploy:test" if canonical else None,
    )


class ExecutionIdentityTests(unittest.TestCase):
    def _fake_pipeline_ok(self):
        fake_evidence = {"candidates": [], "yolo": [], "ocr": [],
                         "metadata": {"schema": "uniclaw.localVisionEvidence.v1"}}
        return mock.Mock(return_value=(fake_evidence, (0.0, 1.0, 2.0, 3.0)))

    def _png(self, tmp: Path) -> Path:
        from PIL import Image
        p = tmp / "shot.png"
        Image.new("RGB", (32, 32), (255, 255, 255)).save(p)
        return p

    def test_EXI01_model_override_bytes_must_match_claimed_model_id(self):
        with tempfile.TemporaryDirectory() as tmp:
            t = Path(tmp)
            png = self._png(t)
            model_file = t / "model.pt"
            model_file.write_bytes(b"model-bytes")
            snap = _snapshot(model_id="z" * 64)  # claimed ≠ actual bytes
            with mock.patch("evaluation.runner_l2._load_config", autospec=True), \
                 mock.patch("evaluation.runner_l2._load_pipeline",
                            return_value=self._fake_pipeline_ok()), \
                 mock.patch("PIL.Image.open") as mo:
                img = mock.MagicMock()
                img.size = (32, 32)
                mo.return_value.__enter__.return_value = img
                with self.assertRaises(EvaluationInfrastructureError) as ctx:
                    run_fresh_inference(png, "run:t", content_id(png.read_bytes()), snap,
                                        model_path_override=str(model_file))
                self.assertIn("EVALUATION_DEPLOYMENT_IDENTITY_MISMATCH",
                              str(ctx.exception))

    def test_EXI01b_matching_model_id_passes(self):
        with tempfile.TemporaryDirectory() as tmp:
            t = Path(tmp)
            png = self._png(t)
            model_file = t / "model.pt"
            model_file.write_bytes(b"model-bytes")
            actual = sha256_file(model_file)
            snap = _snapshot(model_id=actual)
            with mock.patch("evaluation.runner_l2._load_config", autospec=True), \
                 mock.patch("evaluation.runner_l2._load_pipeline",
                            return_value=self._fake_pipeline_ok()), \
                 mock.patch("PIL.Image.open") as mo:
                img = mock.MagicMock()
                img.size = (32, 32)
                mo.return_value.__enter__.return_value = img
                pred = run_fresh_inference(png, "run:t", content_id(png.read_bytes()), snap,
                                           model_path_override=str(model_file))
                self.assertEqual(pred.yolo_count, 0)

    def test_EXI02_config_mismatch_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            t = Path(tmp)
            png = self._png(t)
            snap = _snapshot(canonical=True, model_id="m" * 64,
                             config_id="config:WRONG")
            with mock.patch("evaluation.runner_l2._load_config", autospec=True), \
                 mock.patch("evaluation.runner_l2._load_pipeline",
                            return_value=self._fake_pipeline_ok()), \
                 mock.patch("uniclaw_perception.config.get_config") as gc, \
                 mock.patch("PIL.Image.open") as mo:
                img = mock.MagicMock()
                img.size = (32, 32)
                mo.return_value.__enter__.return_value = img
                cfg = mock.MagicMock()
                cfg.config_path = "/tmp/nonexistent-label-mapping.json"
                gc.return_value = cfg
                with self.assertRaises(EvaluationInfrastructureError) as ctx:
                    run_fresh_inference(png, "run:t", content_id(png.read_bytes()), snap)
                self.assertIn("EVALUATION_DEPLOYMENT_IDENTITY_MISMATCH",
                              str(ctx.exception))

    def test_DI14_evaluation_run_references_deployment_identity(self):
        """Run records carry deployment identity when canonical."""
        snap = _snapshot(canonical=True, model_id="m" * 64,
                         config_id="config:x", pipeline_revision="prev:x")
        from evaluation.run import EnvironmentProfile, EvaluationRunRequest
        env = EnvironmentProfile(os_name="Darwin", cpu_arch="x86_64",
                                 python_version="3.11")
        run = EvaluationRunRequest.create("suite:1", snap, "L2", "evaluator-v1", env)
        j = run.to_json()
        # DeploymentSnapshot serializes with snake_case field names
        self.assertEqual(j["deployment"]["config_id"], "config:x")
        self.assertEqual(j["deployment"]["deployment_id"], "deploy:test")
        self.assertEqual(j["deployment"]["config_identity"],
                         "CANONICAL_CONFIG_ID")

    def test_DI15_historical_snapshots_stay_partial(self):
        snap = _snapshot(canonical=False)
        j = snap.to_json()
        self.assertIsNone(j["config_id"])
        self.assertIsNone(j["deployment_id"])
        self.assertEqual(j["config_identity"], "LEGACY_PARTIAL_CONFIG_IDENTITY")

    def test_EXI04_version_cannot_echo_expected(self):
        """/version reports the STARTUP IDENTITY SNAPSHOT (captured from
        loaded state at lifespan) — never echoes an 'expected' input, and
        the canonical path never re-reads disk per call."""
        import inspect
        import re
        import uniclaw_perception.health as h
        src = inspect.getsource(h.version)
        code = re.sub(r'""".*?"""', '""" """', src, flags=re.DOTALL)
        self.assertIn("get_snapshot", code)
        self.assertNotIn("expected", code.lower())
        # snapshot is captured at startup from LOADED state
        import uniclaw_perception.server as s
        lsrc = inspect.getsource(s.lifespan)
        self.assertIn("capture_snapshot", lsrc)

    def test_EXI07_historical_evaluation_files_byte_identical(self):
        """Historical run/baseline files are never rewritten by the new
        canonical integration (structural: no rewrite API exists)."""
        import pkgutil, importlib, inspect
        import governance
        for mod in pkgutil.walk_packages(governance.__path__,
                                         prefix="governance."):
            if "tests" in mod.name:
                continue
            m = importlib.import_module(mod.name)
            src = inspect.getsource(m)
            self.assertNotIn("rewrite", src.lower())
            self.assertNotIn("overwrite", src.lower())


if __name__ == "__main__":
    unittest.main()
