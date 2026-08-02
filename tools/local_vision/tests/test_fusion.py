from __future__ import annotations

import unittest

from tools.local_vision.fusion import fuse_evidence
from tools.local_vision.schema import Box, Detection, OcrToken


class FusionTests(unittest.TestCase):
    def test_fuses_ocr_inside_yolo_box(self) -> None:
        evidence = fuse_evidence(
            [
                Detection(
                    id="det_1",
                    label="list_item",
                    confidence=0.94,
                    box=Box(20, 100, 300, 180),
                )
            ],
            [
                OcrToken(
                    id="ocr_1",
                    text="Display",
                    confidence=0.91,
                    box=Box(42, 118, 130, 150),
                )
            ],
            image_width=400,
            image_height=800,
        )

        self.assertEqual(evidence["summary"]["candidateCount"], 1)
        candidate = evidence["candidates"][0]
        self.assertEqual(candidate["text"], "Display")
        self.assertEqual(candidate["evidence"]["yoloId"], "det_1")
        self.assertEqual(candidate["evidence"]["ocrIds"], ["ocr_1"])
        self.assertEqual(candidate["centerPx"], [160, 140])

    def test_marks_missing_text_as_risk_for_textual_control(self) -> None:
        evidence = fuse_evidence(
            [
                Detection(
                    id="det_1",
                    label="button",
                    confidence=0.82,
                    box=Box(200, 700, 380, 760),
                )
            ],
            [],
            image_width=400,
            image_height=800,
        )

        self.assertEqual(evidence["candidates"][0]["riskFlags"], ["no_text_evidence"])

    def test_can_promote_unmatched_ocr(self) -> None:
        evidence = fuse_evidence(
            [],
            [
                OcrToken(
                    id="ocr_1",
                    text="About phone",
                    confidence=0.96,
                    box=Box(20, 100, 180, 140),
                )
            ],
            image_width=400,
            image_height=800,
            promote_unmatched_ocr=True,
        )

        candidate = evidence["candidates"][0]
        self.assertEqual(candidate["type"], "text_block")
        self.assertEqual(candidate["riskFlags"], ["ocr_only"])


if __name__ == "__main__":
    unittest.main()
