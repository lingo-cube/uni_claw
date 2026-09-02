"""P-OCR: ocr-backend-selection spec scenarios (perception-ocr-en-v4-normalization).

Maps to specs/perception/ocr-backend-selection/spec.md:
  * declared-language-determines-model (en → en rec; unsupported fails closed)
  * managed-artifact regime (content-addressed registration; unregistered
    weights rejected at load; missing-on-disk artifact fails closed)
"""
from __future__ import annotations

import unittest
from pathlib import Path

from governance.ocr_model_manifest import (
    OcrModelManifest, OcrRole, build_ocr_model_manifest,
    find_registered_manifest, load_ocr_manifests,
    ocr_models_dir, save_ocr_manifest, governance_ocr_models_dir,
)

ROOT = Path(__file__).resolve().parent.parent.parent  # platforms/perception/


class OcrModelManifestTests(unittest.TestCase):
    """Registered managed artifacts (rec + dict from the en_v4 intake)."""

    def test_registered_en_rec_artifacts(self):
        manifests = load_ocr_manifests(ROOT)
        recs = [m for m in manifests if m.role == OcrRole.REC]
        self.assertGreaterEqual(len(recs), 1, "expected registered rec artifacts")
        en_recs = [m for m in recs if m.language == "en"]
        self.assertTrue(en_recs, "expected an en rec artifact registered")

    def test_registration_is_content_addressed(self):
        manifests = load_ocr_manifests(ROOT)
        self.assertTrue(
            all(len(m.artifact_id) == 64 for m in manifests),
            "artifactId must be the full 64-hex content SHA-256")

    def test_loading_onnx_bytes_matches_registered_id(self):
        manifests = load_ocr_manifests(ROOT)
        recs = [m for m in manifests if m.role == OcrRole.REC and m.language == "en"]
        if not recs:
            self.skipTest("no en rec artifact registered in this checkout")
        m = recs[0]
        path = ocr_models_dir(ROOT) / m.file_name
        self.assertTrue(path.exists(), f"managed file missing: {path}")
        # find_registered_manifest is the identity-by-content load guard
        found = find_registered_manifest(ROOT, path)
        self.assertIsNotNone(found)
        self.assertEqual(found.artifact_id, m.artifact_id)

    def test_unregistered_manifest_returns_none(self):
        fake = ROOT / "ocr" / "models" / "__unregistered_fake.txt"
        try:
            fake.write_text("fake", encoding="utf-8")
            # not registered → load guard returns None (reject)
            self.assertIsNone(find_registered_manifest(ROOT, fake))
        finally:
            fake.unlink(missing_ok=True)

    def test_register_missing_file_raises(self):
        # build from a nonexistent path must fail (no fabricated identity)
        with self.assertRaises(Exception):
            build_ocr_model_manifest(
                ROOT / "ocr" / "models" / "does-not-exist.onnx",
                language="en", role=OcrRole.REC)


if __name__ == "__main__":
    unittest.main()