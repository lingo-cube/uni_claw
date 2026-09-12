# FSV-001 — FastScreen（ScreenParser）Integration & Replacement Validation · Human Gate 报告

- 日期：2026-09-12 · Leader：FSV-001（state：changes/FSV-001/state.md）
- 判定建议：**NO_CHANGE**（不集成、不替代；条件性探索留档，见 §8）
- 数据：39 帧业务验证集（36 评分 + 3 stale 排除）× 四臂 × 5 runs；
  报告 `platforms/perception/evaluation/reports/fsv001/`（runId
  fsv001-eed2e12a65a2，summary.md + ANALYSIS.md + 39 帧明细）

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
   （唯一 yolo[]/ocr[] 消费者：ui.detect.{id}.class /
    spatial.artifact.bounds / ui.text.ocr{i}）
→ FastPerception → EvidenceLedger.Admit → WorldModel → grounding/effects
```

候选：docling-project/ScreenParser v2（YOLO11-L@1280，55 类，apache-2.0，
**训练分布 = web 截图**；权重 sha256 `dbcb4f58…`，未入 git）。接入全部经
`FastScreen Provider → Adapter（55→canonical 映射）→ 现有契约`，四个臂
对 C# Runtime **零代码修改**（FastScreenArmContractTests 11/11 证明）。

## 2. Test A — Integration Result（YOLO/OCR 原样 + FastScreen step）

变体 `fastscreen-integration`：screenparse stage（detect∥recognize 后、
fuse 前）做漏检救援（IoU<0.30 ∧ conf≥0.35 追加，109 元素）+
additive `screenParse[]`（structural Optional Evidence + corroboration）。

| 指标（36 帧 pooled） | baseline | A | Δ |
|---|---|---|---|
| element F1（IoU≥0.5 主口径） | 0.116 | 0.114 | ≈0 |
| element R（主） | 0.161 | 0.173 | +0.012 |
| element R/P（center 行容忍） | 0.262/0.149 | 0.262/0.130 | 0 / −0.019 |
| typeAccuracy | 0.670 | 0.679 | +0.009 |
| text exact / CER | 0.532 / 0.202 | 同 | 0 |
| candidate F1 | 0.092 | 0.090 | −0.002 |
| grounding hit | 0.370 | 0.370 | **0** |
| wall P50 / P95 (ms) | 513 / 787 | 1030 / 1403 | **+517 / +615（2.0×）** |
| RSS max (MB) | 860 | 1233 | +373 |

- 唯一显著增益：text-heavy 分层 recall 0.172→0.276（+10.4pp）。
- 救援元素 109 个中仅 6 个命中 GT（5.5%）——增量元素大多无对应
  GT 实体（text_block×91 主导，多为重复/碎片文字框）。
- corroboration 通道从未触发（fs conf≥0.80 在 Android 原生 UI 上极稀，
  OOD 数据事实）；structural evidence 无 runtime buyer（按 §16 未消费）。
- **结论：当前 v1 集成形态以 ~2× 延迟换取≈0 的全局质量变化，不值得。**

## 3. Test B — Replacement Result（相同 downstream contract A/B）

B1 = 变体 `fastscreen-replacement`（detect 换 ScreenParser，OCR 留）；
B2 = OCR 关闭消融（诚实面对 ScreenParser 无文字输出）。

| 指标（36 帧 pooled） | baseline | B1 | B2 |
|---|---|---|---|
| element P / R / F1（IoU 主口径） | 0.091/0.161/0.116 | 0.063/0.137/**0.087** | 同 B1 |
| element R / P（center 行容忍） | 0.262/0.149 | **0.428/0.198** | 同 B1 |
| typeAccuracy（class-agnostic 配对） | 0.670 | **0.760** | 同 B1 |
| text exact / CER | 0.532 / 0.202 | 0.532 / 0.202（OCR 留） | **0.000 / —** |
| candidate F1 | 0.092 | **0.038** | 0.034 |
| grounding hit | 0.370 | **0.435** | **0.065** |
| grounding no-box / off-target | 6 / 5 | **0** / 8 | 0 / 8 |
| wall P50 / P95 (ms) | 513 / 787 | 828 / 1221（+61%） | 443 / 453 |
| RSS max (MB) | 860 | **1416**（+64%） | 1051 |

分层（element recall，B1 vs baseline）：text-heavy **0.448 vs 0.172（+27.6pp
唯一大胜）**、scrollable +2.2pp、sidebar +3.3pp；list **0.016 vs 0.082（−6.6pp）**、
dense −10.8pp、dialog −3.1pp、icon-heavy −5.1pp、settings −6.3pp。

- **B2（整体替代，含删 OCR）：否决**——文字能力归零直接摧毁 grounding
  （0.370→0.065）、candidates 塌缩（11→4 级），ScreenParser 本身不产文字。
- **B1（detector 替代、OCR 保留）：不推荐现在做**——行容忍 recall
  （+16.6pp）/typeAcc（+9pp）/grounding（+6.5pp）/零 no-box 是真实优势
  （框中心落点与叶子类型训练的直接收益）；但 IoU 口径 F1、precision、
  candidate 质量、核心业务分层（list/dense/dialog/settings——恰是
  Android 设置类主场景）全面退步，且延迟 +61%、内存 +64%。根因：
  web 训练分布 OOD + 类词汇粒度（Text/Heading→text_block 泛滥：
  B1 独有检出 443 中 317 是 text_block，GT 命中率 8.8% vs baseline
  独有 295/21.4%），list 行容器语义缺失。
- 有效实验结论形态（任务书 §12 允许）：**"Detector replaced, OCR
  retained" 在当前数据上不成立**；OCR 保留是任何 FastScreen 路线的
  硬前提（B2 已证）。

## 4. Accuracy / Latency / Resource 汇总

见 §2/§3 表（全量含 per-stratum/混淆矩阵/miss 归因：
reports/fsv001/summary.md、ANALYSIS.md）。补充：

- 延迟分解：A 的 screenparse 段 P50=428ms（占 A wall 42%）；B1 的
  screenparse 计入 yolo 段（651 vs 366ms）。CPU 主表 = 生产默认 device。
- MPS：本机实测比 CPU 慢（B1 2233ms vs 626ms），辅助表取消（D10）。
- 混淆亮点：B1 的 text_block→text_block 42 对（强）但 button→icon 9、
  input→list_item 5（Android 图标按钮/输入行语义错位）。

## 5. Protocol Impact

- 响应契约：additive `screenParse[]` 键（变体开时）；`yolo[]/ocr[]/
  candidates[]` 结构不变。默认管道（无变体头）三资产**感知面逐字节
  不变**（configId 严格一致；pipelineRevision/deploymentId = 内容寻址
  源码哈希按设计变化）。
- Runtime：零 C# 生产代码修改；11/11 契约测试证明四臂响应被
  LiveVisionStrategy/FastPerception 现状消费；live 闭环（真机模拟器 +
  真服务 + WorldModel）1/1 绿。
- 变体机制：`X-Pipeline-Variant` 预声明选择（fastscreen-integration /
  fastscreen-replacement(-mps)），lint fail-closed（含 A/B 互斥）。

## 6. Failure Cases（代表性）

1. list 分层（业务主场景）：B1 recall 0.016——ScreenParser 无行容器
   检出（List Item→list_item 映射几乎不触发），fusion 行组合失去输入。
2. 救援假阳性：A 臂 rescue 91/109 为 text_block 碎片（GT 命中 5.5%）。
3. corroboration 永不触发：Android 原生 UI 上 fs conf≥0.80 稀缺
   （三资产合计 1 个）。
4. dense/icon-heavy：B1 的 web 式 Text 泛化产生大量低值检出（P 降）。
5. 方法学：GT 为 a11y 辅助 + 确定性校正（D6r，无视觉核对通道），
   3 帧 stale dump 排除；element 绝对值含框语义偏差（YOLO/ScreenParser
   文字 extent vs GT 整行 extent），四臂同 GT 下相对比较有效。

## 7. Recommendation

**NO_CHANGE**：
- 不将 fastscreen-integration 提升为默认（增益≈0、2× 延迟）。
- 不以 ScreenParser 替代现有 YOLO（核心业务分层全面退步）。
- 绝不删 OCR（B2 证明文字能力不可损失）。

**留档的条件性探索信号**（不构成本轮裁决）：
- text-heavy 场景 +27.6pp、typeAcc/center/grounding 全面占优——若未来
  业务主面转向 web/desktop UI，或出现 mobile 域微调权重，重开 FSV-002。
- FastScreen 抽象（Provider→Adapter→契约）本身成立：两种接入形态
  （叠加/替换）均零 Runtime 改动落地，验证了 §17 的抽象目标。

## 8. Migration Proposal（待人工裁决，均未执行）

| 选项 | 内容 | 建议 |
|---|---|---|
| M1 | 保持现状：实验变体保留（opt-in、默认零影响），不推广 | **推荐** |
| M2 | 验证集资产化（D9 路径 A 帧级 / B 集级登记入 assets/） | 可选，便于后续回归 |
| M3 | FSV-002：mobile-finetuned detector / 选择性 text-heavy 路由评估 | 条件触发 |
| M4 | Fast/Slow Perception Validation（ScreenVLM probe，任务书 §18） | 独立后续 Change |

禁制确认：未删 YOLO/OCR、未改 Runtime contract、未迁移架构、
未引入 ScreenVLM production 依赖、权重未入 git、默认管道行为不变。

## 9. 验证证据索引

- V1 默认回归：pytest（tests/ 115 passed，含三资产逐字节对拍）
- V2 确定性：screenparse/replacement/compare_arms 单测全绿
- V3 四臂 scorecard：reports/fsv001/（内容寻址 runId）
- V4 Runtime 契约：FastScreenArmContractTests 11/11（真实四臂 fixtures）
- V5 真机闭环：LiveClosedLoopBulletTests 1/1（emulator-5554 + live 服务）
- C# 全量：361 + 17 passed

## 10. 补充证据 — 历史失败点四臂探针（信号级，无 GT）

来源：uni-agent real-world-failure-distribution（24 runs：K 类 25% /
F 8.3% / G 8.3% / scroll continuity）映射到验证集 8 帧（devopts 顶/状态/
滚动中、settings 首页/子页、apps 顶/滚动中），四臂 runs=5。
工件：`reports/fsv001/probe/`（probe-signals.md + 聚合
fsv001-8791eddc6f97.json + analyze_signals.py）。信号级非评分级，与
§3 GT 评分互补。

| 失败类 | 测点 | baseline | A | B1 | B2 |
|---|---|---|---|---|---|
| K（toggle 行解析） | "Stay awake" 锚（静态帧） | ✓ | ✓ | ✓ | ✗ 无文字 |
| K（滚动中） | 目标行滚出 viewport | ✗ | ✗ | ✗ | ✗ |
| F（唯一导航候选） | settings 首页带文本行 | 7 行干净 | 同 baseline | 11 行**含重复行** | 0 |
| F（重复行机制） | 同行双类检测 | 无 | 无 | text_block+menu_item 重叠框 | — |
| scroll continuity | 相邻帧共享率 | 0.10/0.18 | 同 | 0.18/0.20 | 0 |

结论（强化 NO_CHANGE）：

1. **B1 制造 F 类失败机制**：同一行被 ScreenParser 双类检出（如
   "Network & internet"、"Internet" 各出现两次，bounds 重叠，fusion
   去重不生效）→ 非唯一导航候选——正是历史 F 类失败的成因面；属
   **新增负信号**（GT 评分的 grounding off-target+3 与此吻合）。
2. **滚动中帧四臂全失效**：目标行滚出视野，fast perception 无跨帧
   记忆；ScreenParser 纯检测器无文字/时序——不解决 K 类滚动场景。
3. **ScreenParser ≠ Slow 层**：文本-空间关联消歧、唯一性、跨帧记忆
   对应 VLM/LLM 语义消歧（P2 escalation），不在 Fast 检测器能力面
   （与任务书 §18 不混入 ScreenVLM 的边界一致）。
4. B2 文字信号结构性归零再证 OCR 硬前提。

（本节为 Human Gate 后补证据，2026-09-12 追加；不影响 §7 判定，
强化其依据。）

## 11. 补充证据 — 检测准确性三面交叉（36 帧 calibrated GT）

工件：`reports/fsv001/probe/detection-report-v2.md` + 聚合
`probe/detect/fsv001-87cc08fde9df.json`（runs=1 准正式；核心指标与
§3 runs=5 主表一致，互证）。新增数据面：

1. **matched-pair bbox IoU**：baseline 0.70 / A 0.71 / **B1 0.83**——
   B1 框几何更准（叶子标注训练收益），配合 typeAcc 0.76 与 §3 center
   口径构成"匹配上的更准、但漏错更多"的完整画像。
2. **A 救援解剖**：117 个救援框中 98 个 text_block、conf 均值 0.53，
   GT 真阳性贡献 +5（R +0.012）——rescue 规则在低置信文本碎片里
   捡垃圾，非交互元素漏检召回。
3. **B1 覆盖缺口**：对 baseline 已检框覆盖仅 55–70%；**icon 类
   7→1（GT 期望 13）**、list_item 全漏——Android 图标/行容器是
   ScreenParser OOD 重灾区（web 训练分布）。

后续候选路径（归入 M3/FSV-002，不在本轮执行）：rescue 限定
interactive 类 + 提高阈值重测；mobile 域 fine-tune 消 OOD 后重评；
replacement 姿势维持不采用。
