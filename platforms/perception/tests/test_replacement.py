"""FSV-001 WI-3 — Replacement Test（Test B）接线测试（FSV-001 D3/D8）。

覆盖：
- impl 注册表 lint：screenparser / screenparser-mps 可声明 + mps 可用性探测
  fail-closed（照 torch-mps 语义）+ 未知 impl 仍 fail-closed + 与集成段互斥；
  变体文件可加载。
- B1（变体 fastscreen-replacement，真实权重）：case-b-off 上响应与 baseline
  同构（yolo/ocr/candidates/summary/metadata/scrollHints + additive
  screenParse[]）；yolo[] 条目 schema 与 baseline 相同；label ∈ canonical
  词汇；OCR retained（ocrCount == baseline）；确定性（同图两次逐字段相等）。
- B2（bench/run_replacement_ablation.py，子进程）：ocr 恒空、candidates 全部
  text==""，yolo[]/screenParse[] 与 B1 同源（同图同 device 一致）。
- 默认回归锚复用在 test_screenparse_integration.py（TestDefaultByteRegression）
  ——本文件不重复。
"""
import json
import subprocess
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import uniclaw_perception.server as server  # noqa: E402
from uniclaw_perception import identity, pipeline  # noqa: E402
from uniclaw_perception.config import load as load_config  # noqa: E402
from uniclaw_perception.health import _model_id  # noqa: E402
from uniclaw_perception.pipeline import (  # noqa: E402
    identity_content,
    lint_against_config,
    load_default,
    load_variants,
)
from uniclaw_perception.screenparse import (  # noqa: E402
    CANONICAL_VOCABULARY,
    SCREENPARSE_LABEL_MAPPING,
    STRUCTURAL_LABELS,
)

_TESTS = Path(__file__).resolve().parent
_FIXTURES = _TESTS / "fixtures" / "fsv001-default-baseline"
_PKG = _TESTS.parent

_ASSETS = {
    "case-b-off": _PKG / "evaluation/assets/captures/golden-run-v1/frames/case-b-off.png",
    "settings-home": _PKG / "evaluation/assets/captures/settings-home-api35-full.png",
}

_YOLO_WEIGHTS = _PKG / "models/yolo/android_ui_detection_yolov8/best.pt"
_SCREENPARSE_WEIGHTS = _PKG / "models/yolo/screenparser_v2/best.pt"

_BENCH_SCRIPT = _PKG / "bench" / "run_replacement_ablation.py"

#: baseline yolo[] 条目 schema（fixture 实捕；B1 必须逐键一致——replacement 仍
#: 是 detect，证据 schema 冻结，见 changes/FSV-001/state.md D8）。
_BASELINE_YOLO_ITEM_KEYS = {"id", "label", "confidence", "bounds", "boundsPx",
                            "center", "centerPx"}


def _build_registry() -> tuple[pipeline.PipelineConfig, pipeline.PipelineConfig]:
    """lifespan 等价注入（照 test_screenparse_integration._build_registry）：
    cfg + default/replacement 管道及四层身份；rapidocr 以受管 en rec 模型配置
    （否则 OCR confidence 与 served baseline 不一致）。"""
    cfg = load_config()
    server._config = cfg
    default_config = load_default()
    lint_against_config(default_config, cfg)
    variants = load_variants()
    variant_config = variants["fastscreen-replacement"].config
    lint_against_config(variant_config, cfg)

    if cfg.ocr_backend == "rapidocr":
        from uniclaw_perception.ocr.rapid import (
            _rapid_ocr_kwargs, configure_ocr_models)
        _rapid_ocr_kwargs.update(configure_ocr_models(language=cfg.ocr_lang))

    revision = identity.compute_pipeline_revision()
    model_id = _model_id()
    registry: dict[str, tuple] = {}
    for key, cfg_ in (("default", default_config),
                      ("fastscreen-replacement", variant_config)):
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


def _run_b1(name: str, *, variant_config: pipeline.PipelineConfig) -> dict:
    from PIL import Image
    img = Image.open(_ASSETS[name]).convert("RGB")
    w, h = img.size
    evidence, _ = server._run_pipeline(
        img, w, h, pipeline=variant_config,
        pipeline_key="fastscreen-replacement")
    return evidence


# ── impl 注册表 lint（确定性，不依赖帧/权重）─────────────────────────────

class TestImplRegistryLint:
    def test_screenparser_declarable(self):
        config = pipeline.parse_pipeline_config(
            {"schemaVersion": 1, "detect": {"impl": "screenparser"}})
        cfg = load_config()
        lint_against_config(config, cfg)  # screenparser → cpu，无需 MPS
        assert pipeline.detect_device(config) == "cpu"

    def test_screenparser_mps_declarable_and_device(self, monkeypatch):
        monkeypatch.setattr(pipeline, "_mps_available", lambda: True)
        config = pipeline.parse_pipeline_config(
            {"schemaVersion": 1, "detect": {"impl": "screenparser-mps"}})
        lint_against_config(config, load_config())
        assert pipeline.detect_device(config) == "mps"

    def test_unknown_impl_still_fail_closed(self):
        with pytest.raises(pipeline.PipelineValidationError):
            pipeline.parse_pipeline_config(
                {"schemaVersion": 1, "detect": {"impl": "screenvision"}})

    @pytest.mark.parametrize("impl", ["torch-mps", "screenparser-mps"])
    def test_mps_unavailable_fail_closed(self, monkeypatch, impl):
        """mps 系 impl 可用性探测：MPS 不可用 → fail-closed（照 torch-mps 现有
        语义，不静默回退 CPU——见 changes/FSV-001/state.md D3）。"""
        monkeypatch.setattr(pipeline, "_mps_available", lambda: False)
        config = pipeline.parse_pipeline_config(
            {"schemaVersion": 1, "detect": {"impl": impl}})
        with pytest.raises(pipeline.PipelineValidationError):
            lint_against_config(config, load_config())

    def test_screenparser_excludes_integration_section(self):
        """D3：replacement（detect=screenparser 系）与集成段互斥——Test B 下
        FastScreen 即 detect，不叠加 Test A 的救援/对照语义。"""
        config = pipeline.parse_pipeline_config({
            "schemaVersion": 1,
            "detect": {"impl": "screenparser"},
            "screenparse": {"minRescueConf": 0.35, "rescueIouMax": 0.30},
        })
        with pytest.raises(pipeline.PipelineValidationError):
            lint_against_config(config, load_config())

    def test_variant_files_loadable(self):
        variants = load_variants()
        for name, impl in (("fastscreen-replacement", "screenparser"),
                           ("fastscreen-replacement-mps", "screenparser-mps")):
            variant = variants[name]
            assert variant.variant_id == name
            assert variant.config.detect is not None
            assert variant.config.detect.impl == impl
            assert variant.config.screenparse is None  # 无集成段


# ── B1（slow：真实权重；权重在场）────────────────────────────────────────

@pytest.mark.skipif(not (_YOLO_WEIGHTS.exists() and _SCREENPARSE_WEIGHTS.exists()),
                    reason="YOLO/ScreenParser 权重不在场（FSV-001 D1 不入 git）")
class TestB1Replacement:
    def test_response_keys_isomorphic_to_baseline(self):
        """B1 响应 keys 与 baseline 同构：baseline 全部键 + additive screenParse。"""
        _, variant_config = _build_registry()
        fixture = json.loads(
            (_FIXTURES / "case-b-off.json").read_text(encoding="utf-8"))
        evidence = _run_b1("case-b-off", variant_config=variant_config)
        assert set(evidence) == set(fixture) | {"screenParse"}

    def test_yolo_schema_and_canonical_labels(self):
        """yolo[] 条目 schema 与 baseline 相同（id/label/confidence/bounds/
        boundsPx/center/centerPx）；label ∈ canonical 词汇（D4 映射值）。"""
        _, variant_config = _build_registry()
        evidence = _run_b1("case-b-off", variant_config=variant_config)
        assert evidence["yolo"], "replacement 下 FastScreen 必须产出 detections"
        for item in evidence["yolo"]:
            assert set(item) == _BASELINE_YOLO_ITEM_KEYS
            assert item["label"] in CANONICAL_VOCABULARY
            assert item["id"].startswith("det_")
            bounds = item["bounds"]
            assert 0.0 <= bounds["x1"] < bounds["x2"] <= 1.0
            assert 0.0 <= bounds["y1"] < bounds["y2"] <= 1.0
            assert len(item["boundsPx"]) == 4

    def test_ocr_retained(self):
        """B1 = detector replaced, OCR retained（D3）：ocr[] 非空且数量与
        baseline 一致（同图同 OCR 路径）。"""
        _, variant_config = _build_registry()
        fixture = json.loads(
            (_FIXTURES / "case-b-off.json").read_text(encoding="utf-8"))
        evidence = _run_b1("case-b-off", variant_config=variant_config)
        assert len(evidence["ocr"]) > 0
        assert evidence["summary"]["ocrCount"] == \
            fixture["summary"]["ocrCount"]
        assert all(t["text"].strip() for t in evidence["ocr"])

    def test_screenparse_block_replacement_semantics(self):
        """screenParse[] additive 键：detections id 沿用 det_{n}（replacement
        下它就是 detect）；corroborations 不适用（空）；rescue 不适用
        （rescuedCount=0）；summary 注明 replacement 模式；structural 仅保留
        结构类（D4 Optional Evidence）。"""
        _, variant_config = _build_registry()
        evidence = _run_b1("case-b-off", variant_config=variant_config)
        sp = evidence["screenParse"]
        assert {"detections", "structural", "corroborations", "summary"} \
            <= set(sp)
        assert sp["summary"]["rawCount"] == \
            sp["summary"]["mappedCount"] + sp["summary"]["structuralCount"]
        assert sp["summary"]["mode"] == "replacement"
        assert sp["summary"]["rescuedCount"] == 0
        assert sp["corroborations"] == []
        # mapped 全量即 detect 输出（det_{n}）：screenParse.detections 携带全量
        # mapped（含非交互类，如 image）；yolo[] = fusion 交互过滤后的响应子集
        # （与 baseline 相同的响应契约）——因此 yolo id 集合 ⊆ screenParse id 集合。
        sp_ids = {d["id"] for d in sp["detections"]}
        yolo_ids = {d["id"] for d in evidence["yolo"]}
        assert yolo_ids <= sp_ids
        assert evidence["summary"]["yoloCount"] <= sp["summary"]["mappedCount"]
        for d in sp["detections"]:
            assert d["id"].startswith("det_")
            assert d["rawLabel"] in SCREENPARSE_LABEL_MAPPING
        for s in sp["structural"]:
            assert s["rawLabel"] in STRUCTURAL_LABELS

    def test_identity_variant_aware(self):
        """metadata：pipelineVariant = fastscreen-replacement；configId 与
        baseline 不同（detect impl 进 configId 轴，D8 变量隔离）。"""
        _, variant_config = _build_registry()
        fixture = json.loads(
            (_FIXTURES / "case-b-off.json").read_text(encoding="utf-8"))
        evidence = _run_b1("case-b-off", variant_config=variant_config)
        meta = evidence["metadata"]
        assert meta["pipelineVariant"] == "fastscreen-replacement"
        assert meta["configId"] != fixture["metadata"]["configId"]

    def test_deterministic_two_runs(self):
        """确定性：同图两次 B1 输出逐字段相等（模型级缓存 + 确定性后处理）。"""
        _, variant_config = _build_registry()
        first = _run_b1("case-b-off", variant_config=variant_config)
        second = _run_b1("case-b-off", variant_config=variant_config)
        assert json.dumps(first, ensure_ascii=False) == \
            json.dumps(second, ensure_ascii=False)


# ── B2（slow：真实权重；bench 脚本子进程）────────────────────────────────

@pytest.mark.skipif(not (_YOLO_WEIGHTS.exists() and _SCREENPARSE_WEIGHTS.exists()),
                    reason="YOLO/ScreenParser 权重不在场（FSV-001 D1 不入 git）")
class TestB2Ablation:
    def test_b2_ocr_empty_candidates_textless_yolo_equals_b1(self, tmp_path):
        """B2（bench/run_replacement_ablation.py，子进程，真实 CLI）：case-b-off
        上 ocr==[] 且 candidates 全部 text==""（promote_unmatched_ocr 无从
        发生）；yolo[]/screenParse[] 与 B1 同源（同图同 device 逐字段一致）。"""
        out_json = tmp_path / "b2-case-b-off.json"
        result = subprocess.run(
            [sys.executable, str(_BENCH_SCRIPT),
             "--image", str(_ASSETS["case-b-off"]),
             "--out", str(out_json)],
            cwd=str(_PKG), capture_output=True, text=True, timeout=900)
        assert result.returncode == 0, \
            f"B2 脚本失败:\nstdout={result.stdout}\nstderr={result.stderr}"
        assert out_json.exists()

        b2 = json.loads(out_json.read_text(encoding="utf-8"))
        assert b2["ocr"] == []
        assert b2["summary"]["ocrCount"] == 0
        assert b2["candidates"]  # 空 OCR 下仍有检测驱动的 candidates（诚实记录）
        for c in b2["candidates"]:
            assert c.get("text", "") == ""  # 无 OCR → 无文字关联（B2 语义）

        # 与 B1 同源：同图同 device 下 yolo[]/screenParse[] 逐字段一致
        _, variant_config = _build_registry()
        b1 = _run_b1("case-b-off", variant_config=variant_config)
        assert json.dumps(b1["yolo"], ensure_ascii=False) == \
            json.dumps(b2["yolo"], ensure_ascii=False)
        assert json.dumps(b1["screenParse"], ensure_ascii=False) == \
            json.dumps(b2["screenParse"], ensure_ascii=False)