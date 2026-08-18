# Design: Phase 2 — Trap & Recovery Architecture

> 对应 Charter §60-E。本文档是 Architecture Proposal；`specs/` 是 Capability Contracts；`scenarios/` 是 SC-P2-001..003 正式执行契约；`tasks.md` 是实施清单。
> 设计原则: Charter §54（先设计后编码）、I-12（无需求不提前实现）、Scenario-first（每个 capability 由 Scenario 购买）。

## 1. Phase 2 Architecture Impact（相对 Phase 1 Spine 的增量）

```
Phase 1 Spine（保留不动）:
  Agent → Container → Traversal → IEnvironment

Phase 2 增量:
  Agent ──→ Recovery（支持组件，非新核心层 — §4）
    │           │
    │           ├── RecoveryPlan（消费 RecoveryAnchor.RestoreRecipe）
    │           ├── RecoveryVerify（对照 VerificationCriteria）
    │           └── RecoveryResult（Verified | Failed）
    │
    ├── Trap（一等 Model，不可变值）
    │     ├── TrapKind（UnexpectedPage | WorldLost | StateMismatch | ...）
    │     ├── TrapScope（Step | Container | Agent）
    │     ├── Source（哪个组件检测到）
    │     ├── Expected / Observed（观测序号引用，非快照 — I-13）
    │     └── Recoverability / Evidence / LastAction
    │
    └── Drift Detection（在现有 ForegroundApplication + IsStillMine 面上）
          │
          └── HG-3：是否需要 WorldBelief.DriftStatus?（待审批）

Traversal ──→ Step Retry（re-observe / re-resolve，有界、确定性、不升级）
                │
                └── Journal 扩展（retry entries）
```

**关键约束**：
- Recovery 是**支持能力**（§4），不是第五核心层
- Trap 是 Model 层不可变值类型，不是 behavioral component
- 所有恢复动作只能由 Agent 恢复路径发出（I-8：低层不得私自恢复）

## 2. Component Ownership Boundaries（I-2）

| 可变状态 | Owner | 宪章依据 |
|---------|-------|---------|
| RunState（全局生命周期） | Agent | Phase 1 不变 |
| WorldBelief | Agent | Phase 1 不变 |
| Active Container Stack | Agent | Phase 1 不变 |
| Container 局部状态 | Container | Phase 1 不变 |
| Traversal 单步状态（journal + retry count） | Traversal | §7 / §22 Step Scope |
| 模拟世界状态 | ScriptedEnvironment（Fake） | Phase 1 不变 |
| TraceEvent 列表 | Agent | Phase 1 不变 |
| **Trap 实例（escalation 期间）** | **流动**（Traversal → Container → Agent 只读转交，不可变值） | I-2（跨 owner 只传不可变快照） |
| **Recovery 进度状态** | **HG-4 待审批**：Option A = Agent 直接持有；Option B = Recovery 组件持有（Agent 拥有决策 authority — I-3） | §5 / §24 |

## 3. Data Flow（Trap → Recovery → Resume）

```
Observation（post-action）
  │
  ├── Container.IsStillMine → false
  ├── ForegroundApplication ≠ expected
  └── WorldBelief 语义页面不可解析
        │
        ▼
  Drift Detection（Agent）
        │
        ▼
  Trap(Kind=UnexpectedPage, Scope=Agent, Expected=S3, Observed=S4, ...)
        │
        ▼
  Agent: Suspend Plan
  Agent: Consume RecoveryAnchor.RestoreRecipe（EntryStrategy + RestoreRecipe）
  Agent: Execute Recovery Actions（Relaunch / Navigate to ExpectedSemanticEntry）
        │
        ▼
  Observe（post-recovery）
        │
        ▼
  Verify（对照 VerificationCriteria）
     ├── PASS → Reconcile → Rebind Container → Resume Plan → Continue
     └── FAIL → Run Failed（显式原因，无 Resume）
```

**数据约束**：
- Trap.Expected / Trap.Observed 是**观测序号引用**（`long?`），不是 Observation 副本（I-13 God Context 防范）
- RecoveryPlan 由 RecoveryAnchor 的 RestoreRecipe 数据驱动（注入语义），不硬编码恢复策略
- 验证步骤可观察：VerificationCriteria + 实际观测 → Trace 事件

## 4. Authority Ownership（I-3）

| 决策 | Authority | 宪章依据 |
|------|-----------|---------|
| 发射 Trap（Agent scope） | Agent | §22 |
| 发射 Trap（Container scope） | Container | §22（Phase 3 消费，枚举预留） |
| Step retry（re-observe / re-resolve） | Traversal | §30 |
| Step retry 耗尽 → escalate | Traversal → Container → Agent（走 Phase 1 `Failed` 路径，不产生 Trap） | I-12（Step-scope Trap 无 Scenario 购买） |
| 发起 Agent Recovery | Agent | §5 / I-8 |
| 选择恢复策略（RestoreRecipe 消费） | Agent | §5 |
| 执行恢复动作 | Agent（通过 IEnvironment） | I-8（低层不得私自恢复） |
| 判定恢复验证通过 | Agent（使用注入 VerificationCriteria） | I-9 |
| Resume（继续 Plan） | Agent | §5 |
| 终止 Run（恢复失败） | Agent | Phase 1 authority 保留 |
| 完成判定 | Agent（基于 Goal evaluator） | I-10 / Phase 1 保留 |

**禁止**：
- Traversal 自行 PressBack / LaunchApp（I-8）
- Container 自行恢复（Phase 2 Container 无恢复能力——Popup 归 Phase 3）
- Recovery 组件自行判定 Run 终止（authority 唯一在 Agent）

## 5. Recovery Lifecycle（相对 Phase 1 的增量）

```
Phase 1 失败路径（保留）:
  Traversal.Failed → Container 转交 → Agent.Fail(reason) → Run Failed

Phase 2 恢复路径（新增）:
  Running → Detect Mismatch → Emit Trap(Scope=Agent)
    → Agent: Suspend Plan（保存当前位置）
    → Agent: Consume RecoveryAnchor.RestoreRecipe
    → Agent: Execute Recovery Actions
    → Agent: Observe
    → Agent: Verify（对照 VerificationCriteria）
       ├── VERIFIED → Reconcile → Rebind Container → Resume Plan → Running
       └── FAILED → Run Failed（显式原因："恢复验证失败：期望 X，实际 Y"）

Step Retry 路径（新增）:
  Traversal.Select 失败
    → Re-observe（不派发动作）
    → Re-resolve
    → 成功 → 继续 Execute
    → 重试耗尽 → Failed(Reason) → escalate to Agent（走 Phase 1 失败路径）

恢复失败后的 Agent 路径:
  Run Failed（Reason = 恢复验证失败原因）
    → 终态 Failed
    → Trace 含验证失败事件
    → 无 Resume / 无后续 Plan 步骤
```

## 6. Model Evolution（Phase 1 → Phase 2）

| Phase 1 模型 | Phase 2 变更 | 购买 Scenario |
|-------------|-------------|--------------|
| TraversalStepResult | **不变**（Succeeded \| Failed 语义保留；Trap 并列新增） | SC-P1-004 不回退 |
| RecoveryAnchor(3 字段) | **+RestoreRecipe, +EntryStrategy**（裁决 8 解除；§20 字段落地） | SC-P2-001 |
| TraceEvent | **+TrapKind?, +TrapScope?, +RecoveryId?**（§28 因果链） | SC-P2-001 / SC-P2-003 |
| TraversalJournalEntry | **+retry entries**（re-observe / re-resolve markers） | SC-P2-002 |
| Observation / ObservedElement | **不变**（无 Fingerprint — 裁决 2；无 coordinate — 裁决 3） | — |
| WorldBelief | **HG-3 待审批**：DriftStatus?（0~1 字段，仅当现有面不足） | SC-P2-001 |
| **（新）Trap** | Kind / Scope / Source / ExpectedSeq? / ObservedSeq? / Recoverability / Evidence / LastAction? | SC-P2-001 |
| **（新）TrapKind** | enum: UnexpectedPage / WorldLost / StateMismatch / TargetLost / PlanInvalid | SC-P2-001 |
| **（新）TrapScope** | enum: Step / Container / Agent | SC-P2-001 / SC-P2-002 |
| **（新）RecoveryResult** | Verified \| Failed(Reason) | SC-P2-003 |

**预计新增类型**：4~6 个不可变值类型 + 2 RecoveryAnchor 字段 + 0~3 TraceEvent 字段。**不新增核心层**（§4 纪律）。

## 7. Architecture Guard Plan（Phase 2D 实施）

| Guard | Phase 1 规则 | Phase 2 规则 | 依据 |
|-------|-------------|-------------|------|
| Guard 1 | csproj 零 ProjectReference | **不变** | I-1 |
| Guard 2 | 零旧 namespace 引用 | **不变** | I-11 |
| Guard 3 | Contract doc + 14 invariants | **不变** | — |
| Guard 4 | AGENTS.md 导航 | **不变** | — |
| Guard 5 | Model 无 Trap 类型声明 | **缩小范围**：Trap 类型仅限 Model/ + Recovery/ 组件 | 裁决 4 解除 |
| Guard 6 | Model 无 coordinate/hierarchy | **不变**（Phase 3） | 裁决 3 |
| **Guard 7（新）** | — | Recovery 组件不得引用 Container/Traversal 内部实现（I-1） | §41 |

**Guard 5 修订**（与 A1 原子完成——移到 Phase 2A，不等到 Phase 2D）：正则从禁止 `Trap|TrapKind|TrapScope` 改为**仅允许**在 `Model/` 与 `Recovery/` 目录出现——其他目录（Agent/Container/Traversal/Startup/World/Environment）仍禁止。**`RecoveryRequest` 保持全目录禁止**（HG-5: Phase 2 不引入 RecoveryRequest，无 Scenario 购买——Charter §24 统一机制待 Phase 3 Container-scope recovery 证明需要）。

## 8. Human Gate Decisions（全部待审批，Phase 2A 启动前必须获批）

### HG-1: Guard 5 修订协议

- **旧规则**：Model 层零 `Trap|TrapKind|TrapScope` 类型声明（`RecoveryRequest` 仍全目录禁止——无 Scenario 购买）
- **新边界**：Trap 类型仅限 `src/UniClaw.Runtime/Model/`（类型定义）+ `src/UniClaw.Runtime/Recovery/`（消费组件，如果 HG-4 选 Option B）
- **新 Guard 7**：Recovery 组件不得引用 Container/Traversal 内部实现（`UniClaw.Runtime.Container` / `UniClaw.Runtime.Traversal` namespace 在 Recovery/ 中禁止）
- **决策点**：Guard delta 与 Trap 引入必须同一 change 原子完成（防止 guard 空窗）

### HG-2: Trap 字段集合（待审批）

- **候选字段**（Charter §21 明确定义）：
  - `Kind`：TrapKind enum（UnexpectedPage / WorldLost / StateMismatch / TargetLost / PlanInvalid / ContainerMismatch）
  - `Scope`：TrapScope enum（Step / Container / Agent）
  - `Source`：string（哪个组件检测到："Traversal.Select" / "Container.IsStillMine" / "Agent.DetectDrift"）
  - `Expected`：long?（期望的观测序号引用，非 Observation 快照——I-13）
  - `Observed`：long?（实际的观测序号引用）
  - `Recoverability`：初步判断（bool 或 enum Recoverable / Unknown / Unrecoverable）
  - `Evidence`：string（检测原因的文本描述）
  - `LastAction`：DeviceAction?（触发 Trap 前的最后一个动作）
- **字段形状决策**：待审批
- **约束**：Expected/Observed 不得嵌入 Observation 副本（I-13 God Context 风险——延续 Phase 1 SourceObservationSequence 模式）

### HG-3: WorldBelief DriftStatus

- **问题**：漂移检测是否需要 WorldBelief 新增字段？
- **现有表面**：`Observation.ForegroundApplication`（直接可读）+ `Container.IsStillMine(observation)`（注入规则判定）+ `WorldBelief.SemanticPage`（Reconcile 后的语义页面名）
- **评估**：当前表面可能已覆盖 SC-P2-001 的漂移检测需求——ForegroundApplication 变化 + IsStillMine 失败 + 语义页面不可解析 三者组合即可判定 Agent-scope 漂移
- **建议**：先不加字段——Scenario 证明现有面不足时再加（裁决 9）
- **决策点**：待审批确认

### HG-4: Recovery 状态 Owner

- **Option A**：Agent 直接持有 Recovery 生命周期状态（`_recoveryState` / `_isRecovering`）——简单，但 Agent.cs 持续膨胀（§50 味道 #1）
- **Option B**：独立 Recovery 组件持有机制状态（恢复进度 / 恢复动作序列 / 验证状态），Agent 持有决策 authority（何时发起恢复、是否 Resume）并组合 Recovery 组件——I-2/I-3 分离更干净
- **Charter §24**：允许统一 Recovery 机制（RecoveryRequest → RecoveryPlanner → RecoveryPlan → RecoveryRuntime → RecoveryResult），但 Phase 2 只买 §35 场景（I-12）
- **建议**：Option B（Recovery 组件）——防止 God Object，且 Charter §4 允许支持能力组件
- **HG-4 / HG-5 耦合**：HG-4 Option B（独立 Recovery 组件）是 HG-5 Option B（RecoveryRequest/Planner/Runtime）的前提——不存在独立组件时无需统一机制。HG-5 Option A（最小 Agent 内方法）与 HG-4 Option A（Agent 直接持有）等价。建议一次 gate 裁决同时拍板二者，或先拍 HG-4（决定是否有组件），HG-5 自动收敛
- **决策点**：待审批确认

### HG-5: Recovery 机制 Scope

- **Charter §24 全量**：RecoveryRequest → RecoveryPlanner → RecoveryPlan → RecoveryRuntime → RecoveryResult
- **Phase 2 只买 §35 场景**：Agent-scope Launcher drift 恢复（I-12——没有第二个恢复场景购买额外机制）
- **建议**：最小化——若 HG-4 选 Option A（Agent 直接持有），恢复逻辑以 Agent 内方法表达（`RecoverFromAgentScopeTrapAsync`）；若 HG-4 选 Option B（独立组件），恢复组件内部仍最小化（RecoveryResult 值类型 + Agent 组合），不引入 RecoveryRequest/Planner/Runtime 类型
- **决策点**：待审批确认（与 HG-4 联动——见 HG-4 耦合说明）

## 9. Deferred（Phase 3 边界，本 change 明确不碰）

| 能力 | 推迟到 | 原因 |
|------|--------|------|
| Container-scope popup recovery | Phase 3（§38） | Charter §39 路线图；Container 无局部恢复动作 |
| Uncertain action（timeout ≠ failed） | Phase 3（§37） | 派发后 timeout 重试归 Robust Execution |
| Scroll identity / Fingerprint | Phase 3（§36 / 裁决 2） | Fingerprint 字段与机制仍 DEFER |
| Dynamic Grounding / local history | Phase 3（§39） | — |
| Coordinate / hierarchy grounding | 未来 Scenario 购买时（裁决 3） | Guard 6 保持 |
| FSM | Phase 3+（§17） | Recovery 协议用普通方法表达 |
| 真实设备 / Vision Adapter | Phase 4（§39） | Fake Environment 优先 |
| Semantic Identity 算法 | Phase 5 | 注入显式规则 |
| DI 容器 | 不引入 | 构造器注入 + 组合根 |
| LLM / VLM / Memory | Phase 5 | §57-12 |

## 10. Phase 1 回归保护

- SC-P1-001..005 全部 Scenario 的 Given / When / Then 不回退（裁决 1）
- 104 项既有测试全部保持绿
- 确定性重放（SC-P1-001 断言 7）仍然成立
- Phase 1 失败路径（SC-P1-002 / SC-P1-003 负向 / SC-P1-004）不受影响——恢复路径仅在新增 data variant 下触发
- Architecture Guards 1-4 + 6 不减弱
