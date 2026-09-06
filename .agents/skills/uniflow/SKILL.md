---
name: uniflow
description: Development Flow baseline + execution engine for this repository — the 8-state spine (UNDERSTAND→RESOLVE→PERSIST→PLAN→IMPLEMENT→REVIEW→VERIFY→CLOSED), entry/resume protocol, failure edges, durability tiers, reproducible verification, and context economics (smart zone). Load before starting any feature, refactor, or bug work, when resuming an in-flight change, and whenever deciding what to do next or whether work is complete.
source: LOCAL_UNIFLOW
---

# UniFlow — Development Flow Baseline + Execution Engine

> **Part A = 流程主干（WHAT，结构冻结，ADR-0007）**
> **Part B = 执行引擎（HOW：自动、低成本、高可靠）**
> Skill 解决特定不确定性或执行特定方法；永不拥有流程。

## Part A — Development Flow 主干

### A0. 形态

```text
ENTER / RESUME（协议，非状态）
  ↓
UNDERSTAND → RESOLVE → PERSIST → PLAN → IMPLEMENT → REVIEW → VERIFY → CLOSED
                                                  ↖ failure edges（A7）↙
                                                           ↓（可选）
                                                  LEARNING HOOK（A8）
```

`BLOCKED` 是正交 disposition（`lifecycle_state` 不变），不是第 9 状态。

### A1. Entry / Resume Protocol

任何 agent 进入一个 Change 前：

```text
1. 读 Change State（STANDARD+ 的 changes/<id>/state.md）
2. 复验（不信记录本身）：
   a. git 实况 vs 声明 base/进度
   b. 引用的 evidence 存在且内容匹配
   c. status 与仓库实际进度一致
3. 一致 → 从该状态续行；任一矛盾 → 回 UNDERSTAND（不带矛盾执行）
```

### A2. UNDERSTAND（automatic first）

模型自己读 request + 仓库 + changes/ + ADR + CONTEXT；先识别已确定项，
再分类剩余未知。不因「symbol 在哪 / 测试在哪 / 现状是什么」问人。

### A3. RESOLVE（uncertainty routing）

| 剩余未知 | 路由 |
|---|---|
| 人歧义（意图/范围/产品含义/重大取舍） | `/grill-with-docs` |
| 域概念（Owner/职责/lifecycle） | `domain-modeling` |
| 外部未知（协议/库/上游行为） | `research` |
| 经验未知（不做实验无法判断） | `prototype` |
| Bug 根因 | `diagnosing-bugs`（诊断在修复前；根因明确才算 RESOLVED） |
| 无 | 直行 |

**Resolve Gate**：Intent 清？Scope 够清？阻塞未知已解？相关决策已定？
Acceptance 可表达？无阻塞 Human Decision？→ 全部满足 =
`RESOLVED`（semantic state，不要求表格）。

### A4. PERSIST（durability ALWAYS, depth VARIABLE）

| 档 | 载体 | 约束 |
|---|---|---|
| MINIMAL | 仅结构化 commit message | 只用于**无需跨会话恢复**的 Change；中断/跨 session → **自动升级 STANDARD** 补建 state.md |
| STANDARD | `changes/<id>/state.md` | Intent/Scope/Out-of-Scope/Decisions/Acceptance/Constraints/verification 声明/Status |
| DECISION-HEAVY | 同上 | + Assumptions/Alternatives（含被拒）/Owner-Authority impact/ADR refs/Residual risks |

Change State = WHAT / WHY / ACCEPTANCE。不是 Plan（HOW）、不是 Ticket、
不是 WorkItem、不是 ADR。

### A5. PLAN（HOW，在 WHAT 之后）

垂直切片 / tracer bullet：`行为 → 模型 → 运行时 → 测试 → 证据`，不按层
拆。简单任务 Plan 可一行。超聪明区的规划问题先拆决策再拆实现。

### A6. IMPLEMENT（tdd default）

RED → 最小实现 → GREEN → Refactor；Bug：复现 → 回归 RED → 修复 →
GREEN。`codebase-design` 仅在边界/接口/职责/测试缝不清或需新抽象时触发。

### A6.5 REVIEW 与 VERIFY（分离，永不合并）

```text
Review = 实现得好吗？（意图对齐/范围/设计/边界/规范/意外改动）
Verify = 完成被证明了吗？（acceptance/测试/观测行为/回归/证据/约束）
```

**验证声明**（STANDARD+ 必含，等级避免与产品 E0-E4 混淆）：

```text
level: CONTRACT（命令/类型/边界）| DETERMINISTIC（单测/性质/转移）
     | SCENARIO（端到端确定性场景）| ENVIRONMENT（真机/外部）
四元组（任何等级必备）：method · expected · actual · evidence ref
```

只有 trace 链接 ≠ 可复现验证。

### A7. 失败转移边

| 失败 | 转移 | 附加 |
|---|---|---|
| Review：implementation defect | → IMPLEMENT | — |
| Review：semantic/assumption defect | → RESOLVE | 修订 Change State（revision 留痕） |
| Verify：实现不满足 acceptance | → IMPLEMENT | — |
| Verify：语义/假设失败 | → RESOLVE | 同上 |
| Verify：harness/环境失败 | 留在 VERIFY | 有界重试；无解 → `disposition: BLOCKED`；恢复条件满足后继续 |

失败尝试是证据：保留不删。

### A8. LEARNING HOOK（可选，CLOSED 后）

三触发任一成立才跑：同类摩擦 ≥2 次（ADR 候选/流程校准）；新术语定型
（CONTEXT 增补）；决策被推翻（ADR supersede）。

### A9. CLOSED

范围完成 + acceptance 被证明 + 无未授权改动 + 文档同步 + 无阻塞 Human
Decision。普通任务不需要毕业报告。

## Part B — 执行引擎（HOW）

### B1. 上下文经济学

```text
1. 聪明区 ≈150K token（工作值，非硬限）：会话早期最敏锐
2. 超聪明区 → 拆分：持久化 Change State + 垂直切片 + fresh SubAgent
   ——不拉长当前会话
3. Leader 聪明区内可延续健康上下文；委派一律 Fresh/Disposable
4. Skill 按需/用户调用，零常驻成本；不一次加载全部
5. 确定性脚本优先于重复模型推理
```

### B2. Route：Direct / Delegate

**Direct**（有界局部目标 / 现有上下文足够 / 风险可控 / 委派无增益）→
就地实现。**Delegate**（独立可自包含目标 / 语义边界清晰 / acceptance
显式 / 上下文可打包）→ transient WorkItem → fresh SubAgent → Result +
Evidence → Leader。确定性操作 → Tool Only。

### B3. WorkItem（transient delegation contract）

schema：`schemas/work-item.schema.json`；载荷按需落 `workitems/`。非
issue tracker / backlog / Plan 存储。禁止 harness 专有字段（session id /
worker id / 模型名 / harness 命令）。修改 acceptance/forbidden/
frozen_decisions = 回 Leader 重新决策。

### B4. Model Routing

`capability → tier → adapter → concrete model`；共享映射在
`model-routing.yaml`；provider 绑定只在 adapter；禁 silent downgrade。

### B5. Adapter 边界（Codex / DSH 对称）

Adapter 负责 discovery/session/注入/工具/模型/Result 返回；不得修改
WorkItem、Acceptance、Decision、Skill procedure、Verification、
Completion 语义。同一 WorkItem 双 Harness 产出可规范化同构结果。
