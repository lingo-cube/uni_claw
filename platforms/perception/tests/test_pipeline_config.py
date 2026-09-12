"""PER-008 A2/A3：管道配置接线（确定性，不依赖帧内容运气）+ fail-closed。

A2 的行为变化不能靠「跑一帧看输出差异」验证——knob 是否改变输出取决于
帧内容（如该帧无 unmatched OCR 时 promote_unmatched_ocr 无效果）。接线
证明用 monkeypatch 捕获 fuse_evidence 实收 kwargs。
"""
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from uniclaw_perception import pipeline  # noqa: E402
from uniclaw_perception.config import PerceptionConfig  # noqa: E402


class _Captured(Exception):
    def __init__(self, kwargs):
        self.kwargs = kwargs
        super().__init__("captured")


def _run_with_capture(monkeypatch, fuse_params):
    import uniclaw_perception.server as server

    def fake_fuse(detections, ocr_tokens, **kwargs):
        raise _Captured(kwargs)

    monkeypatch.setattr(server, "fuse_evidence", fake_fuse)
    # detect/recognize stub：接线测试只针对 fuse 调用点，不加载模型
    monkeypatch.setattr(server, "run_yolo_on_image", lambda img, device="cpu": [])
    monkeypatch.setattr(server, "run_rapid_ocr_on_image",
                        lambda img, text_score=0.5: [])
    from PIL import Image
    image = Image.new("RGB", (64, 64))

    config = pipeline.PipelineConfig.model_validate(
        {"schemaVersion": 1, "fuse": fuse_params})
    cfg = PerceptionConfig()
    monkeypatch.setattr(server, "_config", cfg)
    monkeypatch.setattr(server, "_pipelines",
                        {"default": (config, {})})
    with pytest.raises(_Captured) as captured:
        server._run_pipeline(image, 64, 64, pipeline=config)
    return captured.value.kwargs


class TestWiring:
    def test_default_equals_historical_hardcoded(self, monkeypatch):
        """A1 前提：默认配置的 fuse kwargs == server.py 历史硬编码值。"""
        kwargs = _run_with_capture(monkeypatch, {})
        assert kwargs["promote_unmatched_ocr"] is True
        assert kwargs["stabilize"] is True
        assert kwargs["max_ocr_distance_ratio"] == 0.055
        assert kwargs["interactive_labels"] >= {"text_block", "text"}

    def test_variant_params_reach_engine(self, monkeypatch):
        """A2 接线：变体参数确实传到 fuse_evidence（与默认可区分）。"""
        kwargs = _run_with_capture(monkeypatch, {
            "promoteUnmatchedOcr": False,
            "maxOcrDistanceRatio": 0.02,
            "interactiveExtraLabels": ["text_block"],
        })
        assert kwargs["promote_unmatched_ocr"] is False
        assert kwargs["max_ocr_distance_ratio"] == 0.02
        assert "text" not in kwargs["interactive_labels"]


class TestFailClosed:
    @pytest.mark.parametrize("bad", [
        {"schemaVersion": 1, "fuse": {"unknownKnob": 1}},
        {"schemaVersion": 1, "detect": {"impl": "coreml"}},
        {"schemaVersion": 1, "recognize": {"impl": "nonexistent"}},
        {"schemaVersion": 2},
    ])
    def test_unknown_rejected(self, bad):
        with pytest.raises(pipeline.PipelineValidationError):
            pipeline.parse_pipeline_config(bad)

    def test_recognize_impl_must_match_config(self):
        config = pipeline.parse_pipeline_config(
            {"schemaVersion": 1, "recognize": {"impl": "paddle-crops"}})
        cfg = PerceptionConfig()  # 默认 rapidocr/full → rapidocr-full
        with pytest.raises(pipeline.PipelineValidationError):
            pipeline.lint_against_config(config, cfg)
