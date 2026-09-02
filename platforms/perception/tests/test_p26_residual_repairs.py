"""P26-V2 run 6 residuals — cross-frame sticky label demotion (residual 1)
and demoted-candidate raw-twin cleanup (residual 2).

Real Display-page geometry (run 2 / run 6, settled frames):

    'Color'   x=[43,101]  y=[792,809]  h=17   ← small-font GROUP LABEL
    'Colors'  x=[44,144]  y=[861,885]  h=24   ← normal-font ROW
    gap 52px ≤ label_pair_gap_ratio(3.0) × 24 = 72;  17 < 0.75 × 24 = 18

Residual 1 (run 6 seq 24–25): per-frame detection-height jitter moves the
'Color' box across the 0.75 ratio (h=17 → h=20) so the label briefly
recomposes as a phantom ``menu_item``.  The sticky remedy rides the EXISTING
caller-supplied cross-frame channel: the C# row identity context exports each
known row's latest upstream type (``X-Known-Rows`` additive ``type`` field);
a composed menu_item whose text UNIQUELY matches a known ``NonInteractive``
row is re-demoted in place.  Fail-closed: ambiguous text (multiple ids / any
non-NonInteractive sighting) never demotes; no context → byte-identical
single-frame behavior.

Residual 2 (run 6 seq 27+): after the label-height rule demotes 'Color' to
its NonInteractive ``section_label`` satellite, the raw ``text_block``
candidate from initial construction still floated as a SECOND representation
of the same line.  The duplicate section-label dedup (row-band supporting
ownership fix #3) absorbs a text_block that coincides with an EXISTING
section_label satellite (same normalized text, same title column, vertical
overlap) — one line, one representation.  Deliberately not a general
geometric label-above-row attachment: the frozen S1 corpus case
``uniform_list_ambiguous_midpoint_rejected`` (two distinct short texts in one
cadence slot, which must stay unresolved) falsified that broader rule — only
the operator's role-decided satellite separates a duplicate representation
from a genuine unresolved element.
"""
from __future__ import annotations

import unittest

from uniclaw_perception.fusion.engine import fuse_evidence
from uniclaw_perception.schema import Box, Detection, OcrToken

_WIDTH, _HEIGHT = 720, 1400


def _det(identifier: str, bounds: tuple[float, float, float, float], confidence: float = 0.9) -> Detection:
    return Detection(identifier, "text_block", confidence, Box(*bounds))


def _ocr(identifier: str, text: str, bounds: tuple[float, float, float, float]) -> OcrToken:
    return OcrToken(identifier, text, 0.9, Box(*bounds))


def _display_page(label_height: float = 17.0) -> tuple[list[Detection], list[OcrToken]]:
    """The real run-2/6 Display-page shape.  ``label_height`` reproduces the
    run-6 seq 24–25 detection-height jitter: 17px (settled) composes the
    label-height demotion; 20px (jittered) moves 'Color' across the 0.75
    ratio so the operator composes it as a phantom menu row."""
    label_y2 = 792.0 + label_height
    return (
        [
            _det("d_label", (43.0, 792.0, 101.0, label_y2)),
            _det("d_row", (44.0, 861.0, 144.0, 885.0)),
            _det("d_row2", (44.0, 937.0, 190.0, 961.0)),
            _det("d_row3", (44.0, 1013.0, 210.0, 1037.0)),
        ],
        [
            _ocr("o_label", "Color", (45.0, 794.0, 99.0, 807.0)),
            _ocr("o_row", "Colors", (46.0, 863.0, 142.0, 883.0)),
            _ocr("o_row2", "Screen saver", (46.0, 939.0, 188.0, 959.0)),
            _ocr("o_row3", "Auto-rotate", (46.0, 1015.0, 208.0, 1035.0)),
        ],
    )


def _fuse(
    detections: list[Detection],
    ocr: list[OcrToken],
    stabilize_context: list[dict] | None = None,
) -> dict:
    return fuse_evidence(
        detections,
        ocr,
        image_width=_WIDTH,
        image_height=_HEIGHT,
        interactive_labels={"text_block"},
        promote_unmatched_ocr=True,
        stabilize=True,
        stabilize_context=stabilize_context,
    )


def _menu_texts(evidence: dict) -> list[str]:
    return [
        c["text"] for c in evidence["candidates"]
        if c.get("type") == "menu_item"
    ]


def _section_labels(evidence: dict) -> list[dict]:
    return [
        c for c in evidence["candidates"]
        if c.get("type") == "NonInteractive" and c.get("role") == "section_label"
    ]


_KNOWN_LABEL_ROW = [{"id": "row_001", "text": "Color", "type": "NonInteractive"}]


# ─────────────────────────────────────────────────────────────────────────────
# Residual 1 — cross-frame sticky label demotion
# ─────────────────────────────────────────────────────────────────────────────


class StickyLabelDemotionTests(unittest.TestCase):
    """Run-6 seq 24–25 flip-frame geometry (jittered 'Color' h=20 vs row h=24,
    ratio 0.83 ≥ 0.75 ⇒ the operator composes the phantom row) + caller
    context carrying the established NonInteractive classification."""

    def test_flip_frame_composes_without_context(self):
        # Control (documents the flip): no cross-frame context ⇒ the jittered
        # frame recomposes 'Color' as a phantom menu_item — unchanged
        # single-frame behavior.
        evidence = _fuse(*_display_page(label_height=20.0))
        self.assertIn("Color", _menu_texts(evidence))

    def test_known_noninteractive_context_re_demotes(self):
        evidence = _fuse(*_display_page(label_height=20.0), _KNOWN_LABEL_ROW)
        self.assertNotIn(
            "Color", _menu_texts(evidence),
            "a known NonInteractive label must not recompose as a phantom row",
        )
        labels = [c for c in _section_labels(evidence) if c.get("text") == "Color"]
        self.assertEqual(len(labels), 1)
        self.assertEqual(labels[0]["evidence"]["typeInferred"], "sticky_label_demotion")
        self.assertEqual(labels[0]["evidence"]["knownRowId"], "row_001")
        diagnostics = evidence.get("_diagnostics") or {}
        self.assertTrue(
            any(d.get("text") == "Color" for d in diagnostics.get("stickyLabelDemotion", [])),
            "the demotion is annotated in diagnostics (never silently applied)",
        )

    def test_context_without_type_is_noop(self):
        # Legacy context entries (id+text only) carry no demotion evidence.
        evidence = _fuse(
            *_display_page(label_height=20.0),
            [{"id": "row_001", "text": "Color"}],
        )
        self.assertIn("Color", _menu_texts(evidence))

    def test_interactive_type_context_is_noop(self):
        evidence = _fuse(
            *_display_page(label_height=20.0),
            [{"id": "row_001", "text": "Color", "type": "menu_item"}],
        )
        self.assertIn("Color", _menu_texts(evidence))

    def test_multiple_known_ids_same_text_fails_closed(self):
        # Same text known as two distinct rows (e.g. same text at different
        # positions) — ambiguous, never guess (mirror of the stabilizer's
        # unique-match discipline).
        evidence = _fuse(
            *_display_page(label_height=20.0),
            [
                {"id": "row_001", "text": "Color", "type": "NonInteractive"},
                {"id": "row_002", "text": "Color", "type": "NonInteractive"},
            ],
        )
        self.assertIn("Color", _menu_texts(evidence))

    def test_mixed_type_sightings_fail_closed(self):
        # The same known row id seen as NonInteractive once and as a
        # menu_item once — the latest classification is not exclusively
        # NonInteractive, so the text is not sticky evidence.
        evidence = _fuse(
            *_display_page(label_height=20.0),
            [
                {"id": "row_001", "text": "Color", "type": "NonInteractive"},
                {"id": "row_001", "text": "Color", "type": "menu_item"},
            ],
        )
        self.assertIn("Color", _menu_texts(evidence))

    def test_settled_frame_demotion_unchanged_with_context(self):
        # The settled frame (h=17) already demotes via the operator rule; the
        # sticky pass must not double-emit or alter that representation.
        evidence = _fuse(*_display_page(label_height=17.0), _KNOWN_LABEL_ROW)
        self.assertNotIn("Color", _menu_texts(evidence))
        labels = [c for c in _section_labels(evidence) if c.get("text") == "Color"]
        self.assertEqual(len(labels), 1)


# ─────────────────────────────────────────────────────────────────────────────
# Residual 2 — demoted-candidate raw-twin cleanup (duplicate dedup)
# ─────────────────────────────────────────────────────────────────────────────


class DemotedTwinCleanupTests(unittest.TestCase):
    """Run-6 seq 27+ shape: the operator demotes 'Color' (h=17) to its
    NonInteractive section_label satellite, but the raw text_block twin from
    initial construction floats as a second representation of the same
    line.  The symmetric label attachment must leave exactly ONE
    representation."""

    def test_demoted_label_has_single_representation(self):
        evidence = _fuse(*_display_page(label_height=17.0))
        # Exactly one 'Color' representation survives publication: the
        # NonInteractive section_label (the operator's demotion), never an
        # independent text_block.
        color_candidates = [
            c for c in evidence["candidates"] if c.get("text") == "Color"
        ]
        self.assertEqual(
            len(color_candidates), 1,
            f"one row, one representation; got {[(c.get('type'), c.get('role')) for c in color_candidates]}",
        )
        self.assertEqual(color_candidates[0].get("type"), "NonInteractive")
        self.assertEqual(color_candidates[0].get("role"), "section_label")
        # The twin's absorption is annotated (never silently dropped).
        diagnostics = evidence.get("_diagnostics") or {}
        label_records = [
            s for s in diagnostics.get("rowBandSupporting", [])
            if s.get("role") == "duplicate_section_label_supporting"
            and s.get("text") == "Color"
        ]
        self.assertEqual(len(label_records), 1)
        # The twin's absorption is anchored to the role-decided satellite
        # (the surviving representation of the same physical line).
        self.assertEqual(label_records[0].get("parentText"), "Color")

    def test_rows_below_the_label_still_compose(self):
        evidence = _fuse(*_display_page(label_height=17.0))
        self.assertEqual(
            _menu_texts(evidence), ["Colors", "Screen saver", "Auto-rotate"],
        )


if __name__ == "__main__":
    unittest.main()
