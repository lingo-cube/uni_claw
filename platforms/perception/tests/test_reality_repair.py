"""PER-REAL-01 + RPER-1..RPER-12: reality regression tests for the
raw-pixel toggle candidate generation repair.

These tests run the PRODUCTION pipeline
(``uniclaw_perception.server._run_pipeline`` — real YOLO, real OCR, real
fusion, no mocks) against repo-persisted reality fixtures captured from a
live Android 15 emulator Developer Options page
(``tests/fixtures/reality/*.png``), and assert the repair's behavior on
real screenshots:

- ``developer-options-falsification.png`` — the live falsification frame:
  YOLO emits 34 ``text_block`` detections and ZERO control-class
  detections, yet the page contains 4 real switches. Before the repair the
  production pipeline returned 0 switch candidates (recorded in the
  fixture's groundtruth); after the repair it must discover the real
  switches from raw pixels.
- ``developer-options-scrolled2.png`` — a scrolled frame of the same page
  with 14 real switch rows; exercises multi-toggle discovery, one
  candidate per row, no duplicates, and fail-closed behavior for rows
  without text.

Ground truth in the ``*.groundtruth.json`` files was produced by
independent whole-image structural analysis of the raw frames (NOT by the
fusion pipeline, YOLO, or OCR), and each switch state was confirmed by two
independent methods on the same frame (track median luminance + knob
position, and the C# ImageSwitchStateProvider algorithm).

The C#-oracle in this module replicates the exact production algorithm of
``ImageSwitchStateProvider.ClassifySwitchRegion`` (baseline = median
luminance of the middle-third band; |lum - baseline| >= 60 outlier pixels
counted per left/right half; difference = rightRatio - leftRatio; > +0.15
ON, < -0.15 OFF, else UNKNOWN).

These tests are intentionally slow (real model inference); they are the
reality layer above the fast unit tests in test_toggle_inference.py.
"""
from __future__ import annotations

import json
import re
import unittest
from pathlib import Path

import numpy as np
from PIL import Image

from uniclaw_perception import server as perception_server

_FIXTURES = Path(__file__).parent / "fixtures" / "reality"
_REPO_ROOT = Path(__file__).parents[3]
_YOLO_MODEL = (
    Path(__file__).parents[1]
    / "models" / "yolo" / "android_ui_detection_yolov8" / "best.pt"
)

# ── Pipeline execution (cached, with single-pass call counting) ────────────

_PIPELINE_CACHE: dict[str, dict] = {}
_PASS_COUNTS = {"yolo": 0, "ocr": 0}

_original_run_yolo = perception_server.run_yolo_on_image
_original_run_ocr = perception_server.run_rapid_ocr_on_image


def _counting_yolo(image):
    _PASS_COUNTS["yolo"] += 1
    return _original_run_yolo(image)


def _counting_ocr(image, **kwargs):
    _PASS_COUNTS["ocr"] += 1
    return _original_run_ocr(image, **kwargs)


perception_server.run_yolo_on_image = _counting_yolo
perception_server.run_rapid_ocr_on_image = _counting_ocr


def _run_pipeline_cached(name: str) -> dict:
    if name not in _PIPELINE_CACHE:
        perception_server._config = perception_server.load_config()
        image = Image.open(_FIXTURES / f"{name}.png")
        evidence, _ = perception_server._run_pipeline(image, image.width, image.height)
        _PIPELINE_CACHE[name] = evidence
    return _PIPELINE_CACHE[name]


def _load_groundtruth(name: str) -> dict:
    with open(_FIXTURES / f"{name}.groundtruth.json", "r", encoding="utf-8") as fh:
        return json.load(fh)


def _switch_candidates(evidence: dict) -> list[dict]:
    return [c for c in evidence["candidates"] if c.get("type") == "switch"]


def _raw_candidates(evidence: dict) -> list[dict]:
    return [
        c for c in _switch_candidates(evidence)
        if (c.get("evidence") or {}).get("typeInferred") == "raw_pixel_toggle"
    ]


def _iou_px(a: list, b: list) -> float:
    """Pixel-space IoU for [x1, y1, x2, y2] rectangles."""
    ax1, ay1, ax2, ay2 = a
    bx1, by1, bx2, by2 = b
    ix1, iy1 = max(ax1, bx1), max(ay1, by1)
    ix2, iy2 = min(ax2, bx2), min(ay2, by2)
    if ix2 <= ix1 or iy2 <= iy1:
        return 0.0
    inter = (ix2 - ix1) * (iy2 - iy1)
    union = (ax2 - ax1) * (ay2 - ay1) + (bx2 - bx1) * (by2 - by1) - inter
    return inter / union if union > 0 else 0.0


def _oracle_state(gray: np.ndarray, bounds_px: list) -> str | None:
    """Exact replication of ImageSwitchStateProvider.ClassifySwitchRegion.

    Returns 'ON' / 'OFF' / 'UNKNOWN'.
    """
    x1, y1, x2, y2 = bounds_px
    w = max(0, x2 - x1)
    h = max(0, y2 - y1)
    if w < 8 or h < 8:
        return None
    crop = gray[y1:y2, x1:x2]
    if crop.shape[0] < 4 or crop.shape[1] < 4:
        return None
    mid_x = w // 2
    band_top, band_bottom = h // 3, 2 * h // 3
    band = crop[band_top:band_bottom, :]
    if band.size == 0:
        return None
    baseline = float(np.median(band))
    out = np.abs(band - baseline) >= 60.0
    left = out[:, :mid_x]
    right = out[:, mid_x:]
    if left.size == 0 or right.size == 0:
        return None
    left_ratio = left.sum() / left.size
    right_ratio = right.sum() / right.size
    difference = right_ratio - left_ratio
    if difference > 0.15:
        return "ON"
    if difference < -0.15:
        return "OFF"
    return "UNKNOWN"


def _frame_gray(name: str) -> np.ndarray:
    image = Image.open(_FIXTURES / f"{name}.png").convert("RGB")
    arr = np.asarray(image, dtype=np.float32)
    return arr.mean(axis=2)


def _read_source(rel: str) -> str:
    return (_REPO_ROOT / rel).read_text(encoding="utf-8")


def _text_rows(evidence: dict) -> list[dict]:
    return [
        c for c in evidence["candidates"]
        if c.get("type") in {"text_block", "menu_item"} and c.get("text", "").strip()
    ]


def _row_has_gt_track(row: dict, gt_switches: list[dict]) -> bool:
    """True when any ground-truth track overlaps this row's band."""
    _, ry1, _, ry2 = row["boundsPx"]
    for sw in gt_switches:
        _, sy1, _, sy2 = sw["bounds"]
        if sy1 <= ry2 and sy2 >= ry1:
            return True
    return False


# ── Tests ───────────────────────────────────────────────────────────────────

@unittest.skipUnless(_YOLO_MODEL.exists(), "YOLO model file not present")
class RealityRepairTests(unittest.TestCase):
    """Reality regression tests for the raw-pixel toggle repair."""

    @classmethod
    def setUpClass(cls):
        pass_start = dict(_PASS_COUNTS)
        cls.falsification = _run_pipeline_cached("developer-options-falsification")
        cls.scrolled2 = _run_pipeline_cached("developer-options-scrolled2")
        cls.pass_delta = {
            k: _PASS_COUNTS[k] - pass_start[k] for k in _PASS_COUNTS
        }
        cls.falsification_gt = _load_groundtruth("developer-options-falsification")
        cls.scrolled2_gt = _load_groundtruth("developer-options-scrolled2")
        cls.falsification_gray = _frame_gray("developer-options-falsification")
        cls.scrolled2_gray = _frame_gray("developer-options-scrolled2")

    # 1.4 / 2.1 ── text_block-only YOLO + real toggle -> toggle discovered

    def test_per_real_01_falsification_regression(self):
        gt = self.falsification_gt
        # The falsification record: pre-repair this frame produced 0 switch
        # candidates from 34 text_block-only YOLO detections (baseline
        # detection.confidence 0.35). Baseline updated 2026-08-30 to 37 for
        # detection.confidence = 0.2 (config/label-mapping.json): 37 text_block
        # + 1 input detection, still no control class.
        yolo = self.falsification["yolo"]
        text_blocks = [d for d in yolo if d["label"] == "text_block"]
        self.assertEqual(len(text_blocks), gt["yolo_production_evidence"]["text_block"])
        controls = [d for d in yolo
                    if d["label"] in {"switch", "toggle", "checkbox", "slider"}]
        self.assertEqual(
            len(controls), gt["yolo_production_evidence"]["other_control_classes"])
        self.assertEqual(
            gt["yolo_production_evidence"]["switch_candidates_before_repair"], 0)
        # Post-repair: the raw-pixel path discovers the real switches.
        switches = _switch_candidates(self.falsification)
        self.assertGreaterEqual(len(switches), 3)
        for sw in gt["switches"][:3]:
            matched = any(
                _iou_px(c["boundsPx"], sw["bounds"]) >= 0.7 for c in switches)
            self.assertTrue(matched, f"no candidate matches GT {sw['id']} {sw['bounds']}")

    def test_rper_01_text_block_only_yolo_discovers_toggle(self):
        """2.1 RPER-1: no control-class YOLO evidence, real toggle still
        discovered from raw pixels with tight bounds."""
        evidence = self.falsification
        # Fixture premise (RPER-1): no control-class YOLO evidence — exact
        # class-wise accounting against the recorded production baseline
        # (37 text_block + 1 input at detection.confidence 0.2).
        controls = [d for d in evidence["yolo"]
                    if d["label"] in {"switch", "toggle", "checkbox", "slider"}]
        self.assertEqual(
            len(controls),
            self.falsification_gt["yolo_production_evidence"]["other_control_classes"])
        raw = _raw_candidates(evidence)
        self.assertGreaterEqual(len(raw), 3)
        sw2 = self.falsification_gt["switches"][1]  # 'Stay awake' ON
        matched = next(
            (c for c in raw if _iou_px(c["boundsPx"], sw2["bounds"]) >= 0.7), None)
        self.assertIsNotNone(matched, "raw-pixel path did not find sw2")
        self.assertIn("raw_pixel_toggle", matched.get("riskFlags", []))
        x1, y1, x2, y2 = matched["boundsPx"]
        self.assertLessEqual(x2 - x1, 70)   # tight, not row-sized
        self.assertLessEqual(y2 - y1, 45)

    def test_rper_02_multiple_toggles_correct_tight_bounds(self):
        """2.2 RPER-2: multiple real toggles -> one tight candidate per row,
        no duplicates."""
        evidence = self.falsification
        switches = _switch_candidates(evidence)
        # Each of the three text-row switches has exactly one tight candidate.
        for sw in self.falsification_gt["switches"][:3]:
            matches = [c for c in switches
                       if _iou_px(c["boundsPx"], sw["bounds"]) >= 0.7]
            self.assertEqual(len(matches), 1,
                             f"GT {sw['id']} matched by {len(matches)} candidates")
            x1, y1, x2, y2 = matches[0]["boundsPx"]
            self.assertLessEqual(x2 - x1, 70)
            self.assertLessEqual(y2 - y1, 45)
        # No duplicate candidates (pairwise IoU < 0.6).
        for i in range(len(switches)):
            for j in range(i + 1, len(switches)):
                self.assertLess(_iou_px(switches[i]["boundsPx"],
                                        switches[j]["boundsPx"]), 0.6)
        # Scrolled2: 12 of 14 ground-truth tracks discovered, one each,
        # and every raw-pixel candidate corresponds to a real track.
        sc2 = _switch_candidates(self.scrolled2)
        found = 0
        for sw in self.scrolled2_gt["switches"]:
            if any(_iou_px(c["boundsPx"], sw["bounds"]) >= 0.7 for c in sc2):
                found += 1
        self.assertGreaterEqual(found, 12)
        raw2 = _raw_candidates(self.scrolled2)
        for c in raw2:
            self.assertTrue(
                any(_iou_px(c["boundsPx"], sw["bounds"]) >= 0.7
                    for sw in self.scrolled2_gt["switches"]),
                f"raw-pixel candidate has no ground-truth track: {c['boundsPx']}")
        for i in range(len(sc2)):
            for j in range(i + 1, len(sc2)):
                self.assertLess(_iou_px(sc2[i]["boundsPx"], sc2[j]["boundsPx"]), 0.6)

    def test_rper_03_chevron_non_toggle_rejected(self):
        """2.3 RPER-3: real chevron/non-toggle rows produce no candidates.

        The falsification frame has many chevron rows (Bug report, Memory,
        HDCP checking, Running services, Picture colormode, DSU Loader,
        System UI demo mode, ...). The raw-pixel path must not emit a
        candidate for any of them: the total switch candidate count equals
        the number of real text-row switches (3), and no raw candidate
        exists outside the ground-truth tracks."""
        switches = _switch_candidates(self.falsification)
        self.assertEqual(len(switches), 3)
        raw = _raw_candidates(self.falsification)
        self.assertEqual(len(raw), 3)
        for c in raw:
            self.assertTrue(
                any(_iou_px(c["boundsPx"], sw["bounds"]) >= 0.7
                    for sw in self.falsification_gt["switches"][:3]))

    def test_rper_04_text_only_row_rejected(self):
        """2.4 RPER-4: text-only rows (no toggle-like raw structure) get no
        raw-pixel candidate.

        Data-driven over the scrolled2 frame: for every OCR text row whose
        band contains no ground-truth track, the raw-pixel path must not
        place a candidate in that row's right-side zone."""
        evidence = self.scrolled2
        raw = _raw_candidates(evidence)
        for row in _text_rows(evidence):
            if _row_has_gt_track(row, self.scrolled2_gt["switches"]):
                continue
            _, ry1, _, ry2 = row["boundsPx"]
            lo, hi = ry1 - 12, ry2 + 12
            for c in raw:
                cx1, cy1, cx2, cy2 = c["boundsPx"]
                center_y = (cy1 + cy2) / 2
                cx1_norm = cx1 / 1080.0
                if lo <= center_y <= hi and cx1_norm >= 0.85:
                    self.fail(
                        f"raw-pixel candidate {c['boundsPx']} emitted for "
                        f"text-only row {row['text']!r} ({row['boundsPx']})")

    def test_rper_05_partial_ambiguous_fail_closed(self):
        """2.5 RPER-5: partial/ambiguous controls fail closed.

        - Falsification sw4 (bottom row, ON, no OCR text row): row-anchored
          search cannot reach it -> NO candidate is emitted (no guessing).
        - Scrolled2 s05 (y650, track clipped by its OCR row band) and s14
          (y1797, no text row): both absent."""
        switches = _switch_candidates(self.falsification)
        for c in switches:
            _, cy1, _, cy2 = c["boundsPx"]
            self.assertLess((cy1 + cy2) / 2, 1790,
                            "no candidate may be emitted in sw4's text-less zone")
        raw2 = _raw_candidates(self.scrolled2)
        for c in raw2:
            _, cy1, _, cy2 = c["boundsPx"]
            center_y = (cy1 + cy2) / 2
            self.assertFalse(644 <= center_y <= 687,
                             "clipped track s05 must fail closed (no candidate)")
            self.assertFalse(1791 <= center_y <= 1834,
                             "text-less track s14 must fail closed (no candidate)")

    def test_rper_06_canonical_switch_to_toggle_propagation(self):
        """2.6 RPER-6: inferred candidates carry canonical type 'switch'
        (Python), which maps to Runtime-facing PerceptionType 'toggle' at
        the C# adapter boundary via the existing label mapping."""
        from uniclaw_perception.yolo.labels import YOLO_LABEL_ALIASES
        self.assertEqual(YOLO_LABEL_ALIASES["switch"], "switch")
        for c in _switch_candidates(self.falsification):
            self.assertEqual(c["type"], "switch")
        adapter = _read_source(
            "src/UniClaw.Runtime.Adapters/PhysicalEnvironment.cs")
        self.assertIn('"switch" => "toggle"', adapter)
        self.assertIn('"checkbox" => "toggle"', adapter)

    def test_rper_07_same_frame_switch_state_provider_on(self):
        """2.7 RPER-7: the C# ImageSwitchStateProvider algorithm, applied to
        the SAME frame using the PIPELINE CANDIDATE bounds (not ground
        truth), reads ON for the two teal ON switches."""
        switches = _switch_candidates(self.falsification)
        sw1 = self.falsification_gt["switches"][0]  # master, ON
        sw2 = self.falsification_gt["switches"][1]  # Stay awake, ON
        for gt in (sw1, sw2):
            cand = next(c for c in switches
                        if _iou_px(c["boundsPx"], gt["bounds"]) >= 0.7)
            state = _oracle_state(self.falsification_gray, cand["boundsPx"])
            self.assertEqual(state, "ON",
                             f"{gt['id']} candidate {cand['boundsPx']} "
                             f"read {state}, expected ON")

    def test_rper_08_same_frame_switch_state_provider_off(self):
        """2.8 RPER-8: same-frame C#-oracle on the candidate bounds reads
        OFF for the light-gray OFF switch."""
        switches = _switch_candidates(self.falsification)
        sw3 = self.falsification_gt["switches"][2]  # Automatic updates, OFF
        cand = next(c for c in switches
                    if _iou_px(c["boundsPx"], sw3["bounds"]) >= 0.7)
        state = _oracle_state(self.falsification_gray, cand["boundsPx"])
        self.assertEqual(state, "OFF",
                         f"sw3 candidate {cand['boundsPx']} read {state}")

    def test_rper_09_binding_production_path(self):
        """2.9 RPER-9: Binding production path contract.

        The candidates the repair produces are exactly what the C# Binding
        analysis consumes: normalized [0,1] bounds in the full-screenshot
        frame (the ISwitchStateReader contract), canonical 'switch' type
        normalized to 'toggle' at the adapter boundary, and BindingAnalysis
        selecting elements by the bound object's control type."""
        for c in _switch_candidates(self.falsification):
            b = c["bounds"]
            self.assertTrue(0.0 <= b["x1"] < b["x2"] <= 1.0)
            self.assertTrue(0.0 <= b["y1"] < b["y2"] <= 1.0)
            x1, y1, x2, y2 = c["boundsPx"]
            self.assertGreaterEqual(x1, 0)
            self.assertGreaterEqual(y1, 0)
            self.assertLessEqual(x2, 1080)
            self.assertLessEqual(y2, 1920)
        binding = _read_source("src/UniClaw.Runtime/World/BindingAnalysis.cs")
        self.assertIn('e.PerceptionType', binding)
        self.assertIn("controlType", binding)

    def test_rper_10_state_belief_production_path(self):
        """2.10 RPER-10: StateBelief production path contract.

        StateBeliefReducer requires exactly one toggle-type element with a
        non-null SwitchState to form belief (null otherwise — no
        fabrication). The repair emits one candidate per row, so each row is
        unambiguous; the C# provider fills SwitchState from the same frame."""
        reducer = _read_source("src/UniClaw.Runtime/World/StateBeliefReducer.cs")
        self.assertIn('"toggle"', reducer)
        self.assertIn("SwitchState", reducer)
        # One candidate per row on the falsification frame (3 candidates,
        # no two share a row band).
        switches = _switch_candidates(self.falsification)
        self.assertEqual(len(switches), 3)
        for i in range(len(switches)):
            _, ay1, _, ay2 = switches[i]["boundsPx"]
            for j in range(i + 1, len(switches)):
                _, by1, _, by2 = switches[j]["boundsPx"]
                self.assertFalse(ay1 <= by2 and by1 <= ay2,
                                 "two switch candidates share a row band")

    def test_rper_11_single_screenshot_single_pass(self):
        """2.11 RPER-11: exactly one YOLO pass and one OCR pass per
        screenshot; no second screenshot or model invocation."""
        # Two frames were processed by this class; each must have caused
        # exactly one YOLO call and one OCR call.
        self.assertEqual(self.pass_delta["yolo"], 2)
        self.assertEqual(self.pass_delta["ocr"], 2)
        # Structural: the raw-pixel detector never calls YOLO/OCR or
        # captures anything — it only consumes the already-decoded image.
        detector_src = _read_source(
            "platforms/perception/uniclaw_perception/fusion/heuristics.py")
        detector_body = detector_src[
            detector_src.index("def _detect_toggle_regions_from_image"):]
        self.assertNotIn("run_yolo", detector_body)
        self.assertNotIn("run_rapid_ocr", detector_body)
        self.assertNotIn("screenshot", detector_body)
        self.assertNotIn("screencap", detector_body)
        self.assertNotIn("subprocess", detector_body)

    def test_rper_12_zero_llm_vlm(self):
        """2.12 RPER-12: the repair's production code invokes no LLM/VLM
        and contains no scenario-specific target names."""
        files = [
            "platforms/perception/uniclaw_perception/fusion/heuristics.py",
            "platforms/perception/uniclaw_perception/fusion/engine.py",
            "platforms/perception/uniclaw_perception/yolo/labels.py",
        ]
        forbidden_model = re.compile(
            r"\b(llm|vlm|openai|anthropic|gemini|claude|gpt)\b",
            re.IGNORECASE)
        forbidden_scenario = re.compile(
            r"(DeveloperOptions|AutomaticSystemUpdates|StayAwake|Wifi|"
            r"WiFi|Bluetooth|GrammaticalGender|USBdebugging)",
            re.IGNORECASE)
        for rel in files:
            src = _read_source(rel)
            self.assertIsNone(forbidden_model.search(src),
                              f"LLM/VLM token found in {rel}")
            self.assertIsNone(forbidden_scenario.search(src),
                              f"scenario-specific token found in {rel}")


if __name__ == "__main__":
    unittest.main()
