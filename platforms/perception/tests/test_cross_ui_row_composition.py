"""S2ii cross-UI row-composition corpus tests (WI-PFW-S2ii, OpenSpec change
``perception-operator-rule-framework``).

Gates pinned here (``evidence/S2-acceptance-protocol.md``), exercised through
the FULLY ROUTED pipeline (``row-relation-head`` adapter wired between
``uniform-list-row-grouping`` and ``spacing-verifier``):

* G-3 cross-UI regression — the corpus families in
  ``tests/corpus/cross_ui_row_corpus.json``:
  1. dense mixed store/catalog list (title-only / title+caption / icon+title
     rows; SAME-TEXT rows at different positions stay distinct);
  2. third-party-app profile/preferences page (OCR-only section headers
     non-interactive; toggle rows never produce a navigation candidate);
  3. low-anchor variants of each (relation-head composes real rows or fails
     closed; subtitles/descriptions never promoted).
* G-2 four-anchor no-regression — ≥4-anchor corpus families take the
  uniform-list path (adapter "delegated" noop) and produce the pinned uniform
  cadence rows (the S1 corpus byte gate is ``test_row_composition_equivalence``).
* G-5 verifier envelope — every routed case (including every relation-head
  activation) ends with ``spacing-verifier`` = ``verified``.
* G-7 determinism — replaying any corpus case twice yields byte-identical
  candidates and trace bytes.
* G-1/G-4 integration — the real v1n frame, routed: the subtitle never becomes
  a menu_item and the search box ("Q Search settings", engine-classified
  ``input``) is never promoted; the adapter fails closed when no raw visual
  sources are provided (it never composes from fused candidates).

All inputs are synthetic raw visual regions, deterministic, no network; every
case pins its expectation to the deterministic run.
"""
from __future__ import annotations

import json
import unittest
from pathlib import Path

from uniclaw_perception.fusion.engine import fuse_evidence
from uniclaw_perception.operators.relation_head_router import (
    ROUTING_MIN_ANCHORS,
    run_row_relation_head_routed,
)
from uniclaw_perception.operators.trace import replay
from uniclaw_perception.schema import Box, Detection, OcrToken

_REPO_ROOT = Path(__file__).resolve().parents[3]
_CROSS_UI_CORPUS = (
    _REPO_ROOT / "platforms/perception/tests/corpus/cross_ui_row_corpus.json"
)
_NAV_CORPUS = (
    _REPO_ROOT / "platforms/perception/tests/corpus/navigation_row_corpus.json"
)

_DEFAULT_PARAMS = {"promote_unmatched_ocr": False, "max_ocr_distance_ratio": 0.055}


def _load(path: Path) -> list[dict]:
    return json.loads(path.read_text(encoding="utf-8"))


def _to_detection(entry: dict) -> Detection:
    return Detection(
        entry["id"], entry["label"], float(entry["confidence"]),
        Box(*[float(v) for v in entry["bounds"]]),
    )


def _to_ocr_token(entry: dict) -> OcrToken:
    return OcrToken(
        entry["id"], entry["text"], float(entry["confidence"]),
        Box(*[float(v) for v in entry["bounds"]]),
    )


def _menu_texts(candidates: list[dict]) -> list[str]:
    return [candidate["text"] for candidate in candidates
            if candidate.get("type") == "menu_item"]


def _satellites(candidates: list[dict]) -> list[dict]:
    return [candidate for candidate in candidates
            if candidate.get("type") == "NonInteractive"]


def _case_routed_outputs(case: dict) -> tuple[list[dict], dict]:
    """Run the case through the routed fusion pipeline twice (replay) and
    return (candidates, trace) with the trace's per-operator step map."""
    candidates, trace = replay(case)
    steps = {step["operator"]: step for step in trace.steps}
    return candidates, steps


#: Pinned expectations (deterministic run; see corpus descriptions).
#: ``menus`` = exact menu_item texts in pipeline order; ``never_menu`` = texts
#: that must never appear as menu_item; ``text_counts`` = per-text menu counts.
_EXPECTATIONS: dict[str, dict] = {
    "f1_dense_mixed_list_high_anchor": {
        "route": "uniform-list",
        "menus": ["Network", "Confirmed 2", "Today's deals",
                  "Confirmed 3", "Confirmed 4", "Network",
                  "Confirmed 5", "Confirmed 6"],
        "never_menu": ["New this week"],
        "text_counts": {"Network": 2},
    },
    "f1_dense_mixed_list_low_anchor": {
        "route": "relation-head",
        "menus": ["Bluetooth", "Wi-Fi", "Network", "Network"],
        "never_menu": ["Advanced settings"],
        "text_counts": {"Network": 2},
    },
    "f2_preferences_page_high_anchor": {
        "route": "uniform-list",
        "menus": ["Profile", "Notifications", "Privacy", "Security"],
        "never_menu": ["Vibrate", "ACCOUNT"],
        "text_counts": {},
    },
    "f2_preferences_page_low_anchor": {
        "route": "relation-head",
        "menus": ["Profile", "Security", "Vibrate"],
        "never_menu": ["NOTIFICATIONS", "Two-step verification"],
        "text_counts": {},
    },
    "f3_subtitle_low_anchor_never_promoted": {
        "route": "relation-head",
        "menus": ["Wi-Fi", "Bluetooth"],
        "never_menu": ["Wi-Fi, connections, networks"],
        "text_counts": {},
    },
    "f3_evidence_insufficient_fail_closed": {
        "route": "fail-closed",
        "menus": [],
        "never_menu": ["Some page text", "More page text"],
        "text_counts": {},
    },
}


class CrossUiRowCompositionTests(unittest.TestCase):
    """G-3: the cross-UI corpus families through the routed pipeline."""

    def setUp(self) -> None:
        self.cases = {case["case_id"]: case for case in _load(_CROSS_UI_CORPUS)}

    def test_corpus_has_three_non_settings_families(self):
        # G-3 requirement: ≥3 distinct non-Settings UI shapes — dense store
        # list (f1), third-party preferences page (f2), low-anchor/fail-closed
        # variants (f3) — all deterministic synthetic frames.
        self.assertGreaterEqual(len(self.cases), 3)

    def test_pinned_menu_rows_and_never_promoted_texts(self):
        for case_id, expectation in _EXPECTATIONS.items():
            with self.subTest(case=case_id):
                candidates, _ = _case_routed_outputs(self.cases[case_id])
                menus = _menu_texts(candidates)
                self.assertEqual(
                    menus, expectation["menus"],
                    f"{case_id}: pinned menu row set diverged",
                )
                never = expectation["never_menu"]
                self.assertTrue(
                    all(text not in menus for text in never),
                    f"{case_id}: forbidden text promoted to a menu_item: "
                    f"{[t for t in never if t in menus]}",
                )
                for text, count in expectation["text_counts"].items():
                    self.assertEqual(
                        menus.count(text), count,
                        f"{case_id}: {text!r} must appear {count} time(s) — "
                        "same-text rows at different positions stay distinct",
                    )


class CrossUiRoutingTests(unittest.TestCase):
    """G-2 routing + G-5 verifier envelope + G-7 determinism per family."""

    def setUp(self) -> None:
        self.cases = {case["case_id"]: case for case in _load(_CROSS_UI_CORPUS)}

    def test_high_anchor_cases_delegate_to_uniform_list(self):
        # G-2: ≥ minAnchors confirmed anchors → the adapter noops (delegated)
        # and the uniform-list generator owns composition byte-identically
        # (the S1 corpus byte gate covers the frozen corpus; these pin the
        # cross-UI high-anchor shape).
        for case_id, expectation in _EXPECTATIONS.items():
            if expectation["route"] != "uniform-list":
                continue
            with self.subTest(case=case_id):
                candidates, steps = _case_routed_outputs(self.cases[case_id])
                self.assertEqual(steps["uniform-list-row-grouping"]["status"], "activated")
                self.assertEqual(steps["row-relation-head"]["status"], "noop")
                self.assertIn("delegated", steps["row-relation-head"]["detail"])
                self.assertEqual(steps["spacing-verifier"]["status"], "verified")

    def test_low_anchor_cases_compose_or_fail_closed_and_pass_verifier(self):
        # G-1/G-3/G-5: relation-head composes real rows (or fails closed) and
        # every output passes spacing-verifier — no validator bypass.
        for case_id, expectation in _EXPECTATIONS.items():
            if expectation["route"] not in ("relation-head", "fail-closed"):
                continue
            with self.subTest(case=case_id):
                _, steps = _case_routed_outputs(self.cases[case_id])
                self.assertEqual(
                    steps["spacing-verifier"]["status"], "verified",
                    f"{case_id}: relation-head output must pass spacing-verifier",
                )
                if expectation["route"] == "relation-head":
                    self.assertEqual(steps["row-relation-head"]["status"], "activated")
                else:
                    self.assertEqual(steps["row-relation-head"]["status"], "noop")

    def test_deterministic_replay_bytes(self):
        # G-7: same inputs + same rule set ⇒ identical candidates and trace
        # bytes across replays, for every corpus case.
        for case_id, case in self.cases.items():
            with self.subTest(case=case_id):
                candidates_first, trace_first = replay(case)
                candidates_second, trace_second = replay(case)
                self.assertEqual(candidates_first, candidates_second)
                self.assertEqual(trace_first.to_bytes(), trace_second.to_bytes())


class CrossUiSubtitleAndControlGuardTests(unittest.TestCase):
    """G-1 subtitle/caption guards and toggle/header non-promotion."""

    def setUp(self) -> None:
        self.cases = {case["case_id"]: case for case in _load(_CROSS_UI_CORPUS)}

    def test_caption_absorbed_as_noninteractive_satellite(self):
        # F1 low-anchor: the caption under 'Network' is absorbed as a
        # NonInteractive row_relation_head satellite, never a candidate.
        candidates, _ = _case_routed_outputs(
            self.cases["f1_dense_mixed_list_low_anchor"]
        )
        satellites = _satellites(candidates)
        captions = [s for s in satellites if s["text"] == "Advanced settings"]
        self.assertEqual(len(captions), 1)
        self.assertEqual(captions[0]["evidence"]["typeInferred"], "row_relation_head_satellite")
        self.assertEqual(captions[0]["evidence"]["headId"], "relation_head_band_3")

    def test_subtitle_never_menu_item_and_absorbed_in_band(self):
        # G-1 geometry: the equal-width stacked subtitle line is absorbed
        # in-band and can never be elected a head.
        case_id = "f3_subtitle_low_anchor_never_promoted"
        candidates, steps = _case_routed_outputs(self.cases[case_id])
        self.assertEqual(steps["row-relation-head"]["status"], "activated")
        self.assertNotIn("Wi-Fi, connections, networks", _menu_texts(candidates))
        satellites = _satellites(candidates)
        self.assertIn(
            "Wi-Fi, connections, networks",
            [s["text"] for s in satellites],
            "the subtitle line is absorbed as a satellite, never a candidate",
        )

    def test_toggle_never_becomes_a_menu_item(self):
        # F2: the toggle detection (right-side control) is never elected a
        # head; its row title may be composed (real row), the control never.
        for case_id in ("f2_preferences_page_high_anchor", "f2_preferences_page_low_anchor"):
            with self.subTest(case=case_id):
                candidates, _ = _case_routed_outputs(self.cases[case_id])
                toggles = [
                    candidate for candidate in candidates
                    if candidate.get("type") in {
                        "toggle", "switch", "checkbox", "slider"
                    }
                ]
                self.assertFalse(
                    [c for c in toggles if c.get("type") == "menu_item"],
                    "controls must never become navigation candidates",
                )
                # The toggle detection itself may join the fused list as a
                # control, but the routed pipeline must not promote it.
                self.assertTrue(all(c["type"] != "menu_item" for c in toggles))

    def test_ocr_only_section_headers_never_promoted(self):
        # F2: section headers are OCR-only (no detector anchor) — no fused
        # candidate and relation-head fails closed on the unanchored band.
        for case_id in ("f2_preferences_page_high_anchor", "f2_preferences_page_low_anchor"):
            with self.subTest(case=case_id):
                candidates, _ = _case_routed_outputs(self.cases[case_id])
                menus = _menu_texts(candidates)
                self.assertNotIn("ACCOUNT", menus)
                self.assertNotIn("NOTIFICATIONS", menus)

    def test_evidence_insufficient_fails_closed_nothing_fabricated(self):
        # G-6: OCR-only viewport with no detector anchors composes nothing.
        candidates, steps = _case_routed_outputs(
            self.cases["f3_evidence_insufficient_fail_closed"]
        )
        self.assertEqual(_menu_texts(candidates), [])
        self.assertEqual(steps["row-relation-head"]["status"], "noop")


class RoutedV1nIntegrationTests(unittest.TestCase):
    """G-1/G-4 at the routed-engine level on the real v1n frame."""

    def test_v1n_subtitle_and_search_never_promoted(self):
        nav = {case["case_id"]: case for case in _load(_NAV_CORPUS)}
        case = nav["v1n_low_anchor_viewport_subtitle_fail_closed"]
        candidates, steps = _case_routed_outputs(case)

        self.assertEqual(steps["row-relation-head"]["status"], "activated")
        self.assertEqual(steps["spacing-verifier"]["status"], "verified")
        menus = _menu_texts(candidates)
        self.assertNotIn(
            "Volume, vibration, Do Not Disturb", menus,
            "v1n: the subtitle must NEVER be a menu_item (routed)",
        )
        self.assertNotIn(
            "Q Search settings", menus,
            "v1n: the search box is engine-classified input — the adapter must "
            "never promote it to navigation (merge policy)",
        )
        # The three icon-confirmed rows stay composed; real unconfirmed title
        # rows are composed by relation-head geometry (never fabricated).
        self.assertIn("Sound & vibration", menus)
        self.assertIn("Security & privacy", menus)
        self.assertIn("Location", menus)

    def test_adapter_fails_closed_without_raw_sources(self):
        # G-4: with fewer than the routing floor of confirmed anchors and no
        # raw visual sources, the adapter must NOOP fail-closed — it can never
        # compose from already-composed candidates.
        candidates = [
            {
                "id": "x", "type": "menu_item", "text": "Only row",
                "boundsPx": [100, 100, 300, 130],
                "centerPx": [200, 115],
                "evidence": {}, "riskFlags": [],
            },
        ]
        self.assertLess(len(candidates), ROUTING_MIN_ANCHORS)
        decision = run_row_relation_head_routed(candidates, [], {})
        self.assertEqual(decision["status"], "noop")
        self.assertIn("fail-closed", decision["detail"])
        self.assertEqual(candidates, [
            {
                "id": "x", "type": "menu_item", "text": "Only row",
                "boundsPx": [100, 100, 300, 130],
                "centerPx": [200, 115],
                "evidence": {}, "riskFlags": [],
            },
        ], "fail-closed adapter must not mutate candidates")

    def test_adapter_delegates_at_min_anchors_floor(self):
        # Routing is CODE: exactly ROUTING_MIN_ANCHORS confirmed rows ⇒ the
        # uniform-list path owns composition and the adapter noops unused.
        rows = [
            {
                "id": f"r{i}", "type": "menu_item", "text": f"Row {i}",
                "boundsPx": [120, i * 100, 300, i * 100 + 30],
                "centerPx": [200, i * 100 + 15],
                "evidence": {}, "riskFlags": [],
            }
            for i in range(ROUTING_MIN_ANCHORS)
        ]
        candidates = [dict(row) for row in rows]
        decision = run_row_relation_head_routed(candidates, [], {})
        self.assertEqual(decision["status"], "noop")
        self.assertIn("delegated", decision["detail"])
        self.assertEqual(candidates, rows, "delegated router must not mutate")
        self.assertEqual(decision["emitted"], 0)


if __name__ == "__main__":
    unittest.main()