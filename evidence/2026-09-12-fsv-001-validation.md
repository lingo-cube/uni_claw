# FSV-001 — FastScreen（ScreenParser）Integration & Replacement Validation · Human Gate 报告

- 日期：2026-09-12（v2 修订版）· Leader：FSV-001（state：changes/FSV-001/state.md）
- 判定建议：**NO_CHANGE**（操作结论；依据已在输入域修正后改写，见 §8）
- 主数据：**v2 runId `fsv001-7b1439bb8758`**（`platforms/perception/evaluation/reports/fsv001-v2/`）
  39 帧 × 四臂 × runs=5（36 评分 + 3 stale 排除）
- ⚠️ v1（`reports/fsv001/`，runId fsv001-eed2e12a65a2）**保留为被输入域
  混杂因子污染的中间轮**（见 §2），其 B1 质量劣势结论已被 v2 证伪；
  v1 的探针机制发现（重复行/滚动失效/legacy counts）仍然有效。

---

## 1. Current Pipeline（实验对象的真实结构）

```
AdbScreenshotAcquisition(PNG) → PngImage.Decode(RGBA)
→ VisionServiceClient → POST /v1/analyze_raw
→ [Python provider] preprocess(crop 6.25%/resize≤720)
   → detect: YOLOv8-21cls(android_ui_detection) ∥ recognize: rapidocr(en PP-OCRv4)
   → fuse(operators: row grouping/spacing/heuristics/promotions)
   → remap + enforce_geometry
→ evidence JSON{yolo[],ocr[],candidates[],…} (uniclaw.localVisionEvidence.v1)
→ [C#] envelope 校验 → RawArtifact(D9) → LiveVisionStrategy
   （唯一 yolo[]/ocr[] 消费者）
→ FastPerception → EvidenceLedger.Admit → WorldModel → grounding/effects
```

候选：docling-project/ScreenParser v2（YOLO11-L@1280，55 类，apache-2.0，
训练分布 = web 截图；权重 sha256 `dbcb4f58…`，未入 git）。接入全部经
`FastScreen Provider → Adapter（55→canonical）→ 现有契约`；四臂对 C#
Runtime **零代码修改**（FastScreenArmContractTests 11/11）。

## 2. 输入域修正（D11，本轮最重要的方法学事件）

官方文档核查发现：v1 的 screenparse 阶段消费 **preprocess 后图像**
（crop+resize≤720w），而官方 operating point = **整屏截图**。实测整屏
输入每帧多检出 ~25–50% 元素（settings-home raw 33 vs 22）。v1 对
ScreenParser 系臂系统性不利 → 判定依据不可靠。

修正（WI-6）：screenparse 摄入原始 image，输出经确定性逆映射
（`geom.py::map_original_to_proc`，双向单测锁定；越界 fail-closed 丢弃，
计数入 `screenParse.summary.droppedOffCanvas`，全帧合计 664）回 proc
空间后参与 fusion——fusion/remap/enforce_geometry 零修改。baseline 臂
v2 全指标 Δ=0（锚）；默认管道三资产逐字节回归绿；pytest **136**。

## 3. Test A — Integration Result（YOLO/OCR 原样 + FastScreen step，v2）

| 指标（36 帧 pooled） | baseline | A | Δ |
|---|---|---|---|
| element P / R / F1（IoU≥0.5 主口径） | 0.091/0.161/0.116 | 0.089/0.182/**0.120** | F1 +0.004 |
| element R / P（center 行容忍） | 0.262/0.149 | 0.267/0.131 | +0.005 / −0.018 |
| typeAccuracy | 0.670 | 0.691 | +0.021 |
| text exact / CER | 0.532 / 0.202 | 同 | 0 |
| candidate F1 | 0.092 | 0.090 | −0.002 |
| grounding hit | 0.370 | 0.370 | **0** |
| wall P50 / P95 (ms) | 556 / 823 | **1028 / 1368** | **+472 / +545（1.85×）** |
| RSS max (MB) | 861 | 1253 | +392 |

- 救援命中 10/118（8.5%，v1 为 6/109@5.5%）——仍以低置信 text_block
  碎片为主；text-heavy 分层 recall 增益保持。
- **结论：质量增益真实但微小（F1 +0.004、grounding 持平），延迟近
  2×——cost/benefit 不成立。**

## 4. Test B — Replacement Result（相同 downstream contract，v2）

| 指标（36 帧 pooled） | baseline | B1（换 detector 留 OCR） | B2（+OCR 关闭） |
|---|---|---|---|
| element P / R / F1（IoU 主口径） | 0.091/0.161/0.116 | 0.086/**0.180**/0.116 | 同 B1 |
| element R / P（center 行容忍） | 0.262/0.149 | **0.435 / 0.207** | 同 B1 |
| typeAccuracy（class-agnostic 配对） | 0.670 | **0.750** | 同 B1 |
| bboxIoU（matched） | 0.697 | **0.792** | 同 B1 |
| text exact / CER | 0.532 / 0.202 | 0.532 / 0.202（OCR 留） | **0.000 / —** |
| candidate F1 | 0.092 | 0.063 | 0.092 |
| grounding hit | 0.370 | 0.370（v1 曾 0.435，3 任务因
  resolver 最小包含框换框翻 MISS，OCR 未变） | **0.022** |
| grounding no-box / off-target | 6 / 5 | 0 / 10 | 0 / 10 |
| wall P50 / P95 (ms) | 556 / 823 | **896 / 1215**（+61%） | 458 / 617 |
| yolo 段 P50 (ms) | 395 | **671**（+70%） | 456 |
| RSS max (MB) | 861 | **1429**（+568） | 909 |

分层（element recall，B1 vs baseline，v2）：dense/settings/sidebar/
dialog/list 提升 2–3×；text-heavy **0.483 领先**；**icon-heavy 回退
0.141→0.090（该层 grounding 0.25→0.0，唯一弱层）**。

- **B2（整体替代，含删 OCR）：否决不变**——text 归零、grounding
  崩至 0.022。OCR 是任何 FastScreen 路线的硬前提。
- **B1（detector 替代、OCR 保留）：v2 改写画像**——元素 R 反超
  baseline（0.180 vs 0.161）、F1 打平、center 口径大幅领先（0.435 vs
  0.262）、typeAcc/IoU 更优、零 no-box；但 grounding 平价无优势、
  candidate F1 降、icon-heavy 弱层、延迟 +61%、内存 +568MB。
  **v1 的「系统性质量劣势」结论被证伪**；真实定位 = **延迟/内存预算
  放宽条件下的 PARTIAL_REPLACE 候选项**，不推荐现在落地。

## 5. Accuracy / Latency / Resource 汇总

见 §3/§4 表（per-stratum/混淆矩阵/miss 归因全量：
`reports/fsv001-v2/summary.md` + `ANALYSIS-DELTA.md`；v1↔v2 delta
复现脚本 `evaluation/analysis_delta.py`）。MPS 辅助表取消（D10：本机
MPS 实测慢于 CPU 且无四臂 MPS 变体组合）。

## 6. Protocol Impact

- 响应契约：additive `screenParse[]`（v2 起 summary 携带
  `inputSpace:"original"` + `droppedOffCanvas`）；`yolo[]/ocr[]/
  candidates[]` 结构不变；默认管道（无变体头）三资产**感知面逐字节
  不变**（configId 严格一致）。
- Runtime：零 C# 生产代码修改；11/11 契约测试（四臂真实 fixtures）；
  live 真机闭环 1/1（emulator + 真服务 + WorldModel）。
- 变体：`X-Pipeline-Variant` 预声明（fastscreen-integration /
  fastscreen-replacement(-mps)），lint fail-closed（A/B 互斥）。

## 7. Failure Cases（v2 修订）

1. **icon-heavy 弱层**：整屏输入下 B1 该层 R 反降（0.141→0.090）+
   grounding 归零——Android 图标仍是 ScreenParser OOD 重灾区
   （legacy counts：icon baseline 7 vs GT 13 vs B1 1）。
2. **重复行机制（v1 探针，仍有效）**：同一行被双类检出
   （text_block+menu_item 重叠框，fusion 去重不生效）→ 制造非唯一
   导航候选 = 历史 F 类失败机制面。
3. **滚动中目标四臂全失效**：fast perception 无跨帧记忆；纯检测器
   无文字/时序（K 类滚动场景无解，ScreenParser ≠ Slow 层）。
4. **rescue 低效**：118 救援 10 命中（8.5%），低置信 text_block 碎片
   主导——rescue 规则需限定 interactive 类才有价值（M3 素材）。
5. **structural Optional Evidence 近空而非恒空**：v2 检出 14 条
   （Calendar 6 / Progress bar 6 / Rating Indicator 2）；v1 的「容器类
   =0」是 proc 输入假象。容器类（List/Window/TabBar）在 v2 leaf 注释
   训练下本就稀少——层级/结构理解的正确归属是 ScreenVLM（M4）。
6. 方法学：GT = a11y 辅助 + 确定性校正（D6r，无视觉通道），3 帧
   stale 排除；element 绝对值含框语义偏差（文字 extent vs 整行
   extent），四臂同 GT 相对比较有效。

## 8. Recommendation（修订后）

**NO_CHANGE（操作结论，依据已改写）**：

- 不将 fastscreen-integration 提升默认：F1 +0.004 / grounding 持平
  vs 延迟 1.85×、内存 +392MB——不成比例。
- 不以 ScreenParser 替代现有 YOLO：质量画像 v2 已达平价以上
  （R 反超、typeAcc/IoU/center 更优），**但** grounding 无净收益、
  icon-heavy 弱层、延迟 +61% / 内存 +568MB——当前预算下仍是降级。
- 绝不删 OCR（B2：grounding 0.022）。

**PARTIAL_REPLACE（detector 换、OCR 留）状态变更**：从「被证据否决」
修订为「**延迟/内存预算放宽条件下的候选项**」（延迟预算 ≥ ~900ms/帧
且 icon-heavy 弱层可接受/修复时重评）。不落地，交 Human Gate。

## 9. Migration Proposal（待人工裁决，均未执行）

| 选项 | 内容 | 建议 |
|---|---|---|
| M1 | 保持现状：实验变体保留（opt-in、默认零影响） | **推荐** |
| M2 | 验证集资产化（帧级/集级登记入 assets/） | 可选 |
| M3 | FSV-002：rescue 限定 interactive 类重测 A；mobile 域 fine-tune 消 OOD（icon/行容器）后重评 B1；延迟预算问题一并裁决 | 条件触发 |
| M4 | Fast/Slow Perception Validation（ScreenVLM probe；uni-agent 侧 71-query grounding / 9 题 dump 判型基准可移植，见 state M4 素材记录） | 独立后续 Change |

禁制确认：未删 YOLO/OCR、未改 Runtime contract、未迁移架构、未引入
ScreenVLM production 依赖、权重未入 git、默认管道行为不变。

## 10. 验证证据索引

- V1 默认回归：pytest 136（含三资产逐字节对拍 + 逆映射 17 例 +
  整屏 vs 预处理 slow 对拍）
- V2 确定性：screenparse/replacement/compare_arms/geom 单测全绿
- V3 四臂 scorecard：v2 `reports/fsv001-v2/`（主）+ v1 `reports/fsv001/`
  （混杂中间轮，保留 provenance）
- V4 Runtime 契约：FastScreenArmContractTests 11/11（真实四臂 fixtures）
- V5 真机闭环：LiveClosedLoopBulletTests 1/1
- C# 全量：361 + 17 passed
- 输入域修正前后 baseline 锚 Δ=0；B2 vs B1 yolo 39/39 equal（变量隔离）

## 11. 附：v1 探针补充证据（机制发现仍有效，数值已被 v2 取代）

- 历史失败点探针（`reports/fsv001/probe/`）：B1 重复行（F 类机制）、
  滚动中全失效（K 类）、ScreenParser ≠ Slow 层——机制结论维持；
  其 grounding 0.435 等 v1 数值以 v2 为准。
- 检测准确性三面交叉（`probe/detection-report-v2.md`）：rescue 解剖
  （低置信 text_block 碎片）、icon/list OOD——与 v2 icon-heavy 回退
  互证；其「B1 整体下降」总结论已被 v2 修正为「平价以上、预算内
  不划算」。
