# Screenshots fixture — 视觉 golden 测试资产

供 `VisionGoldenIntegrationTests`（云端 AI）和 `LocalVisionProviderTests.Baseline_GoldenEvidence_MapsToExpectedDto`（本地 Python）使用。

## 1. 云端 AI Golden（VisionGoldenIntegrationTests）

### 资产

- 截图：`*.png` / `*.jpg` / `*.jpeg`
- 预期 golden：截图同目录同名 `.expected.json`
- 实际结果：每次运行写 `.actual.json`

### 运行

```bash
# 校准
UNICLAW_INTEGRATION_SCOPES=vision-golden \
UNICLAW_VISION_UPDATE_EXPECTED=1 \
dotnet test tests/UniClaw.Core.Tests --filter "IntegrationScope=vision-golden"

# 回归
UNICLAW_INTEGRATION_SCOPES=vision-golden \
dotnet test tests/UniClaw.Core.Tests --filter "IntegrationScope=vision-golden"
```

---

## 2. 本地 Python 管线 Golden（local-vision baseline）

### 协议

```
截图 JPEG ──→  Python FastAPI ──→  evidence JSON  ──→  C# MapToPageAnalysisDto ──→  expected JSON
              POST /v1/analyze     (*.evidence.json)                                (*.expected.json)
```

### 资产

```
<name>.jpg                                  # 截图输入
<name>.local-vision.evidence.json           # Python evidence（输入 golden）
<name>.local-vision.expected.json           # C# DTO 期望输出（输出 golden）
<name>.local-vision.actual.json             # 每次运行生成（供 diff）
```

### 输入 — Evidence JSON (`uniclaw.localVisionEvidence.v1`)

| 字段 | 类型 | 说明 |
|------|------|------|
| `image` | `{width, height}` | 原图尺寸 |
| `candidates[]` | array | 融合后的候选元素 |
| `candidates[].type` | string | YOLO label（经 `YOLO_LABEL_ALIASES` 归一化：view→list_item, imageview→icon, text→text_block, line→icon） |
| `candidates[].text` | string | 关联 OCR 文本（无则 `""`） |
| `candidates[].confidence` | float | 工程评分（非概率，仅排序/调试） |
| `candidates[].confidenceDetail` | `{yolo, ocr}` | 分项置信度 |
| `candidates[].bounds` | `{x1,y1,x2,y2}` | 归一化边界框（0-1） |
| `candidates[].boundsPx` | `[int×4]` | 像素边界框（原图坐标系） |
| `candidates[].center` | `{x, y}` | 归一化中心（0-1） |
| `candidates[].evidence` | `{yoloId, ocrIds[], allIds[]}` | 溯源 |
| `candidates[].riskFlags` | `[string]` | low_yolo_confidence / no_text_evidence / low_ocr_confidence / ocr_only |
| `scrollHints` | `{totalCandidates, candidatesNearBottom, scrollbarDetected}` | Python 只出原始值 |
| `metadata` | `{schema, width, height, pipeline, models, configHash}` | 版本追踪 |

### 输出 — C# PageAnalysisDto JSON

`LocalVisionProvider.MapToPageAnalysisDto(evidence)` 经 4 步映射管道：

| 步骤 | 逻辑 |
|------|------|
| Step 1 — Label Mapping | 查 `label-mapping.json`，YOLO label → AI type；未知 → `"text"` + warning |
| Step 2 — Y 轴聚类 | `center.y < 0.08` → `level1_menus`；横向（X方差>Y方差）→ `left`/`right`；纵向 → `top`/`bottom`；无菜单 → null |
| Step 3 — Scroll Gate | 保守化：total=0 → `has_scroll:true, is_end_of_list:false`；total>capacity 或 scrollbar → has_scroll；nearBottom=0 且无scrollbar → is_end_of_list |
| Step 4 — Popup | nonItemLabels 候选 → `is_popup:true`，最近非popup候选 → `close_button` |

DTO 字段：`items` / `level1_menus` / `level1_dir` / `level2_menus` / `level2_dir` / `current_path` / `is_popup` / `popup_info` / `close_button` / `back_button` / `has_scroll` / `is_end_of_list`

### 运行

```bash
# 日常回归（CI 安全，无外部依赖）
dotnet test --filter Baseline_GoldenEvidence

# 校准（模型/代码变更后）
# 1. 生成 evidence
curl -s -X POST http://127.0.0.1:8765/v1/analyze \
  --data-binary @<截图.jpg> > <截图>.local-vision.evidence.json

# 2. 校准 expected
UNICLAW_LOCAL_VISION_UPDATE_EXPECTED=1 dotnet test --filter Baseline_GoldenEvidence

# 3. 人工 diff actual vs expected，确认后提交
```

### 环境变量

| 变量 | 默认值 | 说明 |
|------|--------|------|
| `UNICLAW_YOLO_MODEL` | `artifacts/local-vision/models/android_ui_detection_yolov8/best.pt` | YOLO 模型路径 |
| `UNICLAW_OCR_LANG` | `ch` | OCR 语言（`ch` / `en`） |
| `UNICLAW_OCR_PARALLEL` | `2` | OCR 线程数 |
| `UNICLAW_LOCAL_VISION_UPDATE_EXPECTED` | — | 设为 `1` 进入校准模式 |

---

## 说明

- 云端 golden 采用容差匹配（名称或坐标命中其一即可，额外项允许）。
- 本地 golden 采用 JSON 递归深度比较（null 字段省略兼容）。
- 本目录截图来自真实 Android 设备（PKJ110，1440×3168）。
