"""Stateless row stabilizer tests (WI-CTX / CS-PERCEPTION-ROW-IDENTITY).

Acceptance (leader-locked, DESIGN-SPEC-row-identity-stabilization D3-D5):
  (a) no ``known_rows`` context -> every candidate is new: ``row_id=None`` and
      ``text`` unchanged (single-frame / equivalence-gate compatible)
  (b) with context, a space-only difference maps back to the known ``row_id``
  (c) with context, a garbled row in the candidate band is recovered via
      neighbor-context anchoring
  (d) with context, a genuinely new row gets ``row_id=None``
  (e) ambiguity (two near-equal known rows) -> ``row_id=None`` (never guess)
  (f) engine integration: ``stabilize=True`` tags candidates with ``row_id``
      from ``stabilize_context``; ``stabilize=True`` with no context tags all
      ``None``; the default ``stabilize=False`` leaves candidates untouched
      (no ``row_id`` field) so the S1 equivalence baseline stays byte-identical

The stabilizer is stateless: every test is independent (no shared cache to
reset) and deterministic.
"""
from __future__ import annotations

import copy
import unittest

from uniclaw_perception.fusion.engine import fuse_evidence
from uniclaw_perception.fusion.row_stabilizer import stabilize_with_context
from uniclaw_perception.schema import Box, Detection, OcrToken

_W, _H = 1080, 2400


def _cand(identifier: str, text: str, y: float) -> dict:
    """A minimal candidate with the geometry the stabilizer keys on."""
    return {
        "id": identifier,
        "type": "menu_item",
        "text": text,
        "boundsPx": [100, y, 400, y + 30],
        "centerPx": [250, y + 15],
        "bounds": {"x1": 0.1, "y1": y / _H, "x2": 0.4, "y2": (y + 30) / _H},
        "center": [0.25, (y + 15) / _H],
    }


def _known(row_id: str, text: str) -> dict:
    return {"id": row_id, "text": text}


def _det(identifier: str, label: str, confidence: float,
         box: tuple[float, float, float, float]) -> Detection:
    return Detection(identifier, label, confidence, Box(*box))


def _ocr(identifier: str, text: str, confidence: float,
         box: tuple[float, float, float, float]) -> OcrToken:
    return OcrToken(identifier, text, confidence, Box(*box))


class StabilizeWithContextUnitTests(unittest.TestCase):
    """Direct stabilizer acceptance (a)-(e)."""

    # (a) No context -> every row is new: row_id=None, text untouched.
    def test_no_context_all_new(self):
        frame = [
            _cand("c1", "Network & internet", 200.0),
            _cand("c2", "Apps", 300.0),
            _cand("c3", "Battery", 400.0),
        ]
        out = stabilize_with_context(frame, None)
        self.assertEqual([c["row_id"] for c in out], [None, None, None])
        self.assertEqual(
            [c["text"] for c in out],
            ["Network & internet", "Apps", "Battery"],
            "no context must leave every row's text unchanged",
        )
        # Falsy context (empty list) behaves the same as None.
        frame2 = [_cand("c1", "Apps", 200.0)]
        out2 = stabilize_with_context(frame2, [])
        self.assertEqual(out2[0]["row_id"], None)

    # (b) Space-only difference maps back to the known row_id (exact normalized).
    def test_context_space_difference_maps_to_known_id(self):
        known = [_known("row_001", "Network & internet")]
        cand = _cand("c2", "Network&internet", 205.0)
        out = stabilize_with_context([cand], known)
        self.assertEqual(
            out[0]["row_id"], "row_001",
            "'Network&internet' must map to the known 'Network & internet' row_id",
        )

    # (c) Garbled middle row in the candidate band, recovered via a matching
    # above neighbor (context anchoring).
    def test_context_anchoring_garbled_with_matching_neighbor(self):
        known = [
            _known("row_above", "Above Label"),
            _known("row_mid", "Network & internet"),
            _known("row_below", "Below Label"),
        ]
        frame = [
            _cand("a2", "Above Label", 105.0),
            _cand("m2", "Network & internt", 205.0),  # ~0.857 jaccard, needs context
            _cand("b2", "Below Label", 305.0),
        ]
        out = stabilize_with_context(frame, known)
        self.assertEqual(out[0]["row_id"], "row_above")
        self.assertEqual(
            out[1]["row_id"], "row_mid",
            "garbled middle row with a matching neighbor must map to the known "
            "row_id via context anchoring",
        )
        self.assertEqual(out[2]["row_id"], "row_below")

    # (c-cont) Candidate band but no recognized neighbor -> context cannot
    # confirm -> new row (row_id=None).
    def test_candidate_band_without_context_confirm_is_new(self):
        known = [_known("row_001", "Network & internet")]
        # Garbled, ~0.857 jaccard (candidate band), but no neighbors at all.
        cand = _cand("c1", "Network & internt", 200.0)
        out = stabilize_with_context([cand], known)
        self.assertIsNone(
            out[0]["row_id"],
            "candidate-band row with no recognized neighbor must be new",
        )

    # (d) Genuinely different row -> new (row_id=None).
    def test_context_distinct_row_is_new(self):
        known = [_known("row_001", "Apps")]
        out = stabilize_with_context([_cand("c2", "Storage", 205.0)], known)
        self.assertIsNone(
            out[0]["row_id"],
            "'Apps' and 'Storage' are distinct rows; the new frame must be new",
        )

    # (e) Ambiguity: two known rows near-equally similar, no disambiguating
    # neighbor -> row_id=None (let C# decide; never guess).
    def test_ambiguity_returns_none(self):
        known = [
            _known("row_001", "Confirmed 1"),
            _known("row_002", "Confirmed 2"),
        ]
        # 'Confirmed 3' is ~0.78 jaccard to both; no neighbor context.
        out = stabilize_with_context([_cand("c3", "Confirmed 3", 200.0)], known)
        self.assertIsNone(out[0]["row_id"])

    # Direct-confirm band (>= 0.90) maps without needing neighbor context.
    def test_direct_confirm_band_maps_without_context(self):
        known = [_known("row_001", "Network & internet")]
        # 'Network & interner' (extra trailing 'r') is ~0.93 jaccard — above
        # the direct bar, below exact — so it confirms without a neighbor.
        cand = _cand("c1", "Network & interner", 200.0)
        out = stabilize_with_context([cand], known)
        self.assertEqual(out[0]["row_id"], "row_001")

    # Determinism: two independent stabilizations of identical inputs produce
    # identical row_id assignments (no hidden state or nondeterminism).
    def test_deterministic_replay(self):
        known = [_known("row_001", "Network & internet")]
        frame = [_cand("c2", "Network&internet", 205.0)]

        def run():
            return stabilize_with_context(copy.deepcopy(frame), known)[0]["row_id"]

        self.assertEqual(run(), run())

    # Empty input is a pass-through (no error).
    def test_empty_input_passthrough(self):
        self.assertEqual(stabilize_with_context([], None), [])
        self.assertEqual(stabilize_with_context([], [_known("row_001", "Apps")]), [])

    # Stateless: the same call repeated yields the same result and never
    # accumulates state across calls.
    def test_stateless_across_calls(self):
        known = [_known("row_001", "Network & internet")]
        first = stabilize_with_context(
            [_cand("c1", "Network&internet", 200.0)], known)
        second = stabilize_with_context(
            [_cand("c2", "Network&internet", 200.0)], known)
        self.assertEqual(first[0]["row_id"], "row_001")
        self.assertEqual(second[0]["row_id"], "row_001")


class StabilizeWithContextEngineIntegrationTests(unittest.TestCase):
    """(f) Engine wiring: opt-in ``stabilize`` + ``stabilize_context``."""

    # Default (stabilize=False) must not tag candidates at all: no ``row_id``
    # field, text untouched (preserves the S1 equivalence baseline).
    def test_engine_default_leaves_candidates_untouched(self):
        d = _det("d1", "input", 0.9, (60.0, 510.0, 560.0, 550.0))
        o = _ocr("o1", "Network&internet", 0.85, (100.0, 518.0, 540.0, 542.0))
        evidence = fuse_evidence([d], [o], image_width=_W, image_height=_H)
        cand = evidence["candidates"][0]
        self.assertNotIn(
            "row_id", cand,
            "default (stabilize=False) must not add a row_id field",
        )
        self.assertEqual(cand["text"], "Network&internet")

    # stabilize=True with no context -> every candidate row_id=None.
    def test_engine_stabilize_no_context_all_none(self):
        d = _det("d1", "input", 0.9, (60.0, 510.0, 560.0, 550.0))
        o = _ocr("o1", "Network&internet", 0.85, (100.0, 518.0, 540.0, 542.0))
        evidence = fuse_evidence(
            [d], [o], image_width=_W, image_height=_H, stabilize=True)
        self.assertIsNone(evidence["candidates"][0]["row_id"])

    # stabilize=True with context -> matched candidate carries the known row_id.
    def test_engine_stabilize_with_context_maps_row_id(self):
        known = [_known("row_001", "Network & internet")]
        d = _det("d1", "input", 0.9, (60.0, 510.0, 560.0, 550.0))
        o = _ocr("o1", "Network&internet", 0.85, (100.0, 518.0, 540.0, 542.0))
        evidence = fuse_evidence(
            [d], [o], image_width=_W, image_height=_H,
            stabilize=True, stabilize_context=known)
        self.assertEqual(
            evidence["candidates"][0]["row_id"], "row_001",
            "engine stabilize=True with context must tag the matched row_id",
        )


if __name__ == "__main__":
    unittest.main()
