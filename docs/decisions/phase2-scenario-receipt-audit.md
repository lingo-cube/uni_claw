# Phase 2 Scenario Receipt Audit

> 状态: Complete | 日期: 2026-08-08
> 方法: 每个 Phase 2 生产增量必须有唯一购买 Scenario + Required Semantic + Assertion。无购买者的增量即 I-12 违约。
> 依据: Phase 2C DONE (158/158 tests, 8/8 guards, SC-P2-001/002/003 formal proofs)

## 新增类型 / 字段 / 组件

| # | Production Delta | 购买 Scenario | Required Semantic | Assertion |
|---|-----------------|-------------|-------------------|-----------|
| 1 | `Model/Trap.cs` (7 fields: Kind/Scope/Expected/Observed/Source/Evidence/LastAction) | SC-P2-001 (Evidence 1: Kind=UnexpectedPage, Scope=Agent, Expected/Observed 为观测序号引用非快照) | 裁决 4 + HG-2: 字段冻结；Expected/Observed = long? 序号引用 (I-13: 不嵌 Observation 快照) | AgentRecoveryLauncherDriftTests: Trap 载荷 + Trace 事件 (TrapKind/TrapScope) |
| 2 | `Model/TrapKind.cs` (6 values: UnexpectedPage/WorldLost/StateMismatch/TargetLost/PlanInvalid/ContainerMismatch) | SC-P2-001 (UnexpectedPage 实际发射；余值词汇预留) | Charter §21 分类词汇 (HG-2) | 同上 Evidence 1 |
| 3 | `Model/TrapScope.cs` (Step/Container/Agent) | SC-P2-001 (Agent 发射) + SC-P2-002 (Evidence 3: 无 Trap → TrapScope null) | Charter §21 层级词汇；Phase 2 仅 Agent 发射 (B2)；Step/Container 预留 | StepRetryScenarioTests: zero Trap events; RecoveryVerificationFailureTests: Agent-scope |
| 4 | `Model/RecoveryResult.cs` (Verified \| Failed(Reason)) | SC-P2-003 (Evidence 1: 验证失败事件；Evidence 5: Reason 显式非空) | HG-5: 恰好 2 变体，无 Incomplete/Retryable | RecoveryVerificationFailureTests: RunState=Failed, Reason 含期望 vs 实际 |
| 5 | `Model/RecoveryAnchor.cs` (+RestoreRecipe(string?), +EntryStrategy(string?)) | SC-P2-001 (RecoveryAnchor 消费: RestoreRecipe="Relaunch(Settings)", EntryStrategy="Resolve(SettingsMain)") | 裁决 8 解除 + Charter §20 字段落地 + 裁决 11 (注入数据不硬编码) | Recovery.Begin 消费 RestoreRecipe；AgentRecoveryLauncherDriftTests: anchor 5 字段 |
| 6 | `Model/TraceEvent.cs` (+TrapKind?, +TrapScope?, +RecoveryId?) | SC-P2-001 (Evidence 1/2: Trap + Recovery 事件可区分) + SC-P2-002 (Evidence 3/4: null) + SC-P2-003 (Evidence 1: RecoveryId 关联验证失败) | §28 因果链扩展 (A4)；null 语义: Phase 1 事件不受影响 | StepRetryScenarioTests: TrapKind/TrapScope null；RecoveryVerificationFailureTests: RecoveryId 关联 |
| 7 | `Traversal/Traversal.cs` — TraversalJournalEntry +RetryCount(int, default 0) | SC-P2-002 (Evidence 1: journal 含重试条目；Evidence 5: 重试有界) | A5: 0 = 正常首次 Phase 1 兼容；>0 = 第 N 次重试 | StepRetryScenarioTests: journal [fail(0), retry-marker(1), succeed(1)] |
| 8 | `Recovery/Recovery.cs` (组件: Begin/HasRemainingActions/ExecuteNextAsync/ExecuteActionAsync/ObserveAsync/Verify/ResolveRecoveryAction) | SC-P2-001 (恢复机制) + SC-P2-003 (Verify → Failed) | HG-4 Option B: 机制归组件，决策归 Agent。HG-5: 最小化 — 无 Request/Planner/Runtime。I-1: 依赖仅 Environment + Model (Guard 7) | SC-P2-003 Evidence 6: 无"Dispatched → 直接继续"路径 (I-9 机械保证) |
| 9 | `Agent/Agent.cs` — IsAgentScopeDrift / EmitDriftTrap / RecoverFromDriftAsync / LastTrap | SC-P2-001 (B1/B2/B3) + SC-P2-003 (恢复失败 → Run Failed) | HG-3: 仅用 ForegroundApplication + IsStillMine + SemanticPage 三表面 (无 DriftStatus)。I-8/I-9: authority 在 Agent | SC-P2-001 Evidence 6: action history 无低层恢复动作；SC-P2-003 Evidence 2: RunState=Failed |
| 10 | `Traversal/Traversal.cs` — maxRetries(ctor param, default 0) | SC-P2-002 (Evidence 5: 重试有界 ≤ max retry count) | B4: 仅 Select 重试、零动作派发、耗尽 escalate Phase 1 Failed 路径 (不产生 Trap)。默认 0 = Phase 1 行为字节级一致 (SC-P1-004 不回归) | StepRetryScenarioTests: maxRetries=1 → 1 次 retry 成功；SC-P1-004 missing-target maxRetries=0 不变 |
| 11 | `Startup/Startup.cs` — ctor +restoreRecipe? +entryStrategy? (optional, default null) | SC-P2-001 (RecoveryAnchor.RestoreRecipe 注入；C4 harness 接线) | 裁决 8 解除 — Phase 1 三字段锚点向后兼容 (默认 null = Phase 1 不消费) | AgentRecoveryLauncherDriftTests: anchor 满载 5 字段 |

## 保留不变的 Phase 1 模型

| 模型 | 状态 | 理由 |
|------|------|------|
| TraversalStepResult (Succeeded \| Failed(Reason)) | 保留不动 (SC-P1-004 不回退) | Trap 在 Model 层与其并列，不取代 |
| Observation / ObservedElement | 保留不动 | 无 Fingerprint (裁决 2)；无 coordinate (裁决 3) |
| Container / World / Environment / Plan / Goal | 保留不动 | Phase 2 零需求 |
| Guard 1-4 + Guard 6 | 保留不动 (design.md §7 "不变") | Phase 1 基线，8 [Fact] 全绿 |

## Deferred 缺席清单 (无购买者 → 保持缺席)

| 能力 | 推迟到 | 保护项 |
|------|--------|--------|
| RecoveryRequest / RecoveryPlanner / RecoveryRuntime | Phase 3+ (HG-5) | Guard 5b (RecoveryRequest 全库禁止) |
| WorldBelief.DriftStatus | Phase 3+ (HG-3) | 无字段声明 / 测试扫描 CLEAN |
| Popup recovery / Container-scope recovery | Phase 3 (§38) | design.md §9 |
| Uncertain action (timeout ≠ failed) | Phase 3 (§37) | design.md §9 |
| Fingerprint 字段与机制 | Phase 3+ (裁决 2) | Observation 无 Fingerprint 字段 |
| coordinate / hierarchy model | 未来 Scenario (裁决 3) | Guard 6 |
| FSM | Phase 3+ (§17) | I-7: protocol 普通方法表达 |
| 真实设备 / Vision Adapter | Phase 4 (§39) | Fake Environment 优先 |
| LLM / VLM / Memory | Phase 5 | §57-12 |
| DI 容器 | 不引入 | 构造器注入 + 测试侧组合根 |

## 审计方法

- 以 `openspec/changes/phase2-trap-recovery/scenarios/` 下 SC-P2-001/002/003 的 Evidence Required 编号为唯一断言权威
- 每个生产增量 (git diff Phase 1 baseline → Phase 2C) 必须在上表中有对应行
- **无购买者的增量 = I-12 违约** — 本次审计零发现

## State

Complete — Phase 2 生产增量全部有 Scenario Receipt。Deferred 缺席清单全部可验证。
