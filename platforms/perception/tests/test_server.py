"""Tests for uniclaw_perception.server — HTTP API, pipeline, preprocessing, remap, config.

Migrated from tools/local_vision/tests/test_server.py.
Import paths updated for uniclaw_perception package.
Test assertions preserved exactly.
"""
from __future__ import annotations

import contextlib
import json
import os
import tempfile
import unittest
from contextlib import ExitStack
from io import BytesIO
from pathlib import Path
from unittest import mock

from fastapi.testclient import TestClient
from PIL import Image, ImageDraw

from uniclaw_perception import server
from uniclaw_perception.schema import Box, Detection, OcrToken
from uniclaw_perception.ocr import common as ocr_common


def _jpeg_bytes() -> bytes:
    buf = BytesIO()
    Image.new("RGB", (400, 800), (255, 255, 255)).save(buf, format="JPEG")
    return buf.getvalue()


def _fake_yolo(image, **kwargs) -> list[Detection]:
    return [
        Detection(id="det_1", label="text_block", confidence=0.9,
                  box=Box(50, 100, 250, 180)),
    ]


def _fake_ocr(image, detections, **kwargs) -> list[list[OcrToken]]:
    return [
        [OcrToken(id="ocr_1", text="Display", confidence=0.9,
                  box=Box(60, 115, 200, 145))]
        for _ in detections
    ]


def _fake_rapid_ocr(image, **kwargs) -> list[OcrToken]:
    return [
        OcrToken(id="ocr_1", text="Display", confidence=0.9,
                 box=Box(60, 115, 200, 145)),
    ]


@contextlib.contextmanager
def _patched_pipeline():
    """Mock model inference + warmup. Config loading stays real."""
    with ExitStack() as stack:
        stack.enter_context(mock.patch("uniclaw_perception.server.warmup_yolo"))
        stack.enter_context(mock.patch("uniclaw_perception.server.warmup_ocr"))
        stack.enter_context(mock.patch("uniclaw_perception.server.warmup_rapid_ocr"))
        stack.enter_context(
            mock.patch("uniclaw_perception.server.run_yolo_on_image", side_effect=_fake_yolo))
        stack.enter_context(
            mock.patch("uniclaw_perception.server.run_ocr_on_crops", side_effect=_fake_ocr))
        stack.enter_context(
            mock.patch("uniclaw_perception.server.run_rapid_ocr_on_image", side_effect=_fake_rapid_ocr))
        yield


# ── /health ─────────────────────────────────────────────────────

class HealthTests(unittest.TestCase):
    def setUp(self) -> None:
        from uniclaw_perception import health
        health.set_warm(False)

    def test_health_returns_warm_after_startup(self) -> None:
        from uniclaw_perception import health
        self.assertFalse(health.is_warm())
        with _patched_pipeline():
            with TestClient(server.app) as client:
                resp = client.get("/health")
        self.assertEqual(resp.status_code, 200)
        self.assertEqual(resp.json(), {"status": "ok", "warm": True})
        self.assertTrue(health.is_warm())


# ── POST /v1/analyze ───────────────────────────────────────────

class AnalyzeTests(unittest.TestCase):
    def _analyze(self):
        with _patched_pipeline():
            with TestClient(server.app) as client:
                return client.post(
                    "/v1/analyze",
                    content=_jpeg_bytes(),
                    headers={"Content-Type": "image/jpeg"},
                )

    def test_returns_evidence_with_candidates(self) -> None:
        resp = self._analyze()
        self.assertEqual(resp.status_code, 200)
        self.assertEqual(resp.headers["content-type"], "application/json")
        body = resp.json()
        self.assertGreater(len(body["candidates"]), 0)
        candidate = body["candidates"][0]
        for key in ("type", "text", "center", "bounds", "confidence"):
            self.assertIn(key, candidate)
        self.assertEqual(candidate["text"], "Display")
        for key in ("image", "yolo", "ocr", "candidates", "summary",
                     "metadata", "scrollHints"):
            self.assertIn(key, body)

    def test_scroll_hints_present(self) -> None:
        resp = self._analyze()
        scroll_hints = resp.json()["scrollHints"]
        self.assertIn("totalCandidates", scroll_hints)
        self.assertIn("candidatesNearBottom", scroll_hints)
        self.assertIn("scrollbarDetected", scroll_hints)
        self.assertGreater(scroll_hints["totalCandidates"], 0)

    def test_server_timing_header_present(self) -> None:
        resp = self._analyze()
        timing = resp.headers.get("server-timing")
        self.assertIsNotNone(timing)
        self.assertRegex(timing,
            r"^yolo;dur=[\d.]+(ms)?, ocr;dur=[\d.]+(ms)?, "
            r"fusion;dur=[\d.]+(ms)?, scroll;dur=[\d.]+(ms)?$")
        body = resp.json()
        for banned in ("timing", "latency", "duration"):
            self.assertNotIn(banned, body)


# ── POST /v1/analyze_raw ───────────────────────────────────────

class AnalyzeRawTests(unittest.TestCase):
    def _post_raw(self, body: bytes, width: int, height: int,
                  pixel_format: str | None = None):
        headers = {
            "Content-Type": "application/octet-stream",
            "X-Image-Width": str(width),
            "X-Image-Height": str(height),
        }
        if pixel_format is not None:
            headers["X-Image-Pixel-Format"] = pixel_format
        with _patched_pipeline():
            with TestClient(server.app) as client:
                return client.post("/v1/analyze_raw", content=body,
                                   headers=headers)

    def test_analyze_raw_returns_valid_evidence(self) -> None:
        # Keep the production fixture geometry inside the source frame; the
        # geometry gate must reject rather than clamp out-of-frame evidence.
        img = Image.new("RGBA", (400, 800), (255, 0, 0, 255))
        resp = self._post_raw(img.tobytes(), img.width, img.height)
        self.assertEqual(resp.status_code, 200)
        data = resp.json()
        for key in ("candidates", "scrollHints", "metadata"):
            self.assertIn(key, data)
        self.assertGreater(len(data["candidates"]), 0)
        self.assertEqual(data["metadata"]["schema"], "uniclaw.localVisionEvidence.v1")
        self.assertIn("Server-Timing", resp.headers)

    def test_analyze_raw_body_size_mismatch_returns_400(self) -> None:
        w, h = 100, 200
        body = b"\x00" * (w * h * 4 - 10)
        resp = self._post_raw(body, w, h)
        self.assertEqual(resp.status_code, 400)
        self.assertIn("Body size mismatch", resp.json()["detail"])

    def test_analyze_raw_unsupported_pixel_format_returns_400(self) -> None:
        w, h = 10, 10
        body = b"\x00" * (w * h * 4)
        resp = self._post_raw(body, w, h, pixel_format="2")
        self.assertEqual(resp.status_code, 400)
        self.assertIn("Unsupported pixel format", resp.json()["detail"])

    def test_analyze_raw_vs_analyze_roundtrip(self) -> None:
        img = Image.new("RGB", (400, 800), (255, 255, 255))
        draw = ImageDraw.Draw(img)
        draw.rectangle([50, 50, 150, 100], fill=(0, 0, 0))
        draw.rectangle([50, 150, 200, 200], fill=(0, 0, 0))
        buf = BytesIO()
        img.save(buf, format="JPEG")
        jpeg_bytes = buf.getvalue()
        rgba_bytes = img.convert("RGBA").tobytes()

        with _patched_pipeline():
            with TestClient(server.app) as client:
                with mock.patch.object(server, "_get_config") as mock_cfg:
                    from uniclaw_perception.config import PerceptionConfig
                    cfg = PerceptionConfig()
                    cfg.crop_top = 0.0
                    cfg.crop_bottom = 0.0
                    cfg.max_width = 4000
                    mock_cfg.return_value = cfg

                    resp_jpeg = client.post("/v1/analyze", content=jpeg_bytes,
                        headers={"Content-Type": "image/jpeg"})
                    resp_raw = client.post("/v1/analyze_raw", content=rgba_bytes,
                        headers={"Content-Type": "application/octet-stream",
                                 "X-Image-Width": str(img.width),
                                 "X-Image-Height": str(img.height)})

        self.assertEqual(resp_jpeg.status_code, 200)
        self.assertEqual(resp_raw.status_code, 200)
        jpeg_candidates = resp_jpeg.json().get("candidates", [])
        raw_candidates = resp_raw.json().get("candidates", [])
        self.assertEqual(len(jpeg_candidates), len(raw_candidates))
        for jc, rc in zip(jpeg_candidates, raw_candidates):
            self.assertAlmostEqual(jc["center"]["x"], rc["center"]["x"], delta=0.002)
            self.assertAlmostEqual(jc["center"]["y"], rc["center"]["y"], delta=0.002)


# ── Preprocessing ───────────────────────────────────────────────

class PreprocessTests(unittest.TestCase):
    def test_preprocess_crop_and_resize(self) -> None:
        from uniclaw_perception.preprocessing import preprocess
        img = Image.new("RGBA", (1080, 2400), (255, 255, 255, 255))
        result = preprocess(img, max_width=720, crop_top_ratio=0.0625, crop_bottom_ratio=0.0625)
        self.assertAlmostEqual(result[0].width, 720, delta=1)
        self.assertAlmostEqual(result[0].height, 1400, delta=1)
        self.assertAlmostEqual(result[1], 1080/720, delta=1e-9)
        self.assertEqual(result[2], 150)
        self.assertEqual(result[3], 2400)

    def test_preprocess_no_resize_below_max_width(self) -> None:
        from uniclaw_perception.preprocessing import preprocess
        img = Image.new("RGBA", (400, 800), (255, 255, 255, 255))
        result = preprocess(img, max_width=720, crop_top_ratio=0.0625, crop_bottom_ratio=0.0625)
        self.assertEqual(result[0].size, (400, 700))
        self.assertEqual(result[1], 1.0)
        self.assertEqual(result[2], 50)
        self.assertEqual(result[3], 800)


# ── Remap ───────────────────────────────────────────────────────

class RemapCoordsTests(unittest.TestCase):
    def _sample_evidence(self) -> dict:
        return {
            "candidates": [{
                "label": "btn",
                "boundsPx": [240, 300, 480, 420],
                "bounds": {"x1": 240/720, "y1": 300/1400, "x2": 480/720, "y2": 420/1400},
                "centerPx": [360, 360],
                "center": {"x": 360/720, "y": 360/1400},
                "coordinate": {"x": 360/720, "y": 360/1400},
            }],
            "yolo": [{"label": "icon", "boundsPx": [0, 0, 72, 72]}],
            "ocr": [{"label": "text", "centerPx": [720, 1400]}],
            "image": {"width": 720, "height": 1400},
        }

    def test_remap_restores_fullscreen(self) -> None:
        from uniclaw_perception.remap import remap_coords
        ev = self._sample_evidence()
        remap_coords(ev, 1080/720, 150, 1080, 2400)
        c = ev["candidates"][0]
        self.assertEqual(c["boundsPx"], [360, 600, 720, 780])
        self.assertEqual(c["centerPx"], [540, 690])
        self.assertEqual(c["bounds"], {"x1": round(360/1080,6), "y1": round(600/2400,6),
                                       "x2": round(720/1080,6), "y2": round(780/2400,6)})
        self.assertEqual(c["center"], {"x": round(540/1080,6), "y": round(690/2400,6)})
        self.assertEqual(ev["image"], {"width": 1080, "height": 2400})

    def test_remap_idempotent_when_no_transform(self) -> None:
        from uniclaw_perception.remap import remap_coords
        ev = self._sample_evidence()
        snapshot = json.dumps(ev, sort_keys=True)
        remap_coords(ev, 1.0, 0, 1080, 2400)
        self.assertEqual(json.dumps(ev, sort_keys=True), snapshot)

    def test_remap_only_crop_no_scale(self) -> None:
        from uniclaw_perception.remap import remap_coords
        ev = self._sample_evidence()
        remap_coords(ev, 1.0, 50, 720, 1400)
        c = ev["candidates"][0]
        self.assertEqual(c["boundsPx"], [240, 350, 480, 470])
        self.assertEqual(c["centerPx"], [360, 410])


# ── Config ──────────────────────────────────────────────────────

class ConfigTests(unittest.TestCase):
    def setUp(self) -> None:
        import uniclaw_perception.config as cfg_mod
        cfg_mod._config = None

    def test_default_config_loads(self) -> None:
        from uniclaw_perception.config import load
        cfg = load()
        self.assertEqual(cfg.spatial["edgeThreshold"], 0.92)
        # Default config/label-mapping.json sets detection.confidence = 0.2;
        # the 0.35 in PerceptionConfig is only the code fallback when the key
        # is absent (see config.py detection_confidence default).
        self.assertAlmostEqual(cfg.detection_confidence, 0.2)
        self.assertRegex(cfg.config_hash, r"^[0-9a-f]{64}$")

    def test_metadata_schema_and_config_hash(self) -> None:
        from uniclaw_perception.server import _metadata
        from uniclaw_perception.config import load
        import uniclaw_perception.server as perception_server
        load()
        # server._metadata reads the SERVER module-global (_config), not the
        # config-module one; init it here so the test is self-contained
        # (previously it only passed via ordering side effects from other cases).
        perception_server._config = perception_server.load_config()
        meta = _metadata(1080, 2400)
        self.assertEqual(meta["schema"], "uniclaw.localVisionEvidence.v1")
        self.assertRegex(meta["configHash"], r"^[0-9a-f]{64}$")
        self.assertIn("modelId", meta)
        self.assertIsNotNone(meta["modelId"])


if __name__ == "__main__":
    unittest.main()
