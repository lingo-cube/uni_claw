"""FSV-001 V2：screenparse 模块单测——适配映射完备性、确定性、漏检救援规则。

确定性规则（D2）在 adapter 纯函数层验证（同输入同输出，不含模型/帧运气）；
真实推理的两次运行确定性在 slow 测试（权重在场）验证。
"""
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from uniclaw_perception.schema import Box, Detection  # noqa: E402
from uniclaw_perception.screenparse.adapter import (  # noqa: E402
    CANONICAL_VOCABULARY,
    SCREENPARSE_LABEL_MAPPING,
    STRUCTURAL_LABELS,
    adapt,
    box_iou,
    build_screenparse_evidence,
    collect_corroborations,
    select_rescue,
)
from uniclaw_perception.screenparse.provider import (  # noqa: E402
    ScreenParseDetection,
    run_screenparse_on_image,
)

_PKG = Path(__file__).resolve().parent.parent

#: ScreenParser v2 模型 class vocabulary 快照（源自 D1 验证加载的
#: model.names；slow 测试再与真实权重交叉核对，防漂移）。
MODEL_CLASS_VOCABULARY: frozenset[str] = frozenset({
    "Table", "Column/Browser", "Button", "Utility Button", "App Icon",
    "Navigation Bar", "Status Bar", "Search Field", "Toolbar", "Tooltip",
    "Video", "Tab Bar", "Side Bar", "Slider", "Picker", "ContextMenu",
    "DockMenu", "EditMenu", "Image", "Scroll", "Switch", "File Icon",
    "Chart", "Window", "Screen", "List", "List Item", "PopUp Menu",
    "Steppers", "Toggles", "Text Input", "Rating Indicator", "Checkbox",
    "Radiobox", "Select", "Avatar", "Badge", "Alert", "Progress bar",
    "Bottom navigation", "Breadcrumb", "Page control", "Link", "Menu",
    "Pagination", "Tab", "Search Bar", "Date-Time picker", "Calendar",
    "Text", "Heading", "Code snippet", "Carousel", "Notification", "Logo",
})

_SCREENPARSE_WEIGHTS = _PKG / "models/yolo/screenparser_v2/best.pt"


def _spd(raw_label: str, conf: float, box: Box,
         cls_id: int | None = None) -> ScreenParseDetection:
    return ScreenParseDetection(
        raw_label=raw_label,
        raw_class_id=cls_id if cls_id is not None else 0,
        confidence=conf,
        box=box,
    )


def _det(det_id: str, label: str, conf: float, box: Box) -> Detection:
    return Detection(id=det_id, label=label, confidence=conf, box=box)


class TestMappingCompleteness:
    """55 类每类必须恰好出现在 mapping 或 structural 之一（测试强制）。"""

    def test_55_classes_covered_exactly_once(self):
        mapped = set(SCREENPARSE_LABEL_MAPPING)
        structural = set(STRUCTURAL_LABELS)
        assert mapped & structural == set(), "mapping 与 structural 不得相交"
        assert mapped | structural == set(MODEL_CLASS_VOCABULARY), (
            "55 类漏映射或多了无关类（映射表与模型 class vocabulary 漂移）"
        )
        assert len(mapped) + len(structural) == 55

    def test_mapping_values_within_canonical_vocabulary(self):
        """映射值 ⊆ canonical 词汇（D4：不扩 Runtime ontology）。"""
        assert set(SCREENPARSE_LABEL_MAPPING.values()) <= CANONICAL_VOCABULARY

    def test_mapping_shape(self):
        """值都是规范化 snake_case 标签；键为模型原名。"""
        for raw, canonical in SCREENPARSE_LABEL_MAPPING.items():
            assert raw in MODEL_CLASS_VOCABULARY
            assert canonical == canonical.strip().lower().replace(" ", "_")

    # slow：与真实权重交叉核对 55 类（权重在场才跑）
    @pytest.mark.skipif(not _SCREENPARSE_WEIGHTS.exists(),
                        reason="screenparser 权重不在场（FSV-001 D1 不入 git）")
    def test_model_names_match_snapshot(self):
        from ultralytics import YOLO
        model = YOLO(str(_SCREENPARSE_WEIGHTS))
        assert set(model.names.values()) == set(MODEL_CLASS_VOCABULARY)


class TestAdapt:
    def test_mapped_detection_fields(self):
        dets = [
            _spd("Button", 0.9, Box(0, 0, 10, 10), cls_id=2),
            _spd("Utility Button", 0.8, Box(20, 20, 30, 30), cls_id=3),
        ]
        mapped, structural = adapt(dets)
        assert len(mapped) == 2 and structural == []
        assert [d.id for d in mapped] == ["fs_1", "fs_2"]
        assert [d.label for d in mapped] == ["button", "button"]
        assert [d.raw_label for d in mapped] == ["Button", "Utility Button"]
        assert [d.raw_class_id for d in mapped] == [2, 3]
        assert [d.confidence for d in mapped] == [0.9, 0.8]

    def test_structural_passthrough(self):
        dets = [
            _spd("Table", 0.5, Box(0, 0, 10, 10), cls_id=0),
            _spd("Button", 0.9, Box(20, 20, 30, 30), cls_id=2),
            _spd("Status Bar", 0.4, Box(40, 40, 50, 50), cls_id=6),
        ]
        mapped, structural = adapt(dets)
        assert len(mapped) == 1 and mapped[0].id == "fs_1"
        assert [s.raw_label for s in structural] == ["Table", "Status Bar"]
        assert [s.raw_class_id for s in structural] == [0, 6]

    def test_unknown_class_fail_closed(self):
        with pytest.raises(ValueError):
            adapt([_spd("NotARealClass", 0.9, Box(0, 0, 10, 10))])


class TestSelectRescue:
    """D2 漏检救援：IoU 阈值两侧行为 + conf 阈值 + 边界语义（确定性）。"""

    def _pool(self, rescue_iou_max=0.30, min_rescue_conf=0.35):
        existing = [
            _det("det_1", "icon", 0.9, Box(0, 0, 10, 10)),
            _det("det_2", "button", 0.8, Box(100, 100, 120, 120)),
        ]
        # fs 候选：不重叠（IoU=0）、部分重叠（IoU≈0.143 < 0.30）、强重叠
        # （Box(0,0,12,12) 对 det_1 → IoU≈0.694 ≥ 0.30）
        candidates = [
            _det("fs_1", "text_block", 0.9, Box(200, 200, 210, 210)),
            _det("fs_2", "input", 0.9, Box(5, 5, 15, 15)),
            _det("fs_3", "slider", 0.9, Box(0, 0, 12, 12)),
        ]
        return existing, candidates

    def test_iou_below_threshold_rescued(self):
        existing, candidates = self._pool()
        rescued = select_rescue(candidates, existing,
                                min_rescue_conf=0.35, rescue_iou_max=0.30)
        # fs_1 IoU=0、fs_2 IoU≈0.143 → 救援；fs_3 与 det_1 IoU≈0.694 → 不救援
        assert [d.id for d in rescued] == ["fs_1", "fs_2"]

    def test_conf_below_threshold_not_rescued(self):
        existing, candidates = self._pool()
        candidates[1] = _det("fs_2", "input", 0.30, Box(5, 5, 15, 15))  # conf < 0.35
        rescued = select_rescue(candidates, existing,
                                min_rescue_conf=0.35, rescue_iou_max=0.30)
        assert [d.id for d in rescued] == ["fs_1"]

    def test_conf_at_threshold_rescued(self):
        """边界：conf == min_rescue_conf → 救援（>= 语义，float 精确可比）。"""
        existing, candidates = self._pool()
        candidates[1] = _det("fs_2", "input", 0.35, Box(5, 5, 15, 15))
        rescued = select_rescue(candidates, existing,
                                min_rescue_conf=0.35, rescue_iou_max=0.30)
        assert "fs_2" in [d.id for d in rescued]

    def test_strict_iou_boundary(self):
        """边界：IoU == 阈值 → 不救援（严格 <）。用与实现同源的 box_iou 计算
        期望，验证 select_rescue 与 (iou < rescue_iou_max) 语义一致。"""
        a = Box(0, 0, 100, 100)
        # 同高平移构造：IoU 稳居 0.30 两侧（fs_low≈0.20 < 0.30；fs_high≈0.50 ≥ 0.30）
        b_low = Box(66.667, 0, 166.667, 100)
        b_high = Box(33.333, 0, 133.333, 100)
        assert 0.0 < box_iou(a, b_low) < 0.30
        assert box_iou(a, b_high) > 0.30
        existing = [_det("det_1", "icon", 0.9, a)]
        rescued = select_rescue(
            [_det("fs_low", "text_block", 0.9, b_low),
             _det("fs_high", "text_block", 0.9, b_high)],
            existing, min_rescue_conf=0.35, rescue_iou_max=0.30)
        assert [d.id for d in rescued] == ["fs_low"]

    def test_rescues_against_any_label_and_keeps_order(self):
        existing = [_det("det_1", "image", 0.9, Box(0, 0, 10, 10))]
        candidates = [
            _det("fs_1", "icon", 0.9, Box(50, 50, 60, 60)),
            _det("fs_2", "button", 0.9, Box(70, 70, 80, 80)),
            _det("fs_3", "input", 0.9, Box(0, 0, 12, 12)),  # 与 det_1 IoU≈0.694 → 不救援
        ]
        rescued = select_rescue(candidates, existing,
                                min_rescue_conf=0.35, rescue_iou_max=0.30)
        assert [d.id for d in rescued] == ["fs_1", "fs_2"]

    def test_deterministic_same_input(self):
        existing, candidates = self._pool()
        first = select_rescue(candidates, existing,
                              min_rescue_conf=0.35, rescue_iou_max=0.30)
        second = select_rescue(candidates, existing,
                               min_rescue_conf=0.35, rescue_iou_max=0.30)
        assert [(d.id, d.label, d.confidence, d.box) for d in first] == \
            [(d.id, d.label, d.confidence, d.box) for d in second]


class TestCorroborations:
    def test_records_divergence_fields(self):
        existing = [_det("det_1", "icon", 0.9, Box(0, 0, 10, 10))]
        candidates = [_det("fs_1", "button", 0.9, Box(0, 0, 10, 10))]
        records = collect_corroborations(candidates, existing)
        assert records == [{
            "fsLabel": "button", "yoloLabel": "icon",
            "iou": 1.0, "fsConf": 0.9, "yoloId": "det_1",
        }]

    def test_same_label_no_record(self):
        existing = [_det("det_1", "icon", 0.9, Box(0, 0, 10, 10))]
        candidates = [_det("fs_1", "icon", 0.9, Box(0, 0, 10, 10))]
        assert collect_corroborations(candidates, existing) == []

    def test_conf_below_080_no_record(self):
        existing = [_det("det_1", "icon", 0.9, Box(0, 0, 10, 10))]
        candidates = [_det("fs_1", "button", 0.79, Box(0, 0, 10, 10))]
        assert collect_corroborations(candidates, existing) == []

    def test_iou_below_060_no_record(self):
        existing = [_det("det_1", "icon", 0.9, Box(0, 0, 100, 100))]
        candidates = [_det("fs_1", "button", 0.9, Box(150, 150, 250, 250))]
        iou = box_iou(existing[0].box, candidates[0].box)
        assert iou < 0.60
        assert collect_corroborations(candidates, existing) == []

    def test_first_hit_deterministic(self):
        existing = [
            _det("det_1", "icon", 0.9, Box(0, 0, 10, 10)),
            _det("det_2", "toolbar", 0.9, Box(0, 0, 10, 10)),
        ]
        candidates = [_det("fs_1", "input", 0.9, Box(0, 0, 10, 10))]
        records = collect_corroborations(candidates, existing)
        assert len(records) == 1
        assert records[0]["yoloId"] == "det_1"  # 首个命中


class TestBuildEvidence:
    def test_schema(self):
        from uniclaw_perception.screenparse.adapter import adapt
        raw = [
            _spd("Button", 0.9, Box(0, 0, 10, 10), cls_id=2),
            _spd("Table", 0.5, Box(20, 20, 30, 30), cls_id=0),
        ]
        mapped, structural = adapt(raw)  # 与真实路径同构（raw_label 保留）
        resc = [mapped[0]]
        ev = build_screenparse_evidence(raw, mapped, structural, [], resc, 720, 1280)
        # WI-6（D11）：summary 增 inputSpace="original"（additive；旧 proc 输入
        # 行为可从该字段缺席区分）+ droppedOffCanvas（越界 fail-closed 丢弃数）。
        assert ev["summary"] == {
            "inputSpace": "original",
            "rawCount": 2, "mappedCount": 1,
            "structuralCount": 1, "rescuedCount": 1,
            "droppedOffCanvas": 0,
        }
        d0 = ev["detections"][0]
        assert d0["id"] == "fs_1" and d0["label"] == "button"
        assert d0["rawLabel"] == "Button"
        assert d0["bounds"] == {"x1": 0.0, "y1": 0.0, "x2": 0.013889, "y2": 0.007812}
        assert d0["boundsPx"] == [0, 0, 10, 10]
        assert ev["structural"][0]["rawLabel"] == "Table"
        assert ev["structural"][0]["confidence"] == 0.5
        assert ev["corroborations"] == []

    def test_dropped_off_canvas_counted(self):
        """WI-6：越界丢弃数进 summary.droppedOffCanvas（fail-closed 留痕）。"""
        from uniclaw_perception.screenparse.adapter import adapt
        raw = [_spd("Button", 0.9, Box(0, 0, 10, 10), cls_id=2)]
        mapped, structural = adapt(raw)
        ev = build_screenparse_evidence(raw, mapped, structural, [], [],
                                        720, 1280, dropped_off_canvas=4)
        assert ev["summary"]["droppedOffCanvas"] == 4
        assert ev["summary"]["rawCount"] == 1


# ── slow：真实推理（权重在场）───────────────────────────────────────────

@pytest.mark.skipif(not _SCREENPARSE_WEIGHTS.exists(),
                    reason="screenparser 权重不在场（FSV-001 D1 不入 git）")
class TestRealInference:
    def test_two_runs_field_equal(self):
        """确定性：同一张图两次推理逐字段相等（同输入同输出）。"""
        from uniclaw_perception.config import load as load_config
        load_config()  # provider 需 cfg（screenparse 配置段）
        from PIL import Image
        image = Image.open(
            _PKG / "evaluation/assets/captures/settings-home-api35-full.png"
        ).convert("RGB")
        first = run_screenparse_on_image(image)
        second = run_screenparse_on_image(image)
        assert len(first) == len(second) > 0
        for a, b in zip(first, second):
            assert a.raw_label == b.raw_label
            assert a.raw_class_id == b.raw_class_id
            assert a.confidence == b.confidence
            assert a.box == b.box