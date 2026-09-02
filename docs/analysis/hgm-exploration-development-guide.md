# HGM → UniClaw Exploration 开发指引（可讨论草案）

- DocumentType: `NON_NORMATIVE_ANALYSIS`（开发指引草案，供讨论；不是 Decision / Contract / Spec）
- Status: `DRAFT_FOR_DISCUSSION`
- Authority: `NONE`
- 创建依据：对 HGM（**Huxley-Gödel Machine**，[论文 arXiv:2510.21614](https://arxiv.org/abs/2510.21614) · [源码 metauto-ai/HGM](https://github.com/metauto-ai/HGM)）的分析，以及 Uni-Claw Runtime Exploration 当前生命周期（Roadmap Phase 2 已毕业、Phase 3 待独立 Human Gate、Phase 4 未授权）。
- 相关权威源（只读引用，本文件不修订它们）：
  - Runtime Architecture Contract — `../system/constitution/runtime-architecture-contract.md`（I-1..I-14）
  - Runtime Exploration Roadmap — `../decisions/runtime-exploration-roadmap.md`
  - Phase 2 冻结基线 — `../decisions/runtime-exploration-phase2-capability-baseline-freeze.md`
  - Phase 2 毕业决策 — `../decisions/runtime-exploration-phase2-final-graduation-decision.md`
  - Phase 3 草案 — `../../openspec/changes/uniagent-local-exploration-memory/proposal.md`
  - 当前状态投影 — `../snapshots/latest.md` · 活跃门禁 — `../work/active/current-gates.md`

## Scope（本文档回答什么）

把 HGM 当作**“如何在一个不断扩张的未知空间里，用历史后代证据判断哪里值得继续探索”**的思想来源，整理成 Uni-Claw 可讨论的开发指引。覆盖：HGM 核心思想速览、与 Uni-Claw 现状的对齐、概念映射、落地车道（每一步的 Gate 与禁止项）、明确不照搬清单、边界合规校验、自我迭代闭环、以及供讨论的开放问题。

结论先行：HGM 对 Uni-Claw 最有价值的不是 “Self-Improving Agent”，而是三件事——

1. **评价“子树”而不是只评价“当前节点”**（Clade-Metaproductivity / lineage 生产力）；
2. **Expand 与 Evaluate 分离**（Uni-Claw 对应 Explore / Validate）；
3. **把 Uncertainty 作为调度器的正式输入**（progressive widening + Thompson sampling 的动态平衡）。

这三件事恰好对准 Uni-Claw 从“会探索的 Runtime”走向“越来越知道哪里值得探索的 Runtime”的缺口。

## Forbidden Boundaries（本文档不做什么）

- 不建立或修改 authority / lifecycle / owner / Runtime behavior / implementation authorization（`docs/analysis/AGENTS.md` 治理）。
- 不改写已冻结语义：Depth 0/1/N Run 内不可变；Ledger 是只读 evidence projection、无状态系统、无完成权；`Visited` = rule-satisfied，不是 clicked；unresolved 是一等 fail-closed 结果，不是 failed；RuntimeAgent 只执行不规划；StrategyDirective 接受后不可变。
- 不绕过冻结边界 `Observation → Evidence → Fusion → Belief → Agent`；Memory 只会成为 Prior/Evidence，永不成为 Runtime truth。
- 不批准任何 Phase 3 apply 或 Phase 4（Dynamic Depth / Advanced Exploration / 自动策略生成 / 场景知识库）。Phase 3/4 相关内容一律是“未来能力方向”的讨论素材。

---

# 1. HGM 核心思想速览（只取与 Uni-Claw 相关的部分）

| # | HGM 概念 | 一句话 |
|---|---|---|
| 1 | **Clade-Metaproductivity (CMP)** | 评价节点不只看自身 performance，而看“以它为根的子树的未来后代生产力”。论文的关键观察是 Metaproductivity–Performance Mismatch：当前性能好 ≠ 还能继续产生更好后代。 |
| 2 | **Expand / Evaluate 分离** | `expand()`（生成新 agent/节点）与 `eval_agent()`（给已有 agent 分配新任务、更新后验）是**两个独立决策**，由 Controller 决定当前该做哪个。 |
| 3 | **Progressive Widening** | `n_task_evals^α >= len(nodes)`（默认 α ≈ 0.6）决定是否可以扩展新节点，否则继续验证已有节点——实现“证据增长 vs 搜索空间增长”的动态平衡。 |
| 4 | **Thompson Sampling** | 每个节点维护 Beta(1+success, 1+failure)，每次从后验采样 θ、选 θ 最大的节点。高收益高确定性与潜在高收益高不确定性都有机会被探索；确定地差的节点自然失去预算。 |
| 5 | **Clade statistics** | 把后代 successes/failures 聚合到 clade（祖先）上，用聚合统计指导父节点是否值得继续扩展。 |
| 6 | **失败分类 + 诊断** | 不直接 retry；先按 failure type（unresolved / empty patch / stochasticity / context length / resolved）分类，再 `diagnose_problem()` 选出改进目标。 |
| 7 | **异步解耦** | Expand / Evaluate 解耦后可分别跑在独立 worker 池（线程池）。这是解耦的收益，不是目标本身。 |
| 8 | **best-belief 最终选择** | 论文用 Beta 后验 percentile 选最终 agent。**注意：GitHub 已有 open issue 质疑该选择公式（epsilon 解释、中等 epsilon 偏向早期/评估次数多的 agent）。不照搬公式，只借“贝叶斯不确定性感知搜索”的思想。** |

# 2. 与 Uni-Claw 当前状态的对齐（真实状态，不是假设）

| 阶段 | 实际状态 | 对 HGM 的意义 |
|---|---|---|
| Phase 0/1 | 执行可靠性与探索模型 | 已具备：动作可靠性、Recovery、StrategyDirective 契约、Visited=rule-satisfied |
| Phase 2 | **GRADUATED / CHANGE SET ARCHIVED**：ExplorationLedgerView（discovered/visited/pending/unresolved/unknown-frontier）、closed `ExplorationRule`（ExpandContainer/RecordOnly）、admission-derived `ExplorationExecutionSemantics`、Depth 0/1/N 不可变、unresolved fail-closed 一等结果 | Ledger 已经是一等“证据投影”；HGM 的 lineage/统计语义不应再往里塞状态，而应作为**跨 Run 统计**（Phase 3）与**策略语义**（Phase 4）生长 |
| Phase 2.5 | GRADUATED：UniAgent Emulator Validation Harness（Tier A/B，S1 深度遍历 / S2 fail-closed / S3 cross-run 模拟） | 这是未来 Evolution Plane 的天然评估/回放语料承载者 |
| Phase 3 | READY_FOR_SEPARATE_HUMAN_GATE / NOT_APPLIED：`uniagent-local-exploration-memory` 草案（UniAgent-local、advisory-only、provenance-bearing、pre-Run buyer；cross-session 需 Human 批准） | HGM lineage/未来生产力统计的**正式归宿**：作为 KnowledgeClaim 维度的 branch prior，而绝不是 Runtime 状态 |
| Phase 4 | **NOT_AUTHORIZED**：Dynamic Depth / Unknown Handling / Exploration Strategy Selection | Explore/Validate 分流、Budget Policy、Progressive Widening、TS selector 全部在这里等门禁；Roadmap 自己已经点名这些方向 |

关键校正（相对“现在正做到 Phase 2”的直觉）：

- `unresolved` 作为一等 Outcome —— **Phase 2 已实现**（fail-closed、非 failed、不猜测）。HGM 只是从算法角度再次说明为什么这个决定是对的。注意 **HGM 的 `unresolved_ids`（benchmark 任务未解出）与 Uni-Claw 的 unresolved（探索证据状态）同名不同物**，见 §3.5。
- `parent_id / lineage_id` —— 放进 Ledger 是“新状态/图”，触碰 Phase 2 冻结边界与 SC-P3-CAND-007 “无 graph/stack/manager”禁令，**不推荐**。lineage 应放在 **Phase 3 Memory（跨 Run）** 里由 provenance 记录表达。
-“Depth Control → Budget Policy” —— 这是 Phase 4 议题，不是当前 Phase。Phase 2 的 depth 0/1/N 是唯一冻结深度语义，后续任何 Budget Policy 都只能**在其上叠加**，不能重定义。

# 3. 概念映射表（HGM → Uni-Claw → 归属车道）

| HGM | Uni-Claw 对应 | 归属 | 注意 |
|---|---|---|---|
| Agent Version = Node | ExplorationNode / Container 语义节点（ExpandContainer/RecordOnly；Visited=rule-satisfied） | Phase 2 已毕业（只做概念对应，不新增） | 不做 “Agent commit = Node”；Runtime 不拥有策略节点 |
| CMP / Clade statistics | **BranchPromise**：lineage 后代生产力统计（productive / unresolved / dead-end 比例、平均有效深度、期望 fact 增量、recoverability） | Lane 1（数据/prior）→ Lane 2（策略输入） | **不能裸 count**（`docs/analysis` 级讨论就定调）：必须按 depth / recency / causal weight / 决定性 fact 加权，否则“100 个小 fact 分支”会压过“2 个决定性 Goal Fact 分支” |
| 评价当前节点 vs 子树 | 探索 Desirability = 当前 Outcome + Future Utility（information gain / exploration yield / future reachability / unresolved reduction） | Lane 1/2 | 不改变 Phase 2 完成权；只影响“下一步探索哪里” |
| Expand / Evaluate 分离 | Explore / Validate 分流（Runtime Loop 内显式二选一） | Lane 2 | Runtime 语义变化，必须 OpenSpec + Human Gate |
| Progressive Widening | `E^α ≥ N`（E=已结算的有效验证/结果次数，对应 HGM task evals 的可数单位；N=探索候选节点数）作为 EXTEND/VALIDATE 闸门 | Lane 2 | α 是候选 admission 参数；**E 不是 “evidence 记录数”**而是计数动作/结果，可数单位错位见 §3.5 |
| Thompson Sampling | Branch Thompson Sampling：`Priority = Sample(Beta(1+productive_success, 1+productive_failure)) × ExpectedImpact × Recoverability / (ExpectedCost × Risk)` | Lane 2 | 公式是候选草案；`BranchPromise ≠ Confidence`（promise=从这里继续探索值不值；confidence=事实可不可信） |
| 失败分类 + 诊断 | UnresolvedReason 词典 → Recovery Strategy 映射（见 §4 Lane 0） | Lane 0（词典）→ Lane 2（接线） | 只搬运“分类后分流”的结构：HGM 分类在 agent-improvement 层、Uni-Claw 在 Runtime 探索层，词典值不互译（§3.5） |
| 异步 worker 池 | 决策并发 / 证据处理并发 / 模拟并发；**物理 Action commit 保持串行** | Lane 3 及以后 | 不急着并发 Runtime Action：GUI/physical action 会改变共享世界状态 |
| best-belief 选择 | Human Gate + promotion | Lane 3 | 选择公式有开源争议，不照搬 |

## 3.5 概念混淆核查（术语对账）

把 HGM 术语搬进 Uni-Claw 时，以下成对概念**名称相近但语义层级/对象不同**，本文档的处置统一在此声明，避免被读成一一对应。

| 术语 | HGM 原义（已核源码） | Uni-Claw 对应义 | 混淆风险 | 本文档处置 |
|---|---|---|---|---|
| Node | agent 代码版本节点（`Node(commit_id, parent_id)`，持有 utility_measures） | ExplorationNode / Container 语义节点（规则分类单元） | “代码提交” vs “世界位置” | 只做结构对应（§3 row 1）；不做 “Agent commit = Node”（§5） |
| **expand / ExpandContainer** | `expand()`＝为新 agent 采样 child（离线生成候选） | `ExplorationRule { ExpandContainer, RecordOnly }`（Phase 2 已冻结：派遣进入容器） | **词汇冲突**：同一 “expand” 两种语义 | Lane 2 动作标签统一用 **EXTEND**（生成新探索候选），与冻结规则词汇区分（§4 Lane 2） |
| **resolved / unresolved** | benchmark task 实例是否被 agent `解出`（`total_resolved/unresolved_ids`，`choose_entry` 的输入选择对象） | 探索节点是否满足规则 / 证据是否充分（fail-closed evidence state） | **同名不同物**：任务解出 ≠ 探索证据状态 | 只借“先分类再分流”结构（§3 row 7）；术语不搬运（§2 已注明） |
| success / failure（Beta 计数） | 单次 eval 对 task 实例的 0/1（resolved bit） | `productive_success / productive_failure`（谓词化多维 Outcome） | “没解出题” ≠ “探索未达成” | productive- 前缀区分；谓词口径待定（§8 问题 1） |
| **n_task_evals** | 评估动作次数（每个 eval＝task 实例 × agent） | “evidence 数量”不可直接对应（一次探索可产多条 Observation/Record） | **可数单位错位**：闸门失去“评估预算”原义 | E 定义为已结算的有效验证/结果次数（countable acts），不是 evidence 记录数（§3 row 5；口径待定 §8 问题 5） |
| Depth | 代际/lineage 世代层数（self-improve 次数） | admission 声明的遍历深度 0/1/N（冻结） | 不同量纲：世代 ≠ 探索层 | 文档 Depth 一律指 Uni-Claw 遍历深度；BranchPromise 的 avg depth 同此（§4 Lane 1） |
| Evaluate / Validate / Verify | `eval_agent()`（`hgm_utils.py:154`）：给已有 agent 分配新 benchmark task | VALIDATE＝补证据验证已有节点（Lane 2 候选）；Verify＝Runtime 闭环中的动作后验证（已冻结 loop 动词） | 三个动词混用 | Lane 2 的 VALIDATE 专指“evidence-completion of existing node”，区别于动作后 Verify（§4 Lane 2 插入点） |
| choose_entry 分类 vs UnresolvedReason | agent-improvement 层：选哪个 entry 改进（empty patch / stochasticity / context length / unresolved / resolved） | Runtime 探索层：未决/失败原因分流（NO_OBSERVABLE_CHANGE / STATE_DRIFT / …） | 同构（分类→策略）、不同层不同粒度 | 只搬结构、词典值不互译（§4 Lane 0） |
| CMP vs Weighted Branch Productivity | 后代 success 聚合（子树比率） | 加权 lineage 生产力（depth/recency/causal/决定性 fact 加权） | 裸 count 会推高“小 fact 多”的分支 | 不裸 count（§3 row 2 / §5） |
| BranchPromise vs Confidence | —（HGM 无此区分） | promise＝从这里继续探索值不值；confidence＝某事实可不可信 | 概念合并 | 明确分离（§3 row 6） |
| best-belief 最终选择 | Beta 后验 percentile 选最终 agent（#6/#7 争议） | 完成权＝GoalEvidence + FSM；候选 promote＝Human Gate | “选 agent” ≠ “完成 run” | 不搬公式（§3 row 9 / §5 / 引用核实） |

## 3.6 流程/机制隔离核查（同形不同流）

§3.5 防的是“名词混淆”；本节防的是**“流程合流”**：以下每一对概念**看起来像同一件事，但必须各自运行在独立流程/决策点/Authority 里**。任何把它们实现成同一条流水线或同一个决策入口的 proposal 都应被推翻。

| “看起来一样”的一对 | 若共享同一流程的后果 | 各自的流程与 Authority（Uni-Claw 现状） | 隔离守卫（proposal 级检查项） |
|---|---|---|---|
| **EXTEND 决策**（Lane 2 新）vs **ExpandContainer 规则应用**（Phase 2 冻结）vs **物理 dispatch**（既有执行） | 预算/分流决策被规则解析或执行路径吞掉 → 冻结规则语义被改写，或预算能力形同虚设 | 分类（`ExplorationRuleResolver`，admission-derived，冻结）→ **新增预算/分流决策层** → 执行（既有授权 dispatch，冻结） | 新增决策层只读规则输出与预算，**不生产 Action、不改 RuleResolver/执行分支**；分层见 §4 Lane 2 |
| **HGM `expand()`＝离线生成新候选** vs **Uni-Claw EXTEND＝运行时调度去向** | 把“生成候选”当成“去点新容器”→ 进化逻辑写进 Runtime（self-modify anti-pattern） | 候选生成只在 **Evolution Plane**（Lane 3，离线、无 Runtime authority）；Runtime 只消费已发现/已分类的 frontier | Lane 2 的候选来源只能是已发现节点，**不是模型/进化生成物**；生成永不进入 Runtime 流程 |
| **choose_entry 改进排序**（HGM，离线）vs **UnresolvedReason → Recovery 分流**（Runtime） | 用“挑最差分支去改进”的逻辑接管 Runtime 恢复 → mid-Run 策略突变 | 改进排序＝离线 Evolution Plane（无 authority）；Recovery＝Runtime 冻结语义（Trap → Determine Scope → Recovery → Resume） | Recovery 只响应**当前 Run 的观察**；改进排序只作用于**离线候选集**；两者零共用代码路径 |
| **Branch TS selector（运行时）** vs **TS selector（离线 lineage 搜索）** | 同一个 selector 实例同时管 Runtime 调度与离线进化 → 两个领域状态互相污染 | runtime selector 输入＝Run-local Ledger/证据投影；离线 selector 输入＝跨 Run 统计（Lane 0） | 两个独立实现/实例，输入域隔离；即使算法形状相同也不共用对象 |
| **深度截止**（冻结：超限 fail-closed）vs **widening 闸门**（Lane 2：调度节流） | 两者合成一个“预算检查”→ 非致命变致命，或把不可变 depth 变成动态 | 深度＝admission 声明、Run 内不可变、超限 fail-closed（冻结）；widening＝调度判断、不满足 → VALIDATE 继续（**非失败**） | 两个独立检查点；widening 不得读取/修改 depth，不得使 depth 动态化 |
| **unresolved（运行时证据状态）** vs **lineage unresolved 率（历史统计）** | 历史统计进入当前 Run 的完成判定 → Memory 变 Truth | 运行时 unresolved → Ledger/完成证据（GoalEvidence + FSM，冻结）；历史率 → pre-Run advisory（Phase 3 草案硬边界） | Memory 不参与完成判定；统计仅 advisory；两条流在 `pre-Run plan` 边界处分叉 |
| **VALIDATE（补证据，Lane 2）** vs **Verify（动作后验证，冻结 loop 动词）** | 两决策合一 → “证据不足”被当成“动作失败” | VALIDATE＝节点级决策（该节点需更多证据）；Verify＝动作级步骤（这次动作结果是否符合预期） | 串行双决策点：VALIDATE 决定并执行 Probe 后，**仍必须走既有 Verify**；VALIDATE 不是 Verify 的改名 |
| **CMP 聚合（跨 Run 统计）** vs **Ledger 编译（Run-local 投影）** | 把跨 Run 统计塞进 run-local 投影 → 破坏“纯投影、无状态”冻结 | Ledger＝on-demand 纯投影、输入为冻结 evidence 记录；跨 Run 统计＝离线（Lane 0） | 统计不进 `ExplorationLedgerCompiler` 输入；若未来要 Run 内子树生产力（Phase 4），另走独立 Gate |
| **pre-Run Plan advisory（Memory）** vs **mid-Run 调度决策** | advisory 变成运行时输入 → 动态策略、Memory 影响 active Run | advisory＝Run 前，accepted StrategyDirective 不可变；调度＝Run 内但仅消费已声明预算 | Phase 3 草案已禁止“影响 active Run”；Lane 2 预算 admission-derived、Run 内不可变 |

“同一个思路”红线：HGM 的公式/启发只能在**同形不同流**的意义上被借鉴——被借的是“决策形状”（分类→分流、后验采样、证据-空间平衡），**应用位置与 Authority 永远按 Uni-Claw 现状分层**，禁止把 HGM 里“选 agent 改进”的思路直接套成“Run 内改策略”。

# 4. 落地车道（每条：做什么 / 归属 / 门禁 / 禁止）

## Lane 0 — 零授权即可做的分析/工具层

**做什么**

1. 本文档本身（概念对照 + 讨论议程）。
2. **离线 lineage 统计工具**：只消费现有 Trace / EvidenceRefs / Ledger 投影，产出分支生产力统计（每 lineage：runs、productive、unresolved、dead-end、平均有效深度、期望 fact 增量）。这份统计是 Phase 3 提案的**输入证据**，不是 Runtime 能力。
3. **UnresolvedReason 观察词典**（候选，不接线）：`NO_OBSERVABLE_CHANGE / AMBIGUOUS_TARGET / MISSING_EVIDENCE / STATE_DRIFT / LOOP_DETECTED / DEPTH_EXHAUSTED / BUDGET_EXHAUSTED / ACTION_REJECTED / RECOVERY_FAILED / PERCEPTION_CONFLICT`，以及建议分流（PERCEPTION_CONFLICT→Validate、STATE_DRIFT→Refresh、LOOP_DETECTED→Prune、MISSING_EVIDENCE→Probe、DEPTH_EXHAUSTED→BudgetPolicy、ACTION_REJECTED→Recovery……）。这是观察字典，不是 Runtime 行为。

**门禁**：无（不改 `src/UniClaw.Runtime`、不改 wire/API、不新增 owner）。

**禁止**：把词典接进 Agent 路径；要求 Runtime 新增输出面；给 Ledger 加边/加状态。

## Lane 1 — Phase 3 Exploration Memory 门（下一个正式 Human Gate，现有草案兼容扩展）

**做什么**

- 在 `uniagent-local-exploration-memory` 草案的 KnowledgeClaim 语义上叠加 **lineage prior 维度**：`{lineage identity, historical productivity, avg depth, expected fact gain, recoverability, unresolved ratio, last_validated, scope/version}`。
- 保持草案全部硬边界：UniAgent-local owner、advisory-only、provenance-bearing（FactReference 生产者所有）、pre-Run Exploration Plan buyer、retrieval fail-closed、`UNIAGENT_PRIVATE_CROSS_SESSION` 由 Human 决定、Memory 不应成为 Runtime 事实。
- 对应 HGM lineage statistics + “历史 Branch Promise 初始化 prior”：下次遇到相似状态（如 Android Settings → Developer Options），pre-Run Plan 的 prior 初始化更高。

**门禁**：Phase 3 Memory Human Gate（apply 授权）。本指引只把“lineage prior”列为 Phase 3 门内讨论内容，不授权修改草案。

**禁止**：UNIAGENT_PRIVATE_CROSS_SESSION 之外的生命周期；Memory 注入 Run 中；检索结果参与完成判定；任何 Runtime 状态/权威变化。

## Lane 2 — Phase 4 新 OpenSpec（Runtime 语义变化，需独立 Human Gate + 变更分类 Large）

**做什么（全部是候选 proposal 内容，Roadmap Phase 4 已点名的方向）**

- **ExplorationBudgetPolicy**：把 Depth 从“唯一变量”扩展为预算函数的一个维度（depth + width + uncertainty + validation + cost + risk + branch promise）。建议沿用 Phase 2 模式：**admission-derived、Run 内不可变** 的预算语义（镜像 depth 0/1/N 表），拒绝 mid-Run 动态突变。
- **Explore / Validate 分流（新增决策层，独立于既有分层）**：在现有 Exploration Loop 的 “Select Next Frontier” 决策点之内、**分类（`ExplorationRuleResolver`，冻结）与执行（dispatch，冻结）之间**增加独立决策层，显式三选一（EXTEND＝对已分类 frontier 分配预算继续深入 / VALIDATE＝补证据验证已有节点 / STOP）；新决策层**只读**规则输出与预算、不生产 Action、不改 RuleResolver/执行分支（§3.6 row 1）。动作标签用 **EXTEND** 而非 EXPAND，避免与已冻结词汇 `ExpandContainer` 撞名（§3.5）。
- **Progressive Widening**：`E^α ≥ N` 闸门（E=已结算的有效验证/结果次数 —— countable acts，见 §3.5 可数单位说明）。
- **Branch Thompson Sampling selector**：优先离线（harness）验证 productive 计数口径后，再谈 Runtime 接线。
- **UnresolvedReason → Recovery 映射**：Lane 0 词典正式化。

目标形态（带归属标注的参考图）：

```
                        Goal
                         ↓
                    Observation
                         ↓
                   Evidence Fusion            ← 冻结边界，不动
                         ↓
                    Runtime Belief
                         ↓
                Exploration Compiler
                         ↓
              Exploration Lineage Graph  ← Lane 0 统计 / Lane 1 Memory（跨 Run），非 Ledger 状态
                         ↓
                 Branch Promise Model   ← Lane 1/2
                         ↓
               Exploration Budget Policy ← Lane 2（admission-derived，Run 内不可变）
                         ↓
            ┌────────────┼────────────┐
            ↓            ↓            ↓
         EXTEND      VALIDATE       STOP
            ↓            ↓
          Action       Probe
            └─────┬─────┘
                  ↓
              Observation
                  ↓
            Outcome Update / Ledger      ← Phase 2 已毕业机制
                  ↓
            Trace / Memory
```

**门禁**：独立 Human Gate；OpenSpec 变更分类 **Large**（新 abstraction / boundary）。Roadmap 冻结的 depth 0/1/N 仍是 depth 维度唯一语义。

**禁止**：Gate 前写生产代码；动态深度实现；mid-Run Strategy/预算突变；新增完成权。

## Lane 3 — Evolution Plane（远期，P3；不改 live Runtime）

- 两平面分离：**Runtime Plane**（生产执行，永不被 self-improvement 直接修改）+ **Evolution Plane**（候选 policy / harness / prompt / strategy，离线生成与评估）。
- 流程：生产 Trace → 离线 lineage 统计 → 候选分支 → 评估（复用 `uniagent-emulator-validation-harness` 的 replay/benchmark 协议，Tier A/B）→ 架构 guards + contract tests → **Human Gate** → promote。
- 对齐已有资产：`.ai/skills/uniagent-evolution-loop`（受控演进工作流，不改变 Runtime 权威/协议/生命周期）与 `../work/active/uniagent-evolution-codex-integration-human-gate.md` 的门禁先例。
- HGM 的 “self-modify live code” 明确不做（HGM 仓库自身也警告 model-generated code 执行风险）。promote 必然经过 Human Gate + 架构守卫 + 契约测试（对应项目已有 OpenSpec / Architecture Guard / Contract Invariants / Graduation / Human Gate 治理）。

# 5. 明确不照搬清单

| HGM | Uni-Claw 应该怎么做 |
|---|---|
| pass/fail 二元 utility | 多维 Outcome（Goal + Information + Risk + Cost） |
| benchmark accuracy | Goal + Information + Risk + Cost 的综合证据 |
| Agent commit = Node | Exploration state / strategy = runtime 候选；产出物 ≠ 节点 |
| descendant 全量累计 | depth / recency / causal 加权（BranchPromise 不裸 count） |
| self-modify live code | 只在 Evolution Plane 离线改候选，promote 走 Human Gate |
| 随机 failure entry selection | 结构化 UnresolvedReason 分流 |
| benchmark task = evaluation | Probe / Validate / Replay（复用现有 harness 语义） |
| Beta posterior 直接决定最终 agent | Runtime Decision Gate + 架构约束；final-selection 公式不搬（open issue 争议） |

# 6. 边界合规校验（写入每条 proposal 前的自查表）

- [ ] 不绕过 `Observation → Evidence → Fusion → Belief → Agent`；HGM 内容只进入 **Exploration Control / Meta-Harness**。
- [ ] Memory 只作为 Prior/Evidence 输入；Runtime 永远 fresh-observation 验证后再进 Belief（`Memory → Prior/Evidence → Runtime Validation → Belief`，绝无 `Memory → Truth`）。
- [ ] Ledger 保持只读投影：不新增状态系统、不新增 owner、不拥有完成权。
- [ ] Depth/预算 Run 内不可变；无 mid-Run Strategy 突变；最终完成仍走 GoalEvidence + FSM。
- [ ] RuntimeAgent 不规划、不发明规则；所有语义变化先过 OpenSpec + Human Gate（Large 分类不拆分绕过）。
- [ ] 分层隔离：分类 → 预算/分流决策 → 执行 三层各自独立；新决策层只读规则与预算，不生产 Action、不改 RuleResolver/执行分支（§3.6）。

# 7. 自我迭代闭环（本文档要服务的目标）

**记忆让行为更智能（Phase 3 主循环）**

```
Trace（已有 Observability/EvidenceRefs）
   ↓ 离线统计（Lane 0 工具，只消费现有证据）
lineage 统计（productivity / avg depth / fact gain / recoverability）
   ↓ 写入 Memory（Lane 1，provenance-bearing KnowledgeClaim）
branch prior
   ↓ UniAgent pre-Run 计划 advisory
更优的 Exploration Plan（预算/深度/范围参考）
   ↓ 运行后 outcome 回流
统计更新（recency/death 加权）
```

**探索中摸索可自我迭代的规则（Lane 0→2→3 闭环）**

```
探索失败/未决 → UnresolvedReason 词典分类（Lane 0）
   → 建议分流（Validate / Refresh / Probe / Prune / BudgetPolicy / Recovery，Lane 2 候选）
   → 先以观察证据验证分流收益（harness 回放，Lane 3）
   → 收益成立才作为 policy 候选进入 Human Gate
```

这使 Uni-Claw 沿 “会探索 → 越来越多地知道哪里值得探索 → 候选策略离线迭代” 演进，且每一环都留在既有治理内。

# 8. 开放问题（讨论议程）

1. **命名与定义**：BranchPromise vs ExplorationPromise；`productive(node)` 谓词（`produced_new_fact / reduced_unresolved / advanced_goal / discovered_structure`）由谁定义、用哪份证据计数（Runtime？harness 离线？）。
2. **加权方案**：CMP 加权具体候选（depth / recency / causal / 决定性 fact）；“100 个小 fact vs 2 个决定性 Goal Fact”的权衡如何落成可解释的权重。
3. **lineage 边放哪**：跨 Run 放 Memory（本指引推荐）vs Run 内给 Ledger 加边（触碰“无 graph/stack”冻结，不推荐）。若跨 Run，lineage identity 如何从现有 FactReference / Container identity 导出而不新增 Runtime 输出。
4. **Budget Policy 形态**：admission-derived 不可变预算（推荐，镜像 depth 0/1/N）vs 动态（禁止）。width/validation 是否同为 admission 参数。α 的取值依据。
5. **TS selector 与 E 的口径**：productive success/failure 的后验、以及 widening 闸门的可数单位 E（已结算结果/验证次数 vs evidence 记录数，见 §3.5），都先离线（harness）验证再谈 Runtime；谁做这个离线实验。另外 **runtime 与离线（Evolution Plane）两个 selector 实例必须分离**（§3.6 row 4）。
6. **Phase 3 cross-session 范围**：Human Gate 时需要明确的 `UNIAGENT_PRIVATE_CROSS_SESSION` 边界（哪些会话共享哪些 lineage prior）。
7. **Evolution Plane 裁判**：哪个 harness / benchmark 语料作为候选 branch 的评估裁判；promotion 的 Human Gate 由谁执行（对齐 `uniagent-evolution-loop`）。
8. **unresolved 历史率**：是否在 Phase 3 增加“该分支历史上 unresolved 率”作 prior——注意历史 unresolved 是统计，不是当前 Run 的判断，二者不得混淆。

# 9. 建议的下一步动作（讨论议程，非授权）

1. 把本指引作为 Phase 3 Memory 门 packet 的**背景材料**一起讨论；是否在草案中补 lineage prior 维度由 Human 在门内决定。
2. 做一次 Lane 0 可行性验证：现有 Trace / EvidenceRefs 能否**零 Runtime 改动**地导出 lineage 统计（决定 Phase 3 输入证据是否可信）。
3. 暂不新建 Phase 4 proposal；待 Phase 3 门关闭后，再评估“exploration-budget-policy”作为 Phase 4 首个候选是否值得提交。

## 引用

- HGM 论文 — "Huxley-Gödel Machine: Human-Level Coding Agent Development by an Approximation of the Optimal Self-Improving Machine"（arXiv:2510.21614，v3，2025-10-29）— <https://arxiv.org/abs/2510.21614>
- HGM 源码仓库（metauto-ai/HGM，"🧬 The Huxley-Gödel Machine"）— <https://github.com/metauto-ai/HGM>
- 引用核实（ego-browser，本会话，直接查证源码）：
  - `tree.py:44-54` — `Node.get_decendant_evals()` 聚合整棵子树的 utility_measures（含 pseudo descendant evals）；CMP 的代码落地；
  - `hgm.py:360-377` — `TS_sample()` = `np.random.beta(1+Σs, 1+Σf)` 后取 argmax；`:383-404` `expand()` 用 **descendant evals** 选父节点并以 `parent_id` 建 lineage；`:413-420` 渐进拓宽闸门 `n_task_evals^alpha >= node_count`（alpha 默认 **0.6**，`config.yaml`/`config.py` 一致）；
  - `hgm_utils.py:88-151` — `choose_entry()` 区分 empty patches / stochasticity / context length / unresolved ids / resolved ids（:127/:130/:138/:140-142）；`self_improve_step.py:28` `diagnose_problem()` 生成改进目标；
  - open issues [#6（ε=1 澄清）](https://github.com/metauto-ai/HGM/issues/6) 与 [#7（Picking the Best Belief Agent）](https://github.com/metauto-ai/HGM/issues/7) 证实 best-belief 选择公式存在公开争议——对应 §5 不照搬清单。
- Uni-Claw Runtime Exploration Roadmap — `../decisions/runtime-exploration-roadmap.md`
- Phase 3 Memory 草案 — `../../openspec/changes/uniagent-local-exploration-memory/proposal.md`
- Phase 2 毕业决策 — `../decisions/runtime-exploration-phase2-final-graduation-decision.md`