"""ASSET-01..06: manifest and L2 execution bind exact asset bytes."""
from __future__ import annotations

import tempfile
import unittest
import json
import sys
import types
from pathlib import Path
from unittest import mock

from evaluation import EVALUATION_SCHEMA_VERSION
from evaluation.asset import (
    ASSET_CONTENT_IDENTITY_MISMATCH, AdmissionStance, AssetContentIdentityError,
    ComponentClass, CorpusRole, Criticality, Difficulty, EvaluationAsset,
    PerceptionTask, Provenance, ScenarioDomain, SystemFamily,
    load_asset_manifest, save_asset_manifest,
)
from evaluation.deployment import DeploymentSnapshot
from evaluation.identity import content_id
from evaluation.runner_l2 import EvaluationInfrastructureError, run_fresh_inference


_CLASSIFICATION = dict(
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


def _snapshot() -> DeploymentSnapshot:
    return DeploymentSnapshot(
        service_version="test", schema_version="test", model_name="test",
        model_id="m" * 64, ocr_backend="test", pipeline_revision="test",
        config_identity="LEGACY_PARTIAL_CONFIG_IDENTITY", config_hash="c" * 64)


class AssetIntegrityTests(unittest.TestCase):
    def test_ASSET_01_manifest_internal_asset_id_mismatch_is_refused(self):
        with tempfile.TemporaryDirectory() as tmp:
            source = Path(tmp) / "source.bin"
            source.write_bytes(b"asset-one")
            asset = EvaluationAsset.from_file(source, EVALUATION_SCHEMA_VERSION,
                                               **_CLASSIFICATION)
            manifest = asset.to_manifest()
            manifest["assetId"] = "sha256:" + "0" * 64
            with self.assertRaises(AssetContentIdentityError) as context:
                EvaluationAsset.from_manifest(manifest)
            self.assertIn(ASSET_CONTENT_IDENTITY_MISMATCH, str(context.exception))

    def test_ASSET_02_load_manifest_rejects_internal_identity_mismatch(self):
        with tempfile.TemporaryDirectory() as tmp:
            source = Path(tmp) / "source.bin"
            source.write_bytes(b"asset-two")
            asset = EvaluationAsset.from_file(source, EVALUATION_SCHEMA_VERSION,
                                               **_CLASSIFICATION)
            manifest_dir = Path(tmp) / "manifests"
            path = save_asset_manifest(asset, manifest_dir)
            manifest = json.loads(path.read_text(encoding="utf-8"))
            manifest["assetId"] = "sha256:" + "f" * 64
            path.write_text(json.dumps(manifest), encoding="utf-8")
            with self.assertRaises(AssetContentIdentityError):
                load_asset_manifest(path)

    def test_ASSET_03_source_byte_mismatch_blocks_before_pipeline(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "frame.png"
            path.write_bytes(b"not the claimed bytes")
            with mock.patch("evaluation.runner_l2._load_config") as config, \
                 mock.patch("evaluation.runner_l2._load_pipeline") as pipeline:
                with self.assertRaises(EvaluationInfrastructureError) as context:
                    run_fresh_inference(path, "run:test", "sha256:" + "0" * 64,
                                        _snapshot())
            self.assertIn(ASSET_CONTENT_IDENTITY_MISMATCH, str(context.exception))
            config.assert_not_called()
            pipeline.assert_not_called()

    def test_ASSET_04_verified_buffer_is_the_only_authoritative_image_read(self):
        """Replacing the path after verification cannot affect pipeline pixels."""
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "frame.png"
            path.write_bytes(b"verified image bytes")
            claimed = content_id(path.read_bytes())

            class VerifiedImage:
                size = (2, 2)

                def load(self):
                    pass

                def getpixel(self, _coordinate):
                    return (255, 255, 255)

                def __enter__(self):
                    return self

                def __exit__(self, *_args):
                    return False

            image_module = types.ModuleType("PIL.Image")
            image_module.open = lambda _buffer: VerifiedImage()
            pil_package = types.ModuleType("PIL")
            pil_package.Image = image_module

            def pipeline(image, width, height, **_kwargs):
                path.write_bytes(b"replacement image bytes")
                self.assertEqual(image.getpixel((0, 0)), (255, 255, 255))
                return ({"candidates": [], "yolo": [], "ocr": [],
                         "metadata": {"schema": "test"}}, (0, 0, 0, 0), {})

            with mock.patch("evaluation.runner_l2._load_config"), \
                 mock.patch("evaluation.runner_l2._load_pipeline", return_value=pipeline), \
                 mock.patch.dict(sys.modules, {"PIL": pil_package,
                                               "PIL.Image": image_module}):
                prediction = run_fresh_inference(path, "run:test", claimed, _snapshot())
            self.assertEqual(prediction.source_content_hash, claimed)
            self.assertNotEqual(content_id(path.read_bytes()), claimed)

    def test_ASSET_05_manifest_write_is_write_once(self):
        with tempfile.TemporaryDirectory() as tmp:
            source = Path(tmp) / "source.bin"
            source.write_bytes(b"asset-five")
            asset = EvaluationAsset.from_file(source, EVALUATION_SCHEMA_VERSION,
                                               **_CLASSIFICATION)
            manifest_dir = Path(tmp) / "manifests"
            path = save_asset_manifest(asset, manifest_dir)
            self.assertEqual(path, save_asset_manifest(asset, manifest_dir))

    def test_ASSET_06_prediction_write_is_write_once(self):
        from evaluation.prediction import Prediction, save_prediction
        with tempfile.TemporaryDirectory() as tmp:
            prediction = Prediction("run:test", "sha256:asset", "deploy:test",
                                    "test", (), 0, 0)
            path = save_prediction(prediction, tmp)
            self.assertEqual(path, save_prediction(prediction, tmp))


if __name__ == "__main__":
    unittest.main()
