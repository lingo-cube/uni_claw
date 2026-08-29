"""Deterministic navigation-row composition regressions for IR-G0."""
from __future__ import annotations

import json
import unittest
from pathlib import Path

from PIL import Image

from uniclaw_perception import server as perception_server
from uniclaw_perception.fusion.engine import fuse_evidence, fuse_evidence_from_crops
from uniclaw_perception.schema import Box, Detection, OcrToken


_FIXTURES = Path(__file__).parent / "fixtures" / "reality"
_YOLO_MODEL = (
    Path(__file__).parents[1]
    / "models" / "yolo" / "android_ui_detection_yolov8" / "best.pt"
)


def _det(identifier: str, label: str, bounds: tuple[float, float, float, float], confidence: float = 0.9) -> Detection:
    return Detection(identifier, label, confidence, Box(*bounds))


def _ocr(identifier: str, text: str, bounds: tuple[float, float, float, float]) -> OcrToken:
    return OcrToken(identifier, text, 0.99, Box(*bounds))


def _menu_items(evidence: dict) -> list[dict]:
    return [candidate for candidate in evidence["candidates"] if candidate["type"] == "menu_item"]


def _single_row_evidence(scale: float = 1.0) -> dict:
    def b(x1: float, y1: float, x2: float, y2: float) -> tuple[float, float, float, float]:
        return x1 * scale, y1 * scale, x2 * scale, y2 * scale

    detections = [
        _det("det_title", "text_block", b(120, 90, 360, 120), 0.91),
        _det("det_combined", "text_block", b(120, 90, 430, 170), 0.75),
        _det("det_icon", "icon", b(55, 110, 95, 150), 0.88),
        _det("det_description", "text_block", b(120, 140, 430, 170), 0.86),
    ]
    tokens = [
        _ocr("ocr_title", "Network & internet", b(122, 92, 358, 118)),
        _ocr("ocr_description", "Mobile, Wi-Fi, hotspot", b(122, 142, 428, 168)),
    ]
    return fuse_evidence(
        detections,
        tokens,
        image_width=round(720 * scale),
        image_height=round(1600 * scale),
        promote_unmatched_ocr=True,
    )


def _uniform_list_evidence(
    confirmed_centers: list[float],
    unanchored: list[tuple[str, str, tuple[float, float, float, float]]],
    *,
    controls: list[tuple[str, str, tuple[float, float, float, float]]] | None = None,
    width: int = 600,
    height: int = 1200,
) -> dict:
    detections: list[Detection] = []
    tokens: list[OcrToken] = []
    for index, center in enumerate(confirmed_centers, start=1):
        title_id = f"anchor_title_{index}"
        icon_id = f"anchor_icon_{index}"
        text = f"Confirmed {index}"
        detections.extend([
            _det(title_id, "text_block", (120, center - 15, 300, center + 15)),
            _det(icon_id, "icon", (55, center - 18, 90, center + 18)),
        ])
        tokens.append(_ocr(f"anchor_ocr_{index}", text, (122, center - 13, 298, center + 13)))
    for identifier, text, bounds in unanchored:
        detections.append(_det(identifier, "text_block", bounds))
        tokens.append(_ocr(f"{identifier}_ocr", text, bounds))
    for identifier, label, bounds in controls or []:
        detections.append(_det(identifier, label, bounds))
    return fuse_evidence(
        detections,
        tokens,
        image_width=width,
        image_height=height,
        max_ocr_distance_ratio=0.005,
    )


class NavigationRowCompositionTests(unittest.TestCase):
    def test_title_description_and_overlapping_boxes_become_one_row(self):
        evidence = _single_row_evidence()

        rows = _menu_items(evidence)
        self.assertEqual([row["text"] for row in rows], ["Network & internet"])
        self.assertEqual(rows[0]["evidence"]["yoloId"], "det_title")
        self.assertEqual(
            set(rows[0]["evidence"]["allIds"]),
            {"det_title", "det_combined", "det_description", "det_icon", "ocr_title", "ocr_description"},
        )
        self.assertEqual(len(evidence["yolo"]), 4)
        self.assertEqual(len(evidence["ocr"]), 2)
        self.assertFalse(any(candidate["type"] == "icon" for candidate in evidence["candidates"]))

    def test_row_composition_is_scale_stable(self):
        for scale in (0.5, 1.0, 1.5):
            with self.subTest(scale=scale):
                rows = _menu_items(_single_row_evidence(scale))
                self.assertEqual([row["text"] for row in rows], ["Network & internet"])

    def test_title_only_row_remains_one_menu_item(self):
        evidence = fuse_evidence(
            [_det("title", "text_block", (100, 90, 260, 120)), _det("icon", "icon", (40, 85, 75, 125))],
            [_ocr("title_ocr", "Display", (102, 92, 258, 118))],
            image_width=400,
            image_height=400,
        )

        self.assertEqual([row["text"] for row in _menu_items(evidence)], ["Display"])

    def test_repeated_labels_on_distinct_anchors_remain_distinct(self):
        evidence = fuse_evidence(
            [
                _det("title_1", "text_block", (100, 80, 240, 110)),
                _det("icon_1", "icon", (40, 75, 75, 115)),
                _det("title_2", "text_block", (100, 200, 240, 230)),
                _det("icon_2", "icon", (40, 195, 75, 235)),
            ],
            [
                _ocr("ocr_1", "Accounts", (102, 82, 238, 108)),
                _ocr("ocr_2", "Accounts", (102, 202, 238, 228)),
            ],
            image_width=400,
            image_height=400,
        )

        rows = _menu_items(evidence)
        self.assertEqual([row["text"] for row in rows], ["Accounts", "Accounts"])
        self.assertNotEqual(rows[0]["evidence"]["yoloId"], rows[1]["evidence"]["yoloId"])

    def test_tightly_adjacent_rows_keep_unique_anchors(self):
        evidence = fuse_evidence(
            [
                _det("title_1", "text_block", (100, 85, 230, 115)),
                _det("icon_1", "icon", (40, 80, 75, 120)),
                _det("title_2", "text_block", (100, 135, 230, 165)),
                _det("icon_2", "icon", (40, 130, 75, 170)),
            ],
            [
                _ocr("ocr_1", "First", (102, 87, 228, 113)),
                _ocr("ocr_2", "Second", (102, 137, 228, 163)),
            ],
            image_width=400,
            image_height=400,
        )

        self.assertEqual([row["text"] for row in _menu_items(evidence)], ["First", "Second"])

    def test_equal_distance_to_two_anchors_is_not_promoted(self):
        evidence = fuse_evidence(
            [
                _det("title", "text_block", (100, 105, 250, 135)),
                _det("icon_1", "icon", (40, 80, 75, 120)),
                _det("icon_2", "icon", (40, 120, 75, 160)),
            ],
            [_ocr("ocr_title", "Ambiguous", (102, 107, 248, 133))],
            image_width=400,
            image_height=400,
        )

        # S2 routing (Leader-sanctioned delta; see s2-delta-report.md
        # Changed-case-1): below the 4-anchor floor the routed pipeline
        # composes this equidistant row via row-relation-head.  The chevron
        # attachment layer's semantics are UNCHANGED — the promotion must
        # carry the row_relation_head provenance with its own detector-anchor
        # band, never the chevron row_composition reason.
        rows = _menu_items(evidence)
        self.assertEqual([row["text"] for row in rows], ["Ambiguous"])
        promoted = rows[0]
        self.assertEqual(promoted["id"], "relation_head_band_1")
        self.assertEqual(promoted["evidence"]["typeInferred"], "row_relation_head")
        self.assertEqual(promoted["evidence"]["yoloId"], "title")
        self.assertIn("ocr_title", promoted["evidence"]["ocrIds"])
        # The chevron/attachment layer itself still does not promote
        # equidistant text: the raw text_block detection is emitted with no
        # row provenance, and no menu_item claims row_composition.
        self.assertNotIn(
            "row_composition",
            {row["evidence"].get("typeInferred") for row in rows},
        )
        raw_text = next(
            candidate for candidate in evidence["candidates"]
            if candidate["text"] == "Ambiguous" and candidate["type"] == "text_block"
        )
        self.assertIsNone(raw_text["evidence"].get("typeInferred"))
        self.assertEqual(raw_text["type"], "text_block")

    def test_legacy_crop_fusion_uses_the_same_row_composition(self):
        detections = [
            _det("det_title", "text_block", (120, 90, 360, 120), 0.91),
            _det("det_combined", "text_block", (120, 90, 430, 170), 0.75),
            _det("det_icon", "icon", (55, 110, 95, 150), 0.88),
            _det("det_description", "text_block", (120, 140, 430, 170), 0.86),
        ]
        title = _ocr("ocr_title", "Network & internet", (122, 92, 358, 118))
        description = _ocr("ocr_description", "Mobile, Wi-Fi, hotspot", (122, 142, 428, 168))

        evidence = fuse_evidence_from_crops(
            detections,
            [[title], [title, description], [], [description]],
            image_width=720,
            image_height=1600,
        )

        self.assertEqual([row["text"] for row in _menu_items(evidence)], ["Network & internet"])
        self.assertEqual(
            set(_menu_items(evidence)[0]["evidence"]["allIds"]),
            {"det_title", "det_combined", "det_description", "det_icon", "ocr_title", "ocr_description"},
        )

    def test_uniform_list_recovers_one_uniquely_bracketed_row(self):
        evidence = _uniform_list_evidence(
            [100, 200, 400, 500],
            [("missing_title", "Apps", (120, 285, 240, 315))],
        )

        rows = _menu_items(evidence)
        inferred = next(row for row in rows if row["text"] == "Apps")
        self.assertEqual(inferred["evidence"]["typeInferred"], "uniform_list_bracketed_row")

    def test_uniform_list_groups_inferred_title_and_description(self):
        evidence = _uniform_list_evidence(
            [100, 200, 400, 500],
            [
                ("missing_title", "Apps", (120, 280, 240, 310)),
                ("missing_description", "Recent apps, default apps", (120, 325, 370, 350)),
            ],
        )

        self.assertEqual([row["text"] for row in _menu_items(evidence)].count("Apps"), 1)
        self.assertNotIn("Recent apps, default apps", [row["text"] for row in _menu_items(evidence)])
        apps = next(row for row in _menu_items(evidence) if row["text"] == "Apps")
        self.assertIn("missing_description", apps["evidence"]["allIds"])

    def test_uniform_list_distinguishes_close_compact_description_from_title(self):
        evidence = _uniform_list_evidence(
            [350, 500, 650, 800, 950, 1100, 1400],
            [
                ("missing_title", "System", (120, 1234, 235, 1268)),
                ("missing_description", "Languages, gestures, time, backup", (120, 1275, 430, 1300)),
            ],
            height=1600,
        )

        rows = _menu_items(evidence)
        inferred = next(row for row in rows if row["text"] == "System")
        self.assertEqual(inferred["evidence"]["typeInferred"], "uniform_list_bracketed_row")
        self.assertNotIn("Languages, gestures, time, backup", [row["text"] for row in rows])
        self.assertIn("missing_description", inferred["evidence"]["allIds"])

    def test_uniform_list_does_not_promote_slot_with_trailing_control(self):
        evidence = _uniform_list_evidence(
            [100, 200, 400, 500],
            [("missing_title", "Wi-Fi", (120, 285, 240, 315))],
            controls=[("row_switch", "switch", (430, 282, 500, 318))],
        )

        self.assertNotIn("Wi-Fi", [row["text"] for row in _menu_items(evidence)])

    def test_left_side_switch_label_false_positive_can_still_anchor_navigation(self):
        evidence = fuse_evidence(
            [
                _det("title", "text_block", (120, 85, 260, 115)),
                _det("left_false_switch", "switch", (50, 82, 90, 118)),
            ],
            [_ocr("title_ocr", "Display", (122, 87, 258, 113))],
            image_width=500,
            image_height=500,
            max_ocr_distance_ratio=0.005,
        )

        self.assertEqual([row["text"] for row in _menu_items(evidence)], ["Display"])
        self.assertFalse(any(candidate["type"] == "switch" for candidate in evidence["candidates"]))

    def test_empty_text_detector_artifact_is_not_emitted_as_occurrence(self):
        evidence = fuse_evidence(
            [_det("empty_text", "text_block", (100, 85, 260, 115))],
            [],
            image_width=500,
            image_height=500,
        )

        self.assertEqual(evidence["candidates"], [])

    def test_row_absorbs_ocr_noise_read_from_its_leading_icon(self):
        evidence = fuse_evidence(
            [
                _det("title", "text_block", (120, 85, 260, 115)),
                _det("icon", "icon", (40, 82, 90, 118)),
            ],
            [
                _ocr("title_ocr", "Battery", (122, 87, 258, 113)),
                _ocr("icon_noise", "100%", (50, 88, 82, 110)),
            ],
            image_width=500,
            image_height=500,
            max_ocr_distance_ratio=0.005,
        )

        rows = _menu_items(evidence)
        self.assertEqual([row["text"] for row in rows], ["Battery"])
        self.assertFalse(any(candidate["type"] == "icon" for candidate in evidence["candidates"]))
        self.assertIn("icon_noise", rows[0]["evidence"]["allIds"])

    def test_uniform_list_boundedly_continues_into_lower_viewport(self):
        evidence = _uniform_list_evidence(
            [200, 300, 400, 500, 600, 700],
            [
                ("continuation_one", "Sound", (120, 785, 270, 815)),
                ("continuation_two", "Display", (120, 885, 270, 915)),
                ("beyond_cap", "Wallpaper", (120, 985, 280, 1015)),
            ],
            height=1200,
        )

        texts = [row["text"] for row in _menu_items(evidence)]
        self.assertIn("Sound", texts)
        self.assertIn("Display", texts)
        self.assertNotIn("Wallpaper", texts)
        self.assertNotIn("Wallpaper", [candidate["text"] for candidate in evidence["candidates"]])
        sound = next(row for row in _menu_items(evidence) if row["text"] == "Sound")
        self.assertEqual(sound["evidence"]["typeInferred"], "uniform_list_lower_continuation")

    def test_uniform_list_absorbs_duplicate_box_for_confirmed_anchor(self):
        evidence = _uniform_list_evidence(
            [100, 200, 300, 400],
            [("duplicate", "Confirmed 3", (80, 280, 300, 330))],
        )

        self.assertEqual(
            [candidate["text"] for candidate in evidence["candidates"]].count("Confirmed 3"),
            1,
        )

    def test_uniform_list_recovers_complete_upper_continuation(self):
        evidence = _uniform_list_evidence(
            [300, 400, 500, 600, 700, 800],
            [
                ("prior_title", "Apps", (120, 185, 250, 215)),
                ("prior_description", "Recent apps", (120, 218, 300, 238)),
            ],
            height=1000,
        )

        apps = next(row for row in _menu_items(evidence) if row["text"] == "Apps")
        self.assertEqual(apps["evidence"]["typeInferred"], "uniform_list_upper_continuation")
        self.assertNotIn("Recent apps", [candidate["text"] for candidate in evidence["candidates"]])

    def test_uniform_list_groups_complete_lower_edge_title_and_description(self):
        evidence = _uniform_list_evidence(
            [200, 300, 400, 500, 600, 700],
            [
                ("edge_title", "Location", (120, 805, 260, 830)),
                ("edge_peer", "Location status", (120, 835, 310, 860)),
            ],
            height=1000,
        )

        location = next(row for row in _menu_items(evidence) if row["text"] == "Location")
        self.assertEqual(location["evidence"]["typeInferred"], "uniform_list_lower_continuation")
        self.assertNotIn("Location status", [candidate["text"] for candidate in evidence["candidates"]])

    def test_uniform_list_recovers_two_consecutive_bracketed_rows(self):
        evidence = _uniform_list_evidence(
            [100, 200, 500, 600],
            [
                ("missing_one", "First missing", (120, 285, 280, 315)),
                ("missing_two", "Second missing", (120, 385, 290, 415)),
            ],
        )

        texts = [row["text"] for row in _menu_items(evidence)]
        self.assertIn("First missing", texts)
        self.assertIn("Second missing", texts)

    def test_uniform_list_rejects_entire_multi_slot_bracket_when_one_slot_is_missing(self):
        evidence = _uniform_list_evidence(
            [100, 200, 500, 600],
            [("missing_one", "First missing", (120, 285, 280, 315))],
        )

        self.assertNotIn("First missing", [row["text"] for row in _menu_items(evidence)])

    def test_uniform_list_does_not_activate_for_irregular_spacing(self):
        evidence = _uniform_list_evidence(
            [100, 200, 350, 500],
            [("static_note", "Static information", (120, 265, 310, 295))],
        )

        self.assertNotIn("Static information", [row["text"] for row in _menu_items(evidence)])

    def test_uniform_list_rejects_ambiguous_midpoint_titles(self):
        evidence = _uniform_list_evidence(
            [100, 200, 400, 500],
            [
                ("candidate_a", "Candidate A", (120, 275, 260, 295)),
                ("candidate_b", "Candidate B", (122, 305, 265, 325)),
            ],
        )

        texts = [row["text"] for row in _menu_items(evidence)]
        self.assertNotIn("Candidate A", texts)
        self.assertNotIn("Candidate B", texts)

    def test_uniform_list_rejects_off_column_section_text(self):
        evidence = _uniform_list_evidence(
            [100, 200, 400, 500],
            [("section_header", "Privacy section", (25, 285, 260, 315))],
        )

        self.assertNotIn("Privacy section", [row["text"] for row in _menu_items(evidence)])

    def test_uniform_list_disables_frame_when_inference_ratio_is_too_high(self):
        evidence = _uniform_list_evidence(
            [100, 500, 600, 1000, 1100],
            [
                ("missing_200", "Missing 200", (120, 185, 260, 215)),
                ("missing_300", "Missing 300", (120, 285, 260, 315)),
                ("missing_400", "Missing 400", (120, 385, 260, 415)),
                ("missing_700", "Missing 700", (120, 685, 260, 715)),
                ("missing_800", "Missing 800", (120, 785, 260, 815)),
                ("missing_900", "Missing 900", (120, 885, 260, 915)),
            ],
            height=1200,
        )

        texts = [row["text"] for row in _menu_items(evidence)]
        self.assertNotIn("Missing 200", texts)
        self.assertNotIn("Missing 300", texts)
        self.assertNotIn("Missing 400", texts)
        self.assertNotIn("Missing 700", texts)
        self.assertNotIn("Missing 800", texts)
        self.assertNotIn("Missing 900", texts)

    def test_uniform_list_demotes_proven_clipped_top_row(self):
        evidence = _uniform_list_evidence(
            [50, 150, 250, 350, 450],
            [],
            height=600,
        )
        clipped = next(candidate for candidate in evidence["candidates"] if candidate["text"] == "Confirmed 1")
        # Simulate the production edge symptom: the top title detector/OCR is
        # materially shorter than the complete peer titles.
        clipped["boundsPx"][3] = clipped["boundsPx"][1] + 15
        clipped["centerPx"][1] = clipped["boundsPx"][1] + 7

        # Apply through a second fusion pass with the clipped geometry so the
        # production rule, rather than the test, owns classification.
        detections = [
            _det("edge_title", "text_block", (120, 42, 300, 57)),
            _det("edge_icon", "icon", (55, 32, 90, 68)),
        ]
        tokens = [_ocr("edge_ocr", "Clipped", (122, 43, 298, 56))]
        for index, center in enumerate([150, 250, 350, 450], start=2):
            detections.extend([
                _det(f"full_title_{index}", "text_block", (120, center - 15, 300, center + 15)),
                _det(f"full_icon_{index}", "icon", (55, center - 18, 90, center + 18)),
            ])
            tokens.append(_ocr(f"full_ocr_{index}", f"Full {index}", (122, center - 13, 298, center + 13)))
        result = fuse_evidence(detections, tokens, image_width=600, image_height=600)

        self.assertNotIn("Clipped", [row["text"] for row in _menu_items(result)])
        self.assertNotIn("Clipped", [candidate["text"] for candidate in result["candidates"]])

    def test_uniform_list_keeps_complete_top_row(self):
        evidence = _uniform_list_evidence([50, 150, 250, 350, 450], [], height=600)
        self.assertIn("Confirmed 1", [row["text"] for row in _menu_items(evidence)])


@unittest.skipUnless(_YOLO_MODEL.exists(), "YOLO model file not present")
class NavigationRowCompositionRealityTests(unittest.TestCase):
    def test_live_settings_root_emits_one_title_per_anchored_row(self):
        groundtruth = json.loads(
            (_FIXTURES / "settings-root-row-composition.groundtruth.json").read_text(encoding="utf-8")
        )
        perception_server._config = perception_server.load_config()
        image = Image.open(_FIXTURES / "settings-root-row-composition.png")
        evidence, _ = perception_server._run_pipeline(image, image.width, image.height)
        texts = [row["text"] for row in _menu_items(evidence)]

        for title in groundtruth["expectedAnchoredTitles"]:
            self.assertEqual(texts.count(title), 1, f"expected one fused row for {title!r}: {texts}")
        for description in groundtruth["forbiddenDescriptionCandidates"]:
            self.assertNotIn(description, texts)
        self.assertEqual(len(texts), len(set(texts)), f"duplicate menu_item texts remain: {texts}")


if __name__ == "__main__":
    unittest.main()
