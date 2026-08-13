"""Performance falsifiers: B13 (sample count recorded), B14 (no P99 from
tiny samples)."""
from __future__ import annotations

import unittest

from evaluation.deployment import DeploymentSnapshot
from evaluation.performance import (
    MIN_SAMPLES_P50_P95, MIN_SAMPLES_P99, PerformanceResult,
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


class PerformanceTests(unittest.TestCase):
    def test_B13_sample_count_recorded(self):
        pr = PerformanceResult.create([100.0, 110.0, 120.0], run_id="run:1",
                                      deployment=_snapshot(), asset_id=None,
                                      environment={}, evaluator_revision="ev1",
                                      warm=True)
        j = pr.to_json()
        self.assertEqual(j["sampleCount"], 3)
        self.assertEqual(len(j["samplesMs"]), 3)

    def test_B14_no_p99_from_tiny_samples(self):
        pr = PerformanceResult.create([100.0, 110.0, 120.0], run_id="run:1",
                                      deployment=_snapshot(), asset_id=None,
                                      environment={}, evaluator_revision="ev1",
                                      warm=True)
        j = pr.to_json()
        self.assertNotIn("p99Ms", j["summary"])
        self.assertNotIn("p50Ms", j["summary"])   # n=3 < MIN_SAMPLES_P50_P95
        self.assertNotIn("p95Ms", j["summary"])
        self.assertIn("medianMs", j["summary"])
        self.assertIn("meanMs", j["summary"])

    def test_B14b_p50_p95_only_with_sufficient_samples(self):
        samples = [100.0 + i for i in range(MIN_SAMPLES_P50_P95)]
        pr = PerformanceResult.create(samples, run_id="run:1",
                                      deployment=_snapshot(), asset_id=None,
                                      environment={}, evaluator_revision="ev1",
                                      warm=True)
        j = pr.to_json()
        self.assertIn("p50Ms", j["summary"])
        self.assertIn("p95Ms", j["summary"])
        self.assertNotIn("p99Ms", j["summary"])  # below MIN_SAMPLES_P99

    def test_B14c_p99_only_with_large_samples(self):
        samples = [100.0 + i for i in range(MIN_SAMPLES_P99)]
        pr = PerformanceResult.create(samples, run_id="run:1",
                                      deployment=_snapshot(), asset_id=None,
                                      environment={}, evaluator_revision="ev1",
                                      warm=True)
        j = pr.to_json()
        self.assertIn("p99Ms", j["summary"])

    def test_performance_result_identity_depends_on_samples(self):
        a = PerformanceResult.create([100.0, 110.0], run_id="run:1",
                                     deployment=_snapshot(), asset_id=None,
                                     environment={}, evaluator_revision="ev1",
                                     warm=True)
        b = PerformanceResult.create([100.0, 115.0], run_id="run:1",
                                     deployment=_snapshot(), asset_id=None,
                                     environment={}, evaluator_revision="ev1",
                                     warm=True)
        self.assertNotEqual(a.result_id, b.result_id)


if __name__ == "__main__":
    unittest.main()
