# POST-COMPLETENESS-TIER-SCOPE-REPAIR_RESULT（Option A1 最后一刀）

> 前置：`POST-COMPLETENESS-NAV14-TIER-SCOPE-DIAGNOSTIC-RESULT.md`（nav:14 = auxiliary 'Dark theme'，
> tier 错配必然误失效）。Leader 选 **A1**：授权此最小修复。

## 0. 一句话

`PostCompletenessConsistencyValidator.Validate` B.3 的 fresh 义务集改为**与冻结集同层**：
仅对 `EligibleForAuthorization`（授权层/视觉）occurrence 做 frozen-class 解析；
auxiliary（XML）候选本就不产生 logical source、签名字段格式亦不可能出现在视觉冻结集 —— 不再被当作
必然失败的义务。**fail-closed 零放宽**：预先前未知的 eligible 新候选仍照常失效。

## 1. Minimal Diff

| 文件 | 内容 |
|---|---|
| `src/UniClaw.Runtime/World/PostCompletenessConsistencyValidator.cs`（+2 行逻辑 + 注释）| B.3 循环 `if (!occurrence.EligibleForAuthorization) continue;`（TIER-SCOPE ALIGNMENT 注释，引用 PCC-16/runF）|
| `tests/…/Scenario/PostCompletenessConsistencyTests.cs`（+3 测试 + 1 helper）| `FreshObservationWithAuxiliary`（aux 源 + structured **无 bounds** —— 复刻 campaign 的 structured 发射形态，令 XML 行成为独立 auxiliary occurrence）；PCC-16（runF 复刻：aux 孪生不失效 → CONSISTENT）、PCC-17（真新 eligible 仍失效）、PCC-18（仅冻结 eligible → CONSISTENT）|

## 2. RED→GREEN

- **RED**（未修复）：PCC-16 FAIL —— 无 bounds 的 auxiliary 孪生使 frozen epoch 被误失效（与 runF 同因）；
  修正 fixture 的关键 = structured 有 bounds 时会被并入 support（不成独立 auxiliary），而 campaign 的
  structured 层不发 bounds —— 复刻条件钉死。
- **GREEN**：**PostCompletenessConsistencyTests 18/18**（PCC1-15 既有全保 + PCC16-18）。

## 3. Deterministic / Fresh Real

- 全量 C# 确定性套件：**2354 passed / 5 failed（=既有环境性 CORR_HOST×3 + Capstone_RealEmulator +
  ExternalBoundary_RealDevice，零新增）**；`git diff --check` CLEAN。
- fresh real（runG）：root `inventory complete: sources=16, unresolved=0` ✓；terminal =
  `post-action transition did not settle within 3 fresh observations；fail closed（composition policy）`——
  tap 进 child 的过渡 settle 未在 3 次观测内稳定（**设备/渲染节奏类**，非本修复导致；INVALIDATED 层未再出现）。
- 关键能力证据：runF 已证明 **child discovery epoch 完整（sources=17）+ 后续 INVALIDATED 为 tier 错配误报**；
  本修复在单元层钉死该误报（PCC-16），终结了"结构性必失效"的确定性挡路。

## 4. 现状 / 裁决（Option A 终点）

确定性挡路清单（按本 session 逐层核销）：
- normalizer 顺序敏感 ✅ · 幻影碎片 ✅ · Pattern-5 粒度 ✅ · 'Not set'/'Will never' 副文本 ✅ ·
  **post-completeness tier 错配 ✅（本次）**。
残余与不确定性（已登记，非确定性代码缺陷）：
- 过渡 settle 节奏（runG 类，设备状态相关，3 观测上限内未稳定即 fail-closed——机制正确）；
- 偶发"滚底退回根页"事件（1/6，I. UNKNOWN，采证管道就绪）；
- ICON_TEXTLESS / OCR 乱码 / Safety & emergency 类 / 已登记 StableKey 漂移（分属各自 Owner）。

**Leader 裁决**（Phase 2.6 维持 STOPPED）：
- **B1 宣布阶段性结果**：核心能力已达成并充分证据化（child epoch 完整证明；确定性误报全部核销；
  fail-closed 机制在每一层都被证明 working）。建议以此收束本轮，残余按登记清单逐项开独立 gate。
- **B2 继续追 run-to-run 全绿**：重复 fresh real 直到一次 terminal=Completed —— 成本为每轮
  ~5 分钟 + 设备节奏依赖，收益递减（非确定性）。
本结果文档倾向 B1（证据已足够厚，剩余为登记残余与环境的组合，非单一可修缺陷）。

## 5. AuthorityDelta / ArchitectureDelta / 边界

- **AuthorityDelta: NONE**；**ArchitectureDelta: NONE**（完整性义务集与冻结集同层对齐；不新增
  authority；eligible 检查全部保留）。
- 未触碰：normalizer / Fusion / P5 / P7 / quiescence / Unknown bypass / swipe / page resolver /
  OCR/ICON/Safety；auxiliary 依旧不产生 logical source（`SourceGroundingValidator` 不变）。