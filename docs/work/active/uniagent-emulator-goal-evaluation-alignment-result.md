# UniAgent Emulator Goal Evaluation Alignment — Result

DocumentType: `IMPLEMENTATION_RESULT`
Decision: `PROJECT_LEADER_UNIAGENT_EMULATOR_GOAL_EVALUATION_ALIGNMENT_RESULT`
Change: `openspec/changes/uniagent-emulator-validation-harness/`（GoalEvaluator 验收强度对齐，Human Decision 2026-08-26）
Date: 2026-08-26
Authority: Runtime Architecture Contract I-1..I-14 与 Architecture v1 不变；本结果不新增架构权威。

---

## 1. Human-readable Reality Analysis

Expected：Scenario 声称验证 8/8 required coverage，则 PASS 只能在 Runtime Evidence 足以证明
8/8 满足时给出。Observed：Real Emulator（Tier B 层；非物理设备）上 Runtime 在 fixture `Visited 3/8` 时合法 Completed ——
Runtime 忠实执行了传入的 Goal contract；偏差在 harness 传入的目标函数比 Scenario 声称的弱。
Gap：Scenario spec（8/8）≠ GoalEvaluator（任意可解析页）。修复后（v2 运行）同样 3/8 的
Run 被**正确地**判 Failed + `FAIL_INSUFFICIENT_SCENARIO_COVERAGE` —— 对齐生效。

## 2. Why 3/8 Could Complete（符号级）

`RealityFixtureStrategyBinding.CreateGoal.EvidenceEvaluator`（修改前）：
`Satisfied: ResolvePage(observation) is not null` —— **任意可解析页即满足**。
→ 3/8 时回到根页 → ResolvePage=RootPage → GoalEvidence.Satisfied=true → Agent 合法
Completed。Evidence：`tierb-s1-result.json`（对齐前，terminal Completed @3/8）。
FDP：goal construction within the binding。Owner：Validation Harness。

## 3. Runtime Completion vs Scenario Pass

已实现为**双层判定**：
- Runtime Completion：GoalEvaluator 契约（现为 fixture 外部完成态 8/8 CAPSTONE COMPLETE 行）。
- Scenario Acceptance（独立层，harness 读 fixture 自身外部状态）：
  `scenarioPass = visited >= 8`，否则 `FAIL_INSUFFICIENT_SCENARIO_COVERAGE`。
- 输出显式携带语义声明：`RUNTIME_COMPLETED != VALIDATION_SCENARIO_PASS`。
实证（v2）：Runtime Failed（GoalEvidence 未满足）+ Scenario 3/8 FAIL —— 两层语义都真实工作。

## 4. First Divergence Points（本轮六个，全部 harness 侧，逐一定位后修复）

| # | 现象 | FDP（符号级） | 处置 |
|---|---|---|---|
| A1 | 3/8 即 Completed | EvidenceEvaluator 页可解析即满足 | 改为 `IsCapstoneComplete`（Visited+Capsestone+COMPLETE tokens，镜像 capstone test matcher） |
| A2 | Scenario PASS 无独立判定 | 无 acceptance 层 | TierBProgram 增 Scenario Acceptance（读 fixture 外部态，uiautomator 仅测试侧收集） |
| A3 | 强化后 0/8 即停（只授权可见行） | inventory 只看 `observations[^1]` 单帧 | viewport union（root 帧累计，per-occurrence grounding） |
| A4 | 滚动不发生（屏外行永不出现） | `ViewportExplorationEvaluator: null` | 接 scroll-until-exhausted（毕业契约，镜像 capstone test） |
| A5 | "Unknown interaction affordances remain" | (i) 根页标题 "Fixture Root" 被分类为 parent-return（根无父→UNKNOWN 阻断 completeness）；(ii) 非 Child/Root 文本返回 null=未分类→UNKNOWN | 页感知分类器（root title→NonInteractive）+ 全量分类（null→NonInteractive，含 OCR 噪声 LUMI:MO） |
| A6 | NonInteractive envelopes 被拒（evidence dump: `ManifestMismatch`×N） | capability manifest 只声明 navigation/parent-return 两 meanings | manifest 增声明 `fixture.non-interactive`/`fixture.local-control` |

## 5. Harness-only Change（全部在 ValidationHarness 内）

- `RealityFixtureStrategyBinding`：EvidenceEvaluator 强化（IsCapstoneComplete）、viewport-union
  inventory、scroll-until-exhausted evaluator。
- `FixtureSemanticEnvironment`：context-aware 分类器支持（CurrentObservation 暴露）、
  role 枚举扩展（LocalControl/NonInteractive）+ 对应 meaning/candidate 映射、manifest
  meanings 声明齐全、TIERB_DEBUG_EVIDENCE 诊断钩（evidence dump）。
- `TierBProgram`：Scenario Acceptance 层、页感知+弹窗帧消歧分类器、ADB structured
  hierarchy 通道接入（与 capstone 生产管线同构）。
- Runtime/DriverHost/Harness 生产源：**零修改**（git diff 仅为会话前既有 Phase-2 在途状态）。

## 6. Tier A Validation

51/51 harness（含 S1/S2/S3 Tier A 场景）全绿 —— Tier A fixture world 语义自洽（无 8/8 计数器，
其完成契约即"回到根"），未受对齐影响；确定基线无污染。

## 7. Tier B S1 Revalidation（Real Emulator，强化后；非物理设备）

- **3/8 不再 PASS（对齐核心证明）**：`tierb-s1-3of8-hardened-proof.json` —— Runtime
  Failed + Scenario `FAIL_INSUFFICIENT_SCENARIO_COVERAGE: fixture external state Visited 3/8`。
- **探索深度推进**：修复链后运行真实进入子页并处理至 Child 06（fixture 特意布置的 popup
  obstruction 子页），22 事件，最终 `Source normalization is unresolved` fail-closed。
- **剩余差距（harness 成熟度，非 Runtime 缺陷）**：滚动根帧的 normalization/去重语义 ——
  v3（structured channel 接入后）在根 completeness 处 unresolved。FDP 已定位到
  SourceEquivalenceNormalizer 对多帧 viewport 累计的签名去重路径，capstone 管线以
  vision-only + 其去重逻辑通过；harness binding 需再一轮对齐（继续按 FDP 迭代，无需任何
  Runtime 变更）。**未触发 STOP 条件**：8/8 无需 Runtime 改动即可达成（capstone test 证明
  该管线可达 8/8）。

## 8. Full Regression

harness 51/51 · 确定性 2103/2103 + Semantic 32/32 · 架构守护 61/61 · consistency ALL PASS ·
strict PASS · `git diff --check` PASS · Runtime 生产源零修改（既有 Phase-2 diff 未触碰）。
S2 bounded fail-closed 语义不受影响（S2 路径未改；其 Tier B 证据继续有效）。

## 9. Updated Tier B Decision

**TIER_B_PARTIAL_WITH_EVIDENCE**（维持，差距收窄且换位）：
- 已证明（Real Emulator 层）：验收强度对齐（3/8→FAIL）；探索授权全量（union）；滚动探索激活；
  页感知分类；evidence admission 链路通（Unknown 消除）；进入子页并推进至 popup obstruction。
- 剩余：滚动根帧 normalization 对齐 → 真实 8/8 → S1 PASS。一个已定位的 harness 侧课题。

## 10. AuthorityDelta

`NONE`。零 Runtime/GoalEvidence/FSM/Agent/StrategyContract/wire 改动（STOP 清单全未触发）。

## 11. ArchitectureDelta

`NONE_RUNTIME / VALIDATION_HARNESS_ONLY`（§5 列表）。

## 12. Tier C Recommendation

`HOLD`（与 Human 决策一致）：真实 8/8 S1 收口后重评；届时建议 READY_PENDING_HUMAN_GATE。
