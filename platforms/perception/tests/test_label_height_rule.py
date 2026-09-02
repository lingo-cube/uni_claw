"""Label-height rule (governed) — RED→GREEN for the section-label demotion.

Root signal (font size, pure geometry): box HEIGHT tracks font size, not
text length.  A composed head significantly SHORTER than a vertically
adjacent in-column head is a section label / subtitle line, not a row.

Real-world shape (P26-V2 run 2, Display child page, settled frame seq 19):

    'Color'  x=[43,101]  y=[792,809]  h=17   ← small-font GROUP LABEL
    'Colors' x=[44,144]  y=[861,885]  h=24   ← normal-font ROW
    gap 52px ≤ label_pair_gap_ratio(3.0) × 24 = 72;  17 < 0.75 × 24 = 18

Governance: the rule is registered as operator parameters
(``label_height_ratio`` / ``label_pair_gap_ratio``) in the row-relation-head
contract, so it resolves through the rule framework (resolvedParams +
ruleSetHash in traces) instead of living as a hidden constant.

Fail-closed guards locked by the control cases: equal-height rows never
demote each other; a lone short head (no pair) stays composed; a short line
far beyond the pair-gap bound stays composed.
"""
from __future__ import annotations

import unittest

from uniclaw_perception.operators.registry_defaults import REGISTRY
from uniclaw_perception.operators.relation_head_router import (
    run_row_relation_head_routed,
)
from uniclaw_perception.operators.row_relation_head import (
    ROW_RELATION_HEAD_PARAM_DEFAULTS,
)
from uniclaw_perception.operators.trace import build_raw_sources
from uniclaw_perception.schema import Box, Detection, OcrToken

_WIDTH, _HEIGHT = 720, 1400


def _norm(x1: float, y1: float, x2: float, y2: float) -> Box:
    return Box(x1 * _WIDTH, y1 * _HEIGHT, x2 * _WIDTH, y2 * _HEIGHT)


def _run(detections, ocr):
    holder: list[dict] = []
    bundle = build_raw_sources(detections, ocr, _WIDTH, _HEIGHT)
    decision = run_row_relation_head_routed(holder, detections, {}, bundle)
    return decision, holder


def _label_page() -> tuple[list[Detection], list[OcrToken]]:
    """The real run-2 Display-page shape: 'Color' label above 'Colors' row,
    plus two more normal-font rows so the page establishes its row-height
    modality (the rule requires >= 3 composed heads)."""
    return (
        [
            Detection("d_label", "text_block", 0.92, _norm(43 / _WIDTH, 792 / 1400, 101 / _WIDTH, 809 / 1400)),
            Detection("d_row", "text_block", 0.89, _norm(44 / _WIDTH, 861 / 1400, 144 / _WIDTH, 885 / 1400)),
            Detection("d_row2", "text_block", 0.9, _norm(44 / _WIDTH, 937 / 1400, 190 / _WIDTH, 961 / 1400)),
            Detection("d_row3", "text_block", 0.9, _norm(44 / _WIDTH, 1013 / 1400, 210 / _WIDTH, 1037 / 1400)),
        ],
        [
            OcrToken("o_label", "Color", 0.9, _norm(45 / _WIDTH, 794 / 1400, 99 / _WIDTH, 807 / 1400)),
            OcrToken("o_row", "Colors", 0.9, _norm(46 / _WIDTH, 863 / 1400, 142 / _WIDTH, 883 / 1400)),
            OcrToken("o_row2", "Screen saver", 0.9, _norm(46 / _WIDTH, 939 / 1400, 188 / _WIDTH, 959 / 1400)),
            OcrToken("o_row3", "Auto-rotate", 0.9, _norm(46 / _WIDTH, 1015 / 1400, 208 / _WIDTH, 1035 / 1400)),
        ],
    )


class LabelHeightRuleTests(unittest.TestCase):
    def test_section_label_above_row_is_demoted(self):
        decision, holder = _run(*_label_page())
        menus = [c for c in holder if c.get("type") == "menu_item"]
        texts = [c.get("text") for c in menus]
        self.assertEqual(
            texts, ["Colors", "Screen saver", "Auto-rotate"],
            f"only the real rows compose (label demoted); got {texts}",
        )
        # The router merges each band's satellites next to the band row.
        labels = [
            c for c in holder
            if c.get("type") == "NonInteractive" and c.get("role") == "section_label"
        ]
        self.assertEqual(
            len(labels), 1,
            f"the label is demoted to exactly one section_label satellite; "
            f"holder: {[(c.get('type'), c.get('text')) for c in holder]}",
        )
        self.assertEqual(labels[0].get("text"), "Color")
        self.assertEqual(
            labels[0]["evidence"].get("typeInferred"), "label_height_rule",
        )
        self.assertEqual(
            labels[0]["evidence"].get("attachedTo"), menus[0]["id"],
        )

    def test_label_never_silently_dropped(self):
        decision, holder = _run(*_label_page())
        texts = [c.get("text") for c in holder]
        self.assertIn("Color", texts, "the label text stays visible evidence")

    def test_equal_height_rows_do_not_demote_each_other(self):
        detections = [
            Detection("e1", "text_block", 0.9, _norm(44 / _WIDTH, 792 / 1400, 144 / _WIDTH, 816 / 1400)),
            Detection("e2", "text_block", 0.9, _norm(44 / _WIDTH, 868 / 1400, 160 / _WIDTH, 892 / 1400)),
            Detection("e3", "text_block", 0.9, _norm(44 / _WIDTH, 944 / 1400, 170 / _WIDTH, 968 / 1400)),
        ]
        ocr = [
            OcrToken("eo1", "Battery", 0.9, _norm(46 / _WIDTH, 794 / 1400, 142 / _WIDTH, 814 / 1400)),
            OcrToken("eo2", "Storage", 0.9, _norm(46 / _WIDTH, 870 / 1400, 158 / _WIDTH, 890 / 1400)),
            OcrToken("eo3", "Display", 0.9, _norm(46 / _WIDTH, 946 / 1400, 168 / _WIDTH, 966 / 1400)),
        ]
        decision, holder = _run(detections, ocr)
        texts = [c.get("text") for c in holder if c.get("type") == "menu_item"]
        self.assertEqual(
            sorted(texts), ["Battery", "Display", "Storage"],
            f"equal-height rows all compose; got {texts}",
        )

    def test_single_short_head_stays_composed(self):
        detections = [
            Detection("s1", "text_block", 0.9, _norm(43 / _WIDTH, 792 / 1400, 101 / _WIDTH, 809 / 1400)),
        ]
        ocr = [
            OcrToken("so1", "Color", 0.9, _norm(45 / _WIDTH, 794 / 1400, 99 / _WIDTH, 807 / 1400)),
        ]
        decision, holder = _run(detections, ocr)
        texts = [c.get("text") for c in holder if c.get("type") == "menu_item"]
        self.assertEqual(
            texts, ["Color"],
            "a lone short head has no pair evidence — stays composed (never guess)",
        )

    def test_distant_short_text_stays_composed(self):
        # Short text far beyond the pair-gap bound next to two normal rows:
        # no pair ⇒ no demotion even though the page modality exists.
        detections = [
            Detection("f1", "text_block", 0.9, _norm(43 / _WIDTH, 792 / 1400, 101 / _WIDTH, 809 / 1400)),
            Detection("f2", "text_block", 0.9, _norm(44 / _WIDTH, 929 / 1400, 144 / _WIDTH, 953 / 1400)),
            Detection("f3", "text_block", 0.9, _norm(44 / _WIDTH, 1005 / 1400, 170 / _WIDTH, 1029 / 1400)),
        ]
        ocr = [
            OcrToken("fo1", "Color", 0.9, _norm(45 / _WIDTH, 794 / 1400, 99 / _WIDTH, 807 / 1400)),
            OcrToken("fo2", "Colors", 0.9, _norm(46 / _WIDTH, 931 / 1400, 142 / _WIDTH, 951 / 1400)),
            OcrToken("fo3", "Screen saver", 0.9, _norm(46 / _WIDTH, 1007 / 1400, 168 / _WIDTH, 1027 / 1400)),
        ]
        decision, holder = _run(detections, ocr)
        texts = [c.get("text") for c in holder if c.get("type") == "menu_item"]
        self.assertEqual(
            sorted(texts), ["Color", "Colors", "Screen saver"],
            f"beyond the pair-gap bound nothing demotes; got {texts}",
        )

    def test_subtitle_line_below_row_does_not_compose(self):
        # A short small-font line below a taller row, on a page with an
        # established row-height modality (three normal rows): the label-
        # height rule demotes it to a section_label satellite — it never
        # becomes a navigation row.
        detections = [
            Detection("t1", "text_block", 0.9, _norm(44 / _WIDTH, 861 / 1400, 240 / _WIDTH, 885 / 1400)),
            Detection("t2", "text_block", 0.9, _norm(44 / _WIDTH, 910 / 1400, 200 / _WIDTH, 927 / 1400)),
            Detection("t3", "text_block", 0.9, _norm(44 / _WIDTH, 961 / 1400, 190 / _WIDTH, 985 / 1400)),
        ]
        ocr = [
            OcrToken("to1", "Lock screen", 0.9, _norm(46 / _WIDTH, 863 / 1400, 238 / _WIDTH, 883 / 1400)),
            OcrToken("to2", "Show all content", 0.9, _norm(46 / _WIDTH, 912 / 1400, 198 / _WIDTH, 925 / 1400)),
            OcrToken("to3", "Screen timeout", 0.9, _norm(46 / _WIDTH, 963 / 1400, 188 / _WIDTH, 983 / 1400)),
        ]
        decision, holder = _run(detections, ocr)
        texts = [c.get("text") for c in holder if c.get("type") == "menu_item"]
        self.assertEqual(
            texts, ["Lock screen", "Screen timeout"],
            f"the short line below must never compose a phantom row; got {texts}",
        )
        labels = [
            c for c in holder
            if c.get("type") == "NonInteractive" and c.get("role") == "section_label"
        ]
        self.assertEqual(
            len(labels), 1,
            f"the short line demotes to one section_label satellite; "
            f"holder: {[(c.get('type'), c.get('text')) for c in holder]}",
        )
        self.assertEqual(labels[0].get("text"), "Show all content")


class RuleRegistrationTests(unittest.TestCase):
    def test_params_registered_in_contract_defaults(self):
        self.assertEqual(ROW_RELATION_HEAD_PARAM_DEFAULTS["label_height_ratio"], 0.75)
        self.assertEqual(ROW_RELATION_HEAD_PARAM_DEFAULTS["label_pair_gap_ratio"], 3.0)

    def test_registry_contract_carries_the_rule_params(self):
        contract = REGISTRY._latest.get("row-relation-head")
        self.assertIsNotNone(contract, "row-relation-head must be registered")
        names = set(contract.parameters or {})
        self.assertIn("label_height_ratio", names)
        self.assertIn("label_pair_gap_ratio", names)


if __name__ == "__main__":
    unittest.main()
