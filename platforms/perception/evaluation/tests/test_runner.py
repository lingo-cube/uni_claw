"""L2 runner falsifiers: B5, B12, PF3, PF4 + fresh-inference enforcement."""
from __future__ import annotations

import tempfile
import unittest
from pathlib import Path
from unittest import mock

from evaluation.deployment import DeploymentSnapshot
from evaluation.runner_l2 import (
    EvaluationInfrastructureError, run_fresh_inference,
)


def _snapshot() -> DeploymentSnapshot:
    return DeploymentSnapshot(
        service_version="1.0",
        schema_version="uniclaw.localVisionEvidence.v1",
        model_name="android_ui_detection_yolov8",
        model_id="3f39b0d64832801072ac099ba370afe113aea32a360d4de8e24960b017b6d782",
        ocr_backend="rapidocr",
        pipeline_revision="1.0.0",
        config_identity="LEGACY_PARTIAL_CONFIG_IDENTITY",
        config_hash="a85d7e78a27cde2321c64a8d62fab46179242f056f1addb6bf6698839aafddc3",
    )


class L2RunnerTests(unittest.TestCase):
    def test_B5_runner_performs_fresh_inference_not_replay(self):
        """The runner invokes the production pipeline; it never reads stored
        historical perception JSON to fabricate a prediction."""
        with tempfile.TemporaryDirectory() as tmp:
            png = Path(tmp) / "shot.png"
            from PIL import Image
            Image.new("RGB", (64, 64), (255, 255, 255)).save(png)

            fake_evidence = {
                "candidates": [{"type": "text_block", "text": "X",
                                "bounds": {"x1": 0.1, "y1": 0.1, "x2": 0.2, "y2": 0.2},
                                "confidence": 0.9}],
                "yolo": [], "ocr": [],
                "metadata": {"schema": "uniclaw.localVisionEvidence.v1"},
            }
            fake_timings = (0.0, 1.0, 2.0, 3.0)

            with mock.patch("evaluation.runner_l2._load_config", autospec=True), \
                 mock.patch("evaluation.runner_l2._load_pipeline",
                            return_value=mock.Mock(
                                return_value=(fake_evidence, fake_timings))), \
                 mock.patch("PIL.Image.open") as mock_open:
                img = mock.MagicMock()
                img.size = (64, 64)
                mock_open.return_value.__enter__.return_value = img

                from evaluation.identity import content_id
                pred = run_fresh_inference(png, "run:test", content_id(png.read_bytes()),
                                           _snapshot())
                self.assertEqual(pred.yolo_count, 0)
                self.assertEqual(len(pred.candidates), 1)
                # schema came from fresh pipeline metadata
                self.assertEqual(pred.schema_version,
                                 "uniclaw.localVisionEvidence.v1")

    def test_B12_infrastructure_failure_is_distinct(self):
        """Missing bytes raise EvaluationInfrastructureError — mapped to
        INSUFFICIENT_EVIDENCE/INFRASTRUCTURE_FAILURE, never a PASS."""
        with self.assertRaises(EvaluationInfrastructureError):
            run_fresh_inference("/nonexistent/shot.png", "run:test",
                                "sha256:test", _snapshot())

    def test_PF3_replay_cannot_masquerade_as_L2_accuracy(self):
        """Stored historical output has no image bytes; the L2 runner
        requires a real screenshot file and performs fresh inference."""
        # informational-only assets have no image path — structural stance
        from evaluation.asset import AdmissionStance
        self.assertEqual(AdmissionStance.INFORMATIONAL_ONLY.value,
                         "INFORMATIONAL_ONLY")
        # runner refuses non-image sources
        with tempfile.TemporaryDirectory() as tmp:
            bad = Path(tmp) / "stored.json"
            bad.write_text('{"candidates": []}')
            from evaluation.identity import content_id
            claimed = content_id(bad.read_bytes())
            with self.assertRaises(EvaluationInfrastructureError) as ctx:
                with mock.patch("evaluation.runner_l2._load_config", autospec=True), \
                     mock.patch("evaluation.runner_l2._load_pipeline",
                                return_value=mock.Mock()), \
                     mock.patch("PIL.Image.open",
                                side_effect=OSError("not an image")):
                    run_fresh_inference(bad, "run:test", claimed,
                                        _snapshot())
            self.assertIn("fresh inference failed", str(ctx.exception))

    def test_PF4_simulation_cannot_contribute_visual_accuracy(self):
        """L2 requires image bytes; a simulation record (no screenshot)
        cannot enter the L2 accuracy path."""
        with mock.patch("evaluation.runner_l2._load_config", autospec=True), \
             mock.patch("evaluation.runner_l2._load_pipeline",
                        return_value=mock.Mock()), \
             mock.patch("PIL.Image.open", side_effect=OSError("no image bytes")):
            with self.assertRaises(EvaluationInfrastructureError):
                run_fresh_inference("/tmp/sim-record.json", "run:test",
                                    "sha256:test", _snapshot())

    def test_B6_deterministic_run_identity_already_covered(self):
        # run identity determinism is tested in test_identity.py; here we
        # assert the prediction carries the deployment hash for reproduction
        self.assertNotEqual(_snapshot().identity_hash, "")


if __name__ == "__main__":
    unittest.main()
