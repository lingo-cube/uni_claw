# Runtime Simulator Iterative Planning Gate — Design Result

DocumentType: `GATE_DESIGN`
Decision: `PROJECT_LEADER_RUNTIME_SIMULATOR_ITERATIVE_PLANNING_GATE_RESULT`
Human Direction: `PROJECT_LEADER_RUNTIME_SIMULATOR_ITERATIVE_PLANNING_GATE`（2026-08-26：
PhysicalDevice DEFER · REAL_EMULATOR_FIRST · Phase3Memory DEFER · RuntimeChange NOT_AUTHORIZED）
Supersedes (scope-wise): `runtime-full-traversal-acceptance-analysis.md` §8/§13/§18 的单 Run 直取设计
Date: 2026-08-26
Status: **设计完成，停止于实现之前**（未创建 OpenSpec、未改 Runtime、未实现 Memory、未入物理设备）

---

## 1. Decision

**采纳迭代规划优先路线，并将原 Phase 2.6 拆为 2.6A/2.6B，合并为单一 validation-only
OpenSpec change（推荐名 `runtime-iterative-full-traversal-acceptance`，经分析确认而非机械
采用——理由 §12）。** 原 analysis 的能力结论不变（Runtime 零缺口、validation-only gap），
但其 Level 1–4 "单 Strategy 直取全树" 的验收形态被本设计取代：Full Traversal 现在是
**多轮上层学习收敛后的终局 Run**，而非首轮目标。

## 2. Why Simulator First

1. 真机 Settings 树含 destructive 节点（重置/账户/安全配置）；迭代实验的本质是"允许计划
   犯错"，错误的代价在模拟器上可接受、在真机上不可接受——**模拟器先行的首要理由是安全
   学习代价，不是便利**。
2. 迭代循环会产生大量探索性 Run（预期 5–15 轮）；真机每轮的设备状态恢复成本与风险不成比例。
3. Human 方向已裁定 REAL_EMULATOR_FIRST；Physical Device 推迟到 §10 Entry Gate 之后。

## 3. Human-readable Target Loop（冻结）

```
User Goal
→ Emulator Initial Plan（保守：Unknown→RecordOnly/FailClosed）
→ StrategyDirective A（closed vocabulary：objective/scope/depth/constraints/completion/adaptation）
→ 独立 Runtime Run A（fresh observe→ground→authorize→execute→verify→evidence→terminal）
→ Emulator Evidence Interpretation（读 wire 冻结面 Result：admission/events/snapshot/trap/
   evidenceRefs/terminal + Scenario Acceptance 外部对账）
→ Session Knowledge A（ephemeral，见 §5）
→ Plan Revision（必须产出 Plan Delta，见 §7）
→ StrategyDirective B（新 StrategyId，独立 Run B）
→ ……收敛条件：bounded scope exhaustion / 明确不可安全处理 frontier / Runtime-Contract gap
```

每轮 Run 的 Emulator 合法动作集 = 恰一次 `run.strategy.start` + 冻结只读面轮询。
（Phase 2.5 已证的 zero-intervention 边界在每轮内继续成立。）

## 4. Runtime vs Upper-Agent Ownership（不变式清单）

| 归属 | 内容 |
|---|---|
| Runtime（每 Run，冻结语义） | Observation/Grounding/Discovery/Classification/Authorization/Traversal/Scroll/Enter/Record/Return/Reconcile/Revisit/Unknown/Boundary/Verification/Ledger/GoalEvidence/FSM terminal |
| Upper-Agent（跨 Run，本阶段=Emulator 会话） | Goal interpretation、Strategy 生成与修订、Evidence interpretation、Session Knowledge、Plan Delta |
| 禁止线 | Runtime 无 Memory/无跨 Run state/无 mid-run 干预/无 strategy mutation/无 hidden path；Emulator 无 Run 内控制/无 completion 裁决 |

**两条不等式逐轮断言**：`HISTORICAL_KNOWLEDGE != CURRENT_WORLD_TRUTH`（每 Run fresh
observation 不被知识替代）；`HISTORICAL_RESULT != RUNTIME_ACTION_AUTHORITY`（知识只塑
Strategy，从不直接授权动作——授权仍走每 Run 的 CandidateAuthorization）。
**声明纪律**：`Upper Agent learns; Runtime executes fresh.`（§8 双能力分开，绝不声明
Runtime 自学习。）

## 5. Session Knowledge Model（ephemeral validation artifact）

每条 Knowledge = 不可变记录：
`{ KnowledgeType, SemanticAnchor, SourceRunId, EvidenceRefs, ObservedRole, Scope,
   Disposition, Confidence, ValidityAssumption }`。

KnowledgeType **全部复用毕业词汇**（映射源：`InteractionAffordanceKind`、
`TypeLevelElementCategory`、`StrategyProhibitedEffect`、EBD boundary disposition、RESAR
unresolved/frontier）：
- `KnownContainer`（↔ NavigationCandidate/NavigableContainer）
- `KnownRecordOnly`（↔ record-only visited/unknown-frontier 语义）
- `KnownLocalControl`（↔ LocalControl）
- `KnownExternalBoundary`（↔ AuthorizedBoundary disposition / ExternalBoundaryCrossing）
- `KnownNonInteractive`（↔ NonInteractive）
- `KnownUnresolved`（↔ unresolved ledger）
- `KnownPotentiallyStateMutating`（↔ StateChangingControl/StateMutation 禁止效应证据）

**零新 Runtime 语义**。知识只允许来源：fresh observable semantics、typed semantic
capability 输出、Runtime evidence、boundary disposition、既往**安全**执行证据。禁止：
无证据猜测、UI 文案硬编码直入 truth。Knowledge 生命周期=会话；Run 开始时 Runtime 收到的
只有 StrategyDirective（知识绝不注入 Runtime belief/grounding/authorization——它只影响
Emulator 怎么写下一个 directive）。

## 6. Safety Learning Rules（真机前置的核心纪律）

- **默认保守**：无法证明安全 ⇒ `RecordOnly`/fail-closed，绝不 ExploreByExecution。
- **禁止危险试错**："点一下看看危不危险" 不存在；destructive/state-mutating 候选
  （重置/删除/账户/安全配置/开发者选项/装卸/支付认证/网络关键态）只能通过 **observational
  evidence**（typed capability 分类、结构性信号如 switch class、boundary disposition）
  识别为 `KnownPotentiallyStateMutating`/`KnownExternalBoundary`，然后**下一轮 Strategy
  以 constraints/dispatch-policy 排除**——从未通过执行它们来学习。
- **能力面支撑（源码已确认）**：Strategy 的 `ConstraintSet.prohibitedEffects`
  （StateMutation/ExternalBoundaryCrossing）与 per-strategy `CreateDispatchPolicy` 正是
  把 Knowledge 变成更安全下一 Run 的**既有合同杠杆**——Plan Revision 不需要任何新面。

## 7. Plan Revision Contract

每轮固定产物六元组：`{ PreviousPlan, ObservedResult, NewKnowledge, RemainingUnknowns,
PlanDelta, NextStrategy }`。PlanDelta 必须显式解释变化及其证据，且是**可执行差异**
（落在 directive 的可变自由度内：depth、constraints、dispatch policy、objective
kind/typed criterion、scope 边界、completion kind）。缺 PlanDelta 的轮次=无效轮（要么
No-Op 并记录原因，要么终止循环）。

## 8. Iterative Acceptance Criteria（"越跑越聪明"的可证伪断言）

**双能力分开声明**：
- A `RUNTIME_SINGLE_RUN_AUTONOMY`（Phase 2.5 已毕业，本轮每轮复断言：恰一次 start + 零干预）
- B `UPPER_AGENT_CROSS_RUN_PLAN_ADAPTATION`（本轮新验收）

B 的通过断言（全部 evidence-linked，禁裸计数）：
1. ≥3 轮真实 adaptation，每轮 PlanDelta 有 EvidenceRefs 溯源；
2. 已判 RecordOnly 类节点在后续 Run 的 dispatch 面中被排除（观察面：后续 Run 的
   ActionDispatched 事件中该类目标为零，且原因可追溯到 Knowledge 条目而非碰巧）；
3. known external boundary 类不再作为 recursive child 进入 directive 的 allowed 集；
4. known local control 不再被当 navigation candidate 授权；
5. unresolved 集合单调不增或每次不降都有明确新证据解释（诚实性）；
6. 危险类候选从未被执行（安全断言：全程 ActionDispatched 与
   KnownPotentiallyStateMutating/ExternalBoundary 集合的交集为空）；
7. 探索效率改善（如重复访问同语义容器次数下降）**由 PlanDelta 因果解释**——
   "点击变少" 本身不是标准（§6 原文遵守）；
8. 每轮两条不等式断言成立；Emulator 全程零 Run 内干预（call log 逐轮核）。

## 9. Simulator Full Traversal Entry Criteria（Stage D 门槛）

Stage A（保守首探）→ B（Evidence-informed replan）→ C（迭代扩展）就绪判据：
1. §8 断言 1–6 全部成立；
2. Session Knowledge 覆盖率证据（known/discovered 比例 + unresolved 诚实清单）；
3. 剩余 unknown 全部为 observational-only 可判或明确不可安全处理；
4. 模拟器 regression 全绿、零 Runtime gap、零 intervention。
满足后 Stage D：生成**成熟终局 Strategy**（单 Run 尽量全树；允许多 Run 收尾但每个都独立），
验收 = 原 analysis §10 十四条（Ledger 对账、GoalEvidence+FSM、双层判定、双不等式）——
claim 仍为 `RUNTIME_AGENT_CAN_AUTONOMOUSLY_EXHAUST_A_REAL_BOUNDED_UI_TREE`
（Real Emulator 层；不含 physical claim）。

## 10. Physical Device Safety Entry Gate（= Human 方向的 10 条 + 产物）

模拟器侧满足 §9 + Human 列表 1–10 后：`PhysicalDevice: READY_FOR_SEPARATE_HUMAN_GATE`。
必备产物 **Simulator-derived Advisory Knowledge Package**（内容=Human §10 清单 + 每条
provenance/scope/device/version 假设）：定位 **UniAgent pre-Run planning advisory only**
——不得注入 Runtime belief、不得绕过 fresh observation/grounding/authorization（与
§4/§5 的禁止线同源）。

## 11. Phase 3 Memory Learning Inputs（DEFER ≠ 取消）

本阶段把 Emulator 会话当 **Memory 需求探测器**：记录所有真实产生 PlanDelta 的 Knowledge
类型、其 evidence 依赖、confidence 演化、失效假设（模拟器/版本/scope）。2.6 结束后反向
核对 `uniagent-local-exploration-memory` 草案四基线（Buyer=pre-Run advisory/Owner=
UniAgent-local/Scope=UNIAGENT_PRIVATE_CROSS_SESSION/Influence=PRE_RUN_ADVISORY_ONLY）
能否承载这些实测类型——预期基线不变、**存储 schema 从实测收敛**，但该结论留给 2.6 末
的核对记录，不在本轮预定。

## 12. OpenSpec Structure Recommendation

**单一 change：`runtime-iterative-full-traversal-acceptance`**（一个 validation-only
OpenSpec，含 2.6A 迭代规划验收 + 2.6B 模拟器全树验收两个 Stage 组；Physical Device 明确
out-of-scope 另设 Gate）。分析理由（非机械采用）：
1. 两者共享同一 harness 组合、同一 Session Knowledge 模型、同一安全规则与不等式——
   拆两个 change 会复制边界定义并制造跨 change 的知识模型耦合；
2. B 的 Entry Criteria 就是 A 的产出（§9），生命周期上不可独立毕业；
3. 与 Phase 2.5 同型（validation-tooling、NONE_RUNTIME），规模相当，一个 change 可控。
Roadmap 位置：Phase 2.6（替代原 analysis 的 2.6 定位；Physical Device 为 2.6 之后的独立
Gate 行）。`PHASE3_MEMORY_HUMAN_GATE` 顺延至 2.6 完成后。

## 13. Human Gate Required（本轮请求的授权）

1. **创建** `runtime-iterative-full-traversal-acceptance` OpenSpec change（Large：
   validation-only 新验收面；proposal 起草是 Exact Next Step）；
2. Real Emulator 战役授权（沿 Phase 2.5 同一 AVD/fixture 机制 + 真实 Settings app；
   不涉物理设备）。
（Runtime 改动授权：**不请求**——设计判定维持零 Runtime 缺口；若执行中出现 Runtime-owner
FDP 即 `STOPPED_AT_<reason>` 回 Gate。）

## 14. AuthorityDelta

`NONE`（纯设计文档；未改代码/契约/归档件；本文取代的只是未实施的方案章节）。

## 15. ArchitectureDelta

`NONE`。

## 16. Exact Next Step

Human 批准 §13 后：
1. 起草 change 四件套（proposal/design/spec/tasks）：冻结 §3–§8 循环/知识/安全/断言为
   spec requirements；Stage A–D 与 Physical Gate 为 tasks 结构；
2. harness 侧最小新增：SessionKnowledgeStore（ephemeral，会话内不可变记录）+
   PlanDelta 记录器 + 迭代 runner（复用 Phase 2.5 Driver/Collector/Verifier/Gates 全套）
   + SettingsStrategyBinding（生产 SettingsSemanticCapability 的 strategy 适配，承接原
   analysis 的 §18-2）；
3. Stage A 首轮保守 Run 起步，逐轮 PlanDelta 驱动；
4. 各 Stage 独立验收 + 回归；§10 产物与 2.6 毕业评审回 Human。

**本轮到此为止——设计停止于实现之前。**
