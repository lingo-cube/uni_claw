# PER-002 — Fast Perception Vertical Slice（A0 评估 → A 证据提取 → B 切片）
lifecycle_state: closed · disposition: none · depth: standard · base: a2bf82e9

## Intent（WHAT/WHY）
UIW-001 后缺口移到 Perception：target 侧没有真实 Fast 路径把 GUI raw
artifacts 转为 ObservationProposal。本轮从 uni-agent 筛出仍有语义价值的
真实感知资产，经一次性 Compatibility/Normalization 层转化为 target-neutral
corpus，再证明 Target 自己的 RawArtifact→FastPerception→ObservationProposal
→EvidenceLedger→UIWorld 主干可运行。

## Scope（阶段）
- A0：独立评估 legacy 资产（Format×Semantic → DIRECT/ADAPT/REFERENCE_ONLY/REJECT）。
- A：提取 raw/provider/failure 证据；建 target-neutral corpus（真实资产为主）；
  产出 docs/analysis/legacy-perception-evidence-assessment.md（Authority: NONE）。
- B：产品 FastPerception（RawArtifact/IFastPerceptionStrategy/P2 producer 薄件）
  + TDD S1–S8 + adapter 产物验证测试 + UIW-001 全量回归。

## Out of Scope（禁止）
Simulator/Trace 迁移/Replay 引擎/VLM/Vector DB/learned Re-ID/Observation
Control 外部边/device-driver 迁移/Container taxonomy/navigation graph；
不为读旧文件改 Product schema（ObservationProposal 仅在 proven buyer 下最小扩展
——本轮零扩展）；legacy 世界真相（CurrentPage/ContainerId/expectedIdentity）
不得进 target Authority。

## Decisions
- 资产选型（全部真实，来自 uni-agent git）：
  DIRECT = golden-run-v1 case-a-before（真实 YOLO+OCR provider JSON，dual
  normalized/pixel 坐标）+ scenario-manifest；ADAPT = reality-evidence
  uiautomator XML+PNG 对（SCROLL-01 v1/v2、POPUP-01 before/popup、POPUP-04
  dialog、NAV-03 parent/childA）；REFERENCE_ONLY = semantic-assets heldout
  （expectedIdentity 标签）、p26 fusion traces、vlm-compare、training mini-data；
  REJECT = docs/work/active 三张无上下文 debug 截图（provenance unknown）。
- Adapter = 一次性 file-based 工具 tools/legacy-perception-import/import.cs
  （uiautomator XML → target-neutral observations；产物 committed 到
  tests/.../Perception/Corpus；adapter dies with migration，Product 代码零感知）。
- 观测 subject 约定（corpus 层，非产品协议）：ui.text.<key> / ui.node.<key>.class
  / ui.node.<key>.clickable / spatial.artifact.bounds.<key>（frame=artifact，
  P-UW-16 满足）/ perception.page.signature（Fast 语义 hint）。
- 产品 seam：IFastPerceptionStrategy 输出最小 ArtifactObservation(Subject,
  Value)；FastPerception 统一构造完整 Provenance（producer/CaptureTime 取自
  artifact metadata——perception 无 wall-clock；scope/lineage 带 artifact id）。
- 真实失败资产核查结论：legacy 代码自证「No real FailureEpisode exists in the
  current corpus」（evaluation/failure_candidate.py）；S8 用真实 artifact +
  显式 synthetic 降级（degraded detection）负向验证，不伪造 legacy failure。

## Acceptance（= graduation conditions，见指令 §35）
S1–S8 GREEN；adapter 产物验证 GREEN；UIW-001 回归 GREEN；无
Simulator/Trace/Slow/VLM/Vector；perception 零 belief/identity authority；
partial/missing 不产生假 absence/假 New；至少一个真实 legacy 资产端到端使用
（满足：scroll01 v1→v2 真机帧）。

## Constraints
不伪造证据；real coverage 不足如实标注；既有 93 测试零回归。

## Verification
```yaml
verification:
  level: DETERMINISTIC
  method: >
    dotnet test（全解决方案）；
    新增：FastPerceptionSliceTests（S1–S8，8）+ LegacyImportAdapterTests（6）；
    产品：src/UniClaw.Kernel/Perception/FastPerception.cs（RawArtifact 内容寻址 +
    IFastPerceptionStrategy seam + P2 producer 薄件，ObservationProposal 零扩展）；
    corpus：tests/.../Perception/Corpus（10 场景 = 7 legacy-adapted + 1 legacy-direct
    + 2 synthetic-on-legacy-artifact，adapter = tools/legacy-perception-import/import.cs）
  expected: >
    S1–S8 GREEN；adapter 产物验证 GREEN；UIW-001（含 93 既有）零回归；
    perception 零 belief/identity authority；P2/P3 通路（无直连 WorldModel）；
    missing detection ≠ absence；spatial 值必带 frame；partial → Insufficient 非 New；
    scroll+evidence → Matched 可能；scroll prior 不压反证；坏输出不产生假 truth
  actual: >
    107/107 GREEN（Kernel 90 + Agent 17；新增 14，既有 93 零回归）。
    真实资产端到端：SCROLL-01 v1→v2 真机帧（S3 Matched）+ golden-run-v1 真实
    YOLO+OCR 输出（S1 DIRECT 路径）+ POPUP-01 真实 AlertDialog（S7 Overlays
    relation）。失败侧如实：无真实 FailureEpisode（legacy 代码自证），S8 用
    真实 artifact + 显式 synthetic 降级 → REAL_ASSET_COVERAGE_PARTIAL。
    滚动行文本位移按 §18 冻结语义入 Conflict（latest 不获胜）且 identity 不变（S3）。
  evidence: dotnet test 输出（2026-09-09）；docs/analysis/legacy-perception-evidence-assessment.md
```

## Status log
2026-09-09 · understanding→persisted · A0/A 盘点完成（subagent 代码级 +
  抽样交叉验证；golden DIRECT 资产确认；POPUP-02 无 dialog 帧、NAV-03
  child 与 parent 文本相同等事实修正）；corpus 资产提取完成
2026-09-09 · persisted→implemented · adapter 工具（uiautomator XML→observations，
  含复用 resource-id 去重）+ golden DIRECT 直读 + synthetic 场景如实标注；
  产品 FastPerception + S1–S8/adapter tests；3 处测试数据理解修正
  （坐标轴序、滚动行文本 Conflict 语义、跨帧 bounds）→ GREEN
2026-09-09 · implemented→reviewed · REVIEW：产品零 legacy 感知（ObservationProposal
  未扩展）；perception 不触碰 WorldModel（只经 P2/P3）；无 Simulator/Trace/
  VLM/Vector；confidence 未入 payload（§19 无 buyer）
2026-09-09 · reviewed→verified→closed · 107/107；graduation 条件全满足（real
  coverage partial 如实标注）→ FAST_PERCEPTION_EXECUTABLE_BASELINE_ESTABLISHED
  + REAL_ASSET_COVERAGE_PARTIAL
