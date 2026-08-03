from __future__ import annotations

import os
import sys
import tempfile
import types
import unittest
from pathlib import Path
from unittest import mock

import numpy as np
from PIL import Image

from tools.local_vision import backends, fusion
from tools.local_vision.schema import Box, Detection, OcrToken


# ── fakes ──────────────────────────────────────────────────

class _FakeBox:
    """模拟 ultralytics box：xyxy/cls/conf 支持 .tolist()/.item()。"""

    def __init__(self, xyxy: list[float], cls: int, conf: float) -> None:
        self.xyxy = [np.array(xyxy, dtype=float)]
        self.cls = [np.array([cls], dtype=int)]
        self.conf = [np.array([conf], dtype=float)]


class _FakeResult:
    def __init__(self, boxes, names: dict[int, str]) -> None:
        self.boxes = boxes
        self.names = names


class _FakeYOLO:
    """模拟 ultralytics.YOLO：记录加载路径，predict 返回固定结果。"""

    loaded_paths: list[str] = []

    def __init__(self, model_path: str) -> None:
        _FakeYOLO.loaded_paths.append(model_path)
        self.model_path = model_path

    def predict(self, **kwargs) -> list[_FakeResult]:
        return [
            _FakeResult([_FakeBox([10, 20, 100, 80], 0, 0.93)], {0: "button"}),
            _FakeResult(None, {}),
        ]


class _FakeOcr:
    """模拟 PaddleOCR：接受 ndarray 或 str，返回 paddle 风格原始结果。"""

    def __init__(self, text: str = "Display", conf: float = 0.95,
                 reject_ndarray: bool = False) -> None:
        self.text = text
        self.conf = conf
        self.reject_ndarray = reject_ndarray
        self.calls: list[tuple[str, object]] = []

    def ocr(self, source, cls: bool = True) -> list[list[list[object]]]:
        self.calls.append(("ocr", source))
        if self.reject_ndarray and isinstance(source, np.ndarray):
            raise TypeError("ndarray not supported")
        # paddleocr 原始格式: [page] → [line...] → [box_polygon, (text, score)]
        return [[
            [
                [[0, 0], [30, 0], [30, 20], [0, 20]],
                (self.text, self.conf),
            ],
        ]]

    def predict(self, source) -> list[list[list[object]]]:
        self.calls.append(("predict", source))
        return self.ocr(source, cls=True)


def _simple_image() -> Image.Image:
    return Image.new("RGB", (400, 800), (255, 255, 255))


def _tracking_named_temp(created: list[str]):
    real = tempfile.NamedTemporaryFile

    def _wrap(*args, **kwargs):
        f = real(*args, **kwargs)
        created.append(f.name)
        return f

    return _wrap


# ── backends: YOLO 内存推理（V20）─────────────────────────

class RunYoloOnImageTests(unittest.TestCase):
    def setUp(self) -> None:
        backends._yolo_model_cache.clear()
        fake_ultralytics = types.ModuleType("ultralytics")
        fake_ultralytics.YOLO = _FakeYOLO
        self._sys_modules_patch = mock.patch.dict(
            sys.modules, {"ultralytics": fake_ultralytics})
        self._sys_modules_patch.start()

    def tearDown(self) -> None:
        self._sys_modules_patch.stop()
        backends._yolo_model_cache.clear()

    def test_run_yolo_on_image_returns_valid_detections(self) -> None:
        detections = backends.run_yolo_on_image(
            _simple_image(), model_path="yolo11n.pt",
            image_size=640, confidence=0.35, device="cpu")

        self.assertEqual(len(detections), 1)
        det = detections[0]
        self.assertEqual(det.id, "det_1")
        self.assertEqual(det.label, "button")  # normalize_yolo_label 生效
        self.assertAlmostEqual(det.confidence, 0.93)
        self.assertEqual((det.box.x1, det.box.y1, det.box.x2, det.box.y2),
                         (10.0, 20.0, 100.0, 80.0))

    def test_model_cached_across_calls(self) -> None:
        _FakeYOLO.loaded_paths = []
        image = _simple_image()
        backends.run_yolo_on_image(image, model_path="m.pt",
                                   image_size=640, confidence=0.35, device="cpu")
        backends.run_yolo_on_image(image, model_path="m.pt",
                                   image_size=640, confidence=0.35, device="cpu")
        backends.run_yolo_on_image(image, model_path="other.pt",
                                   image_size=640, confidence=0.35, device="cpu")

        # 同一 model_path 只加载一次；不同路径各一次
        self.assertEqual(_FakeYOLO.loaded_paths, ["m.pt", "other.pt"])


# ── backends: ROI 裁剪 + 多线程 OCR（V14 / V21）────────────

class RunOcrOnCropsTests(unittest.TestCase):
    def setUp(self) -> None:
        backends.configure_roi_padding({})

    def tearDown(self) -> None:
        backends.configure_roi_padding({})

    def test_aligned_output_and_offset_coordinates(self) -> None:
        image = _simple_image()
        detections = [
            Detection(id="det_1", label="button", confidence=0.9,
                      box=Box(50, 100, 150, 150)),
            Detection(id="det_2", label="icon", confidence=0.9,
                      box=Box(50, 300, 150, 350)),
            Detection(id="det_3", label="switch", confidence=0.9,
                      box=Box(50, 500, 150, 550)),
        ]
        fake = _FakeOcr()
        with mock.patch.object(backends, "_get_ocr", return_value=fake):
            results = backends.run_ocr_on_crops(image, detections, language="ch")

        # 3 个 detection → 3 个 token 列表，逐位对齐
        self.assertEqual(len(results), 3)
        self.assertEqual(len(results[0]), 1)
        self.assertEqual(results[1][0].text, "Display")
        self.assertEqual(results[2][0].text, "Display")

        # crop 坐标系 (0,0)-(30,20) + 裁剪偏移 (50,100) → 原图 (50,100)-(80,120)
        token = results[0][0]
        self.assertEqual((token.box.x1, token.box.y1, token.box.x2, token.box.y2),
                         (50.0, 100.0, 80.0, 120.0))

        # ndarray 直传（RGB→BGR 翻转后的 numpy 数组），零临时文件
        self.assertIsInstance(fake.calls[0][1], np.ndarray)

    def test_empty_detections_returns_empty_list(self) -> None:
        self.assertEqual(backends.run_ocr_on_crops(_simple_image(), []), [])

    def test_null_crop_returns_empty_slot(self) -> None:
        image = _simple_image()  # 400x800
        detections = [
            Detection(id="det_1", label="button", confidence=0.9,
                      box=Box(50, 100, 150, 150)),
            # 完全在图像外的框 → 裁剪为 None → 对应槽位 []
            Detection(id="det_out", label="button", confidence=0.9,
                      box=Box(1000, 1000, 1100, 1100)),
        ]
        fake = _FakeOcr()
        with mock.patch.object(backends, "_get_ocr", return_value=fake):
            results = backends.run_ocr_on_crops(image, detections, language="ch")
        self.assertEqual(len(results), 2)
        self.assertEqual(len(results[0]), 1)
        self.assertEqual(results[1], [])

    def test_ndarray_path_creates_no_temp_files(self) -> None:
        image = _simple_image()
        detections = [Detection(id="det_1", label="button", confidence=0.9,
                                box=Box(50, 100, 150, 150))]
        fake = _FakeOcr()
        created: list[str] = []
        with mock.patch.object(backends, "_get_ocr", return_value=fake), \
             mock.patch.object(tempfile, "NamedTemporaryFile",
                               _tracking_named_temp(created)):
            backends.run_ocr_on_crops(image, detections, language="ch")

        self.assertEqual(created, [])  # ndarray 直传 → 无临时文件

    def test_file_fallback_for_incompatible_paddleocr(self) -> None:
        image = _simple_image()
        detections = [Detection(id="det_1", label="button", confidence=0.9,
                                box=Box(50, 100, 150, 150))]
        fake = _FakeOcr(reject_ndarray=True)
        created: list[str] = []
        with mock.patch.object(backends, "_get_ocr", return_value=fake), \
             mock.patch.object(tempfile, "NamedTemporaryFile",
                               _tracking_named_temp(created)):
            results = backends.run_ocr_on_crops(image, detections, language="ch")

        self.assertEqual(len(results[0]), 1)  # fallback 仍返回有效结果
        self.assertEqual(len(created), 1)  # 创建了一次临时 PNG
        self.assertFalse(os.path.exists(created[0]))  # 已清理

    def test_call_paddle_ocr_normalizes_path_and_passes_ndarray(self) -> None:
        ocr = mock.Mock()
        ocr.ocr.side_effect = [TypeError("no cls kwarg"), "raw-result"]
        result = backends._call_paddle_ocr(ocr, Path("/tmp/x.png"))
        self.assertEqual(result, "raw-result")
        # Path → str 归一化后才传给 ocr.ocr()
        self.assertEqual(ocr.ocr.call_args_list[0][0][0], "/tmp/x.png")

        arr = np.zeros((10, 10, 3), dtype=np.uint8)
        ocr.ocr.side_effect = ["raw-nd"]
        result2 = backends._call_paddle_ocr(ocr, arr)
        self.assertEqual(result2, "raw-nd")
        self.assertIs(ocr.ocr.call_args_list[-1][0][0], arr)  # ndarray 原样直传


# ── backends: ROI padding 配置（R-14）─────────────────────

class RoiPaddingPxTests(unittest.TestCase):
    def setUp(self) -> None:
        backends.configure_roi_padding({})

    def tearDown(self) -> None:
        backends.configure_roi_padding({})

    def test_computes_from_config(self) -> None:
        backends.configure_roi_padding(
            {"x": 0.15, "y": 0.10, "minPx": 8, "maxPx": 64})
        try:
            # max(0.15*100, 0.10*50, 8) = 15
            self.assertEqual(backends._roi_padding_px(100, 50), 15)
            # 小框 → minPx 下限生效
            self.assertEqual(backends._roi_padding_px(10, 10), 8)
            # 大框 → maxPx 上限生效
            self.assertEqual(backends._roi_padding_px(1000, 800), 64)
        finally:
            backends.configure_roi_padding({})

    def test_defaults_when_spec_empty(self) -> None:
        self.assertEqual(backends._roi_padding_px(100, 50), 15)
        self.assertEqual(backends._roi_padding_px(10, 10), 8)
        self.assertEqual(backends._roi_padding_px(1000, 800), 64)


# ── fusion: fuse_evidence_from_crops（V15 / V27 / V11）────

class FuseEvidenceFromCropsTests(unittest.TestCase):
    def test_direct_association_no_spatial_matching(self) -> None:
        detections = [
            Detection(id="det_1", label="list_item", confidence=0.9,
                      box=Box(20, 100, 300, 180)),
            # "image" 不在行控件集合（icon/switch/toggle/checkbox）内，
            # 不触发 chevron 同行重分类，type 保持 YOLO label
            Detection(id="det_2", label="image", confidence=0.8,
                      box=Box(300, 100, 330, 140)),
            Detection(id="det_3", label="switch", confidence=0.85,
                      box=Box(20, 200, 300, 240)),
        ]
        crops_ocr = [
            [OcrToken(id="ocr_1", text="Display", confidence=0.91,
                      box=Box(42, 118, 130, 150))],
            [],
            [OcrToken(id="ocr_2", text="On", confidence=0.9,
                      box=Box(30, 205, 60, 225))],
        ]
        evidence = fusion.fuse_evidence_from_crops(
            detections, crops_ocr, image_width=400, image_height=800)

        # 候选数 == 检测数
        self.assertEqual(evidence["summary"]["candidateCount"], 3)
        self.assertEqual(len(evidence["candidates"]), 3)
        # 直接 zip 关联：det_1 ↔ ocr_1
        c0 = evidence["candidates"][0]
        self.assertEqual(c0["type"], "list_item")
        self.assertEqual(c0["text"], "Display")
        self.assertEqual(c0["evidence"]["yoloId"], "det_1")
        self.assertEqual(c0["evidence"]["ocrIds"], ["ocr_1"])

    def test_promote_unmatched_ocr_false_blocks_promotion(self) -> None:
        detections = [
            Detection(id="det_1", label="button", confidence=0.9,
                      box=Box(20, 100, 300, 180)),
        ]
        crops_ocr = [
            [OcrToken(id="ocr_1", text="Save", confidence=0.95,
                      box=Box(30, 110, 120, 140))],
        ]
        evidence = fusion.fuse_evidence_from_crops(
            detections, crops_ocr, image_width=400, image_height=800,
            promote_unmatched_ocr=False)

        # OCR-only token 不提升为 text_block candidate
        self.assertEqual(len(evidence["candidates"]), 1)
        for c in evidence["candidates"]:
            self.assertNotEqual(c["type"], "text_block")
            self.assertNotIn("ocr_only", c["riskFlags"])

    def test_evidence_schema_fields_present(self) -> None:
        detections = [
            Detection(id="det_1", label="button", confidence=0.9,
                      box=Box(20, 100, 300, 180)),
        ]
        crops_ocr = [
            [OcrToken(id="ocr_1", text="OK", confidence=0.95,
                      box=Box(30, 110, 120, 140))],
        ]
        evidence = fusion.fuse_evidence_from_crops(
            detections, crops_ocr, image_width=400, image_height=800)

        for key in ("image", "yolo", "ocr", "candidates", "summary"):
            self.assertIn(key, evidence)
        candidate = evidence["candidates"][0]
        for key in ("id", "type", "text", "confidence", "confidenceDetail",
                    "bounds", "boundsPx", "center", "centerPx",
                    "evidence", "riskFlags"):
            self.assertIn(key, candidate)
        # R-7: confidenceDetail.yolo / confidenceDetail.ocr
        self.assertAlmostEqual(candidate["confidenceDetail"]["yolo"], 0.9)
        self.assertAlmostEqual(candidate["confidenceDetail"]["ocr"], 0.95)
        # 无 token 时 confidenceDetail.ocr 为 None
        no_text = fusion.fuse_evidence_from_crops(
            detections, [[]], image_width=400, image_height=800)
        self.assertIsNone(no_text["candidates"][0]["confidenceDetail"]["ocr"])

    def test_chevron_heuristic_preserved(self) -> None:
        # 同行 text_block + icon → text_block 重分类为 menu_item
        detections = [
            Detection(id="det_1", label="icon", confidence=0.9,
                      box=Box(300, 100, 330, 140)),
            Detection(id="det_2", label="text_block", confidence=0.9,
                      box=Box(20, 100, 290, 140)),
        ]
        crops_ocr = [
            [],
            [OcrToken(id="ocr_1", text="Settings", confidence=0.9,
                      box=Box(30, 105, 200, 135))],
        ]
        evidence = fusion.fuse_evidence_from_crops(
            detections, crops_ocr, image_width=400, image_height=800)

        c = evidence["candidates"][1]
        self.assertEqual(c["type"], "menu_item")
        self.assertEqual(c["evidence"]["typeInferred"], "row_alignment")


if __name__ == "__main__":
    unittest.main()
