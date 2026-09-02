"""P-OCR text-level ground-truth assertions (perception-ocr-en-v4-normalization).

Design D5: the existing 3-GT suite cannot distinguish OCR quality (four models
all hit 5/5 on coarse title matching).  This layer adds TEXT-LEVEL assertions:
normalized OCR output on the reality fixtures must hit the GT-protected text
(e.g. `Network & internet`), including the cases previously broken by the
Chinese model (glued tokens) — so a future model regression is observable.

These tests exercise the OCR+nomalization pipeline directly (no YOLO): the GT
texts here are exactly the screen titles the fusion layer relies on.
"""
from __future__ import annotations

import json
import re
import unittest
from pathlib import Path

from PIL import Image

from uniclaw_perception.ocr.rapid import (
    configure_ocr_models, _rapid_ocr_kwargs, run_rapid_ocr_on_image,
)

REALITY = Path(__file__).resolve().parent.parent.parent / "tests" / "fixtures" / "reality"


def _norm(s: str) -> str:
    return re.sub(r"[^a-z0-9]", "", str(s).lower())


def _initialize_en_rec() -> None:
    """Resolve en rec model via the managed-artifact path and seed the
    singleton kwargs (same path as server lifespan)."""
    if not _rapid_ocr_kwargs:
        _rapid_ocr_kwargs.update(configure_ocr_models(language="en"))


class TextLevelGtAssertions(unittest.TestCase):
    """GT-protected screen text must survive OCR + normalization."""

    @classmethod
    def setUpClass(cls):
        _initialize_en_rec()

    def test_settings_root_titles_hit_after_normalization(self):
        img = REALITY / "settings-root-row-composition.png"
        gt = json.loads(
            (REALITY / "settings-root-row-composition.groundtruth.json")
            .read_text(encoding="utf-8"))
        tokens = [t.text for t in run_rapid_ocr_on_image(
            Image.open(img).convert("RGB"), text_score=0.5)]
        joined = " ".join(_norm(t) for t in tokens)
        for title in gt["expectedAnchoredTitles"]:
            self.assertIn(
                _norm(title), joined,
                f"GT-protected title {title!r} not found after normalization "
                f"(tokens={tokens})")

    def test_no_trailing_period_noise_on_titles(self):
        """en_v4's postfix-period noise must be stripped by the layer."""
        img = REALITY / "developer-options-falsification.png"
        tokens = [t.text for t in run_rapid_ocr_on_image(
            Image.open(img).convert("RGB"), text_score=0.5)]
        noisy = [t for t in tokens
                 if t.rstrip(".!?;:") != t and "&" not in t]
        self.assertEqual(
            noisy, [],
            f"normalized tokens still carry trailing punctuation: {noisy}")

    def test_glued_token_example_is_recovered(self):
        """Direct spec sample: the concatenation layer output (pure unit)."""
        from uniclaw_perception.ocr.normalize import normalize_ocr_token
        self.assertEqual(
            normalize_ocr_token("Disableadbauthorizationtimeout"),
            "Disable adb authorization timeout")


if __name__ == "__main__":
    unittest.main()