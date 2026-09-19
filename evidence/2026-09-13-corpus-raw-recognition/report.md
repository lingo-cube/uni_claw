# Corpus 测试资产 · YOLO+OCR 原始识别提取

- **日期**: 2026-09-13
- **目的**: 从 7 个 corpus 测试资产（截图）提取 **YOLO 检测 + OCR 文本识别的
  原始输出**（位置 + 文本），**不做** fusion / 聚类 / 分类
  （`candidates` 丢弃）。下游消费方：聚类算法优化实验 / LLM 慢感知实验。
- **资产来源**: `tests/UniClaw.Kernel.Tests/Perception/Corpus/artifacts/`
  下 7 张 PNG（每张配 uiautomator XML 层级真值，`scenarios.json` 为同一
  真值的归一化版——本提取**不用**真值，输出纯识别原始结果）。

## 复现

```bash
# venv + 模型前置（一次性环境已存在：.perception/venv，models/yolo/*best.pt）
MPLCONFIGDIR=/tmp/uniclaw-mplcache .perception/venv/bin/python \
  platforms/perception/evaluation/tools/extract_corpus_layout.py \
  --assets-dir tests/UniClaw.Kernel.Tests/Perception/Corpus/artifacts \
  --out-dir evidence/2026-09-13-corpus-raw-recognition/artifacts
```

- 管线：`bench/run_l2.py` 同款 in-process 全生产管线（YOLO
  `android_ui_detection_yolov8/best.pt` + RapidOCR en，默认 pipeline
  variant `default`），`capture_stage_views=True` 取 `rawLabel/rawClassId`。
- 预处理：顶部/底部各裁 `6.25%`（1080×1920 → 各 120px），最长边缩至 720；
  **所有坐标已通过 remap 映射回原屏 1080×1920 空间**（`boundsPx` 为整数像素）。

## 结果摘要（run-manifest.json）

| scenarioId | YOLO dets | YOLO label 分布 | OCR tokens |
|---|---|---|---|
| `nav03-childa` | 1 | input×1 | 1 |
| `nav03-parent` | 3 | input×2, text_block×1 | 3 |
| `popup01-dialog` | 8 | input×2, text_block×6 | 5 |
| `popup04-page` | 4 | input, popup, button, text_block 各×1 | 3 |
| `popup09-before` | 1 | input×1 | 1 |
| `scroll01-v1` | 11 | text_block×11 | 11 |
| `scroll01-v2` | 11 | text_block×11 | 12 |

逐资产全量数据见 `artifacts/<scenarioId>.yolo-ocr.raw.json`。

## 字段说明（每资产 `<id>.yolo-ocr.raw.json`）

```jsonc
{
  "schema": "uniclaw.yoloOcrRaw.v1",
  "scenarioId": "nav03-parent",
  "artifact": ".../nav03-parent.png",
  "frame": { "width": 1080, "height": 1920 },
  "yolo": [{
    "id": "det_3", "label": "input",            // 归一化 label（YOLO_LABEL_ALIASES 后）
    "confidence": 0.217919,
    "bounds": { "x1": 0.0486, "y1": 0.0648, ... },  // 归一化 [0,1]
    "boundsPx": [52, 124, 1036, 231],           // 原屏像素 [l,t,r,b]
    "center": { "x": 0.504, "y": 0.093 }, "centerPx": [544, 178],
    "rawLabel": "EditText", "rawClassId": 7     // 模型原始类名/id（未归一化）
  }, ...],
  "ocr": [{
    "id": "ocr_1", "text": "Child B", "confidence": 0.9097,
    "bounds": {...}, "boundsPx": [514, 136, 570, 162],
    "center": {...}, "centerPx": [543, 150]
  }, ...]
}
```

## 已知观察（供下游实验参考）

> 2026-09-13 追加更正：初版把 scroll01 首行缺失记为"PNG 与 XML 序号位移"，
> 经像素带逐带 OCR 复核，**真实原因是预处理裁剪致盲**（见下）。以本条为准。

### 1. 预处理裁剪致盲（上游损失，非聚类可救）

管线在推理前裁掉顶部/底部各 `6.25%`（1080×1920 → 各 **120px**），裁掉的
像素对 YOLO/OCR 完全不可见。把 `nav03-parent.png` 逐带 OCR 复核：

| PNG 文本带 | 内容 | 管线是否看到 |
|---|---|---|
| y 55-74 | `NAV_03-PageA` | ✗（在裁剪带内） |
| y 82-118 | `Child A` | ✗（在裁剪带内） |
| y 130-166 | `Child B` | ✓ |
| y 180-215 | `Child C` | ✓ |
| y 230-262 | `RESET SCENARIO` | ✓ |

按 XML 真值统计被裁掉的**有文本**节点（`y2 <= 120` 或 `y1 >= 1800`）：

| scenario | 顶部裁剪内被丢文本 | 底部裁剪内被丢文本 |
|---|---|---|
| nav03-childa / nav03-parent | `NAV_03 — Page A` | — |
| popup04-page | `POPUP_04 — Return Top Left`、`STATE: READY` | — |
| popup09-before | `POPUP_09 — Back Triggers Dialog`、`STATE: READY` | — |
| scroll01-v1 / v2 | `SCROLL_01 — Long List` | `RESET SCENARIO` |

- **7 个资产里 6 个的页面标题整条被裁掉**。
- **scroll01 "序号位移" 由此解释**：v1 的 `Item 01`、v2 的 `Item 02` 文本带
  位于 y≈102-114（节点框 97-205 / 81-157 跨过裁剪线，但文字像素在带内），
  故 OCR 首行直接从下一行开始——不是资产内容错位。

### 2. `nav03-childa` 资产的 PNG 与 XML 不是同一屏幕（资产配对问题）

逐带 OCR `nav03-childa.png`：`NAV_03-PageB`(y55-74)、`Return`(y82-118)、
`RESET SCENARIO`(y130-166)；而同族的 `nav03-parent.png` 是
`NAV_03-PageA` + `Child A/B/C` + `RESET SCENARIO`。但
`nav03-childa.xml` / `scenarios.json` 对 childa 记录的却是 Page A +
Child A/B/C 拓扑。**该资产的截图与 XML 真值描述不同屏幕**——本提取输出以
PNG 实际像素为准（原始识别无误），但**拿它做真值对照会得到误导性结论**。
另注意 nav03 的 sibling/parent 两个场景在 `scenarios.json` 中本就标注为
"文本/结构完全相同" 的 identity 歧义场景。

### 3. 低检出场景

`nav03-childa`、`popup09-before` 各仅 1 个 YOLO 检测。除去上述裁剪因素，
top 区域合成 UI 相对真实 UI 训练域偏移也使检测召回偏低（检测阈值
`detection.confidence = 0.2`，`maxWidth = 720` 缩放）。

## 产物

- `artifacts/*.yolo-ocr.raw.json` ×7 —— 原始识别数据（本提取主交付物）
- `artifacts/run-manifest.json` —— 运行身份（configId/deploymentId/模型/
  OCR 后端）+ 逐场景计数 + 分段计时
- 提取脚本：`platforms/perception/evaluation/tools/extract_corpus_layout.py`