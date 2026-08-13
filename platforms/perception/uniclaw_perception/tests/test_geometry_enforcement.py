"""CORR-GEO-01..08 — complete response-boundary geometry enforcement."""
from __future__ import annotations

import math
import unittest

from uniclaw_perception.remap import (
    enforce_geometry, enforce_stage_views, validate_geometry,
)

LIMITS = (1080, 2400)


def _item(bounds=None, bounds_px=None, **extra):
    d = dict(extra)
    if bounds is not None:
        d["bounds"] = bounds
    if bounds_px is not None:
        d["boundsPx"] = bounds_px
    return d


def _evidence(candidates=(), yolo=(), ocr=()):
    return {"candidates": list(candidates), "yolo": list(yolo),
            "ocr": list(ocr)}


class GeometryEnforcementTests(unittest.TestCase):
    def test_CORR_GEO01_candidate_invalid_rejected(self):
        bad = _item({"x1": -0.2, "y1": 0.1, "x2": 0.4, "y2": 0.2},
                    [10, 10, 40, 20])
        good = _item({"x1": 0.1, "y1": 0.1, "x2": 0.4, "y2": 0.2},
                     [10, 10, 40, 20])
        ev = _evidence(candidates=[bad, good])
        enforce_geometry(ev, orig_limits=LIMITS)
        self.assertEqual(len(ev["candidates"]), 1)
        self.assertEqual(ev["candidates"][0], good)

    def test_CORR_GEO02_yolo_invalid_rejected(self):
        # the historical attack payload: x1=-0.2, x2=2.4, y2=2.3
        bad = _item({"x1": -0.2, "y1": 0.1, "x2": 2.4, "y2": 2.3},
                    [10, 10, 40, 20])
        ev = _evidence(yolo=[bad])
        enforce_geometry(ev, orig_limits=LIMITS)
        self.assertEqual(ev["yolo"], [])
        self.assertIn("INVALID_GEOMETRY",
                      [d.get("code") for d in ev.get("diagnostics", [])])
        self.assertEqual(ev.get("status"), "INVALID_GEOMETRY")

    def test_CORR_GEO03_ocr_invalid_rejected(self):
        bad = _item({"x1": 0.1, "y1": 0.1, "x2": 0.4, "y2": 0.2},
                    [-50, 10, 40, 20])   # boundsPx out of frame
        ev = _evidence(ocr=[bad])
        enforce_geometry(ev, orig_limits=LIMITS)
        self.assertEqual(ev["ocr"], [])

    def test_CORR_GEO04_mixed_siblings_preserve_only_valid(self):
        bad = _item({"x1": -0.2, "y1": 0.1, "x2": 0.4, "y2": 0.2},
                    [10, 10, 40, 20])
        good1 = _item({"x1": 0.1, "y1": 0.1, "x2": 0.4, "y2": 0.2},
                      [10, 10, 40, 20])
        good2 = _item({"x1": 0.5, "y1": 0.5, "x2": 0.6, "y2": 0.6},
                      [50, 50, 60, 60])
        ev = _evidence(candidates=[bad, good1, good2])
        enforce_geometry(ev, orig_limits=LIMITS)
        self.assertEqual(ev["candidates"], [good1, good2])

    def test_CORR_GEO05_all_invalid_invalid_geometry_not_ok_empty(self):
        bad1 = _item({"x1": -0.2, "y1": 0.1, "x2": 0.4, "y2": 0.2},
                     [10, 10, 40, 20])
        bad2 = _item({"x1": 0.1, "y1": 0.1, "x2": 0.4, "y2": 0.2},
                     [10, 10, 40, -20])
        ev = _evidence(yolo=[bad1, bad2])
        enforce_geometry(ev, orig_limits=LIMITS)
        self.assertEqual(ev["yolo"], [])
        self.assertEqual(ev.get("status"), "INVALID_GEOMETRY")
        self.assertNotEqual(ev.get("status"), "OK_EMPTY")

    def test_CORR_GEO06_no_nan_inf_reversed_survives(self):
        for bad_bounds in (
            {"x1": float("nan"), "y1": 0.1, "x2": 0.4, "y2": 0.2},
            {"x1": 0.1, "y1": float("inf"), "x2": 0.4, "y2": 0.2},
            {"x1": 0.9, "y1": 0.1, "x2": 0.4, "y2": 0.2},   # reversed
            {"x1": 0.1, "y1": 0.9, "x2": 0.4, "y2": 0.2},   # reversed y
        ):
            ev = _evidence(candidates=[_item(bad_bounds, [10, 10, 40, 20])])
            enforce_geometry(ev, orig_limits=LIMITS)
            self.assertEqual(ev["candidates"], [], f"survived: {bad_bounds}")

    def test_CORR_GEO07_no_silent_clamp(self):
        bad = _item({"x1": -0.2, "y1": 0.1, "x2": 0.4, "y2": 0.2},
                    [10, 10, 40, 20])
        valid, rejected = validate_geometry(
            [bad], space_label="NORMALIZED_PRODUCTION", pixel_limits=LIMITS)
        self.assertEqual(valid, [])          # dropped, not clamped
        self.assertEqual(rejected, 1)
        self.assertNotIn(bad, valid)

    def test_CORR_GEO08_stage_views_own_contracts_explicit(self):
        # proc-pixel views validated against proc limits; fused against orig
        views = {
            "rawModelDetections": [
                _item({"x1": 0.1, "y1": 0.1, "x2": 0.4, "y2": 0.2},
                      [10, 10, 40, 20]),
                _item({"x1": 0.1, "y1": 0.1, "x2": 0.4, "y2": 0.2},
                      [10, 10, 400000, 20]),   # beyond proc limits (720x1400)
            ],
            "fusedEvidence": [
                _item({"x1": 0.1, "y1": 0.1, "x2": 0.4, "y2": 0.2},
                      [10, 10, 40, 20]),
            ],
        }
        ev = {}
        enforce_stage_views(views, ev, proc_limits=(720, 1400),
                            orig_limits=LIMITS)
        self.assertEqual(len(views["rawModelDetections"]), 1)
        self.assertEqual(len(views["fusedEvidence"]), 1)
        self.assertIn("INVALID_GEOMETRY",
                      [d.get("code") for d in ev.get("diagnostics", [])])

    def test_validator_deterministic_pure(self):
        good = _item({"x1": 0.1, "y1": 0.1, "x2": 0.4, "y2": 0.2},
                     [10, 10, 40, 20])
        v1, r1 = validate_geometry([good], space_label="X", pixel_limits=LIMITS)
        v2, r2 = validate_geometry([good], space_label="X", pixel_limits=LIMITS)
        self.assertEqual(v1, v2)
        self.assertEqual(r1, r2)
        self.assertTrue(all(math.isfinite(c) for item in v1
                            for c in item["bounds"].values()))


if __name__ == "__main__":
    unittest.main()
