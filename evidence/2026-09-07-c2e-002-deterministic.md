# Evidence — C2E-002 DETERMINISTIC verification

- Date: 2026-09-07
- Change: `changes/C2E-002/state.md`
- Command: `dotnet test`（UniClaw.Kernel.slnx；.NET SDK 10.0.400；net10.0）
- Level: DETERMINISTIC（纯内存 fake world，scripted driver / observation
  provider / policy，零 IO/设备/时钟依赖）

## Result

```text
已通过 UniClaw.Kernel.Tests.ControlToEffectTests.Accepted1_InvalidOrIncompleteContractIsRejectedWithZeroRunStateSideEffects
已通过 UniClaw.Kernel.Tests.ControlToEffectTests.Accepted2_ActIntentProducesFourIndependentArtifactsInUnmergeableOrder
已通过 UniClaw.Kernel.Tests.ControlToEffectTests.Accepted3_CandidateBindingIsNeverCanonicalAndCanonicalBindsCurrentRevision
已通过 UniClaw.Kernel.Tests.ControlToEffectTests.Accepted4_GateOnlyEnforcesAuthorizationAndNeverRejudges
已通过 UniClaw.Kernel.Tests.ControlToEffectTests.Accepted5_AttemptIsNotEffectOnlyPostActionObservationProducesRevision
已通过 UniClaw.Kernel.Tests.ControlToEffectTests.Accepted6_ProgressAdvancesWithCyclesAndAssuranceStateStaysOutOfRunState
已通过 UniClaw.Kernel.Tests.ControlToEffectTests.Accepted7_HypothesisAndPlanTypesHaveNoPathIntoAuthorities
已通过 UniClaw.Kernel.Tests.ControlToEffectTests.Accepted8_EvidenceAndBeliefChangeOnlyThroughExistingPaths
已通过 UniClaw.Kernel.Tests.ControlToEffectTests.Accepted9_ContractViewIsImmutableAcrossSameVersionReadmission
已通过 UniClaw.Kernel.Tests.ControlToEffectTests.Accepted10_RecoveryReentersLoopAndBlindRetryWithoutNewRevisionIsRejected

已通过! - 失败: 0，通过: 18，已跳过: 0，总计: 18   [dotnet test, 2026-09-07]
```

其中 E2B-001 既有 8 用例（EvidenceToBeliefTests）零改动、全部保持
GREEN。构建零 error / 零 CS 警告（仅环境级 NU1900 NuGet 漏洞库缓存
不可写，与 E2B-001 相同，与代码无关）。

## TDD 过程（失败尝试是证据，保留）

- 实现与测试同一 pass 协作完成（偏离严格 test-first 桩 RED，如实记录）。
- 首次运行：失败 2 / 通过 16。失败项 = 验收 5 / 验收 8 用例把
  post-action 观察断言为「WorldState 直接更新为新值」，与 E2B-001 已
  定型语义冲突（同 subject 不相容 claim → 显式 Conflict，不静默覆盖，
  established 值保留）。属测试预期缺陷而非实现缺陷；修正断言为
  「新 revision + Conflicts 显式携带 challenging evidence 引用」
  （恰为 state.md Assumption 3「E2B claim/conflict 词汇直接承载
  Effect Evidence 回流」的落地）。
- 第二次运行：失败 0 / 通过 18。

## REVIEW（fresh SubAgent，轴 A-E）与修复

轴 C（范围纪律）/ 轴 D（意外改动）PASS；不变量 21-27、32-34 逐条 PASS。
发现与处置：

| # | 级别 | 发现 | 处置 |
|---|---|---|---|
| F1 | major | §17 Canonical Binding 缺「dispatch 后失效」失效源：同 revision 内同 intent 可重复 dispatch | 已修：binding 派生有效性 = revision 仍 current **且** 未被 dispatch 消费（从 append-only ReceiptLog 推导，仍无 event）；Accepted3 增组合面再投递拒绝断言（gate reason `binding-already-dispatched`、receipt 仍单次） |
| F2 | minor | authority 校验集中在 UniKernel 组合缝；EffectBoundary 公有方法直连可绕过 intent/judgment 溯源 | 记入 state.md Residual Risks（本片组合面 = UniKernel，P5；开放直连前需补 provenance 校验） |
| F3 | minor | `forbidden-effects-declared` 检查无用例覆盖 | 已修：Accepted1 增 `ForbiddenEffects = null` 变体 |
| F4 | minor | HypothesisLog 原地改写历史条目，破坏 append-only 惯例 | 已修：纯 append-only；可废弃性改为派生判定（同 target 后来者取代） |
| F5 | nit | Dispatch 对 null judgment 产出 gate decision 而非 fail-closed 抛出 | 已修：ArgumentNullException（gate reason 只表达 judgment 内容） |
| F6 | minor | blind-retry 负向仅经直连 assurance.Judge，未证组合面 | 已修：Accepted10 增伪造 intent 过 kernel.Act 被 IsIssued 拒绝断言 |
| F8 | nit | Bind 对缺失 EffectClass 静默产出空串 binding | 已修：ArgumentException fail-closed |
| F7 | hygiene | 无关未跟踪 `.tmp-hf-intake/` 不得入 commit | 显式路径提交规避（不改动该目录） |

修复后复跑 `dotnet test`：**失败 0 / 通过 18 / 跳过 0**（2026-09-07，
与上文结果列表一致；E2B 8 用例持续零改动 GREEN）。

## 验收 ↔ 用例映射

| 验收 | 用例 |
|---|---|
| 1 契约准入 fail-closed | Accepted1_InvalidOrIncompleteContractIsRejectedWithZeroRunStateSideEffects |
| 2 四产出独立留痕/次序 | Accepted2_ActIntentProducesFourIndependentArtifactsInUnmergeableOrder |
| 3 Candidate ≠ Canonical | Accepted3_CandidateBindingIsNeverCanonicalAndCanonicalBindsCurrentRevision |
| 4 Gate 只执法 | Accepted4_GateOnlyEnforcesAuthorizationAndNeverRejudges |
| 5 Attempt ≠ Effect | Accepted5_AttemptIsNotEffectOnlyPostActionObservationProducesRevision |
| 6 Run State 边界 | Accepted6_ProgressAdvancesWithCyclesAndAssuranceStateStaysOutOfRunState |
| 7 plan/hypothesis 负向封闭 | Accepted7_HypothesisAndPlanTypesHaveNoPathIntoAuthorities |
| 8 E2B 权威零穿透 | Accepted8_EvidenceAndBeliefChangeOnlyThroughExistingPaths |
| 9 Contract View immutable | Accepted9_ContractViewIsImmutableAcrossSameVersionReadmission |
| 10 Recovery 重新入环 | Accepted10_RecoveryReentersLoopAndBlindRetryWithoutNewRevisionIsRejected |
