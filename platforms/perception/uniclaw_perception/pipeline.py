"""Pipeline configuration: schema, loading, lint, variants（PER-008 D1/D3/D10）.

显式化（D8）：STAGES 声明管道 DAG（当前串行实现；依赖声明为 OPT-001
并行化留缝）。可配置化：`config/pipeline.json`（默认 = 今日行为）+
`config/pipeline-variants/*.json`（预声明变体，启动全量 lint，请求经
`X-Pipeline-Variant` 选择，**不携带配置内容**）。校验用 pydantic（fastapi
已带入 venv，零新依赖）；未知 stage/impl/param → fail-closed。

配置面边界（有意收窄，无 buyer 不加 knob）：本文件只拥有 server.py 曾
硬编码的面（fusion 四 knob + impl 声明）；preprocess/OCR 参数仍归
config.py（env > label-mapping.json），单一真相源不复制。
"""
from __future__ import annotations

import json
from pathlib import Path
from typing import Any, Literal

from pydantic import BaseModel, ConfigDict, Field, field_validator

_PKG_ROOT = Path(__file__).resolve().parent.parent  # platforms/perception/

# ── 显式 stage DAG（D8：依赖声明，实现先串行）──────────────────────────
# remap/validate/assemble 无独立参数面（代码拥有语义），仍显式声明以固化
# 拓扑；参数化 stage = detect/recognize/screenparse/fuse。
STAGES: tuple[dict[str, Any], ...] = (
    {"stage": "preprocess", "params": False, "dependsOn": []},
    {"stage": "detect", "params": True, "dependsOn": ["preprocess"]},
    {"stage": "recognize", "params": True, "dependsOn": ["preprocess"]},
    # FSV-001 D2：screenparse 插在 detect∥recognize 之后、fuse 之前；fuse 改
    # 依赖 screenparse（detect/recognize 经其传递依赖）。默认管道（无
    # screenparse 段）该阶段恒等——fuse 输入的 detection 池与旧一致。
    {"stage": "screenparse", "params": True,
     "dependsOn": ["detect", "recognize"]},
    {"stage": "fuse", "params": True, "dependsOn": ["screenparse"]},
    {"stage": "remap", "params": False, "dependsOn": ["fuse"]},
    {"stage": "validate", "params": False, "dependsOn": ["remap"]},
    {"stage": "assemble", "params": False, "dependsOn": ["validate"]},
)

# impl 注册表（D10）：枚举当下真实路径；recognize 的有效 impl 由
# cfg.ocr_backend/ocr_mode 推导并在启动期校验 ∈ 注册表。
STAGE_IMPLS: dict[str, frozenset[str]] = {
    # OPT-001 S2：torch-mps = 同权重换 device（D1 首批后端）；可用性启动期
    # 探测，不可用 → fail-closed（不静默回退 CPU）。
    # FSV-001 D3：screenparser/screenparser-mps = Test B replacement——FastScreen
    # 承担 detect（screenparser 推理计入 yolo 段语义；mps 可用性探测照
    # torch-mps 语义，lint 处 fail-closed）。
    "detect": frozenset({
        "torch-yolo", "torch-mps",
        "screenparser", "screenparser-mps",
    }),
    "recognize": frozenset({"rapidocr-full", "rapidocr-roi", "paddle-crops"}),
    "fuse": frozenset({"operator-pipeline"}),
}

DETECT_IMPL_DEVICE = {
    "torch-yolo": "cpu",
    "torch-mps": "mps",
    "screenparser": "cpu",
    "screenparser-mps": "mps",
}

#: screenparser 系 detect impl（FSV-001 D3）：FastScreen 即 detect——replacement
#: 模式下 detect 阶段改调 screenparse provider + adapter（server.py 接线）。
#: 与 screenparse 集成段（Test A）互斥：lint 处 fail-closed（见 lint_against_config）。
SCREENPARSER_DETECT_IMPLS: frozenset[str] = frozenset({
    "screenparser", "screenparser-mps",
})

def detect_device(config: "PipelineConfig") -> str:
    """detect 有效 impl → torch device（pipeline.py 单一真相源）。"""
    impl = config.detect.impl if config.detect is not None else "torch-yolo"
    return DETECT_IMPL_DEVICE[impl]


class PipelineValidationError(RuntimeError):
    """Pipeline 配置不可加载/lint 失败——启动必须中止（fail-closed）。"""


class FuseParams(BaseModel):
    """fusion 四 knob（D7）——默认值 = server.py 今日硬编码值（行为冻结）。"""
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    interactive_extra_labels: list[str] = Field(
        default=["text_block", "text"], alias="interactiveExtraLabels")
    promote_unmatched_ocr: bool = Field(
        default=True, alias="promoteUnmatchedOcr")
    stabilize: bool = Field(default=True)
    max_ocr_distance_ratio: float = Field(
        default=0.055, gt=0.0, le=1.0, alias="maxOcrDistanceRatio")


class ScreenParseParams(BaseModel):
    """FastScreen screenparse 阶段参数（FSV-001 D2；Test A 集成变体专用）。

    默认值 = D2 决策给定值。extra=forbid：未知字段 fail-closed（lint 覆盖）。
    minRescueConf 的 (0,1] 界为防御性补充（负值/超 1 会让救援恒真/恒假，
    静默失效）——任务书只给了 rescueIouMax 的界，此处按 fail-closed 精神
    补齐并记录为偏离项。
    """
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    min_rescue_conf: float = Field(
        default=0.35, gt=0.0, le=1.0, alias="minRescueConf")
    rescue_iou_max: float = Field(
        default=0.30, gt=0.0, lt=1.0, alias="rescueIouMax")


class ImplDecl(BaseModel):
    model_config = ConfigDict(extra="forbid")

    impl: str


class PipelineConfig(BaseModel):
    model_config = ConfigDict(extra="forbid")

    schema_version: Literal[1] = Field(alias="schemaVersion")
    fuse: FuseParams = Field(default_factory=FuseParams)
    detect: ImplDecl | None = None
    recognize: ImplDecl | None = None
    # FSV-001 D2：screenparse 段缺省 = 阶段关闭（默认管道行为零变化——
    # 回归锚）；显式声明（含空对象）即启用。
    screenparse: ScreenParseParams | None = None

    @field_validator("detect")
    @classmethod
    def _detect_impl(cls, value: ImplDecl | None) -> ImplDecl | None:
        if value is not None and value.impl not in STAGE_IMPLS["detect"]:
            raise PipelineValidationError(
                f"unknown detect impl: {value.impl!r} "
                f"(registered: {sorted(STAGE_IMPLS['detect'])})")
        return value

    @field_validator("recognize")
    @classmethod
    def _recognize_impl(cls, value: ImplDecl | None) -> ImplDecl | None:
        if value is not None and value.impl not in STAGE_IMPLS["recognize"]:
            raise PipelineValidationError(
                f"unknown recognize impl: {value.impl!r} "
                f"(registered: {sorted(STAGE_IMPLS['recognize'])})")
        return value


class PipelineVariant(BaseModel):
    """预声明变体 = PipelineConfig + 变体名（请求经 X-Pipeline-Variant 选择）。"""
    model_config = ConfigDict(extra="forbid")

    variant_id: str = Field(alias="variantId")
    config: PipelineConfig


def effective_recognize_impl(cfg: Any) -> str:
    """由 cfg（单一真相源）推导 recognize 有效 impl。"""
    if cfg.ocr_backend != "rapidocr":
        return "paddle-crops"
    return "rapidocr-roi" if cfg.ocr_mode == "roi" else "rapidocr-full"


def _mps_available() -> bool:
    try:
        import torch
        return torch.backends.mps.is_available()
    except Exception:
        return False


def lint_against_config(config: PipelineConfig, cfg: Any) -> None:
    """启动期交叉校验：显式声明的 impl 必须与 cfg 推导一致（fail-closed）。"""
    derived = effective_recognize_impl(cfg)
    if config.recognize is not None and config.recognize.impl != derived:
        raise PipelineValidationError(
            f"pipeline recognize impl {config.recognize.impl!r} 与配置推导的 "
            f"{derived!r} 不一致（ocr_backend={cfg.ocr_backend}, "
            f"ocr_mode={cfg.ocr_mode}）——单一真相源冲突")
    if config.detect is not None:
        if config.detect.impl not in STAGE_IMPLS["detect"]:
            raise PipelineValidationError(
                f"detect impl {config.detect.impl!r} 未注册 "
                f"(registered: {sorted(STAGE_IMPLS['detect'])})")
        if config.detect.impl in ("torch-mps", "screenparser-mps") \
                and not _mps_available():
            raise PipelineValidationError(
                f"detect impl {config.detect.impl!r} 声明但 MPS 不可用——"
                "fail-closed，不静默回退 CPU（改回 cpu 系 impl 或修复 MPS 环境）")
        # FSV-001 D3：replacement（detect=screenparser 系）与集成段（screenparse
        # 段）互斥——Test B 下 FastScreen 即 detect，mapped 全量即 detect 输出，
        # 不叠加集成救援/corroboration；同时声明会让两条路径互相污染，fail-closed。
        if config.detect.impl in SCREENPARSER_DETECT_IMPLS \
                and config.screenparse is not None:
            raise PipelineValidationError(
                f"detect impl {config.detect.impl!r} 与 screenparse 集成段互斥——"
                "Test B（replacement）下 FastScreen 即 detect，不叠加 Test A 的"
                "救援/对照语义（见 changes/FSV-001/state.md D3）")


def _load_config_dict(path: Path) -> dict[str, Any]:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise PipelineValidationError(f"pipeline 配置不可读: {path} ({error})") from None


def parse_pipeline_config(data: dict[str, Any]) -> PipelineConfig:
    try:
        return PipelineConfig.model_validate(data)
    except Exception as error:
        raise PipelineValidationError(f"pipeline 配置校验失败: {error}") from None


DEFAULT_PIPELINE_FILE = _PKG_ROOT / "config" / "pipeline.json"
VARIANTS_DIR = _PKG_ROOT / "config" / "pipeline-variants"


def load_default() -> PipelineConfig:
    """默认管道配置（= 今日行为）。缺文件 → 使用代码内默认（同值）。"""
    if not DEFAULT_PIPELINE_FILE.exists():
        return PipelineConfig.model_validate({"schemaVersion": 1})
    return parse_pipeline_config(_load_config_dict(DEFAULT_PIPELINE_FILE))


def load_variants() -> dict[str, PipelineVariant]:
    """加载全部预声明变体（启动全量 lint；任一失败 → 中止）。"""
    variants: dict[str, PipelineVariant] = {}
    if not VARIANTS_DIR.exists():
        return variants
    for path in sorted(VARIANTS_DIR.glob("*.json")):
        data = _load_config_dict(path)
        variant_name = data.pop("variantId", None) or path.stem
        try:
            variant = PipelineVariant.model_validate({
                "variantId": variant_name,
                "config": data,
            })
        except Exception as error:
            raise PipelineValidationError(
                f"变体 {path.name} 校验失败: {error}") from None
        if variant.variant_id in variants:
            raise PipelineValidationError(f"变体名冲突: {variant.variant_id!r}")
        variants[variant.variant_id] = variant
    return variants


def identity_content(config: PipelineConfig, variant_id: str | None) -> dict[str, Any]:
    """configId 的 pipeline 轴内容（变体感知）。

    screenparse 段**条件包含**（仅启用时）：默认管道的 identity dict 与
    引入前结构逐字节一致 → 默认 configId 不变（回归锚成立）；变体带
    screenparse 段 → configId 自动随变体差异（PER-008 D2）。
    """
    content: dict[str, Any] = {
        "schemaVersion": config.schema_version,
        "detect": config.detect.impl if config.detect else "derived:torch-yolo",
        "recognize": config.recognize.impl if config.recognize else "derived",
        "fuse": config.fuse.model_dump(mode="json", by_alias=True),
        "variantId": variant_id,
    }
    if config.screenparse is not None:
        content["screenparse"] = config.screenparse.model_dump(
            mode="json", by_alias=True)
    return content
