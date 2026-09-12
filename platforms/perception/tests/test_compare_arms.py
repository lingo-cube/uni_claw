"""FSV-001 WI-5a — 四臂 A/B 评测 harness 单测（tests/test_compare_arms.py）。

覆盖（全部纯函数；不跑真服务）：
- matcher（greedy 一对一 / class 相等 / IoU 阈值 / unmatched）+ 与
  bench/score.py::match 语义等价（随机化对拍）。
- bbox 质量（IoU 分布 / center 偏差 px）、type accuracy、混淆矩阵。
- 文本指标（exact / CER / 贪心一对多去重 / 空 OCR 如实标记）。
- candidate 级（interactive 子集 / text association）。
- grounding resolver（click-text / click-nth-item / click-state）各 hit/miss
  分支（no-text-match / contain-fail / no-box / off-target）+ unsupported-kind
  + gt-element-missing（NOT_SCORABLE）+ state 已知限制注明。
- NOT_SCORABLE 语义：缺 elements/groundingTasks → null + 计数，永不为零。
- 统计（分位守门 / Server-Timing 解析 / hit-test 边距）。

真正的 uvicorn 服务 / 权重推理仅出现在显式 skipif 的 slow 测试（weights 在场
才跑）。默认套件零重依赖、不碰网络。
"""
import json
import sys
from pathlib import Path

import pytest

_PKG = Path(__file__).resolve().parent.parent  # platforms/perception/
sys.path.insert(0, str(_PKG))
sys.path.insert(0, str(_PKG / "bench"))

import compare_arms as ca  # noqa: E402

_YOLO_WEIGHTS = _PKG / "models/yolo/android_ui_detection_yolov8/best.pt"
_SCREENPARSE_WEIGHTS = _PKG / "models/yolo/screenparser_v2/best.pt"
_LEGACY_PNG = _PKG / "evaluation/assets/captures/golden-run-v1/frames/case-b-off.png"


# ── 合成响应/GT 工厂（确定性）───────────────────────────────────────────────

def synth_response(yolo=None, ocr=None, candidates=None, w=1080, h=2400):
    return {
        "metadata": {"width": w, "height": h},
        "yolo": yolo if yolo is not None else [],
        "ocr": ocr if ocr is not None else [],
        "candidates": candidates if candidates is not None else [],
    }


def det(det_id, label, bbox):
    x1, y1, x2, y2 = bbox
    return {
        "id": det_id, "label": label, "confidence": 0.9,
        "bounds": {"x1": x1 / 1080, "y1": y1 / 2400, "x2": x2 / 1080, "y2": y2 / 2400},
        "boundsPx": [x1, y1, x2, y2],
        "center": {"x": (x1 + x2) / 2 / 1080, "y": (y1 + y2) / 2 / 2400},
        "centerPx": [(x1 + x2) // 2, (y1 + y2) // 2],
    }


def ocr_tok(tok_id, text, bbox):
    x1, y1, x2, y2 = bbox
    return {
        "id": tok_id, "text": text, "confidence": 0.99,
        "boundsPx": [x1, y1, x2, y2],
        "centerPx": [(x1 + x2) // 2, (y1 + y2) // 2],
    }


def gt_elem(gt_id, cls, norm_bounds, text=None, interactive=False, state=None):
    return {
        "gtId": gt_id, "gtClass": cls, "bounds": norm_bounds, "text": text,
        "interactive": interactive, "state": state,
    }


def FULL_GT():
    """带 elements + expectedTexts + groundingTasks 的完整 GT。"""
    return {
        "schemaVersion": "uniclaw.fastscreenValset.gt.v1",
        "frameId": "ffffffffffff",
        "elements": [
            gt_elem("gt_1", "button",
                    {"x1": 0.09, "y1": 0.083, "x2": 0.28, "y2": 0.104},
                    text="Wi-Fi", interactive=True),
            gt_elem("gt_2", "list_item",
                    {"x1": 0.09, "y1": 0.125, "x2": 0.83, "y2": 0.142},
                    text="Bluetooth", interactive=True),
        ],
        "expectedTexts": ["Wi-Fi", "Bluetooth"],
        "groundingTasks": [
            {"taskId": "g1", "kind": "click-text", "query": "Wi-Fi",
             "expectGtId": "gt_1"},
        ],
    }


def COUNT_ONLY_GT():
    """counts/texts 级 GT（elements 空；legacy 资产形态）。"""
    return {
        "schemaVersion": "1.0",
        "elements": [],
        "expectedTexts": ["Search settings"],
    }


# ── Matcher ──────────────────────────────────────────────────────────────────

class TestMatcher:
    def test_iou_overlap_and_disjoint(self):
        assert ca._iou((0, 0, 10, 10), (0, 0, 10, 10)) == 1.0
        # 等面积框平移半宽：inter=50, union=150 → 1/3
        assert ca._iou((0, 0, 10, 10), (5, 0, 15, 10)) == pytest.approx(1 / 3)
        assert ca._iou((0, 0, 10, 10), (20, 20, 30, 30)) == 0.0

    def test_greedy_one_to_one_prefers_highest_iou(self):
        preds = [{"type": "button", "bounds": (0, 0, 20, 20)}]
        gts = [
            {"gtClass": "button", "bounds": (0, 0, 20, 20)},
            {"gtClass": "button", "bounds": (10, 0, 30, 20)},
        ]
        m = ca.match_greedy(preds, gts)
        assert m["tp"] == 1
        pair = m["matches"][0]
        assert pair["gtIndex"] == 0  # 更高 IoU 的先配

    def test_class_equality_required(self):
        preds = [{"type": "text_block", "bounds": (0, 0, 20, 20)}]
        gts = [{"gtClass": "button", "bounds": (0, 0, 20, 20)}]
        m = ca.match_greedy(preds, gts)
        assert m["tp"] == 0
        assert m["fp"] == 1 and m["fn"] == 1  # class 不等不配对 → 双 unmatched

    def test_iou_threshold_boundary(self):
        # 内含框覆盖一半：inter=50, union=100 → IoU 恰 0.5 → 配对
        preds = [{"type": "button", "bounds": (0, 0, 10, 10)}]
        gts_half = [{"gtClass": "button", "bounds": (0, 0, 5, 10)}]
        assert ca.match_greedy(preds, gts_half)["tp"] == 1
        # IoU = 0.4 < 0.5 → 不配对
        gts_low = [{"gtClass": "button", "bounds": (0, 0, 4, 10)}]
        assert ca.match_greedy(preds, gts_low)["tp"] == 0

    def test_none_bounds_skipped_in_unmatched(self):
        preds = [{"type": "button", "bounds": None}]
        gts = [{"gtClass": "button", "bounds": (0, 0, 10, 10)}]
        m = ca.match_greedy(preds, gts)
        assert m["tp"] == 0
        assert m["unmatchedPredictions"] == []  # None bounds 不进 unmatched
        assert m["unmatchedGroundTruth"] == [0]

    def test_equivalent_to_score_py_match(self):
        """与 bench/score.py::match 语义等价（随机化对拍；score import ~0.2s）。"""
        import random
        rng = random.Random(20260912)
        import score as bench_score
        for _ in range(60):
            n_pred = rng.randint(0, 8)
            n_gt = rng.randint(0, 8)
            preds = [
                {"type": rng.choice(["button", "list_item", "text_block", "icon"]),
                 "bounds": tuple(round(rng.uniform(0, 90), 2) for _ in range(4))}
                for _ in range(n_pred)
            ]
            gts = [
                {"gtClass": rng.choice(["button", "list_item", "text_block", "icon"]),
                 "bounds": tuple(round(rng.uniform(0, 90), 2) for _ in range(4))}
                for _ in range(n_gt)
            ]
            mine = ca.match_greedy(preds, gts)
            ref = bench_score.match(preds, gts)
            assert mine["tp"] == ref["tp"]
            assert mine["fp"] == ref["fp"]
            assert mine["fn"] == ref["fn"]
            assert mine["matches"] == ref["matches"]
            assert mine["unmatchedPredictions"] == ref["unmatchedPredictions"]
            assert mine["unmatchedGroundTruth"] == ref["unmatchedGroundTruth"]


# ── BBox / 距离 / 命中判定 ───────────────────────────────────────────────────

class TestGeometry:
    def test_gt_bounds_to_px(self):
        px = ca.gt_bounds_to_px(
            {"bounds": {"x1": 0.1, "y1": 0.2, "x2": 0.9, "y2": 0.25}},
            1080, 2400)
        assert px == pytest.approx((108, 480, 972, 600))

    def test_hit_test_within_margin(self):
        # center 恰在 bounds 外 8px → hit；9px → miss
        assert ca.hit_test((118, 208), (108, 192, 120, 212), margin=8) is True
        assert ca.hit_test((129, 208), (108, 192, 120, 212), margin=8) is False
        assert ca.hit_test((108, 192), (108, 192, 120, 212), margin=8) is True

    def test_center_distance(self):
        assert ca.center_distance_px((0, 0), (3, 4)) == 5.0

    def test_smallest_containing_box(self):
        yolo = [
            det("d1", "list_item", [100, 200, 900, 400]),
            det("d2", "button", [120, 205, 260, 245]),  # 面积更小
        ]
        box = ca._smallest_containing_yolo(yolo, (190, 225))
        assert box["id"] == "d2"


# ── 文本 / CER ───────────────────────────────────────────────────────────────

class TestText:
    def test_levenshtein_known(self):
        assert ca.levenshtein("kitten", "sitting") == 3
        assert ca.levenshtein("", "abc") == 3
        assert ca.levenshtein("same", "same") == 0

    def test_text_similarity(self):
        assert ca.text_similarity("abc", "abc") == 1.0
        assert ca.text_similarity("abc", "abd") == pytest.approx(2 / 3)

    def test_text_targets_dedupe_and_skip_empty(self):
        gt = {
            "expectedTexts": ["  Wi-Fi ", "Wi-Fi", ""],
            "elements": [gt_elem("a", "button", {}, text="Bluetooth"),
                         gt_elem("b", "button", {}, text=None),
                         gt_elem("c", "button", {}, text="Wi-Fi")],
        }
        assert ca.text_targets(gt) == ["Wi-Fi", "Bluetooth"]

    def test_exact_hits_and_cer(self):
        gt = {"expectedTexts": ["Wi-Fi", "Bluetooth"], "elements": []}
        ocr = [ocr_tok("o1", "Wi-Fi", [0, 0, 10, 10]),
               ocr_tok("o2", "Blue tooth", [0, 0, 10, 10])]
        r = ca.score_text(ocr, gt)
        assert r["exactHitRate"] == 0.5
        assert r["exactHits"] == 1
        assert r["cerMean"] is not None and 0 < r["cerMean"] <= 1

    def test_greedy_one_to_one_dedup(self):
        # 同一 target 文本出现两次，第二个只匹配到近似 token
        gt = {"expectedTexts": ["R", "R"], "elements": []}
        ocr = [ocr_tok("o1", "R", [0, 0, 10, 10]),
               ocr_tok("o2", "Rr", [0, 0, 10, 10])]
        r = ca.score_text(ocr, gt)
        assert r["exactHits"] == 1  # 贪心一对一：第二个 target 拿不到第二个 exact

    def test_empty_ocr_honest_zero_and_marked(self):
        gt = {"expectedTexts": ["Wi-Fi"], "elements": []}
        r = ca.score_text([], gt)
        assert r["ocrEmpty"] is True
        assert r["exactHitRate"] == 0.0  # 如实 0
        assert r["cerMean"] is None      # 未定义，不伪造
        assert "B2" in r["note"]

    def test_not_scorable_when_no_text_targets(self):
        r = ca.score_text([], {"expectedTexts": [], "elements": []})
        assert r["stance"] == "NOT_SCORABLE"
        assert r.get("targetCount") == 0


# ── Element / Candidate 评分 ─────────────────────────────────────────────────

class TestElementScoring:
    RESP = synth_response(
        yolo=[
            det("d1", "button", [100, 200, 300, 250]),
            det("d2", "list_item", [100, 300, 900, 340]),
            det("d3", "text_block", [100, 350, 500, 380]),
        ],
        ocr=[
            ocr_tok("o1", "Wi-Fi", [120, 205, 260, 245]),
            ocr_tok("o2", "Bluetooth", [120, 305, 400, 335]),
        ],
        candidates=[
            {"type": "button", "text": "Wi-Fi", "boundsPx": [100, 200, 300, 250]},
            {"type": "list_item", "text": "Bluetooth", "boundsPx": [100, 300, 900, 340]},
        ],
    )
    GT = FULL_GT()

    def test_element_prf(self):
        q = ca.score_frame(self.RESP, self.GT, 1080, 2400)
        e = q["elements"]
        assert e["stance"] == "SCORED"
        assert e["tp"] == 2 and e["fp"] == 1 and e["fn"] == 0
        assert e["precision"] == pytest.approx(2 / 3, abs=1e-3)  # 0.6667（4 位舍入）
        assert e["recall"] == 1.0
        assert e["f1"] == pytest.approx(0.8)

    def test_bbox_quality(self):
        e = ca.score_frame(self.RESP, self.GT, 1080, 2400)["elements"]
        assert e["bboxIou"]["pairs"] == 2
        assert e["bboxIou"]["mean"] is not None
        assert e["centerDeltaPx"]["pairs"] == 2
        assert all(d >= 0 for d in e["centerDeltaPairsPx"])

    def test_type_accuracy_and_confusion(self):
        e = ca.score_frame(self.RESP, self.GT, 1080, 2400)["elements"]
        # 本合成帧 class 全对 → type accuracy（class-agnostic 配对口径）= 1.0
        assert e["typeAccuracy"] == 1.0
        assert e["typeAccuracyMode"] == "class-agnostic-iou0.5"
        assert e["confusion"]["button"]["button"] == 1
        assert e["confusion"]["list_item"]["list_item"] == 1

    def test_type_accuracy_class_agnostic_confusion(self):
        """（WI-5b 修正 b）type accuracy 主口径 = class-agnostic 贪心 IoU≥0.5
        配对下的 label==gtClass 比例：找对了元素但类型判错 → typeAcc < 1.0
        + 混淆对计数（predLabel→gtClass top-N）。"""
        resp = synth_response(yolo=[
            det("d1", "text_block", [100, 200, 300, 252]),  # 位置对、类型错
            det("d2", "button", [500, 200, 700, 252]),      # 位置对、类型对
        ])
        gt = {"elements": [
            gt_elem("gt_1", "button",
                    {"x1": 0.09, "y1": 0.083, "x2": 0.28, "y2": 0.105},
                    interactive=True),
            gt_elem("gt_2", "button",
                    {"x1": 0.46, "y1": 0.083, "x2": 0.65, "y2": 0.105},
                    interactive=True),
        ], "expectedTexts": [], "groundingTasks": []}
        e = ca.score_frame(resp, gt, 1080, 2400)["elements"]
        assert e["typeAccuracy"] == 0.5            # 2 配对、1 类型正确
        assert e["typeMatchedPairs"] == 2
        assert e["confusion"]["text_block"]["button"] == 1  # 类型判错落入混淆
        assert e["confusion"]["button"]["button"] == 1
        top = e["confusionTopN"]
        # 等计数时按 predLabel 字典序（button < text_block）
        assert top[0] == {"predLabel": "button", "gtClass": "button",
                          "count": 1}
        assert top[1] == {"predLabel": "text_block", "gtClass": "button",
                          "count": 1}
        assert len(top) == 2

    def test_type_accuracy_none_when_no_agnostic_pair(self):
        """class-agnostic 配对无结果 → typeAccuracy=None（不伪造为 0）。"""
        resp = synth_response(yolo=[det("d1", "text_block", [0, 0, 10, 10])])
        gt = {"elements": [
            gt_elem("gt_1", "button",
                    {"x1": 0.8, "y1": 0.8, "x2": 0.9, "y2": 0.9}, interactive=True),
        ], "expectedTexts": [], "groundingTasks": []}
        e = ca.score_frame(resp, gt, 1080, 2400)["elements"]
        assert e["typeAccuracy"] is None

    def test_secondary_center_row_tolerant(self):
        """（WI-5b 修正 c）center 次级口径（行容忍）：YOLO 文字 extent vs GT
        整行 extent——框 IoU 不足 0.5（主口径 miss）但预测中心 ∈ GT 框 →
        center 口径配对。"""
        resp = synth_response(yolo=[
            det("d1", "list_item", [300, 300, 500, 340]),  # 窄 extent
        ])
        gt = {"elements": [
            gt_elem("gt_1", "list_item",
                    {"x1": 0.09, "y1": 0.125, "x2": 0.83, "y2": 0.142}),
        ], "expectedTexts": [], "groundingTasks": []}
        e = ca.score_frame(resp, gt, 1080, 2400)["elements"]
        # 主口径：IoU≈0.19 < 0.5 → recall=0
        assert e["recall"] == 0.0 and e["tp"] == 0
        # center 口径：中心 (400, 320) ∈ gt_1 整行框 → tp=1
        sec = e["secondary"]
        assert sec["mode"] == "center"
        assert sec["recall"] == 1.0 and sec["precision"] == 1.0
        assert sec["tp"] == 1 and sec["fp"] == 0 and sec["fn"] == 0

    def test_secondary_center_class_equal_only(self):
        """center 口径沿用 class 相等约束：类型不同不配对。"""
        resp = synth_response(yolo=[
            det("d1", "text_block", [300, 300, 500, 340]),
        ])
        gt = {"elements": [
            gt_elem("gt_1", "list_item",
                    {"x1": 0.09, "y1": 0.125, "x2": 0.83, "y2": 0.142}),
        ], "expectedTexts": [], "groundingTasks": []}
        e = ca.score_frame(resp, gt, 1080, 2400)["elements"]
        assert e["secondary"]["tp"] == 0
        assert e["secondary"]["fn"] == 1

    def test_secondary_center_center_outside_box(self):
        """预测中心不在 GT 框内 → center 口径也不配对。"""
        resp = synth_response(yolo=[
            det("d1", "list_item", [900, 900, 1000, 940]),  # 远离 GT 行
        ])
        gt = {"elements": [
            gt_elem("gt_1", "list_item",
                    {"x1": 0.09, "y1": 0.125, "x2": 0.83, "y2": 0.142}),
        ], "expectedTexts": [], "groundingTasks": []}
        e = ca.score_frame(resp, gt, 1080, 2400)["elements"]
        assert e["secondary"]["tp"] == 0
        assert e["secondary"]["fn"] == 1 and e["secondary"]["fp"] == 1

    def test_aggregate_quality_type_and_secondary_pooled(self):
        """pooled：typeAccuracy（加权配对）、混淆 top-N、center 次级 P/R/F1。"""
        resp = synth_response(yolo=[
            det("d1", "text_block", [100, 200, 300, 252]),
            det("d2", "button", [500, 200, 700, 252]),
        ])
        gt = {"elements": [
            gt_elem("gt_1", "button",
                    {"x1": 0.09, "y1": 0.083, "x2": 0.28, "y2": 0.105},
                    interactive=True),
            gt_elem("gt_2", "button",
                    {"x1": 0.46, "y1": 0.083, "x2": 0.65, "y2": 0.105},
                    interactive=True),
        ], "expectedTexts": [], "groundingTasks": []}
        q1 = ca.score_frame(resp, gt, 1080, 2400)
        q2 = ca.score_frame(resp, gt, 1080, 2400)
        agg = ca.aggregate_quality([q1, q2])
        e = agg["elements"]
        assert e["stance"] == "SCORED"
        assert e["typeMatchedPairs"] == 4
        assert e["typeCorrectPairs"] == 2
        assert e["typeAccuracy"] == 0.5
        assert e["confusionTopN"][0] == {
            "predLabel": "button", "gtClass": "button", "count": 2}
        sec = e["secondary"]
        assert sec["mode"] == "center"
        assert sec["tp"] == 2 and sec["fn"] == 2
        assert sec["recall"] == 0.5

    def test_candidate_metrics(self):
        c = ca.score_frame(self.RESP, self.GT, 1080, 2400)["candidates"]
        assert c["stance"] == "SCORED"
        assert c["precision"] == 1.0 and c["recall"] == 1.0
        assert c["textAssociation"]["rate"] == 1.0

    def test_candidates_not_scorable_without_interactive_gt(self):
        gt = {"elements": [gt_elem("a", "text_block", {}, interactive=False)],
              "expectedTexts": [], "groundingTasks": []}
        c = ca.score_frame(self.RESP, gt, 1080, 2400)["candidates"]
        assert c["stance"] == "NOT_SCORABLE"
        assert "interactive" in c["note"]

    def test_element_not_scorable_counts_only_gt(self):
        resp = synth_response(yolo=[det("d1", "text_block", [0, 0, 10, 10])],
                              ocr=[ocr_tok("o1", "Search settings", [0, 0, 400, 40])])
        q = ca.score_frame(resp, COUNT_ONLY_GT(), 1080, 2400)
        assert q["elements"]["stance"] == "NOT_SCORABLE"
        assert q["notScorable"]["elements"] is True
        # counts-only GT 的 expectedTexts 仍可支撑 text 评分
        assert q["text"]["stance"] == "SCORED"
        assert q["text"]["exactHits"] == 1


# ── Grounding resolver ───────────────────────────────────────────────────────

def _hitting_resp():
    """Wi-Fi 行：ocr token 中心落在 button 框内；button 中心恰在 GT 内。"""
    return synth_response(
        yolo=[
            det("d1", "button", [100, 200, 300, 252]),
            det("d2", "list_item", [100, 300, 900, 340]),
            det("d3", "toggle", [100, 500, 300, 540]),
            det("d4", "text_block", [100, 600, 500, 630]),
        ],
        ocr=[
            ocr_tok("o1", "Wi-Fi", [110, 206, 290, 246]),
            ocr_tok("o2", "Bluetooth", [120, 305, 400, 335]),
            ocr_tok("o3", "ToggleRow", [110, 505, 290, 535]),
        ],
    )


class TestGrounding:
    GT = FULL_GT()

    def _make_gt(self, **kw):
        return dict(self.GT, **kw)

    def test_click_text_hit(self):
        resp = _hitting_resp()
        q = ca.score_frame(resp, self.GT, 1080, 2400)["grounding"]
        assert q["hitCount"] == 1 and q["hitRate"] == 1.0
        task = q["tasks"][0]
        assert task["status"] == "HIT"
        assert task["resolved"]["yoloId"] == "d1"

    def test_click_text_no_text_match(self):
        resp = _hitting_resp()
        gt = self._make_gt(groundingTasks=[
            {"taskId": "g_miss", "kind": "click-text", "query": "不存在文本",
             "expectGtId": "gt_1"}])
        q = ca.score_frame(resp, gt, 1080, 2400)["grounding"]
        t = q["tasks"][0]
        assert t["status"] == "MISS" and t["reason"] == "no-text-match"
        assert q["missReasons"] == {"no-text-match": 1}

    def test_click_text_contain_fail(self):
        """OCR 命中 query 但其中心不被任何 yolo 框包含 → contain-fail。"""
        resp = synth_response(
            yolo=[det("d1", "button", [100, 400, 300, 440])],  # 框远离 token
            ocr=[ocr_tok("o1", "Wi-Fi", [110, 200, 290, 240])])
        gt = self._make_gt(groundingTasks=[
            {"taskId": "g_cf", "kind": "click-text", "query": "Wi-Fi",
             "expectGtId": "gt_1"}])
        q = ca.score_frame(resp, gt, 1080, 2400)["grounding"]
        t = q["tasks"][0]
        assert t["status"] == "MISS" and t["reason"] == "contain-fail"

    def test_click_text_smallest_box_wins(self):
        """多个包含框 → 取面积最小者（嵌套文本块内嵌按钮）。"""
        resp = synth_response(
            yolo=[
                det("d_outer", "list_item", [100, 200, 900, 252]),
                det("d_inner", "button", [100, 200, 300, 252]),  # 更小
            ],
            ocr=[ocr_tok("o1", "Wi-Fi", [110, 206, 290, 246])])
        gt = self._make_gt(groundingTasks=[
            {"taskId": "g_s", "kind": "click-text", "query": "Wi-Fi",
             "expectGtId": "gt_1"}])
        q = ca.score_frame(resp, gt, 1080, 2400)["grounding"]
        assert q["tasks"][0]["resolved"]["yoloId"] == "d_inner"

    def test_click_nth_item_by_row_order(self):
        resp = synth_response(
            yolo=[
                det("d2", "list_item", [100, 400, 900, 440]),
                det("d1", "button", [100, 100, 300, 140]),
                det("d3", "toggle", [100, 700, 300, 740]),
            ],
            ocr=[])
        gt = self._make_gt(elements=[
            gt_elem("gt_a", "button", {"x1": 0.09, "y1": 0.04, "x2": 0.28, "y2": 0.06},
                    interactive=True),
            gt_elem("gt_b", "list_item", {"x1": 0.09, "y1": 0.16, "x2": 0.83, "y2": 0.18},
                    interactive=True),
            gt_elem("gt_c", "toggle", {"x1": 0.09, "y1": 0.29, "x2": 0.28, "y2": 0.31},
                    interactive=True),
        ], groundingTasks=[
            {"taskId": "g_n1", "kind": "click-nth-item", "query": {"nth": 1},
             "expectGtId": "gt_a"},   # 按 (y1,x1) 排序：d1 最先 → 命中 gt_a
            {"taskId": "g_n3", "kind": "click-nth-item", "query": {"nth": 3},
             "expectGtId": "gt_c"},
        ])
        q = ca.score_frame(resp, gt, 1080, 2400)["grounding"]
        assert q["hitCount"] == 2 and q["hitRate"] == 1.0

    def test_click_nth_out_of_range_no_box(self):
        resp = _hitting_resp()
        gt = self._make_gt(groundingTasks=[
            {"taskId": "g_ob", "kind": "click-nth-item", "query": {"nth": 99},
             "expectGtId": "gt_1"}])
        q = ca.score_frame(resp, gt, 1080, 2400)["grounding"]
        t = q["tasks"][0]
        assert t["status"] == "MISS" and t["reason"] == "no-box"

    def test_off_target(self):
        """解析出的框中心不在 expectGtId bounds（±8px）内 → off-target。"""
        resp = synth_response(
            yolo=[det("d1", "button", [100, 900, 300, 940])],
            ocr=[ocr_tok("o1", "Wi-Fi", [110, 906, 290, 934])])
        gt = self._make_gt(groundingTasks=[
            {"taskId": "g_ot", "kind": "click-text", "query": "Wi-Fi",
             "expectGtId": "gt_1"}])  # gt_1 在屏幕上部
        q = ca.score_frame(resp, gt, 1080, 2400)["grounding"]
        t = q["tasks"][0]
        assert t["status"] == "MISS" and t["reason"] == "off-target"

    def test_click_state_notes_limitation(self):
        resp = _hitting_resp()
        gt = self._make_gt(elements=[
            gt_elem("gt_1", "button",
                    {"x1": 0.09, "y1": 0.083, "x2": 0.28, "y2": 0.105},
                    text="Wi-Fi", interactive=True),
            # ToggleRow 所在的 toggle 行（框中心 = (200, 520)）
            gt_elem("gt_3", "toggle",
                    {"x1": 0.09, "y1": 0.208, "x2": 0.28, "y2": 0.225},
                    text="ToggleRow", interactive=True),
        ], groundingTasks=[
            {"taskId": "g_st", "kind": "click-state", "query": "ToggleRow",
             "expectGtId": "gt_3"}])
        q = ca.score_frame(resp, gt, 1080, 2400)["grounding"]
        t = q["tasks"][0]
        assert t["status"] == "HIT"
        assert "state" in (t.get("note") or "")  # 已知限制如实注明

    def test_unsupported_kind(self):
        resp = _hitting_resp()
        gt = self._make_gt(groundingTasks=[
            {"taskId": "g_us", "kind": "click-selected",
             "query": {"state": "on"}, "expectGtId": "gt_1"}])
        q = ca.score_frame(resp, gt, 1080, 2400)["grounding"]
        t = q["tasks"][0]
        assert t["status"] == "UNSUPPORTED" and t["reason"] == "unsupported-kind"
        # unsupported 不进 hit-rate 分母
        assert q["scoredCount"] == 0 and q["hitRate"] is None
        assert q["unsupportedCount"] == 1

    def test_expect_gt_id_missing_not_scorable(self):
        resp = _hitting_resp()
        gt = self._make_gt(groundingTasks=[
            {"taskId": "g_bad", "kind": "click-text", "query": "Wi-Fi",
             "expectGtId": "gt_404"}])
        q = ca.score_frame(resp, gt, 1080, 2400)["grounding"]
        t = q["tasks"][0]
        assert t["status"] == "NOT_SCORABLE"
        assert t["reason"] == "gt-element-missing"
        assert q["notScorableCount"] == 1

    def test_grounding_not_scorable_when_missing(self):
        gt = {"elements": [], "expectedTexts": [], "groundingTasks": []}
        q = ca.score_frame(_hitting_resp(), gt, 1080, 2400)["grounding"]
        assert q["stance"] == "NOT_SCORABLE"
        assert q.get("hitRate") is None     # 永不为零/不伪造
        assert q.get("taskCount") is None


# ── 统计 / 解析 ──────────────────────────────────────────────────────────────

class TestStats:
    def test_latency_stats_small_n_nearest_rank(self):
        s = ca.latency_stats([100.0, 200.0, 300.0, 400.0, 500.0], gated=False)
        assert s["medianMs"] == 300.0
        assert s["p50Ms"] == 300.0
        assert s["p95Ms"] == 500.0  # nearest-rank ceil(0.95*5)=5 → max
        assert s["gates"]["p50p95"] is False

    def test_latency_stats_pooled_gated(self):
        vals = [float(i) for i in range(1, 21)]  # n=20 ≥ 10
        s = ca.latency_stats(vals, gated=True)
        assert s["p50Ms"] is not None and s["p95Ms"] is not None
        assert s["p95Ms"] >= s["p50Ms"]

    def test_latency_stats_empty(self):
        s = ca.latency_stats([], gated=False)
        assert s["n"] == 0 and s["medianMs"] is None

    def test_parse_server_timing(self):
        header = ("yolo;dur=471.2, ocr;dur=12.0, screenparse;dur=501.3, "
                  "fusion;dur=18.0, scroll;dur=0.4, serialize;dur=3.1, gc;dur=0.5")
        parsed = ca.parse_server_timing(header)
        assert parsed["yolo"] == pytest.approx(471.2)
        assert parsed["screenparse"] == pytest.approx(501.3)
        assert parsed["scroll"] == pytest.approx(0.4)

    def test_parse_server_timing_missing_and_none(self):
        assert ca.parse_server_timing(None)["yolo"] is None
        parsed = ca.parse_server_timing("yolo;dur=10.0")
        assert parsed["yolo"] == 10.0
        assert parsed["ocr"] is None  # 缺分段 → None

    def test_text_metric_scored_never_zero_forbidden(self):
        """NOT_SCORABLE 语义：缺 GT 面 → 指标为 None，不是 0。"""
        gt = {"elements": [], "expectedTexts": [], "groundingTasks": []}
        q = ca.score_frame(synth_response(), gt, 1080, 2400)
        assert q["elements"]["stance"] == "NOT_SCORABLE"
        assert q["elements"].get("precision") is None
        assert q["grounding"]["stance"] == "NOT_SCORABLE"
        assert q["grounding"].get("hitRate") is None


# ── rejected 帧处理（WI-5b 修正 a）──────────────────────────────────────────

class TestRejectedFrames:
    def test_is_rejected_gt_prefix_match(self):
        assert ca.is_rejected_gt({"reviewStatus": "rejected-stale-dump"}) is True
        assert ca.is_rejected_gt({"reviewStatus": "rejected-other"}) is True

    def test_is_rejected_gt_non_rejected(self):
        assert ca.is_rejected_gt({"reviewStatus": "calibrated"}) is False
        assert ca.is_rejected_gt({"reviewStatus": "leader-verified"}) is False
        assert ca.is_rejected_gt({}) is False
        assert ca.is_rejected_gt(None) is False
        assert ca.is_rejected_gt({"reviewStatus": None}) is False

    def test_is_rejected_gt_rejected_prefix_not_substring(self):
        # startswith 语义：'rejected' 子串出现在中间不算（如 'not-rejected'）
        assert ca.is_rejected_gt({"reviewStatus": "not-rejected-stale-dump"}) is False

    def test_discover_frames_excludes_non_frame_gt(self, tmp_path):
        """gt/ 下非帧工件（calibration-decisions.json）不入帧清单——
        帧 id 契约 = 12 位 hex（D5/D6）。"""
        (tmp_path / "frames").mkdir()
        (tmp_path / "gt").mkdir()
        (tmp_path / "frames" / "aaaa11111111.png").write_bytes(b"png")
        (tmp_path / "gt" / "aaaa11111111.json").write_text("{}")
        (tmp_path / "gt" / "calibration-decisions.json").write_text("{}")
        frames = ca.discover_frames(tmp_path)
        ids = [f["frameId"] for f in frames]
        assert ids == ["aaaa11111111"]
        assert "calibration-decisions" not in ids

    def test_sock_path_short_enough(self, tmp_path):
        """（WI-5b）UDS socket 必须短路径：即使 out_dir 很深（fsv001 契约
        路径），socket 也在 OS 临时目录且 < 104B（macOS sun_path 上限）。"""
        deep_out = tmp_path / ("a" * 60) / "evaluation" / "reports" / "fsv001"
        sock = ca._arm_sock_path("baseline", deep_out)
        assert len(str(sock)) < 104
        assert "baseline" in sock.name
        # 同 out_dir 同臂 → 同名（确定性）；异 out_dir → 异名（防并行碰撞）
        assert sock == ca._arm_sock_path("baseline", deep_out)
        assert sock != ca._arm_sock_path("baseline", tmp_path / "other")

    def test_render_report_lists_skipped_frames(self):
        """summary.md 显式列出 skippedForQuality 帧（frameId + reviewStatus）。"""
        report = {
            "timestamps": {"runStartUtc": "2026-09-12T00:00:00+00:00"},
            "matcherRevision": "matcher-greedy-v1",
            "iouThreshold": 0.5, "hitMarginPx": 8,
            "reportFile": "fsv001-abc.json",
            "cli": {"arms": ["baseline"]},
            "frames": [{"frameId": "5df0424f787c"}],
            "arms": {
                "baseline": {
                    "quality": {
                        "skippedForQuality": 3,
                        "skippedForQualityFrames": [
                            {"frameId": "5df0424f787c",
                             "reviewStatus": "rejected-stale-dump"},
                        ],
                    },
                    "latency": {"wall": {"n": 0}, "segments": {}},
                    "rss": {},
                },
            },
        }
        md = ca.render_markdown_report(report)
        assert "5df0424f787c(rejected-stale-dump)" in md
        assert "skippedForQuality" in md


# ── 帧发现（数据驱动，不硬编码帧清单）───────────────────────────────────────

class TestFrameDiscovery:
    def test_union_of_frames_and_gt(self, tmp_path):
        (tmp_path / "frames").mkdir()
        (tmp_path / "gt").mkdir()
        (tmp_path / "frames" / "aaaa11111111.png").write_bytes(b"png-a")
        (tmp_path / "frames" / "bbbb22222222.png").write_bytes(b"png-b")
        (tmp_path / "gt" / "cccc33333333.json").write_text("{}")
        frames = ca.discover_frames(tmp_path, gt_suffix="json")
        ids = [f["frameId"] for f in frames]
        assert ids == ["aaaa11111111", "bbbb22222222", "cccc33333333"]
        by_id = {f["frameId"]: f for f in frames}
        assert by_id["aaaa11111111"]["pngPath"] is not None
        assert by_id["aaaa11111111"]["gtPath"] is None
        assert by_id["cccc33333333"]["pngPath"] is None  # gt-only 帧：缺 PNG 如实标记

    def test_stratum_from_meta_and_default(self, tmp_path):
        (tmp_path / "frames").mkdir()
        (tmp_path / "frames" / "aaaa11111111.png").write_bytes(b"png-a")
        (tmp_path / "frames" / "bbbb22222222.png").write_bytes(b"png-b")
        (tmp_path / "frames" / "aaaa11111111.meta.json").write_text(
            '{"frameId":"aaaa11111111","stratum":"dialog"}')
        frames = ca.discover_frames(tmp_path)
        by_id = {f["frameId"]: f for f in frames}
        assert by_id["aaaa11111111"]["stratum"] == "dialog"
        assert by_id["bbbb22222222"]["stratum"] == "unknown"  # 无 meta → unknown

    def test_frame_subset_prefix_filter(self):
        # run_arms 的 subset 过滤（前缀匹配）——直接走 discover + filter 语义
        frames = [
            {"frameId": "abcdef123456"}, {"frameId": "123456abcdef"},
        ]
        kept = [f for f in frames
                if any(f["frameId"].startswith(p) for p in ("abc", "zzz"))]
        assert [f["frameId"] for f in kept] == ["abcdef123456"]


# ── Slow（真实权重 / 真实服务；默认 suite 不跑）──────────────────────────────

@pytest.mark.skipif(not (_YOLO_WEIGHTS.exists() and _SCREENPARSE_WEIGHTS.exists()),
                    reason="YOLO/ScreenParser 权重不在场（FSV-001 D1 不入 git）")
class TestB2InProcessSlow:
    """B2 进程内 runner 冒烟（真实权重）。注意：B2 消融会改写 server 模块单例
    （_pipelines / OCR 入口），必须快照-恢复，否则污染同进程后续测试
    （WI-3 的 B2 测试用子进程隔离，本 harness 进程内 runner 在测试里等价隔离）。"""

    @pytest.fixture(autouse=True)
    def _restore_server_state(self):
        import uniclaw_perception.server as server
        before = (
            getattr(server, "_config", None),
            dict(getattr(server, "_pipelines", {})),
            getattr(server, "run_rapid_ocr_on_image", None),
            getattr(server, "rapid_ocr_one_crop", None),
        )
        yield
        server._config = before[0]
        server._pipelines = before[1]
        server.run_rapid_ocr_on_image = before[2]
        server.rapid_ocr_one_crop = before[3]

    def test_b2_runner_ocr_empty_and_shape(self):
        """B2Arm 进程内 runner：ocr 恒空、candidates 无文字、计时齐全。"""
        arm = ca.B2Arm("cpu")
        arm.ensure_ready()
        sample = arm.analyze(_LEGACY_PNG.read_bytes())
        body = json.loads(sample["body"])
        assert body["ocr"] == []
        assert all(c.get("text", "") == "" for c in body["candidates"])
        assert body["summary"]["ocrCount"] == 0
        assert sample["serverTimingMs"]["yolo"] is not None
        assert sample["serverTimingMs"]["fusion"] is not None
        assert sample["wallMs"] > 0
        arm.stop()

    def test_b2_deterministic_two_runs(self):
        arm = ca.B2Arm("cpu")
        arm.ensure_ready()
        first = json.loads(arm.analyze(_LEGACY_PNG.read_bytes())["body"])
        second = json.loads(arm.analyze(_LEGACY_PNG.read_bytes())["body"])
        assert json.dumps(first, ensure_ascii=False) == \
            json.dumps(second, ensure_ascii=False)
        arm.stop()