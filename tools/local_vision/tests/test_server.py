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
# 8.5 regression: _run_pipeline 提取后 /v1/analyze 行为不变 —— 本类全部
# 用例即为回归验证（证据结构、scrollHints、Server-Timing 与 refactor 前一致）。

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


# ── POST /v1/analyze_raw（raw-rgba-screenshot-pipeline 8.1-8.6）──────────

class AnalyzeRawTests(unittest.TestCase):
    """/v1/analyze_raw：RGBA 原始缓冲直传（PIL frombytes 零解码）。"""

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
        """8.1: 合法 RGBA → 200 + 完整证据结构 + Server-Timing。"""
        img = Image.new("RGBA", (200, 400), (255, 0, 0, 255))
        resp = self._post_raw(img.tobytes(), img.width, img.height)

        self.assertEqual(resp.status_code, 200)
        data = resp.json()
        for key in ("candidates", "scrollHints", "metadata"):
            self.assertIn(key, data)
        self.assertGreater(len(data["candidates"]), 0)
        self.assertEqual(data["metadata"]["schema"],
                         "uniclaw.localVisionEvidence.v1")
        self.assertIn("Server-Timing", resp.headers)

    def test_analyze_raw_body_size_mismatch_returns_400(self) -> None:
        """8.2: body 长度 != w*h*4 → 400，detail 含尺寸与实际/期望字节数。"""
        w, h = 100, 200
        body = b"\x00" * (w * h * 4 - 10)  # 差 10 字节
        resp = self._post_raw(body, w, h)

        self.assertEqual(resp.status_code, 400)
        detail = resp.json()["detail"]
        self.assertIn("Body size mismatch", detail)
        self.assertIn(str(w), detail)
        self.assertIn(str(h), detail)

    def test_analyze_raw_unsupported_pixel_format_returns_400(self) -> None:
        """8.3: X-Image-Pixel-Format != 1 → 400 Unsupported pixel format。"""
        w, h = 10, 10
        body = b"\x00" * (w * h * 4)
        resp = self._post_raw(body, w, h, pixel_format="2")

        self.assertEqual(resp.status_code, 400)
        self.assertIn("Unsupported pixel format", resp.json()["detail"])

    def test_analyze_raw_vs_analyze_roundtrip(self) -> None:
        """8.6: 同一画面经 JPEG(/v1/analyze) 与 raw RGBA(/v1/analyze_raw)
        → 候选数量相等、center 坐标偏差 ≤ 0.002。

        preprocess 参数置恒等（crop=0、maxWidth 不小于宽度）：raw 路径由此
        与 /v1/analyze 处理完全相同的像素与几何，融合层归一化坐标系一致，
        center 才能对齐到 0.002 内（preprocess 本身的 crop/resize 由 8.4 覆盖）。
        """
        img = Image.new("RGB", (400, 800), (255, 255, 255))
        draw = ImageDraw.Draw(img)
        draw.rectangle([50, 50, 150, 100], fill=(0, 0, 0))  # 模拟 UI 元素
        draw.rectangle([50, 150, 200, 200], fill=(0, 0, 0))

        buf = BytesIO()
        img.save(buf, format="JPEG")
        jpeg_bytes = buf.getvalue()
        rgba_bytes = img.convert("RGBA").tobytes()

        with _patched_pipeline():
            with TestClient(server.app) as client:
                # 必须在 lifespan（_load_spatial 重写全局量）之后再打补丁，
                # 否则启动时会被 label-mapping.json 的值覆盖
                with mock.patch.object(server, "_CROP_TOP", 0.0), \
                     mock.patch.object(server, "_CROP_BOTTOM", 0.0), \
                     mock.patch.object(server, "_MAX_WIDTH", 4000):
                    resp_jpeg = client.post(
                        "/v1/analyze", content=jpeg_bytes,
                        headers={"Content-Type": "image/jpeg"})
                    resp_raw = client.post(
                        "/v1/analyze_raw", content=rgba_bytes,
                        headers={"Content-Type": "application/octet-stream",
                                 "X-Image-Width": str(img.width),
                                 "X-Image-Height": str(img.height)})

        self.assertEqual(resp_jpeg.status_code, 200)
        self.assertEqual(resp_raw.status_code, 200)

        jpeg_candidates = resp_jpeg.json().get("candidates", [])
        raw_candidates = resp_raw.json().get("candidates", [])
        self.assertEqual(
            len(jpeg_candidates), len(raw_candidates),
            f"Candidate count differs: JPEG={len(jpeg_candidates)}, "
            f"RAW={len(raw_candidates)}")
        for jc, rc in zip(jpeg_candidates, raw_candidates):
            self.assertAlmostEqual(jc["center"]["x"], rc["center"]["x"],
                                   delta=0.002)
            self.assertAlmostEqual(jc["center"]["y"], rc["center"]["y"],
                                   delta=0.002)


# ── _preprocess（8.4）─────────────────────────────────────

class PreprocessTests(unittest.TestCase):
    """8.4: _preprocess crop+resize 输出尺寸与 C# ImageResizer.ResizeToMaxWidth
    同参（tolerance: 1px）。默认参数：cropTop/cropBottom = 0.0625、
    maxWidth = 720（与 label-mapping.json 的 spatial.preprocessing 同值）。"""

    def test_preprocess_crop_and_resize(self) -> None:
        img = Image.new("RGBA", (1080, 2400), (255, 255, 255, 255))
        with mock.patch.object(server, "_CROP_TOP", 0.0625), \
             mock.patch.object(server, "_CROP_BOTTOM", 0.0625), \
             mock.patch.object(server, "_MAX_WIDTH", 720):
            result = server._preprocess(img)

        # crop: 上 150 + 下 150 → 1080×2100；resize: 720/1080 → 720×1400
        self.assertAlmostEqual(result[0].width, 720, delta=1)
        self.assertAlmostEqual(result[0].height, 1400, delta=1)

        # 变换参数：scale = 1080/720 = 1.5（原图→预处理图的倍率），top_px = 150，orig_h = 2400
        self.assertAlmostEqual(result[1], 1080 / 720, delta=1e-9)
        self.assertEqual(result[2], 150)
        self.assertEqual(result[3], 2400)

    def test_preprocess_no_resize_below_max_width(self) -> None:
        """宽度 ≤ maxWidth 不缩放（spec scenario: 仅 crop）。"""
        img = Image.new("RGBA", (400, 800), (255, 255, 255, 255))
        with mock.patch.object(server, "_CROP_TOP", 0.0625), \
             mock.patch.object(server, "_CROP_BOTTOM", 0.0625), \
             mock.patch.object(server, "_MAX_WIDTH", 720):
            result = server._preprocess(img)

        self.assertEqual(result[0].size, (400, 700))  # 400×800 → 上下各裁 50

        # 宽度 ≤ maxWidth 不缩放：scale = 1.0，仅记录 crop 偏移
        self.assertEqual(result[1], 1.0)
        self.assertEqual(result[2], 50)
        self.assertEqual(result[3], 800)


# ── _remap_coords（坐标回映全屏原图）────────────────────────

class RemapCoordsTests(unittest.TestCase):
    """缩放/crop 后所有输出坐标必须回映到原始全屏像素空间：
    x_orig = x_preproc * scale, y_orig = y_preproc * scale + top_px，
    归一化坐标基于原图宽高重算（用户约束：缩放不影响归一化准确性）。"""

    def _sample_evidence(self) -> dict:
        return {
            "candidates": [
                {
                    "label": "btn",
                    "boundsPx": [240, 300, 480, 420],   # 预处理空间
                    "bounds": {"x1": 240/720, "y1": 300/1400, "x2": 480/720, "y2": 420/1400},
                    "centerPx": [360, 360],
                    "center": {"x": 360/720, "y": 360/1400},
                    "coordinate": {"x": 360/720, "y": 360/1400},
                }
            ],
            "yolo": [{"label": "icon", "boundsPx": [0, 0, 72, 72]}],
            "ocr": [{"label": "text", "centerPx": [720, 1400]}],
            "image": {"width": 720, "height": 1400},
        }

    def test_remap_restores_fullscreen_pixel_and_normalized_coords(self) -> None:
        """scale=1.5、top_px=150（1080×2400 原图 → 720×1400 预处理图）：
        像素坐标回映全屏，归一化坐标 = 原图像素 / 原图宽高。"""
        ev = self._sample_evidence()
        server._remap_coords(ev, 1080 / 720, 150, 1080, 2400)

        c = ev["candidates"][0]
        # boundsPx: x*1.5, y*1.5+150 → [360, 600, 720, 780]
        self.assertEqual(c["boundsPx"], [360, 600, 720, 780])
        self.assertEqual(c["centerPx"], [540, 690])
        # 归一化 = 全屏像素 / 1080×2400（精度 6 位）
        self.assertEqual(c["bounds"], {"x1": round(360/1080, 6), "y1": round(600/2400, 6),
                                       "x2": round(720/1080, 6), "y2": round(780/2400, 6)})
        self.assertEqual(c["center"], {"x": round(540/1080, 6), "y": round(690/2400, 6)})
        self.assertEqual(c["coordinate"], {"x": round(540/1080, 6), "y": round(690/2400, 6)})
        # yolo / ocr 同样回映
        self.assertEqual(ev["yolo"][0]["boundsPx"], [0, 150, 108, 258])
        self.assertEqual(ev["ocr"][0]["centerPx"], [1080, 2250])
        # image 尺寸还原为原图
        self.assertEqual(ev["image"], {"width": 1080, "height": 2400})

    def test_remap_idempotent_when_no_transform(self) -> None:
        """scale=1.0 且 top_px=0 → 直接返回，evidence 原样。"""
        ev = self._sample_evidence()
        snapshot = json.dumps(ev, sort_keys=True)
        server._remap_coords(ev, 1.0, 0, 1080, 2400)
        self.assertEqual(json.dumps(ev, sort_keys=True), snapshot)

    def test_remap_only_crop_no_scale(self) -> None:
        """仅 crop（scale=1.0、top_px>0）：像素 x 不变，y 加偏移；归一化重算。"""
        ev = self._sample_evidence()
        server._remap_coords(ev, 1.0, 50, 400, 800)

        c = ev["candidates"][0]
        self.assertEqual(c["boundsPx"], [240, 350, 480, 470])
        self.assertEqual(c["centerPx"], [360, 410])
        self.assertEqual(c["center"], {"x": round(360/400, 6), "y": round(410/800, 6)})
        self.assertEqual(ev["image"], {"width": 400, "height": 800})


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
