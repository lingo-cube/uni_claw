# PER-014 Typed Semantic Consumer Migration — Cutover Evidence（Slices D/E，2026-09-26）

Worker：GLM-5.3-Flash（Slice D/E）。Slices A–C（R1 缝 / R2 验证 cutover /
R3 obligation cutover）由前置 agent 落于工作树，本证据一并登记（只读引用）。
Base：156f9c96 · 未 commit（硬约束遵守）。

## 1. Per-slice 记录（what / where，file:line）

### Slice A — R1 只读缝（前置 agent）

- `src/UniClaw.Kernel/Perception/UiHierarchy/SemanticCheckedResolver.cs:35`
  （新增）：role→typed-checked 只读解析缝。occurrence 恰一解析 →
  `ui.node.{captureId}#idx.checked` claim → capture 收敛（canonical 时序）→
  唯一性定案；capability 缺席 → Unsupported；零/多/缺席 → Unknown；禁止
  写回/裁决/producer 侧绑定（:31-33 doc）。
- 单测：`tests/UniClaw.Kernel.Tests/Perception/UiHierarchy/SemanticCheckedResolverTests.cs`
  （T1–T5，见 §3）。

### Slice B — R2 验证链 cutover（前置 agent）

- `src/UniClaw.Kernel/UniKernel.cs:464` 验证路由改 `TryRouteTypedVerification`
  （实现 :491-560）：门① DesiredState≠null（权威域）；门②③ occurrence 唯一
  + typed claim 在案（Unknown/Unsupported → null 回视觉路径，fail-closed）；
  门④ capture timestamp > dispatchTime（:527）；Partial → 显式
  insufficient-evidence 不回视觉不折叠（:530-541）。
- `PostActionXmlRouter` 保留为 rollback/egress（R2，冻结在案，零生产调用）。

### Slice C — R3 obligation cutover（前置 agent）

- `src/UniClaw.Kernel/World/WorldModel.cs:1303` `DeriveEntityObligationFactKind`
  改经 R1 缝（:1313 FromPresentation 严格解析 RequiredState，非 typed 词汇 →
  Unknown；Unknown/Unsupported ≠ Satisfied，零折叠）。
- `src/UniClaw.Kernel/UniKernel.cs:722` `UnsatisfiedMandatoryObligations` 传入
  canonical records（:728-733）供时序。
- `src/UniClaw.Kernel/Runtime/KernelRunDriver.cs:793`
  `AnyMandatoryObligationSatisfied`（typed 权威路径）+ :976 hollow guard 改判；
  :458-461/:609-612 DesiredState → `CheckedSemantics.FromPresentation` 严格解析。
- `src/UniClaw.Kernel/Assurance/RuntimeAssurance.cs:66-67,74-78` satisfaction
  经 `CheckedSemantics.FromPresentation`（Unknown 不判 satisfied）。
- `src/UniClaw.Host/HostRunner.cs:38-46` TargetState 词汇 checked/unchecked；
  `src/UniClaw.Host/Program.cs:77-79` 同。
- `src/UniClaw.Kernel/Perception/UiHierarchy/CheckedSemantics.cs:110-129`
  `FromPresentation`（一对一映射含 fixture 域 enabled/disabled；未知词 → null）。

### Slice D — R5 writer gating（本轮）

- `src/UniClaw.Host/HostRunner.cs:46` `HostOptions.LegacyStateEgress`（默认
  **OFF**）；:89 传入 LiveFrameFeed；:168-170 run 标记：facts.json 落
  `legacyEgress = liveFeed.LegacyEgressObserved`（M-08 legacy/degraded 如实留档）。
- `src/UniClaw.Host/LivePerception.cs:107,123` 旗字段；:114
  `LegacyEgressObserved`（发射即置位）；:154-167 `MapTargetStateClaim` 调用被
  旗门控——默认关时 **XML 来源零 `{role}.state` role-state claims**；开 =
  回滚路径恢复并置位 run 级标记。
- wifi 探针 writer 不变（`LivePerception.cs:182-190`，post 相 `_readSwitchState`
  → `switch.state` 单一 producer egress，零生产读者）。
- ConflictResolver legacy 路径**保留未改**（`src/UniClaw.Kernel/World/ConflictResolver.cs`，
  R4 休眠）。正常 typed run 零 legacy 冲突。

### Slice E — guards / tests / recount（本轮）

1. 冻结守卫修订：`tests/UniClaw.Kernel.Tests/LegacyStateSurfaceFreezeTests.cs:14-30`
   —— doc comment 更新为 post-migration reality（writers / egress / rollback
   旗在列；零 UNJUSTIFIED readers；RED-on-new-touchpoint 语义保留），名单 7 文件
   （UniKernel reader 已移除 = M-10 事实登记）。
2. 新测试：`tests/UniClaw.Kernel.Tests/Perception/UiHierarchy/Per014CutoverTests.cs`
   （T6–T10，见 §3）+ `tests/UniClaw.Host.Tests/LegacyEgressGateTests.cs`
   （旗默认面）。
3. legacy-pinning 测试迁移：DesiredStateSatisfactionTests /
   EntityObligationFulfillmentTests / Per009RemediationTests /
   KernelRunDriverTests(+Finalization) / AgentPlanPolicyConflictTests /
   DescriptorMatcherDriftTests / WorldModelCanonicalOracleTests /
   WorldModelPerformanceTests / PostActionXmlRouterTests（A–C 轮已迁，本轮全绿
   确认；显式 legacy-egress 面（PostActionXmlRouterTests、
   UiAutomatorDumpDebtTests、Per009RemediationTests Tier0 冲突销案族）按
   keep-justified 保留）。

## 2. Post-migration reader / writer inventory（可复算 = T10）

Regex `\.state\b`（大小写敏感）扫 src/UniClaw.Host + src/UniClaw.Kernel；
基线冻结于 `Per014CutoverTests.FrozenInventory`（:212-221）。

| 文件:行 | 触点 | 处置 |
|---|---|---|
| src/UniClaw.Host/UiAutomatorDump.cs:19,191,231,649 | `{role}.state` 映射 writer（:231 为发射点） | keep-justified（egress writer，R5 旗门控） |
| src/UniClaw.Host/HostRunner.cs:39,41 | 回滚旗注释 / TargetState 词汇 | keep-justified（rollback flag + egress scope） |
| src/UniClaw.Host/LivePerception.cs:154,185,187,204 | wifi 探针 `switch.state` writer（:185/:187 发射点）+ 注释 | keep-justified（单一 producer egress，零生产读者） |
| src/UniClaw.Kernel/Compatibility/LegacyStateProjection.cs:23 | egress-only 投影 doc | keep-justified（egress 投影） |
| src/UniClaw.Kernel/Evidence/SharedSubjects.cs:6,17,18 | `State()` 常量（:18 发射点） | keep-justified（常量） |
| src/UniClaw.Kernel/Runtime/AgentPlanPolicy.cs:49 | 通用 subject 前缀聚焦（subject-parametric） | keep-justified（非 legacy 耦合，R6） |
| src/UniClaw.Kernel/World/ConflictResolver.cs:90,101,104,105 | `*.state` 权威裁决（**唯一功能性 reader**：:104-105） | keep-justified（R4 legacy-only rollback 面，休眠） |

生产 `*.state` reader 合计 = 1 处（ConflictResolver.cs:104-105，justified）；
生产 typed 无关读者 = 0；无 unjustified readers；零删除（M-10 四条件未触发）。

## 3. T1–T10 → 测试映射与结果（dotnet test UniClaw.Kernel.slnx，2026-09-26）

| # | 测试 | 结果 |
|---|---|---|
| T1 Checked 解析 | `SemanticCheckedResolverTests.T1_CheckedClaim_ResolvesChecked` | PASS |
| T2 Unchecked 解析 | `...T2_UncheckedClaim_ResolvesUnchecked` | PASS |
| T3 Partial 不折叠 | `...T3_PartialClaim_Preserved_NotCollapsed` | PASS |
| T4 Unknown fail-closed | `...T4_UnknownConditions_FailClosedWithReason` | PASS |
| T5 Unsupported fail-closed | `...T5_UnsupportedCapability_FailClosed` | PASS |
| T6 两轴并存 | `Per014CutoverTests.T6_RenderedOff_Alone_DoesNotDeriveSemanticChecked` / `T6_RenderedOff_And_TypedChecked_Coexist_NoCrossAxisDerivation` / `T6_TwoAxes_NoAutoConflict_InWorldModel` | PASS ×3 |
| T7 typed-only + 零 dual-read | `Per014CutoverTests.T7_TypedOnlyBelief_ObligationAndVerificationWork` / `T7_AddingLegacySwitchStateClaim_DoesNotChangeOutcomes` | PASS ×2 |
| T8 Policy 零新 authority | `Per014CutoverTests.T8_PolicyRuntime_ReferencesNoResolver_NoWorldModelWriteApi` | PASS |
| T9 Assurance 零新 authority | `Per014CutoverTests.T9_RuntimeAssurance_StillReadOnlyEvaluate` | PASS |
| T10 reader inventory 复算 | `Per014CutoverTests.T10_LegacyStateInventory_Recomputable_ZeroUnjustifiedReaders` + `LegacyStateSurfaceFreezeTests` + `LegacyEgressGateTests` ×2 | PASS |

## 4. 验证四元组（method / expected / actual / evidence）

| 项 | Method | Expected | Actual | Evidence |
|---|---|---|---|---|
| 全解 | `dotnet test UniClaw.Kernel.slnx` | 0 fail | 0 fail：Kernel 634 / Host 63 / Simulation 184 / Agent.Dsh 121 / Agent 17 / Core 14 / FileSystemRealization 9 = **1042 PASS / 0 FAIL** | 本轮运行输出；DSH_TEST_PERCEPTION_LIVE 未设 → HostLiveFullTests 早退 env-skip（1 全真链路测试，非失败） |
| 场景认证 | `python3 tools/scenario_certify.py --change PER-014 --check` | PASS，expectationsDigest 不变 | PASS（29 files, 0 violations）；29 个 scenario diff 仅 runtimeSourceHash 重盖章 + certifiedByChange=PER-014，**expectationsDigest 全部逐字节不变**（无 scenario 编码 TargetState 词汇，无需接受任何期望变更） | `git diff scenarios` 汇总（±行仅 4 类） |
| 覆盖 | `python3 tools/scenario-coverage.py --run` | 全绿 | 29/29 certified by PER-014；truth chain 对 coverage.trx 验证通过；terminal-distinction 1/1 | 工具输出 |
| 卫生 | `git diff --check` | 干净 | exit 0，无 whitespace 错误 | 命令输出 |

## 5. Blockers / ruling 偏差

- **无实施性 blocker**。两处记录：
  1. M-08「journal **或** progress 标记」实现取 progress 侧（HostRunner facts.json
     `legacyEgress`，HostRunner.cs:168-170）——execution journal 为 effect-only
     语义（D3），legacy 发射非 effect，落 facts 最小且不污染 journal 语义。
  2. Slice E2 T7 的「outcomes 不变」证明在 obligation 维度逐条相等 +
     R1 缝 subject 过滤面（T6 / resolver 单测锁 `ui.node.*.checked` only）；
     WorldModel 级再追加 legacy claim 后 VerifyPostActionEffect 因
     revision-local container 语义（非本次改动引入）不可直调，已在测试注释登记
     （Per014CutoverTests.cs:124-130）。
- R1–R6 其余均按 state.md 裁决原文实现；PER-011 Fusion / Policy 词汇 /
  authority / dual-read / Unknown 折叠 / legacy 删除 / PER-009 目录：零触碰。

## 6. Leader（GLM-5.3）复核与终局验证（2026-09-26）

```yaml
leader_review:
  method: >
    独立复算 + G3/G4 裁决 + G1/G2/G5 spot-check：diff 全文件清单审计
    （authority 面）、SemanticCheckedResolver/FromPresentation/调用点逐行
    审、生产 *.state 触点 grep 复计、全量 dotnet test 复跑、
    scenario_certify --change PER-014 --all + --check、scenario-coverage
    --run、git diff --check
  expected: >
    G1–G5 全 PASS；零新增 authority；零 Unknown/Partial/Unsupported 折叠；
    零 rendered→semantic 推导； unjustified production readers = 0
  actual: >
    G1 PASS（复计 5 处触点全部有主：UiAutomatorDump:231 writer[R5 旗门控]/
    HostRunner:85 scope 准入[writer 支撑]/LivePerception:185 wifi 探针
    egress writer/SharedSubjects:18 常量/ConflictResolver:104 R4 休眠
    rollback 面；UniKernel 生产读取已删）；G2 PASS（T3/T4/T5 + 未知词
    null→fail-closed，零折叠点）；G3 PASS（FromPresentation = M-04 无损
    词对比较边缘适配器，不产 claim；T6×3 两轴并存零推导零自动 Conflict）；
    G4 PASS（PolicyRuntime/Agent.Dsh 零触碰——diff 清单核；Resolver 只读；
    WorldModel 改动为 obligation 判定委托非 authority；T8/T9 反射证）；
    G5 PASS（unjustified readers = 0；删除四条件：readers=0 ✓ / fixtures
    PASS ✓ / cutover acceptance ✓ / rollback 观察窗 ✗ 未观测 → 不删，
    readiness 如实登记）
  findings_and_fixes: >
    ① Worker 的 WorldModel doc 注释与实现矛盾（声称拒绝 on/off，实际按
    M-04 无损对接受）——Leader 裁决接受无损对实现、修正注释为真实语义；
    ② 注释修正移动 Kernel 源哈希 → 29 场景认证过期（C8 法定代价）——
    Leader 重跑 --change PER-014 --all + --check + coverage --run 复绿
    （本节即该轮复算记录）
  evidence: >
    本文件 + git diff（工作树）+ certification blocks（29 文件
    certifiedByChange=PER-014）+ coverage.trx 真值链
```

复算最终态：全 solution 1042 通过 / 0 失败（Kernel 634 · Host 63 ·
Simulation 184 · Agent.Dsh 121 · Agent 17 · Core 14 · FSRealization 9）；
certification 29/29 PASS（expectationsDigest 全部逐字节不变，仅
runtimeSourceHash/certifiedByChange 位移）；coverage 29/29 真值链验证；
git diff --check CLEAN；live 门控测试 env-skip 如实登记。
