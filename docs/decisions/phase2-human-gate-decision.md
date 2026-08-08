# ADR: Phase 2 Human Gate — Trap & Recovery Architecture Decisions

> 状态: Approved | 日期: 2026-08-08
> 关联: phase2-trap-recovery OpenSpec change
> 依据: PHASE_2_CHANGE_VALIDATED (runtime-validator independent review)

## Context

Phase 1 (Deterministic Runtime) 完成并通过独立验收（104/104 tests, 6/6 guards, 5/5 scenarios, validator PASS）。
Phase 2 引入 Trap 一等模型与 Agent-scope Recovery，由 Charter §39 / §60-E 授权。
5 个架构决策点在 Phase 2A 实施前需冻结——本 ADR 记录每一项的决策、依据与约束。

## Decisions

### HG-1: Guard 5 Revision Protocol

**Decision**: Approved. Guard 5 从"全目录禁止 Trap 类型声明"缩窄为"Trap 类型仅限 Model + Recovery 组件"。

**Boundary**:
- Trap 类型声明（Trap / TrapKind / TrapScope）**仅允许**在:
  - `src/UniClaw.Runtime/Model/` — 不可变值类型定义
  - `src/UniClaw.Runtime/Recovery/` — 恢复组件（HG-4 已批准独立 Recovery 组件）
- 其他目录（Agent / Container / Traversal / Startup / World / Environment）**仍禁止**
- `RecoveryRequest` **保持全目录禁止**——Phase 2 不引入（HG-5）

**Trap 禁止**:
- 不得拥有 RunState
- 不得判定完成（I-10: 完成 authority 唯一在 Agent）
- 不得触发恢复 authority（I-7: Trap 是 evidence，不是 intelligence）
- 不得包含 Observation 快照（I-13: Expected/Observed 仅为观测序号引用 `long?`）

**新 Guard**: Guard 7 — Recovery 组件不得引用 Container/Traversal 内部实现（`using UniClaw.Runtime.Container` / `using UniClaw.Runtime.Traversal` 在 Recovery/ 下禁止——I-1 依赖方向）

**Atomicity**: Guard 5 修订与 A1（Trap 类型定义）必须在同一 change 内原子完成（tasks.md Phase 2A A6），防止 guard 空窗。

**Charter basis**: §41（Architecture Tests 机械保证）、I-8（escalate 不偷权）、I-13（不聚合 God Context）、裁决 4（Trap 模型 Phase 2 引入）

### HG-2: Trap Field Shape

**Decision**: Approved. Phase 2 Trap 采用最小字段集合。

**Approved fields** (4 core + 3 auxiliary = 7):
  - `Kind` — `TrapKind` enum（UnexpectedPage / WorldLost / StateMismatch / TargetLost / PlanInvalid / ContainerMismatch）
  - `Scope` — `TrapScope` enum（Step / Container / Agent）——Step 与 Container 为 Charter §21 词汇预留，Phase 2 仅 Agent scope 实际发射
  - `Expected` — `long?`（期望的观测序号引用，非 Observation 快照——I-13）
  - `Observed` — `long?`（实际的观测序号引用，非 Observation 快照——I-13）
  - `Source` — `string`（检测组件标识："Agent.DetectDrift"）
  - `Evidence` — `string`（检测原因的文本描述）
  - `LastAction` — `DeviceAction?`（触发 Trap 前的最后一个动作）

**Explicitly excluded** (no Scenario purchase — I-12):
  - `Confidence` / `Probability` — AI 语义层（Phase 5）
  - `Severity` — 恢复策略层（无 Scenario 消费）
  - `Timestamp` — Phase 1 使用 SequenceNumber 确定性序号（裁决 6），不用真实时间
  - `HistoricalMemoryFields` — Memory 组件 Phase 5

**Constraint**: Expected / Observed 为观测序号引用——不得嵌入 Observation 副本（I-13 God Context 防范，延续 Phase 1 SourceObservationSequence 模式）。

**Charter basis**: §21（Trap 最小字段定义）、I-12（无 Scenario 购买不加字段）、裁决 6（SequenceNumber 替代 Timestamp）

### HG-3: WorldBelief Drift Representation

**Decision**: Approved. Phase 2 **不引入** WorldBelief.DriftStatus 字段。

**Rationale**: 现有三个表面已覆盖 SC-P2-001 的漂移检测需求:
  - `Observation.ForegroundApplication` — 直接可读的前台应用变化
  - `Container.IsStillMine(observation)` — 注入规则判定的语义页面归属
  - `WorldBelief.SemanticPage` — Reconcile 后的语义页面名（null = 不可解析 → Unknown, §10）

**When to revisit**: 仅当未来 Scenario 证明现有表面不足（如"轻微漂移但 ForegroundApplication 不变 + IsStillMine 仍为 true + SemanticPage 仍可解析"的矛盾场景）时，才在 Scenario 购买下新增 DriftStatus 字段（裁决 9: Scenario pays for field）。

**Charter basis**: §10（World Belief 允许 Unknown）、I-12（无需求不提前实现）、I-13（不聚合 God Context）、裁决 9（无断言消费不加字段）

### HG-4: Recovery State Ownership

**Decision**: Approved. **Option B — 独立 Recovery 组件持有恢复机制状态，Agent 持有决策 authority。**

**Ownership split** (I-2: 一个 mutable state 一个 owner):
  - **Recovery 组件 owns**: 恢复进度状态（当前恢复阶段: Idle / Restoring / Verifying / Verified / Failed）、恢复动作序列、验证准备状态
  - **Agent owns**: RunState（全局生命周期）、最终决策 authority（I-3: 何时发起恢复、是否 Resume、是否终止 Run）

**Agent 组合 Recovery** (I-1 / §4): Agent 持有 Recovery 组件实例，调用其方法执行恢复机制，但 Agent 自己做决策——Recovery 组件只提供机制，不拥有决策 authority。

**Component boundary**: Recovery 是支持能力（§4），不是第五核心层。不参与 Agent → Container → Traversal → Environment spine。

**Rationale**:
  - 防止 Agent.cs 持续膨胀（§50 味道 #1: God Object）
  - I-2 / I-3 分离更干净: Recovery 组件 owner ≠ Agent decision authority
  - Charter §4 明确允许支持能力组件（"Agent 可以编排能力，但具体能力必须由明确组件提供"）

**Charter basis**: §4（支持能力）、§5（Agent 是 Run 级最高控制者）、I-2（单 owner）、I-3（单 authority）、§50（禁止 God Object）

### HG-5: Recovery Mechanism Scope

**Decision**: Approved. Phase 2 采用**最小 Recovery scope**——Recovery 组件内部保持简单，不引入 Charter §24 全量统一机制。

**Phase 2 Recovery scope** (仅 SC-P2-001..003 购买):

```
RecoveryAnchor.RestoreRecipe 消费
  ↓
恢复动作执行（Relaunch / Navigate — 通过 IEnvironment）
  ↓
Post-recovery Observe
  ↓
Verification（对照 VerificationCriteria）
  ↓
RecoveryResult（Verified | Failed(Reason)）
  ↓
Agent decision（Resume | Fail）
```

**Explicitly excluded from Phase 2**:
  - `RecoveryRequest` — 无 Scenario 需要独立请求/响应模型
  - `RecoveryPlanner` — 恢复策略单一（RestoreRecipe 注入数据），无需通用规划器
  - `RecoveryPlan` 独立类型 — 恢复动作序列由 RestoreRecipe 字符串 + Agent 解释驱动
  - `RecoveryRuntime` — 恢复执行复用现有 IEnvironment 端口
  - Generic recovery framework — 等 Phase 3 Container-scope Popup recovery（§38）证明需要统一机制时再提取

**When to extract unified mechanism**: Phase 3 Container-scope recovery 场景购买后，若出现第二个恢复路径（Agent-scope + Container-scope），且二者出现重复机制时，按 Charter §24 提取 RecoveryRequest / RecoveryPlanner / RecoveryRuntime。

**Rationale**: I-12（没有第二个恢复场景购买统一机制）、§60-E（Phase 2 只要求 Recovery Scenario 可运行）、Charter §24 授权但不强制（允许统一机制，Phase 2 最小化优先）

**HG-4/HG-5 coupling**: HG-4 Option B（独立 Recovery 组件）使 HG-5 的最小化在组件边界内可行——组件存在，但内部简单，外部 API 仅为 `RecoverAsync` + `VerifyAsync` → `RecoveryResult`。不引入 Request/Planner/Runtime 不削弱组件独立性。

**Charter basis**: §24（统一机制可选）、I-12（YAGNI）、§60-E（E. Recovery WiFi Scenario）

## Approved Constraints (Human Gate 裁决的全局约束)

1. **Phase 1 invariants frozen**: I-1..I-14 全部不变。Phase 2 不新增、不修改、不删除任何 Architecture Invariant。
2. **Phase 1 Scenario semantics preserved**: SC-P1-001..004 行为断言 1-4 不回退（裁决 1）。SC-P1-004 断言 5（架构断言）经本 gate 明确修订——Guard 5 缩窄为 Trap 类型仅限 Model + Recovery 组件。
3. **Phase 3 capabilities deferred**: Popup recovery（§38）、Uncertain action（§37）、Scroll identity（§36 / 裁决 2）、Fingerprint 字段与机制、Dynamic Grounding、local history、coordinate/hierarchy grounding 全部保持 Phase 3 boundary。
4. **Scenario-first evolution preserved**: 每个 capability 必须由 Scenario 购买（延续 Phase 1 的 裁决 9 / 裁决 7 模式）。Phase 3 的 Container-scope recovery 不得在 Phase 2 提前实现。
5. **No new core architecture layers**: Recovery 是支持能力（§4），Agent → Container → Traversal → Environment spine 不增加第五层。
6. **No premature abstraction**: RecoveryRequest / RecoveryPlanner / RecoveryRuntime 等待至少第二个恢复 Scenario 证明需要后再引入（Charter §24 + I-12）。
7. **Deterministic replay preserved**: SC-P1-001 断言 7 重放不回归——恢复路径同样必须确定性、可重放。
8. **Guard 5 + Guard 7 与 Trap 引入原子完成**: Guard 5 修订 + Guard 7 新增与 A1（Trap 类型定义）在同一 change 内原子完成（tasks.md Phase 2A A6/A7），不得出现 guard 空窗。

## Implementation Authorization

以上 5 项 Human Gate 决策全部 **Approved**。

Phase 2A implementation 授权启动，约束如下:
- tasks.md Phase 2A A1-A7 可执行
- Guard 5 修订（A6）与 Trap 类型定义（A1）原子完成
- Phase 2B-D 待 Phase 2A 验收通过后按序推进
- 如实施过程中发现本 gate 裁决与代码现实冲突 → 停止，走 HUMAN_GATE 上报（不得自行改 gate 裁决）
- Phase 2 独立验收就绪后走 runtime-validator 流程

Phase 2 acceptance criteria (remains):
- 3 Scenario（SC-P2-001/002/003）全部 Fake 可运行
- RecoveryAnchor 完整消费
- I-9 闭环（act→observe→verify→reconcile→resume）
- Phase 1 全量回归（104 tests + 重放）
- Architecture Guards 1-4 + 5 revised + 6 + 7 new 全部通过

## Status

```
PHASE_2_HUMAN_GATE_APPROVED
```
