# Container Runtime V2 — Repository Mapping（P0 前置结论）

> DocumentType: `ANALYSIS_REPOSITORY_MAPPING`
> Status: `DRAFT — MAPPING CONCLUSIONS（P0 契约表输入；NOT_FROZEN / NOT_AUTHORIZED）`
> Authority: `NONE`
> Scope: 回答脑暴稿（v3-final §10/§11）列出的五项 Repository Mapping 强制结论。全部基于本轮代码核验，非推测。
> EvidenceRef: `src/UniClaw.Runtime/Container/Container.cs` · `src/UniClaw.Runtime/World/ContainerSemanticCorrection.cs` · `src/UniClaw.Runtime/Model/ContainerRuntimeV2.cs` · `src/UniClaw.Runtime/Model/Observation/Observation.cs` · `src/UniClaw.Runtime/Observability/RuntimeObservability.cs` · `src/UniClaw.Runtime.Agent` progress 笔画（`Agent.cs` / `Agent.ContainerReconciliation.cs` / `BranchProgressEvidence.cs`）
> 禁止边界: 本文档是 mapping 结论记录，不是实现授权、不改变任何 owner、不触发代码修改。

---

## 1. LocalModel owner budget

**现状盘点**（`Container.cs`，Container 自述"唯一 owner（I-2）"的 page-local mutable state 共 7 项）：

| 现有 state | 职责 | 处置（对应新模型） |
|---|---|---|
| `_observation`（CurrentObservation） | 当前观测快照 | **DERIVE** → `CurrentContainer.CurrentSliceRef → Slice`；Container 不再持有 |
| `_viewportExplorationObservations` | bounded 同页 Observation 历史（SC-P3-CAND-007） | **MOVE** → `LocalModel.Evidence.SliceRefs[]`（含 active/archived 分层） |
| `_executedSteps` | 页内已执行步骤（visited/local progress） | **MOVE/DERIVE** → Agent `BranchProgressEvidence`（已存在该职责）+ LocalModel interaction evidence |
| `_isLocalComplete` | 局部完成 bool | **DERIVE** → `ContainerLocalComplete`（coverage + obligations + closure 三条件）；bool owner 退役 |
| `_localPageBeliefState`（SemanticBeliefState） | identity evidence 融合信念 | **MOVE** → LocalModel Assessments（`CONTAINER_IDENTITY` claim，经 EvidencePolicy 聚合；FuseBelief 纯函数保留为聚合器之一） |
| `_objectBindings`（observation-local） | 对象绑定快照 | **DELETE/REPLACE**（B2）→ 绑定分析降级为 correspondence / membership evidence 输入；Occurrence 模型取代绑定实体 |
| `_objectStateBeliefs` | 对象状态信念字典 | **DERIVE** → Occurrence `StateHints` + LogicalItem `State`（canonical） |

保留不动：`identityRule` / `semanticPageName`（显式注入规则，降级为 Node.SemanticIdentityCandidate 的 evidence 来源）；CP12 step executor forwarding（执行层，非世界模型）。

**Budget 结论**：`NET_NEW_MUTABLE_TRUTH = +1`（LocalModel per-Node 聚合入 `ContainerRuntimeV2State`）。Container 现有 7 项 world state：1 DERIVE、2 MOVE、1 DERIVE、1 MOVE、1 DELETE、1 DERIVE —— 净效果是**集中而非叠加**。**双轨期（B2 shadow）Container 旧 state 与 LocalModel 并存 = 受控 shadow 比对本体**（分歧率统计即两者对账），切换后旧 world-state 退役；`_branchProgress`（Agent）全程 NOT_REWRITTEN。该 budget 在 Stage B2 实施时以 ArchitectureGuard 测试机械验证（Container 不再出现 world-state 字段）。

## 2. existing Container/page-local truth 的 REUSE / MOVE / DERIVE / DELETE

见上表（#1 的处置列即为本项答案）。补充两项非 `Container.cs` 的 page-local truth：

| 现有 symbol | 处置 | 说明 |
|---|---|---|
| `SemanticBeliefState` / `SemanticReconciliation.FuseBelief`（纯函数） | REUSE（作为聚合器） | 融合逻辑保留，产物降级为 LocalModel assessment evidence |
| `Agent.ContainerReconciliation` progress 对账（`ProgressLedgerKeysMatch` / `IsExactCompletedSiblingReplacement` / `SameStableProgress`） | REUSE PATTERN | supersession 消费机制笔画（见 #5） |

## 3. ordering primitive 是否已有可复用 symbol

**结论：REUSE，不新造 global semantic clock。** 两个已核验符号分工如下：

| symbol | 现有职责 | 新模型中的延续角色 |
|---|---|---|
| `SemanticEvidenceRevision`（`ContainerRuntimeV2.cs:81`） | V2 state 的单调 commit version；reducer 以其执行 stale/replay 拒绝（:755 "evidence revision is stale or already committed"） | **延续为 V2 state commit version**（OCC 买家）。Slice/Occurrence 的 atomic commit 进入同一 reducer 时自然共用该 version —— 语义未变，只是多了递增事件类型 |
| `Observation.SequenceNumber`（Observation.cs，裁决 6：确定性单调递增） | run-local observation 排序（Container 各 continuity 检查均以其为准） | **延续为 observation-order binding**（freshness binding 的组成分量） |
| Observability trace 链（RunId/ContainerId/StepId/ActionId） | 因果 trace | **trace metadata**（`EvidenceOrdering = optional trace metadata`） |

约束核验：OCC stale-rejection 买家由 `SemanticEvidenceRevision` 原位保留，无需 CREATE。`REVISION != FRESHNESS / TRUTH / CAUSAL_BINDING` 不变量不受影响。

## 4. rejected diagnostics 由谁承载

**现状**：Runtime core 无 domain 级 ValidatorDecision/trace record 类型（grep 核验：`class/record *Trace/Diagnostic/ValidationRecord` 零匹配）。既有承载通道：

- `RuntimeObservability`（`src/UniClaw.Runtime/Observability/RuntimeObservability.cs`）：span + `ObservabilityLayer/Component` 分层（Container.cs:160 已在用：`StartSpan("RefreshSnapshot", ObservabilityLayer.Container, ...)`）；
- DriverHost `RuntimeEventEnvelope`（事件外发通道）；
- 感知侧 causal trace 在 `platforms/`（fusion `causal_trace.py`，evidence-only，见 debugging landscape —— Python 侧，非 Runtime domain）。

**结论：EXTEND `RuntimeObservability` span/event 通道**承载 acceptance validator 决策（payload = `ObservationRef + candidate summary + reject reason + validator decision`，属 trace metadata）。**不需要 CREATE Runtime domain object** —— 与 RB-F5（REUSE/EXTEND first）一致；若 Stage B2 实施中发现 span 通道无法满足 anomaly 消费（消费方需要结构化查询），再回到 mapping 升级为最小 CREATE（需新证据）。

## 5. supersession correction：REUSE PATTERN / EXTEND / CREATE

**payload 实证**（`World/ContainerSemanticCorrection.cs`）：

- `ContainerSemanticCorrectionFact`（:118）字段域 = **container identity / trigger 语义域**：`AssessmentKind(Challenge|Correct)`、`ObservationRef/EvidenceRevision`（revision-bound ✓）、`NodeRef/SourceNodeRef`、`TriggerOccurrenceRef/TransitionOccurrenceRef`、`ActualTriggerSemantic/ObservedContainerSemantic/CorrectedIdentityCandidate/RelationSemantic`；无 mutation 副作用标志（`HasAppliedObligationMutation=false` 等 ✓）。
- `ContainerObligationReevaluationInput`（:193）同样是 page/obligation 语义域（`IntendedSemantic` 字符串候选、misclick/traversal context）。

**结论**：

```text
REUSE PATTERN  ：revision-bound fact + owner reevaluation input + no-mutation flags 的三段式
                （+ Agent.ContainerReconciliation 的 progress 对账消费机制）
CREATE（最小） ：LogicalItemSupersession 新 payload —— PriorLogicalItemRefs[] /
                ResultingLogicalItemRefs[] / deltaKind / evidence refs / binding refs
                （P0-C 契约表 C3 已定最小字段）
不 EXTEND      ：现有 payload 是 container-identity 域；EXTEND 会把它扩成万能 envelope
                （RB-14 明令禁止）并把 container-identity 语义拖进 item-membership 域
```

---

## Mapping 结论对 P0 契约表的回填

| P0 契约项 | mapping 结论影响 |
|---|---|
| A1 RuntimeAcceptance | 诊断走 Observability span（#4），无新 domain object |
| A2 Slice | ordering = `SemanticEvidenceRevision` 延伸（#3） |
| B1 LocalModel | owner budget = +1 集中式；Container 7 项 world state 处置表生效（#1/#2） |
| C3 Supersession | REUSE PATTERN + CREATE 最小 payload（#5）；消费机制 REUSE `Agent.ContainerReconciliation` pattern |

**本 mapping 与 P0 契约表一并停在 Human Gate。** 进入 OpenSpec propose 需 Human 再授权。
