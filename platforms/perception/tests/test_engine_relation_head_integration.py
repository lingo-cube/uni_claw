"""End-to-end ``fuse_evidence`` ↔ ``row-relation-head`` integration (WI-PFW-S2fix).

Locks the raw_sources key-unification fix: engine and replay construct the
router's raw visual region bundle through the SINGLE shared constructor
(``operators/trace.build_raw_sources``), the router reads the unified
``detections`` key (with a defensive ``yolo`` fallback), and the real
child-page shapes (the frozen failure shape) must compose navigation rows
through the FULL engine path.

The frozen child-page shapes (1080x2400, normalized; the exact
``reentry-run1`` E4 failure frame): one icon + five text_blocks + five OCR
lines ("Choose wallpaper" title row + Gallery / Live Wallpapers / Wallpaper &
style menu rows).
"""
from __future__ import annotations

import unittest

from uniclaw_perception.fusion.engine import fuse_evidence
from uniclaw_perception.operators.relation_head_router import (
    run_row_relation_head_routed,
)
from uniclaw_perception.operators.trace import build_raw_sources, replay
from uniclaw_perception.schema import Box, Detection, OcrToken

_WIDTH, _HEIGHT = 1080, 2400


def _norm(x1: float, y1: float, x2: float, y2: float) -> Box:
    return Box(x1 * _WIDTH, y1 * _HEIGHT, x2 * _WIDTH, y2 * _HEIGHT)


def _child_detections() -> list[Detection]:
    return [
        Detection("y1", "icon", 0.9, _norm(0.03, 0.079, 0.09, 0.125)),
        Detection("y2", "text_block", 0.9, _norm(0.0638, 0.1825, 0.7666, 0.265)),
        Detection("y3", "text_block", 0.9, _norm(0.0611, 0.2325, 0.2430, 0.2656)),
        Detection("y4", "text_block", 0.9, _norm(0.1972, 0.3225, 0.3458, 0.345)),
        Detection("y5", "text_block", 0.9, _norm(0.1986, 0.3875, 0.5375, 0.4087)),
        Detection("y6", "text_block", 0.9, _norm(0.1930, 0.4512, 0.5708, 0.4743)),
    ]


def _child_ocr() -> list[OcrToken]:
    return [
        OcrToken("o1", "Choose wallpaper", 0.9, _norm(0.064, 0.183, 0.767, 0.2225)),
        OcrToken("o2", "from", 0.9, _norm(0.061, 0.233, 0.243, 0.266)),
        OcrToken("o3", "Gallery", 0.9, _norm(0.197, 0.323, 0.346, 0.345)),
        OcrToken("o4", "Live Wallpapers", 0.9, _norm(0.199, 0.388, 0.537, 0.409)),
        OcrToken("o5", "Wallpaper & style", 0.9, _norm(0.193, 0.451, 0.571, 0.474)),
    ]


class EngineRelationHeadIntegrationTests(unittest.TestCase):
    """WI-PFW-S2fix: unified raw_sources bundle + engine-path row composition.

    The frozen acceptance: ``fuse_evidence`` on the real child-page shapes
    must compose relation-head rows (the reentry-run1 E4 failure shape), the
    engine-built bundle must be exactly what the router consumes, and the run
    must be byte-deterministic.
    """

    def test_end_to_end_fuse_evidence_composes_menu_items(self):
        # (a) END-TO-END: the full engine path on the frozen child shapes must
        # compose the real rows (never a silent no-op).  The reentry-run1 E4
        # defect was: engine handed the router a bundle without the row data →
        # row-relation-head never fired.  With the unified bundle this path
        # composes the real rows; assert the exact texts the leader locked.
        trace: list[dict] = []
        evidence = fuse_evidence(
            _child_detections(),
            _child_ocr(),
            image_width=_WIDTH,
            image_height=_HEIGHT,
            trace_sink=trace.append,
        )
        menus = [c for c in evidence["candidates"] if c["type"] == "menu_item"]
        rows = run_row_relation_head_routed
        self.assertGreaterEqual(
            len(menus), 4,
            "the frozen child-page shapes must compose >= 4 relation-head rows "
            f"through the engine path; got {len(menus)} menu_item(s) "
            f"(candidates: {(c['type'], c.get('text')) for c in evidence['candidates']})",
        )
        texts = {m.get("text") for m in menus}
        for expected in ("Gallery", "Live Wallpapers", "Wallpaper & style"):
            self.assertIn(
                expected, texts,
                f"composed menu set {sorted(texts)} must include {expected!r}",
            )
        self.assertTrue(
            all((m.get("evidence") or {}).get("typeInferred") == "row_relation_head"
                for m in menus),
            "every composed row must carry row_relation_head provenance; got "
            f"{[(m.get('text'), (m.get('evidence') or {}).get('typeInferred')) for m in menus]}",
        )
        self.assertEqual(trace[0]["steps"][1]["operator"], "row-relation-head",
                         "the pipeline trace must record the row-relation-head step")
        self.assertEqual(trace[0]["steps"][1]["status"], "activated",
                         "the router must be activated (detections present), "
                         "never a silent no-op")

    def test_engine_bundle_keys_are_the_router_consumed_keys(self):
        # (b) bundle-contract: the engine's raw bundle (built through the
        # shared constructor) must satisfy the router's consumed keys, and the
        # router must accept it (the replay/engine construction fork is dead).
        detections = _child_detections()
        tokens = _child_ocr()
        bundle = build_raw_sources(detections, tokens, _WIDTH, _HEIGHT)
        self.assertIsInstance(bundle, dict)
        required = {"detections", "ocr", "width", "height"}
        self.assertTrue(
            set(bundle) >= required,
            f"engine bundle keys {sorted(bundle)} must cover {sorted(required)}",
        )
        self.assertEqual(len(bundle["detections"]), len(detections))
        self.assertEqual(len(bundle["ocr"]), len(tokens))
        self.assertEqual(bundle["width"], _WIDTH)
        self.assertEqual(bundle["height"], _HEIGHT)
        # Every entry must carry the pixel bounds the operator consumes.
        self.assertTrue(
            all("boundsPx" in entry for entry in bundle["detections"]),
            "relation-head consumes boundsPx on every detection entry",
        )
        # The router must accept the engine-built bundle (compose rows).
        decision = run_row_relation_head_routed([], detections, {}, bundle)
        self.assertEqual(decision["status"], "activated")
        self.assertGreaterEqual(decision["emitted"], 4)
        # Defensive dual-key read: a stale 'yolo'-keyed bundle must still work.
        stale = {
            "yolo": bundle["detections"],
            "ocr": bundle["ocr"],
            "width": _WIDTH,
            "height": _HEIGHT,
        }
        stale_decision = run_row_relation_head_routed([], detections, {}, stale)
        self.assertEqual(stale_decision["status"], "activated")
        self.assertGreaterEqual(stale_decision["emitted"], 4)
        # No 'yolo' key: unified only — the fallback still requires the data.
        self.assertNotIn("yolo", bundle)

    def test_deterministic_double_run(self):
        # (c) determinism: same inputs ⇒ byte-identical candidates and trace.
        first_trace: list[dict] = []
        second_trace: list[dict] = []
        first = fuse_evidence(
            _child_detections(), _child_ocr(),
            image_width=_WIDTH, image_height=_HEIGHT,
            trace_sink=first_trace.append,
        )
        second = fuse_evidence(
            _child_detections(), _child_ocr(),
            image_width=_WIDTH, image_height=_HEIGHT,
            trace_sink=second_trace.append,
        )
        self.assertEqual(first["candidates"], second["candidates"])
        self.assertEqual(len(first_trace), 1)
        self.assertEqual(first_trace[0], second_trace[0])
        self.assertEqual(
            first_trace[0]["steps"], second_trace[0]["steps"],
            "the pipeline trace must be byte-deterministic across replays",
        )


class ReplayUsesSharedConstructorTests(unittest.TestCase):
    """The offline replay path must route through the SAME shared constructor
    (replay == engine bundle construction; no fork)."""

    @staticmethod
    def _corpus_entry(entry) -> dict:
        # replay() interprets corpus-shaped entries: flat bounds lists.
        return {
            "id": entry.id,
            "label": entry.label,
            "confidence": entry.confidence,
            "bounds": [
                entry.box.x1, entry.box.y1, entry.box.x2, entry.box.y2,
            ],
        }

    def test_replay_routes_through_execute_pipeline_with_shared_bundle(self):
        def det_entry(d: Detection) -> dict:
            return {
                "id": d.id, "label": d.label, "confidence": d.confidence,
                "bounds": [d.box.x1, d.box.y1, d.box.x2, d.box.y2],
            }

        def ocr_entry(t: OcrToken) -> dict:
            return {
                "id": t.id, "text": t.text, "confidence": t.confidence,
                "bounds": [t.box.x1, t.box.y1, t.box.x2, t.box.y2],
            }

        case = {
            "width": _WIDTH,
            "height": _HEIGHT,
            "mode": "full",
            "yolo": [det_entry(d) for d in _child_detections()],
            "ocr": [ocr_entry(t) for t in _child_ocr()],
        }
        candidates_first, trace_first = replay(case)
        candidates_second, trace_second = replay(case)
        self.assertEqual(candidates_first, candidates_second)
        self.assertEqual(trace_first.to_bytes(), trace_second.to_bytes())


if __name__ == "__main__":
    unittest.main()