"""FSV-001 V1（契约回归）+ 变体路径接线测试。

默认管道回归锚 = 三项既有资产（golden-run-v1/case-b-off、settings-home、
wifi-on）在实现前捕获的响应 JSON fixture（tests/fixtures/fsv001-default-
baseline/，见 repo 内该目录 README 的捕获记录）。比对语义：响应 JSON 与
基线**逐字节一致**，唯二例外 = metadata.pipelineRevision / deploymentId——
二者按 PER-007 R2 内容寻址实现模块源码哈希，引入 screenparse 行为模块必然
改变（configId 明确逐字节一致——由条件包含的 identity_content 保证）。

变体路径（X-Pipeline-Variant: fastscreen-integration）为 slow 冒烟（权重在
场）：走真实推理，断言 screenParse additive 键 + schema + yolo/ocr 结构不
变 + Server-Timing 分段。
"""
import json
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import uniclaw_perception.server as server  # noqa: E402
from uniclaw_perception import identity, pipeline  # noqa: E402
from uniclaw_perception.config import load as load_config  # noqa: E402
from uniclaw_perception.health import _model_id  # noqa: E402
from uniclaw_perception.pipeline import (  # noqa: E402
    ScreenParseParams,
    identity_content,
    lint_against_config,
    load_default,
    load_variants,
)

_TESTS = Path(__file__).resolve().parent
_FIXTURES = _TESTS / "fixtures" / "fsv001-default-baseline"
_PKG = _TESTS.parent

_ASSETS = {
    "case-b-off": _PKG / "evaluation/assets/captures/golden-run-v1/frames/case-b-off.png",
    "settings-home": _PKG / "evaluation/assets/captures/settings-home-api35-full.png",
    "wifi-on": _PKG / "evaluation/assets/captures/wifi-slice2-calibration/frames/wifi-on-emulator-5554.png",
}

_YOLO_WEIGHTS = _PKG / "models/yolo/android_ui_detection_yolov8/best.pt"
_SCREENPARSE_WEIGHTS = _PKG / "models/yolo/screenparser_v2/best.pt"


def _build_registry() -> tuple[pipeline.PipelineConfig, pipeline.PipelineConfig]:
    """lifespan 等价注入（bench/run_l2.py 先例 + server.lifespan OCR 配置）：
    cfg + default/variant 管道及四层身份；rapidocr 以受管 en rec 模型配置
    （与真实服务同构——否则 OCR confidence 与 served baseline 不一致）。
    返回 (default_config, variant_config)。"""
    cfg = load_config()
    server._config = cfg
    default_config = load_default()
    lint_against_config(default_config, cfg)
    variants = load_variants()
    variant_config = variants["fastscreen-integration"].config
    lint_against_config(variant_config, cfg)

    if cfg.ocr_backend == "rapidocr":
        from uniclaw_perception.ocr.rapid import (
            _rapid_ocr_kwargs, configure_ocr_models)
        _rapid_ocr_kwargs.update(configure_ocr_models(language=cfg.ocr_lang))

    revision = identity.compute_pipeline_revision()
    model_id = _model_id()
    registry: dict[str, tuple] = {}
    for key, cfg_ in (("default", default_config),
                      ("fastscreen-integration", variant_config)):
        variant_id = None if key == "default" else key
        config_id = identity.build_config_id(
            cfg, identity_content(cfg_, variant_id))
        deployment_id = identity.compute_deployment_id(
            "uniclaw.localVisionEvidence.v1", model_id, config_id,
            revision["pipelineRevision"])
        registry[key] = (cfg_, {
            "configId": config_id,
            "pipelineRevision": revision["pipelineRevision"],
            "deploymentId": deployment_id,
        })
    server._pipelines = registry
    return default_config, variant_config


def _identity_fields(body: dict) -> tuple[str, str]:
    meta = body["metadata"]
    return meta["pipelineRevision"], meta["deploymentId"]


class TestStagesAndConfig:
    def test_stages_dag(self):
        stages = {s["stage"]: s for s in pipeline.STAGES}
        sp = stages["screenparse"]
        assert sp["params"] is True
        assert sp["dependsOn"] == ["detect", "recognize"]
        assert stages["fuse"]["dependsOn"] == ["screenparse"]
        # 全序拓扑：preprocess → detect/recognize → screenparse → fuse → …
        names = [s["stage"] for s in pipeline.STAGES]
        assert names.index("preprocess") < names.index("detect")
        assert names.index("detect") < names.index("screenparse") < names.index("fuse")

    def test_screenparse_params_defaults_and_alias(self):
        params = ScreenParseParams()
        assert params.min_rescue_conf == 0.35
        assert params.rescue_iou_max == 0.30
        parsed = ScreenParseParams.model_validate(
            {"minRescueConf": 0.5, "rescueIouMax": 0.2})
        assert parsed.min_rescue_conf == 0.5 and parsed.rescue_iou_max == 0.2

    @pytest.mark.parametrize("bad", [
        {"screenparse": {"unknownKnob": 1}},
        {"screenparse": {"rescueIouMax": 1.5}},
        {"screenparse": {"rescueIouMax": 0.0}},
        {"screenparse": {"minRescueConf": -1.0, "rescueIouMax": 0.3}},
    ])
    def test_bad_screenparse_fail_closed(self, bad):
        data = {"schemaVersion": 1, **bad}
        with pytest.raises(pipeline.PipelineValidationError):
            pipeline.parse_pipeline_config(data)

    def test_variant_file_loaded(self):
        variants = load_variants()
        variant = variants["fastscreen-integration"]
        assert variant.variant_id == "fastscreen-integration"
        assert variant.config.screenparse is not None
        assert variant.config.screenparse.min_rescue_conf == 0.35
        assert variant.config.screenparse.rescue_iou_max == 0.30

    def test_identity_content_conditional(self):
        """默认管道 identity dict 与引入前结构一致（无 screenparse 键）→
        configId 回归锚成立；变体带 screenparse 段 → configId 差异。"""
        default_config = load_default()
        legacy = {
            "schemaVersion": 1,
            "detect": "torch-yolo",
            "recognize": "derived",
            "fuse": {
                "interactiveExtraLabels": ["text_block", "text"],
                "promoteUnmatchedOcr": True,
                "stabilize": True,
                "maxOcrDistanceRatio": 0.055,
            },
            "variantId": None,
        }
        assert identity_content(default_config, None) == legacy
        variants = load_variants()
        variant = variants["fastscreen-integration"].config
        variant_content = identity_content(variant, "fastscreen-integration")
        assert "screenparse" in variant_content
        assert variant_content["screenparse"] == {
            "minRescueConf": 0.35, "rescueIouMax": 0.30}
        assert identity.canonical_hash(identity_content(default_config, None)) != \
            identity.canonical_hash(variant_content)

    def test_default_config_id_unchanged(self):
        """默认 configId 与实现前基线一致（逐字节）。"""
        cfg = load_config()
        config_id = identity.build_config_id(
            cfg, identity_content(load_default(), None))
        baseline = json.loads(
            (_FIXTURES / "settings-home.json").read_text(encoding="utf-8"))
        assert config_id == baseline["metadata"]["configId"]


class TestServerTiming:
    def test_legacy_string_byte_identical(self):
        legacy = (f"yolo;dur={1.0:.1f}, ocr;dur={2.0:.1f}, "
                  f"fusion;dur={3.0:.1f}, scroll;dur={4.0:.1f}, "
                  f"serialize;dur={5.0:.1f}, gc;dur={6.0:.1f}")
        assert server._server_timing(1.0, 2.0, 3.0, 4.0, 5.0, 6.0) == legacy
        assert server._server_timing(1.0, 2.0, 3.0, 4.0, 5.0, 6.0,
                                     screenparse_ms=None) == legacy

    def test_screenparse_segment_position(self):
        timing = server._server_timing(1.0, 2.0, 3.0, 4.0, 5.0, 6.0,
                                       screenparse_ms=7.0)
        assert timing.startswith("yolo;dur=1.0, ocr;dur=2.0, "
                                 "screenparse;dur=7.0, fusion;dur=3.0")
        assert "screenparse" in timing


# ── 默认管道字节等价回归（V1；需 YOLO+OCR 权重）─────────────────────────

@pytest.mark.skipif(not _YOLO_WEIGHTS.exists(),
                    reason="YOLO 权重不在场")
class TestDefaultByteRegression:
    @pytest.mark.parametrize("name", ["case-b-off", "settings-home", "wifi-on"])
    def test_default_response_byte_equal(self, name):
        from PIL import Image
        default_config, _ = _build_registry()
        fixture = json.loads(
            (_FIXTURES / f"{name}.json").read_text(encoding="utf-8"))
        img = Image.open(_ASSETS[name]).convert("RGB")
        w, h = img.size
        evidence, _ = server._run_pipeline(
            img, w, h, pipeline=default_config, pipeline_key="default")
        body = json.dumps(evidence, ensure_ascii=False)
        fixture_body = json.dumps(fixture, ensure_ascii=False)

        # configId 必须逐字节一致（回归锚）
        assert evidence["metadata"]["configId"] == fixture["metadata"]["configId"]
        # 唯一允许差异 = 内容寻址实现源码的 pipelineRevision/deploymentId
        assert body != fixture_body
        new_ids = _identity_fields(evidence)
        old_ids = _identity_fields(fixture)
        assert new_ids != old_ids
        ev = json.loads(body)
        fixture_clone = json.loads(fixture_body)
        for key in ("pipelineRevision", "deploymentId"):
            ev["metadata"].pop(key)
            fixture_clone["metadata"].pop(key)
        assert json.dumps(ev, ensure_ascii=False) == \
            json.dumps(fixture_clone, ensure_ascii=False), name


# ── 变体路径（slow：真实推理；权重在场）───────────────────────────────

@pytest.mark.skipif(not (_YOLO_WEIGHTS.exists() and _SCREENPARSE_WEIGHTS.exists()),
                    reason="YOLO/ScreenParser 权重不在场（FSV-001 D1 不入 git）")
class TestVariantPath:
    def test_variant_response_screenparse_schema(self):
        from PIL import Image
        from uniclaw_perception.screenparse import (
            CANONICAL_VOCABULARY, SCREENPARSE_LABEL_MAPPING,
            STRUCTURAL_LABELS,
        )
        _, variant_config = _build_registry()
        img = Image.open(_ASSETS["settings-home"]).convert("RGB")
        w, h = img.size
        evidence, (t0, t1, t2, t3, t_sp) = server._run_pipeline(
            img, w, h, pipeline=variant_config,
            pipeline_key="fastscreen-integration")

        sp = evidence.get("screenParse")
        assert sp is not None, "变体响应必须含 screenParse additive 键"
        # schema：detections / structural / corroborations / summary
        assert {"detections", "structural", "corroborations", "summary"} \
            <= set(sp)
        assert sp["summary"]["rawCount"] == \
            sp["summary"]["mappedCount"] + sp["summary"]["structuralCount"]
        assert sp["summary"]["rescuedCount"] >= 0
        assert sp["summary"]["mappedCount"] > 0  # settings-home 屏有 UI 元素
        for d in sp["detections"]:
            assert d["id"].startswith("fs_")
            assert d["label"] in CANONICAL_VOCABULARY
            assert d["rawLabel"] in SCREENPARSE_LABEL_MAPPING
            assert isinstance(d["rawLabel"], str)
            assert 0.0 < d["confidence"] <= 1.0
            bounds = d["bounds"]
            assert 0.0 <= bounds["x1"] < bounds["x2"] <= 1.0
            assert 0.0 <= bounds["y1"] < bounds["y2"] <= 1.0
            assert len(d["boundsPx"]) == 4
        for s in sp["structural"]:
            assert s["rawLabel"] in STRUCTURAL_LABELS

        # yolo/ocr/candidates 结构不变（evolve=additive）
        for item in evidence["yolo"]:
            assert {"id", "label", "confidence", "bounds", "boundsPx",
                    "center", "centerPx"} <= set(item)
        assert {"text", "confidence", "bounds", "boundsPx"} <= \
            set(evidence["ocr"][0])
        assert isinstance(evidence["candidates"], list)
        assert evidence["summary"]["yoloCount"] >= \
            sp["summary"]["rescuedCount"]

        # Server-Timing 分段：screenparse 段在 fuse 之前、正值
        assert t_sp is not None
        assert (t_sp - t2) > 0
        assert (t3 - t_sp) >= 0
        timing = server._server_timing(
            yolo_ms=(t1 - t0) * 1000,
            ocr_ms=(t2 - t1) * 1000,
            fusion_ms=(t3 - t_sp) * 1000,
            scroll_ms=0.0,
            screenparse_ms=(t_sp - t2) * 1000,
        )
        assert "screenparse;dur=" in timing

    def test_variant_pipeline_for(self):
        _build_registry()
        key, config = server._pipeline_for("fastscreen-integration")
        assert key == "fastscreen-integration"
        assert config.screenparse is not None
        key2, _ = server._pipeline_for(None)
        assert key2 == "default"

    def test_default_zero_effect_path_unchanged(self):
        """默认管道（无 screenparse 段）：_run_pipeline 不加载 ScreenParser、
        不加 screenParse 键、t_sp = None（代码路径零效果）。"""
        from PIL import Image
        default_config, _ = _build_registry()
        img = Image.open(_ASSETS["wifi-on"]).convert("RGB")
        w, h = img.size
        evidence, (_, _, _, _, t_sp) = server._run_pipeline(
            img, w, h, pipeline=default_config, pipeline_key="default")
        assert "screenParse" not in evidence
        assert t_sp is None


# ── WI-6（D11）：整屏输入域修正对拍（slow：真实推理）────────────────────

@pytest.mark.skipif(not (_YOLO_WEIGHTS.exists() and _SCREENPARSE_WEIGHTS.exists()),
                    reason="YOLO/ScreenParser 权重不在场（FSV-001 D1 不入 git）")
class TestVariantPathW16:
    """输入域修正生效的直接证据（state.md D11）：变体路径摄原图（整屏
    operating point），检出数 ≥ 修正前（proc 输入）口径；坐标逆映射回 proc
    空间后参与 rescue/序列化；summary 带 inputSpace/droppedOffCanvas。"""

    def _proc_image(self, img):
        from uniclaw_perception.preprocessing import preprocess
        cfg = load_config()
        proc_img, _, _, _ = preprocess(
            img, max_width=cfg.max_width, crop_top_ratio=cfg.crop_top,
            crop_bottom_ratio=cfg.crop_bottom)
        return proc_img

    def test_fullscreen_raw_detects_at_least_proc(self):
        """整屏输入检出数 ≥ 预处理输入（provider 层直接对拍：settings-home）。"""
        from PIL import Image
        from uniclaw_perception.screenparse import run_screenparse_on_image
        img = Image.open(_ASSETS["settings-home"]).convert("RGB")
        full = run_screenparse_on_image(img)
        proc = run_screenparse_on_image(self._proc_image(img))
        assert len(full) >= len(proc), (
            f"整屏 {len(full)} 应 ≥ 预处理 {len(proc)}（D11 修正方向）")

    def test_variant_response_input_space_and_counts(self):
        """变体响应：summary.inputSpace="original"；整屏总检出（rawCount +
        droppedOffCanvas）≥ 修正前 proc 口径检出；droppedOffCanvas ≥ 0。"""
        from PIL import Image
        from uniclaw_perception.screenparse import run_screenparse_on_image
        _, variant_config = _build_registry()
        img = Image.open(_ASSETS["settings-home"]).convert("RGB")
        w, h = img.size
        evidence, _ = server._run_pipeline(
            img, w, h, pipeline=variant_config,
            pipeline_key="fastscreen-integration")
        sp = evidence["screenParse"]
        assert sp["summary"]["inputSpace"] == "original"
        assert sp["summary"]["droppedOffCanvas"] >= 0
        total_full = (sp["summary"]["rawCount"]
                      + sp["summary"]["droppedOffCanvas"])
        proc_dets = run_screenparse_on_image(self._proc_image(img))
        assert total_full >= len(proc_dets), (
            f"整屏总检出 {total_full} 应 ≥ 修正前 proc 口径 {len(proc_dets)}")
        # 保留元素坐标必须落在 proc 画布内（fail-closed 后置保证）
        proc_img = self._proc_image(img)
        proc_w, proc_h = proc_img.size
        for d in sp["detections"]:
            x1, y1, x2, y2 = d["boundsPx"]
            assert 0 <= x1 < x2 <= proc_w and 0 <= y1 < y2 <= proc_h
        assert sp["summary"]["rawCount"] == \
            sp["summary"]["mappedCount"] + sp["summary"]["structuralCount"]

    def test_integration_two_runs_equal(self):
        """确定性：同图两次 integration 变体响应逐字段相等（WI-6 对拍要求）。"""
        from PIL import Image
        _, variant_config = _build_registry()
        img = Image.open(_ASSETS["settings-home"]).convert("RGB")
        w, h = img.size
        first, _ = server._run_pipeline(
            img, w, h, pipeline=variant_config,
            pipeline_key="fastscreen-integration")
        second, _ = server._run_pipeline(
            img, w, h, pipeline=variant_config,
            pipeline_key="fastscreen-integration")
        assert json.dumps(first, ensure_ascii=False) == \
            json.dumps(second, ensure_ascii=False)