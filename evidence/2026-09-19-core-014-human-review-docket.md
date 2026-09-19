# CORE-014 Human Review Docket — 恢复消费编排（恢复入口 / pending 消费）

> 日期: 2026-09-19
> Change: `changes/CORE-014/state.md`（planned；implementation_forbidden）
> 上游: ADR-0023 · CORE-013（closed：执行源写入侧已产品化，567/567）
> 契约约束（不可协商的输入）: Discovery 只读；重试/补偿/重观察决策归
> Control/Agent 流（ADR-0023 决策 4）；UnknownOutcome 唯一合法后继是
> re-observe，永不盲补发；terminal 后 Run 冻结（不变量 42）。
> 本文件只提供裁决材料。每个 `DECIDE:` 是需要用户显式回答的决策槽。

**问题陈述**：CORE-013 之后产品侧执行源是只写的——`DiscoverPending` /
`GetAttempt` / `LinkRetry` / `LinkCompensation` 目前只有测试在消费。
重启后谁发现未决、pending 以什么身份进入哪条决策路径，尚无产品 buyer。
本 docket 裁定恢复消费的最小正确形状。

---

## Q1 恢复入口 Owner：谁在重启后触发 DiscoverPending？

**仓库事实**

- `KernelRunDriver` 是 internal 可恢复 phase 状态机
  （NeedInitialObservation → NeedDecision → StepAct → StepVerify →
  TerminalEvaluation）；自述「不拥有任何 canonical domain truth」。
- `UniKernel` 拥有 composition/lifecycle coordination（activation
  latch，D20）；`Legal Activation` 是一次性幂等 lifecycle command。
- 仓库尚无产品 composition root（`SimulationHost` 与
  `ProductHostClosureTests` 是仅有的组合面；Product Host 只锁定依赖
  闭包，不是运行中的 host）。
- ADR-0023：消费决策归 Control/Agent 流——kernel 侧最多「发现并呈现」。

**选项**

| 选项 | 形状 | 评价 |
|---|---|---|
| a | KernelRunDriver 新 phase（NeedRecoveryDiscovery，激活后、初始观察前自动发现并呈现） | kernel 内自动化；但 driver 不拥有 canonical truth，且目前无真实恢复 buyer 驱动该 phase 的语义（为保机制造状态） |
| b | UniKernel 激活面扩展（activation 时发现，结果作为输入面呈现） | 把恢复输入绑进 lifecycle command；改变 activation 幂等语义的风险 |
| c | Host/UniAgent 侧编排：kernel 不动，恢复消费方（未来的 Product Host / UniAgent 恢复流）直接调执行源读 API | 最小、零 kernel 改动、符合「消费决策归 Control/Agent」；恢复编排语义留给真实 buyer 出现时定义（NO_REAL_BUYER 边界处理先例：Run Snapshot 的 P5 载荷 deferred） |
| D（建议） | c 为 v1，同时把 a 记录为「有真实 buyer 后的可选演进」 | 不为无 buyer 的 kernel 状态花实现；ADR/契约约束已被 c 满足 |

`DECIDE-Q1`：v1 = 选项 c（kernel 零改动，恢复消费归 Host/UniAgent 编排）？

---

## Q2 发现时机与 Run 关系：跨 Run 的 pending 是什么身份？

**仓库事实**

- journal 是路径作用域（组合作用域），不携带 RunId；RunId 由契约内容
  确定性派生（同 bundle 同 RunId——CORE-011 已用作关联提示）。
- Host B 是新组合：新 RunModel、新 activation；上一 Run 的
  obligations/verifications 属其 Run State（terminal 冻结，不变量 42）。
- pending 记录的 correlation 原语（IntentId/BindingId/RevisionId）指向
  产生它的 Run 的 revision——在新 Run 里这些 revision 已非 current。

**选项**

| 选项 | 形状 | 评价 |
|---|---|---|
| a | adopt：pending 进入新 Run 的决策上下文（如 AgentDecisionContext 扩展） | P25 输入面改动；跨 Run 语义重（上一 Run 的 binding/revision 在新 Run 已失效，adopt 需要重新 grounding 语义） |
| B（建议） | advisory-only：pending 是恢复消费方的查询面输入，不进任何 Run State、不进 AgentDecisionContext；新 Run 的决策若需要，由消费方经 re-observe → re-ground → 新 binding 走既有路径（UnknownOutcome 的唯一合法后继正是 re-observe） | 与契约「Discovery 只读」和 re-observe 语义零冲突；无 P25 改动 |
| c | journal 携带 RunId、按 run 过滤发现 | 有用但非 v1 必需（路径即作用域）；作为记录格式演进留待真实需要 |

`DECIDE-Q2`：v1 = advisory-only（选项 B），pending 不进 Run State /
P25 输入面？

---

## Q3 pending 的合法后继动作面：v1 范围裁到哪里？

**仓库事实（契约既定）**

- 合法后继集合：查询（GetAttempt 还原完整准备集合）；协调
  （AppendReceipt(attemptId, null) 显式声明仍无 Receipt——CORE-013
  测试已用）；重观察（既有 P2/P3 入证路径）；LinkRetry（新 Attempt
  同 Effect——外部重试决策）；LinkCompensation（新 Effect——补偿决策）。
- 禁止：盲重发（binding 已消费/revision 已失效）；把 pending 解释为
  成功/失败/已发送；伪造失败。
- LinkRetry/LinkCompensation 是「决策动作」——按 ADR-0023 归
  Control/Agent 流，产品代码目前无此决策逻辑。

**选项**

| 选项 | v1 范围 | 评价 |
|---|---|---|
| a | 发现 + 呈现 + 协调动作（含 LinkRetry/LinkCompensation 的自动化策略） | 自动化重试/补偿策略是完整的恢复决策系统——远超当前 buyer 证明；且策略语义（何时重试、何时补偿）无人裁决过 |
| B（建议） | 发现 + 查询 + 显式未决声明（AppendReceipt null）三项只读/登记性动作；LinkRetry/LinkCompensation 保持 API 备用、决策逻辑 deferred | 与 CORE-013 已实现面完全一致（零新增产品代码即可满足）；决策系统等真实 buyer 与 Human 裁决 |

`DECIDE-Q3`：v1 = 选项 B（决策性动作 deferred，无自动重试/补偿）？

---

## Q4 journal 生命周期与产品组合默认

**仓库事实**

- `SimulationHost.Compose` 已有注入缝（可选参数）；未注入 = 既有行为。
- 产品 composition root 不存在于仓库（Q1 事实）；journal 路径目前仅
  测试临时目录（`FileExecutionJournal(filePath)`，EnsureDirectory）。
- 无任何清理/retention 机制；append-only 文件只增不减。

**选项**

| 选项 | 形状 | 评价 |
|---|---|---|
| a | 现在就定产品默认（Product Host 创建时必注入 journal，路径策略 + retention 全套） | Product Host 本身不存在——为其预制策略是 NO_REAL_BUYER 反模式 |
| B（建议） | 保持注入可选 + 测试态现状；产品默认（必注入与否、路径/retention）作为 Product Host 组合根的显式决策，待该 Change 出现时一并裁决；本 docket 只记录约束：journal 生命周期归 composition root，不归 EffectBoundary/执行源自身 | 边界清晰（执行源不自管生命周期）；不预支不存在的宿主决策 |

`DECIDE-Q4`：v1 = 选项 B（产品默认 deferred 至 Product Host Change，
生命周期归组合根）？

---

## 授权边界（若任何裁决需要实现轮）

按建议方案（Q1=c / Q2=B / Q3=B / Q4=B），CORE-014 的「实现」量约为
**零产品代码**——四项建议都是「不做什么 + 把约束记档」。若裁决偏离
建议（如 Q1 选 a/b），实现授权需另行单独给出并点名改动面。

## 建议裁决摘要

Q1=c（kernel 零改动，消费归 Host/UniAgent）· Q2=B（advisory-only）·
Q3=B（决策动作 deferred）· Q4=B（产品默认 deferred 至 Product Host）。
