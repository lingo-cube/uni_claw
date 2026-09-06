# 方案 — Development Flow v1 采纳与 UniFlow 执行引擎重定位

> PlanType: ARCHITECTURE_PLAN · Status: DRAFT / AWAITING_APPROVAL
> 依据：Development Flow v1 directive（2026-09-06，8 状态主干逐字采纳为流程基线）
> 复杂度：DECISION-HEAVY（流程基线更换，部分取代 ADR-0003/0004 条款）
> 前置参考：Phase 0-5 全部调查、语义迁移矩阵、uni-agent V1 流程机制（Phase 0/1 提取）

## 1. 新分层模型（冻结）

```text
Development Flow v1   = WHAT：8 状态主干 + Bug 旁路 + 8 原则（流程基线，不再由 UniFlow 定义）
UniFlow               = HOW：自动、低成本、高可靠地执行这套 Flow
Skill                 = 解决特定不确定性 OR 执行特定工程方法（永不拥有生命周期）
```

8 状态主干：`UNDERSTAND → RESOLVE → PERSIST → PLAN → IMPLEMENT → REVIEW →
VERIFY → CLOSE`；语义 token：`RESOLVED / PERSISTED / PLANNED / IMPLEMENTED /
VERIFIED / CLOSED`（RESOLVED 是 semantic state，不要求固定表格）。

## 2. 与现状的 Delta（逐项）

| 维度 | 现状（uniflow skill + ADR-0002..0004） | Flow v1 目标 | 变更性质 |
|---|---|---|---|
| 生命周期定义 | 四 Semantic Gate（uniflow skill 内定义） | Flow v1 八状态；Resolve Gate 取代 Gate 1；CLOSED 检查取代 Gate 4；PERSISTED/PLANNED 吸收 Gate 2 语义 | **取代** |
| Explore 默认 | Feature 默认 grill-with-docs | **Automatic Understanding 默认**；grill 仅 HUMAN AMBIGUITY 触发 | 取代 |
| 持久化 | Plan 仅 Context Boundary 持久化 | **Durability ALWAYS**：所有 Change 持久化 Change State，深度三档（MINIMAL/STANDARD/DECISION-HEAVY） | **取代** |
| 不确定性路由 | Pre-UniFlow 双路径（Feature/Bug） | 六路由表（人歧义/域歧义/外部未知/经验未知/根因未知/无未知） | 细化 |
| research / prototype | 第二批「按需」未装 | **升级为核心 Explore skill** → 需安装 | 升级 |
| to-spec | 明确排除 | 维持不装（可选 synthesis 工具、非必经节点；与「不造同重量 Spec 系统」的既有裁决相容） | 不变 |
| Direct/Delegate、WorkItem、Context 策略、Review≠Verify、fail-closed | 已有 | 保留，**移入「执行引擎」区**（属 HOW 不属 WHAT） | 保留归位 |
| 四 Gate 冻结（ADR-0004） | 冻结 | 被 Flow v1 取代 → ADR 标 superseded-by-0007 | 退役 |

## 3. 工件模型变更

```text
新增  changes/    Change State（WHAT/WHY/ACCEPTANCE/Status；深度三档）
保留  plans/      HOW（Plan，IMPLEMENT 前；简单任务可极短）
保留  workitems/  transient delegation contract（不变，ADR-0002）
保留  evidence/   完成证据（不变）
保留  docs/adr/   durable architectural WHY（DECISION-HEAVY 引用）
```

关系声明（directive 原文）：Change State ≠ Plan/Ticket/WorkItem/ADR/会话记忆。

三档模板要点：MINIMAL = Intent/Scope/Acceptance/refs/Status；STANDARD 加
Out-of-Scope/Decisions/Constraints/Verification expectation；DECISION-HEAVY
再加 Assumptions/Alternatives（含被拒）/Owner-Authority impact/ADR refs/
Residual risks。

## 4. 吸收原分支（uni-agent V1）机制 → Flow v1 执行引擎

| V1 机制（Phase 0/1 提取） | 去向 |
|---|---|
| Tool-Only 事实优先 + Leader 自主 | AUTOMATIC FIRST 的执行形态 |
| H4-3 auto-continue（16 停机条件） | 状态自动推进的 stop-condition 来源（阻塞人不阻塞流程的反面清单） |
| H4-1 leader-decision（10 decision_type） | 压缩为 Flow 状态转移 + Delegate 决策点（不复活状态机） |
| task/result contract + ResultGate | WorkItem schema + VERIFIED 证据核对（语义已迁移） |
| ProfileContextKey 缓存 | **低成本**支柱：Leader 健康上下文延续；委派仍 Fresh（**高可靠**支柱）——两者折中已内建于 Context 策略 |
| openspec tasks 勾选 + finalize/投影再生 | 退役：Change State 恒持久化 + 深度分级取代 ceremony；不复活派生投影依赖 |
| Two-Lane / 六 Hard Gates | 已压缩（语义迁移矩阵 MIGRATE_SEMANTIC 行）；由不确定性路由 + Human Gate 承担 |

## 5. 文件级实施清单（批准后执行，单次提交序列）

1. **ADR-0007**：采纳 Development Flow v1 为流程基线；UniFlow 重定位为执行
   引擎；ADR-0003/0004 加 `superseded-by` 标记（0002 WorkItem 语义不变）
2. **重写 `.agents/skills/uniflow/SKILL.md`**：Part A = Flow v1 全文（WHAT，
   冻结引用）；Part B = 执行引擎（HOW：状态推进规则、Direct/Delegate、
   WorkItem、Context 策略、model-routing 指针）
3. **新增 `changes/README.md`** + 三档模板（lazy：首个 Change 创建时生效）
4. **安装 `research` + `prototype`**（mattpocock/skills 上游原文，一条命令）
5. **CONTEXT.md** 增补：Change State、Durability 三档、状态 token、
   Automatic Understanding
6. **AGENTS.md** 真相表更新（流程源=uniflow skill Part A；新增 changes/ 行）
7. **schemas/work-item.schema.json**：required_skills enum + research/prototype

## 6. UniFlow 执行引擎三目标（实施后的迭代问题域，本方案不展开）

- **自动**：8 状态转移条件表（何种证据使状态翻页；停机条件源自 H4-3 吸收）
- **低成本**：Durability 深度即成本闸；Leader 上下文延续；确定性脚本优先
- **高可靠**：fail-closed 全保留（未 RESOLVED 不实现；无证据不 CLOSED；
  Review≠Verify）

## 7. 待批准决策

| # | 决策 | 建议 |
|---|---|---|
| a | `changes/` 形态 | `changes/<change-id>/state.md`（MINIMAL 允许单文件扁平） |
| b | uniflow skill 单文件（A+B 两部）vs Flow 独立文件 | **单文件**（skill 发现机制保持一套；A 部标注冻结） |
| c | research/prototype 立即安装 | **是**（Flow v1 核心路由目标必须有实体） |
| d | ADR-0003/0004 处置 | 加 superseded-by-0007 头注，不删原文 |

## 8. Stop Conditions

- 所有者不批准任一核心条款 → 回本方案修订，不动现有 uniflow skill
- 实施中发现 Flow v1 与现有不可退役资产（WorkItem schema、ADR-0002）冲突
  → 停止并升级
