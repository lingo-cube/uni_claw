"""FSV-001 WI-6：screenparse 坐标域逆映射（原图 → proc）单测
（state.md D11）。

纯函数层验证（不依赖权重/帧运气）：
- 已知点双向：以 preprocessing.preprocess 源码推导的逆映射公式构造已知点，
  正向（crop+resize）/逆向（本模块）往返一致。
- 边界语义：proc 画布边界（含边界）保留；越界（裁掉带/跨界）丢弃——fail-
  closed，不 clamp（决定记录进 droppedOffCanvas）。
- 计数/顺序确定性；非有限坐标 fail-closed；几何参数非法 → ValueError。
- 自定义几何（proc_h/crop_h ≠ 1/scale 的 int 取整情形）：两轴各自比例，
  像素级精确。
"""
import math
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from uniclaw_perception.screenparse.geom import map_original_to_proc  # noqa: E402
from uniclaw_perception.screenparse.provider import ScreenParseDetection  # noqa: E402
from uniclaw_perception.schema import Box  # noqa: E402


def _spd(label: str, x1: float, y1: float, x2: float, y2: float,
         conf: float = 0.9) -> ScreenParseDetection:
    return ScreenParseDetection(raw_label=label, raw_class_id=0,
                                confidence=conf, box=Box(x1, y1, x2, y2))


#: 默认几何 = cfg 默认（1080×2400 屏）：crop 6.25%/6.25% → top/bottom=150，
#: crop_h=2100；resize ≤720w → scale=1.5，proc 720×1400。
class TestKnownPointMapping:
    CANON = dict(orig_w=1080, orig_h=2400, top_px=150.0, bottom_px=150.0,
                 proc_w=720, proc_h=1400)

    def test_center_point_exact(self):
        """原图中心 (540, 1200) → proc (360, 700)（公式推导已知点）。"""
        kept, dropped = map_original_to_proc(
            [_spd("Button", 520, 1180, 560, 1220)], **self.CANON)
        assert dropped == 0 and len(kept) == 1
        b = kept[0].box
        assert b.x1 == pytest.approx(520 * (720 / 1080))          # 346.67
        assert b.y1 == pytest.approx((1180 - 150) * (1400 / 2100))  # 686.67
        assert b.x2 == pytest.approx(560 * (720 / 1080))
        assert b.y2 == pytest.approx((1220 - 150) * (1400 / 2100))

    def test_bidirectional_round_trip(self):
        """双向验证：逆映射（orig→proc）后再按 preprocess 前向公式
        （proc→orig）回到原坐标——浮点往返一致（公式互为精确逆）。

        用内点（1×1 盒完整落在原图裁剪区内；贴边/角落盒因越界被丢弃是
        D11 边界语义，由 TestBoundarySemantics 覆盖）。"""
        pts = [(540.0, 1200.0), (17.0, 1111.0), (999.0, 239.0),
               (1.0, 151.0), (1079.0, 2249.0), (0.5, 2000.0)]
        for ox, oy in pts:
            kept, dropped = map_original_to_proc(
                [_spd("Button", ox, oy, ox + 1.0, oy + 1.0)], **self.CANON)
            assert kept, f"点 {(ox, oy)} 应保留（dropped={dropped}）"
            px, py = kept[0].box.x1, kept[0].box.y1
            # 前向：proc → orig（与 preprocess 同式）
            assert px * (1080 / 720) == pytest.approx(ox, abs=1e-9)
            assert py * (2100 / 1400) + 150 == pytest.approx(oy, abs=1e-9)

    def test_identity_no_resize(self):
        """无 resize（orig_w ≤ max_width）：x 比例 1.0，y 仅 top 平移。"""
        geom = dict(orig_w=700, orig_h=1200, top_px=75.0, bottom_px=75.0,
                    proc_w=700, proc_h=1050)
        kept, dropped = map_original_to_proc(
            [_spd("Button", 100, 100, 200, 200)], **geom)
        assert dropped == 0
        assert (kept[0].box.x1, kept[0].box.y1) == (100.0, 25.0)
        assert (kept[0].box.x2, kept[0].box.y2) == (200.0, 125.0)

    def test_custom_geometry_per_axis_factors(self):
        """proc_h/crop_h ≠ 1/scale（PIL int 取整场景）：两轴各自比例精确。"""
        geom = dict(orig_w=800, orig_h=1200, top_px=75.0, bottom_px=50.0,
                    proc_w=600, proc_h=806)  # 806 = int(1075 / (800/600)) 取整
        # crop_h = 1200 - 75 - 50 = 1075
        kept, _ = map_original_to_proc(
            [_spd("Button", 100, 100, 200, 200)], **geom)
        b = kept[0].box
        assert b.x1 == pytest.approx(100 * (600 / 800))           # 75.0
        assert b.x2 == pytest.approx(200 * (600 / 800))           # 150.0
        assert b.y1 == pytest.approx((100 - 75) * (806 / 1075))   # ≈18.74
        assert b.y2 == pytest.approx((200 - 75) * (806 / 1075))   # ≈93.71


class TestBoundarySemantics:
    """D11 越界语义：完全在 proc 画布（含边界）→ 保留；否则丢弃（fail-closed）。"""

    CANON = dict(orig_w=1080, orig_h=2400, top_px=150.0, bottom_px=150.0,
                 proc_w=720, proc_h=1400)

    def test_exact_boundary_kept(self):
        """贴边（x2=proc_w、y2=proc_h / x1=0、y1=0）→ 保留（含边界）。"""
        kept, dropped = map_original_to_proc(
            [_spd("Button", 0, 150, 1080, 2250)], **self.CANON)
        assert dropped == 0 and len(kept) == 1
        assert kept[0].box == Box(0.0, 0.0, 720.0, 1400.0)

    def test_top_band_dropped(self):
        """整个检测在裁掉的顶部带宽内（y < top_px）→ 越界丢弃。"""
        kept, dropped = map_original_to_proc(
            [_spd("Status Bar", 0, 20, 100, 120)], **self.CANON)
        assert kept == [] and dropped == 1

    def test_bottom_band_dropped(self):
        """整个检测在裁掉的底部带宽内（y > orig_h - bottom_px）→ 丢弃。"""
        kept, dropped = map_original_to_proc(
            [_spd("Button", 0, 2300, 100, 2390)], **self.CANON)
        assert kept == [] and dropped == 1

    def test_straddling_top_crop_line_dropped(self):
        """跨界（y1 < top_px < y2）→ 映射后 y1_proc < 0 → 丢弃（不 clamp）。"""
        kept, dropped = map_original_to_proc(
            [_spd("Button", 500, 140, 600, 160)], **self.CANON)
        assert kept == [] and dropped == 1

    def test_straddling_bottom_crop_line_dropped(self):
        kept, dropped = map_original_to_proc(
            [_spd("Button", 500, 2240, 600, 2270)], **self.CANON)
        assert kept == [] and dropped == 1

    def test_right_overhang_dropped(self):
        """水平越界（x2 > orig_w）→ x2_proc > proc_w → 丢弃。"""
        kept, dropped = map_original_to_proc(
            [_spd("Button", 1100, 500, 1200, 600)], **self.CANON)
        assert kept == [] and dropped == 1

    def test_left_negative_dropped(self):
        kept, dropped = map_original_to_proc(
            [_spd("Button", -50, 500, 100, 600)], **self.CANON)
        assert kept == [] and dropped == 1

    def test_mixed_kept_and_dropped_counts(self):
        """混合：保留 + 丢弃分别计数；顺序与输入一致（确定性）。"""
        dets = [
            _spd("Button", 0, 20, 100, 120, conf=0.5),    # 顶部带宽 → drop
            _spd("Button", 100, 500, 200, 600, conf=0.6),  # 保留
            _spd("Button", 2300, 2300, 2400, 2400),        # 底部带宽 → drop
            _spd("Switch", 300, 1000, 400, 1100, conf=0.8),  # 保留
        ]
        kept, dropped = map_original_to_proc(dets, **self.CANON)
        assert dropped == 2
        assert [d.raw_label for d in kept] == ["Button", "Switch"]
        assert [d.confidence for d in kept] == [0.6, 0.8]
        # 保留者坐标 = 逆映射值（抽查 + 全量公式复查）
        assert kept[0].box.x1 == pytest.approx(100 * (720 / 1080))
        assert kept[0].box.y1 == pytest.approx((500 - 150) * (1400 / 2100))
        assert kept[1].box.x1 == pytest.approx(300 * (720 / 1080))

    def test_nonfinite_fail_closed(self):
        """非有限坐标 → 丢弃并计数（fail-closed，不传播 NaN 进池）。"""
        nan_det = ScreenParseDetection(
            raw_label="Button", raw_class_id=0, confidence=0.9,
            box=Box(float("nan"), 500, 600, 700))
        kept, dropped = map_original_to_proc([nan_det], **self.CANON)
        assert kept == [] and dropped == 1

    def test_deterministic_same_input(self):
        dets = [
            _spd("Button", 0, 20, 100, 120, conf=0.5),
            _spd("Button", 100, 500, 200, 600, conf=0.6),
        ]
        first = map_original_to_proc(dets, **self.CANON)
        second = map_original_to_proc(dets, **self.CANON)
        assert [(d.raw_label, d.confidence, d.box) for d in first[0]] == \
            [(d.raw_label, d.confidence, d.box) for d in second[0]]
        assert first[1] == second[1]


class TestFailClosedGeometry:
    def test_zero_or_negative_dims_raise(self):
        with pytest.raises(ValueError):
            map_original_to_proc([], orig_w=0, orig_h=2400, top_px=150.0,
                                 bottom_px=150.0, proc_w=720, proc_h=1400)
        with pytest.raises(ValueError):
            map_original_to_proc([], orig_w=1080, orig_h=2400, top_px=0.0,
                                 bottom_px=0.0, proc_w=0, proc_h=1400)

    def test_crop_h_negative_raises(self):
        """crop_h ≤ 0（top+bottom 裁剪超过全高）→ 非法几何，fail-closed。"""
        with pytest.raises(ValueError):
            map_original_to_proc([], orig_w=1080, orig_h=2400, top_px=2000.0,
                                 bottom_px=2000.0, proc_w=720, proc_h=1400)

    def test_empty_input(self):
        kept, dropped = map_original_to_proc(
            [], orig_w=1080, orig_h=2400, top_px=150.0, bottom_px=150.0,
            proc_w=720, proc_h=1400)
        assert kept == [] and dropped == 0