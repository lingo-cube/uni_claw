# Legacy Perception Evidence Assessment（uni-agent → Target Fast Perception）

> DocumentType: `LEGACY_PERCEPTION_EVIDENCE_ASSESSMENT`
>
> Status: `CANDIDATE / NOT_ADOPTED`
>
> Authority: `NONE`
>
> Date: `2026-09-09`
>
> Scope: `uni-agent` 分支 perception 实现代码与真实资产的独立兼容性评估
> （PER-002A0/A）→ PER-002B target-neutral corpus 与 Fast Perception slice 的证据基础
>
> Forbidden Boundary: 本文是 Legacy Evidence / Architecture Reference。uni-agent
> 提供 Evidence，不提供 Authority；legacy file format ≠ target protocol；
> legacy Container/Page truth ≠ target canonical truth。所有结论来自
> `git ls-tree`/`git show` 的真实代码与资产（含对 subagent 盘点的抽样交叉验证），
> 不以旧报告为证据。

---

## 1. Capability Inventory（真实实现）

| Capability | Symbol | Input | Output | Consumer | Target assessment |
|---|---|---|---|---|---|
| YOLO UI detection | `run_yolo_on_image`（`platforms/perception/uniclaw_perception/yolo/inference.py`；模型 `models/yolo/android_ui_detection_yolov8/best.pt` 6.2MB） | PIL Image | `Detection{id,label,confidence,box,raw_label}` | fusion engine | **DEFER**（real model runtime not yet migrated；观测语义已由 corpus 记录承载） |
| OCR | `ocr/rapid.py`（RapidOCR/ONNX en_PP-OCRv4 7.7MB）、`ocr/paddle.py`、`ocr/normalize.py` | 全图或 per-detection crop | `OcrToken{id,text,confidence,box}` | fusion | **DEFER**（同上；真实 OCR 输出经 DIRECT corpus 保留） |
| Fusion/layout | `fusion/engine.py`（58KB）+ `heuristics/row_stabilizer/row_grouping/scoring` | detections+tokens+dims | candidates/yolo/ocr+trace | C# `LocalVisionPerceptionSource` | **ADAPT idea**（row composition/stabilization 思想；实现不迁移） |
| Operator rules | `operators/row_relation_head.py`（42KB）等 | candidate rows | composed rows+trace | fusion | **ADAPT idea**（布局组合规则语义） |
| Geometry 校验 | `schema.py Box`、`remap.py enforce_geometry` | evidence dict | geometry-validated | server boundary | **ADOPT IDEA**（dual normalized+pixel 坐标纪律 → corpus/manifest 沿用） |
| Screen capture | C# `AdbScreenshotSource`（`adb exec-out screencap -p` → PNG） | device | `ScreenshotCapture(bitmap,w,h)` | observation loop | **ADAPT idea**（未来 device 层；本轮不迁移 driver） |
| UI hierarchy dump | uiautomator XML（reality-evidence 配对资产） | device screen | node tree（text/resource-id/class/bounds pixel） | fixture 证据 | **ADAPT**（本轮 corpus 主源；已转 target-neutral） |
| Semantic/VLM + embedding | `SemanticEvidenceV2`、bge-small profile V4（384-d cosine） | element text+type | container-identity evidence | container/world model | **REFERENCE_ONLY / DEFER**（identity 判定属 UIWorld；embedding 仅作未来 similarity evidence strategy） |
| Trace capture | `FileTraceCaptureStore`（bundle 格式有码无实例） | TraceCaptureBundle | capture dir | replay tests | **REFERENCE_ONLY**（分支上零实例 committed；见 trace 分析文档） |

## 2. Asset Compatibility Matrix（真实资产）

| Asset 组 | Format | Semantics | Provenance | Disposition |
|---|---|---|---|---|
| `tests/.../Perception/Assets/golden-run-v1`（3 帧 + golden provider JSON + scenario-manifest，22KB JSON/97KB PNG） | DIRECT（plain JSON，schemaVersion，dual normalized/pixel） | observation evidence（golden JSON 无 world truth） | 真实 emulator-5554，recordedReality，sha256-pinned | **DIRECT**（case-a-before 已端到端使用） |
| `wifi-slice2-calibration`（OFF/ON 帧 + perception JSON + adb-verified provenance） | DIRECT | observation evidence + adb 验证真值 | 真实 | **DIRECT**（本轮未入 corpus，备用） |
| `tools/android-runtime-reality-fixture/reality-evidence`（POPUP/NAV/SCROLL/COMPOSE，PNG+uiautomator XML 对） | DIRECT（PNG 1080×1920 RGBA + XML pixel bounds） | observation evidence（无 CurrentPage/ContainerId 字段） | 真实 emulator fixture app，README 场景表 | **ADAPT**（7 帧已入 corpus：SCROLL-01 v1/v2、POPUP-01 before/popup、POPUP-04、NAV-03 parent/childA） |
| `platforms/perception/evaluation`（synthetic fixtures/groundtruth/predictions，content-addressed） | DIRECT | 纯 observation evidence | 真实评测跑批 | **DIRECT**（合格但本轮未选；YOLO runtime 未迁移，帧无配对语义上下文） |
| p26-v2-run8/9 帧序列 + fusion traces（334–737KB） | ADAPTABLE（trace schema 大、无 reader 格式文档） | 混合：trace 内含 CurrentPage/container 判定 | 真实调试 run | **REFERENCE_ONLY**（帧干净可用，trace 含 legacy 世界真相不迁移） |
| `semantic-assets/heldout`（ContainerIdentity v1–v4，48 case，含 WrongPage 负例） | DIRECT | `expectedIdentity` 标签 = legacy identity 真值 | 真实人工策展 | **REFERENCE_ONLY**（未来 identity 策略的评估语料；不得作为 observation） |
| `vlm-compare`（qwen vs ui-tars 记录，含误分类） | ADAPTABLE | observation evidence（预测对比） | 真实 | **REFERENCE_ONLY** |
| training mini-data（YOLO txt 标签 + mini-run 权重） | DIRECT | observation evidence | 真实训练产物 | **REFERENCE_ONLY**（模型 runtime 未迁移） |
| `docs/work/active` 3 张 debug 截图 | 可解析 | 无上下文 | **unknown** | **REJECT**（provenance unknown，语义无法建立） |

坐标约定事实：perception JSON = dual（`bounds` normalized 0..1 + `boundsPx` pixel）；uiautomator XML = pixel `[l,t][r,b]`；heldout corpus = 无坐标。corpus 统一为 artifact-frame pixel（`spatial.artifact.bounds.*`，P-UW-16 满足）。

## 3. Failure Corpus（真实失败证据核查）

- 真实 OCR/detector miss 图像转储：**未保存**（`evaluation/failure_candidate.py` 自证
  “No real FailureEpisode exists in the current corpus → boundary proven with
  SYNTHETIC provenance only”）。
- 真实几何攻击负例：`test_geometry_enforcement.py`（x1=-0.2/x2=2.4 载荷，仅测试内构造，无图）。
- 真实修复对：`perception-navigation-row-composition-repair/evidence/{before,after}.png`（row 组合失败→修复，双帧保留）。
- 真实 VLM 误分类记录：`vlm-compare/hard4-*.json`（truth `section_label`→pred `page_header` 等；部分无图）。
- 真实降级帧（本轮负向使用）：`POPUP-01/before.png`（无 dialog 节点——missing detection ≠ absence 的真实验证载体）。

**结论**：S8 无可恢复的“provider 输出坏数据”真实资产；采用「真实 artifact + 显式
synthetic 降级」（`popup04-degraded`，provenance 如实标注）负向验证，不伪造 legacy failure。

## 4. Adopt / Adapt / Reject / Defer

- **ADOPT IDEA**：dual 坐标纪律（normalized+pixel）；content-addressed fixture；
  sha256-pinned scenario manifest；uiautomator 配对采集模式（PNG+结构 dump）。
- **ADAPT**（已执行）：uiautomator XML → target-neutral observations
  （`tools/legacy-perception-import/import.cs` 一次性 adapter；含 ListView 复用
  resource-id 的帧内 subject 去重）；golden provider JSON → observations（DIRECT 直读）。
- **REJECT**：legacy fusion trace 中的 CurrentPage/container 判定（世界真相不迁移）；
  `expectedIdentity` 标签作为输入真值；无 provenance 的 debug 截图。
- **DEFER**：YOLO/OCR real model runtime、fusion/operator 实现、embedding/VLM、
  device capture driver——等真实 buyer（Slow/Retrieval/设备接入 change）。

## 5. Coverage Gaps

| 场景 | 证据状态 |
|---|---|
| 文本观测（settings 页） | **真实**（golden OCR：Internet/Wi-Fi/AndroidWifi…） |
| 滚动连续性（同 signature、内容位移） | **真实**（SCROLL-01 v1→v2 真机帧） |
| 滚动反证（不同页面） | **真实**（SCROLL-01 v1 → POPUP-04 页） |
| 相似布局/不同语义 | **真实**（POPUP-09 before vs POPUP-04 页） |
| Overlay/Dialog | **真实**（POPUP-01 before/popup，AlertDialog 层级完整） |
| duplicate-titles/distinct-identity（SCROLL-02 语义） | **真实资产**（NAV-03 parent/childA 文本相同）+ KnownAmbiguity 记录；Fast 无法判别（这正是 Slow/未来策略的 buyer 证据） |
| 部分观测 | 真实 artifact + **synthetic** 检测子集（无真实 partial-detection 转储） |
| Provider 坏输出 | 无真实资产（代码自证未保存）；**synthetic** 降级显式标注 |

→ `REAL_ASSET_COVERAGE_PARTIAL`（partial/failure 两类无真实 provider 侧资产，其余真实）。
