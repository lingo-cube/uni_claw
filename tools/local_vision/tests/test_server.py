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
from PIL import Image

from tools.local_vision import backends, server
from tools.local_vision.schema import Box, Detection, OcrToken


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
    # D-XXX: rapidocr 全图路径返回扁平 token 列表（融合层空间匹配关联 YOLO）
    return [
        OcrToken(id="ocr_1", text="Display", confidence=0.9,
                 box=Box(60, 115, 200, 145)),
    ]


@contextlib.contextmanager
def _patched_pipeline():
    """Mock 掉模型推理 + 预热（lifespan 的 _load_spatial 保持真实执行）。

    D-198 后默认后端为 rapidocr（server 实际调用 run_rapid_ocr_on_crops/
    warmup_rapid_ocr）；paddleocr 路径一并 mock，保证 _OCR_BACKEND 环境
    覆盖下测试仍稳定。
    """
    with ExitStack() as stack:
        stack.enter_context(mock.patch("tools.local_vision.server.warmup_yolo"))
        stack.enter_context(mock.patch("tools.local_vision.server.warmup_ocr"))
        stack.enter_context(mock.patch("tools.local_vision.server.warmup_rapid_ocr"))
        stack.enter_context(
            mock.patch("tools.local_vision.server.run_yolo_on_image", side_effect=_fake_yolo))
        stack.enter_context(
            mock.patch("tools.local_vision.server.run_ocr_on_crops", side_effect=_fake_ocr))
        stack.enter_context(
            mock.patch("tools.local_vision.server.run_rapid_ocr_on_image", side_effect=_fake_rapid_ocr))
        yield


# ── /health（V10 / R-9）───────────────────────────────────

class HealthTests(unittest.TestCase):
    def setUp(self) -> None:
        server._WARM = False  # 模拟"预热完成前"的初始状态（R-9）

    def test_health_returns_warm_after_startup(self) -> None:
        self.assertFalse(server._WARM)  # 预热完成前 warm=false
        with _patched_pipeline():
            with TestClient(server.app) as client:
                resp = client.get("/health")
        self.assertEqual(resp.status_code, 200)
        self.assertEqual(resp.json(), {"status": "ok", "warm": True})
        self.assertTrue(server._WARM)  # lifespan 预热完成后 warm=true


# ── POST /v1/analyze（V11 / V12 / V13）────────────────────

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
        # 基础证据结构完整
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
        self.assertRegex(
            timing,
            r"^yolo;dur=[\d.]+(ms)?, ocr;dur=[\d.]+(ms)?, "
            r"fusion;dur=[\d.]+(ms)?, scroll;dur=[\d.]+(ms)?$",
        )
        # 时序不进 JSON body（spec: 无 timing/latency 字段）
        body = resp.json()
        for banned in ("timing", "latency", "duration"):
            self.assertNotIn(banned, body)


# ── 配置读取（1.2 / V22 / R-6）────────────────────────────

class ConfigTests(unittest.TestCase):
    def test_default_config_loads(self) -> None:
        server._load_spatial()
        self.assertEqual(server._SPATIAL["edgeThreshold"], 0.92)
        self.assertEqual(server._SPATIAL["level1MaxY"], 0.08)
        self.assertAlmostEqual(server._DETECTION_CONF, 0.35)
        self.assertRegex(server._CONFIG_HASH, r"^[0-9a-f]{64}$")
        # roiPadding 已下发到 backends（R-14）
        self.assertEqual(backends._ROI_PADDING_SPEC["maxPx"], 64)

    def test_edge_threshold_from_config_used_in_scroll_hints(self) -> None:
        server._load_spatial()
        candidates = [
            {"center": {"y": 0.93}},  # > 0.92 → near bottom
            {"center": {"y": 0.91}},  # ≤ 0.92 → not
        ]
        hints = server._scroll_hints(candidates)
        self.assertEqual(hints["totalCandidates"], 2)
        self.assertEqual(hints["candidatesNearBottom"], 1)
        self.assertFalse(hints["scrollbarDetected"])

    def test_env_override_path_and_values(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            cfg_path = Path(tmp) / "label-mapping.json"
            cfg_path.write_text(json.dumps({
                "schema": "uniclaw.labelMapping.v1",
                "mappings": {"button": "menu_item"},
                "nonItemLabels": ["popup"],
                "spatial": {
                    "level1MaxY": 0.08,
                    "edgeThreshold": 0.90,
                    "roiPadding": {"x": 0.15, "y": 0.10, "minPx": 8, "maxPx": 64},
                },
                "detection": {"confidence": 0.40},
            }), encoding="utf-8")

            with mock.patch.dict(os.environ, {"UNICLAW_LABEL_MAPPING": str(cfg_path)}):
                server._load_spatial()

            self.assertAlmostEqual(server._SPATIAL["edgeThreshold"], 0.90)
            self.assertAlmostEqual(server._DETECTION_CONF, 0.40)  # 非硬编码 0.35
            # 阈值 0.90（非硬编码 0.92）
            candidates = [{"center": {"y": 0.91}}, {"center": {"y": 0.89}}]
            hints = server._scroll_hints(candidates)
            self.assertEqual(hints["candidatesNearBottom"], 1)

    def test_metadata_schema_and_config_hash(self) -> None:
        server._load_spatial()
        meta = server._metadata(1080, 2400)
        self.assertEqual(meta["schema"], "uniclaw.localVisionEvidence.v1")
        self.assertEqual(meta["width"], 1080)
        self.assertEqual(meta["height"], 2400)
        self.assertIn("pipeline", meta)
        self.assertIn("models", meta)
        self.assertRegex(meta["configHash"], r"^[0-9a-f]{64}$")
        self.assertEqual(len(meta["configHash"]), 64)


if __name__ == "__main__":
    unittest.main()
