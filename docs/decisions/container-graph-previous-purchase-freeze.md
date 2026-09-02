# Container Graph Previous Purchase Freeze

> Date: 2026-08-31
> Decision: `CONTAINER_GRAPH_PREVIOUS_PURCHASE_FROZEN`
> Scope: ContainerGraph、ActiveContainerContext、Container transition/context projection、Fast Container identity，以及可能被解释为 Graph/Current/Return authority 的历史实现与声明。
> Authority: Human direction in the Container Runtime V2 Project Leader gate.
> Lifecycle effect: 冻结旧购买语义；不删除历史、不归档 active change、不声明 Runtime V2 已购买或毕业。

## Decision

从本决策生效起，仓库中此前与 ContainerGraph / ActiveContainer / transition / context projection / Fast identity 相关的 proposal、实现、测试、Guard、task completion 和 graduation 声明，只能作为历史证据或待 reconciliation 的实现资产。

```text
CONTAINER_GRAPH_PREVIOUS_PURCHASE_FROZEN

OLD_TASK_CHECKBOX != RUNTIME_V2_PURCHASE
OLD_GRADUATION != RUNTIME_V2_GRADUATION
OLD_IMPLEMENTATION != CURRENT_ARCHITECTURE_AUTHORITY
```

冻结不撤销已经由真实证据验证的底层不变量，例如 fresh grounding、Observation-as-evidence、GoalEvidence completion、verified return、fail-closed observation acceptance、Container-local evidence ownership。冻结的是对这些机制的旧架构解释及其扩展权，不是删除它们的实现。

## Frozen purchase surfaces

| Surface | Current evidence state | Freeze disposition |
|---|---|---|
| `runtime-active-container-context-and-transition-semantics` | Active OpenSpec；未提交实现已完成 tasks 1.1–5.2；6.x verification 未完成 | `FROZEN_FOR_V2_RECONCILIATION`；禁止继续按旧语义扩展或凭 tasks 勾选宣布毕业 |
| `ActiveContainerContext.ActiveExecutionContainer` | Agent-owned Run-local execution-obligation context | 保留实现作为迁移输入；不得再称为当前物理 Container truth |
| `ActiveAncestorPath` | 有序父级执行义务路径；用于 verified return / ancestry guard | 保留路径证据；不得解释为 canonical parent、Graph topology 或 navigation route |
| `ContainerTransition` closed kinds/dispositions | 已有不可变事件、分类器、原子 commit 和 read projection | 保留 evidence/ref/atomicity 机制；旧 kind 语义必须进入 V2 occurrence reconciliation |
| Fast Semantic Container Identity baseline | 已归档且有 graduation 声明；provider 只产 candidate evidence | 其“candidate-only / no authority”边界继续有效；不得据此声称 Fast Resolver、Fast Trust 或 Runtime V2 已购买 |
| StableKey / RowIdentity container-domain work | 有单元/矩阵证据；真实 child buyer 未获得，明确 `MISSING_TRANSITION_OBSERVABILITY_SEAM` | 保留 container-scope correlation；不得用 Action heuristic 或 StableKey 充当 transition/identity truth |
| Existing Container traversal/completeness mechanisms | 多个已毕业/验证机制 | 作为 KEEP/MOVE/DELETE/DEFER 输入；不得因 V2 名称变化被批量重写 |

完整证据和逐项 disposition 见 [Container Runtime V2 Purchase Reconciliation Ledger](../analysis/container-runtime-v2-purchase-reconciliation-ledger.md)。

## Explicitly superseded or rejected semantics

以下语义无论是否曾被文档、代码命名或测试隐含，都不得进入 Runtime V2：

```text
GRAPH_NODE_HAS_CANONICAL_PARENT
CONTAINER_GRAPH_IS_NAVIGATION_PLANNER
KNOWN_EDGE_AUTHORIZES_ACTION
HISTORICAL_RELATION_IS_CURRENT_WORLD_TRUTH
TRANSITION_EXPECTATION_IS_OBSERVED_TRANSITION
NODE_REQUIRES_PROVEN_IDENTITY_BEFORE_EXISTENCE
RETURN_IS_REVERSED_ENTRY_EDGE
GRAPH_CURRENT_ACTIVE_CONTAINER_HAVE_PARALLEL_AUTHORITY
ACTIVE_EXECUTION_OBLIGATION_IS_CURRENT_PHYSICAL_LOCATION
RUN_GLOBAL_VISITED_SEMANTIC_IDENTITY_REJECTS_ALL_REENTRY
```

Replacement constraints:

```text
CONTAINER_GRAPH != NAVIGATION_PLANNER
KNOWN_EDGE != ACTION_AUTHORIZATION
HISTORICAL_RELATION != CURRENT_WORLD_TRUTH
ACTION_EXPECTATION != WORLD_TRUTH
RETURN_EXPECTATION != RETURN_TRUTH
NODE_EXISTS != NODE_IDENTITY_PROVEN
NODE_HAS_NO_CANONICAL_PARENT
ENTRY_RELATION != RETURN_RELATION
CURRENT_CONTAINER != PENDING_EXECUTION_OBLIGATION
```

## Preservation and rollback

- 不删除或改写历史 OpenSpec、Decision、evidence、tests 或 production symbols。
- 新 V2 Apply 必须先给出旧符号的 `KEEP / MOVE / DELETE / DEFER` 归属和替换顺序。
- 在替换完成前，可以保留兼容适配，但不得形成第二套 mutable current-location truth。
- 若 V2 实验被 falsify，可停止新 seam 并继续使用已冻结实现的已验证行为；不得恢复上述 rejected semantics。
- `NET_NEW_MUTABLE_TRUTH = 0` 仍是硬预算；任何新增 current/graph/trust mutable state 必须删除或派生掉等价旧状态。

## Lifecycle truth

本决策不把 Working Draft 提升为规范，也不宣告新的 OpenSpec 已 Apply：

```text
Working Draft: ARCHITECTURE_CANDIDATE
Previous purchase: FROZEN_FOR_RECONCILIATION
Container Runtime V2: NOT_YET_GRADUATED
```
